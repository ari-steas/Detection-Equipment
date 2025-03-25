using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.Sync;

namespace DetectionEquipment.Shared.BlockLogic.IffReflector
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "IffReflector")]
    internal class IffReflectorBlock : ControlBlockBase<IMyConveyorSorter>
    {
        public MySync<string, SyncDirection.BothWays> IffCode;
        public MySync<bool, SyncDirection.BothWays> ReturnHash;
        public string IffCodeCache = "";

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            IffCode.ValueChanged += sync =>
            {
                IffCodeCache = ReturnHash ? "H" + sync.Value.GetHashCode().ToString() : "S" + sync.Value;
            };
            ReturnHash.ValueChanged += sync =>
            {
                IffCodeCache = sync.Value ? "H" + IffCode.Value.GetHashCode().ToString() : "S" + IffCode.Value;
            };

            IffCode.Value = "";
            ReturnHash.Value = true;
            new IffControls().DoOnce(this);

            if (!IffMap.ContainsKey(Block.CubeGrid))
                IffMap.Add(Block.CubeGrid, new HashSet<IffReflectorBlock>());
            IffMap[Block.CubeGrid].Add(this);
        }

        public override void MarkForClose()
        {
            base.MarkForClose();
            IffMap[Block.CubeGrid].Remove(this);
            if (IffMap[Block.CubeGrid].Count == 0)
                IffMap.Remove(Block.CubeGrid);
        }

        private static Dictionary<IMyCubeGrid, HashSet<IffReflectorBlock>> IffMap = new Dictionary<IMyCubeGrid, HashSet<IffReflectorBlock>>();
        public static string[] GetIffCodes(IMyCubeGrid grid)
        {
            HashSet<IffReflectorBlock> map;
            if (!IffMap.TryGetValue(grid, out map))
                return Array.Empty<string>();
            var codes = new HashSet<string>();
            foreach (var reflector in map)
                if (reflector.Block.Enabled)
                    codes.Add(reflector.IffCodeCache);

            var array = new string[codes.Count];
            int i = 0;
            foreach (var code in codes)
            {
                array[i] = code;
                i++;
            }

            return array;
        }
    }
}
