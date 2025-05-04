using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using DetectionEquipment.Shared.Networking;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.IffReflector
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "IffReflector")]
    internal class IffReflectorBlock : ControlBlockBase<IMyConveyorSorter>
    {
        public SimpleSync<string> IffCode;
        public SimpleSync<bool> ReturnHash;

        public string IffCodeCache { get; private set; } = "";
        protected override ControlBlockSettingsBase GetSettings => new IffReflectorSettings(this);

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            IffCode = new SimpleSync<string>(this, Block.CubeGrid.CustomName, (value, fromNetwork) =>
            {
                IffCodeCache = ReturnHash.Value ? "H" + IffCode.Value.GetHashCode() : "S" + IffCode.Value;
            });
            ReturnHash = new SimpleSync<bool>(this, false, (value, fromNetwork) =>
            {
                IffCodeCache = ReturnHash.Value ? "H" + IffCode.Value.GetHashCode() : "S" + IffCode.Value;
            });
            
            new IffControls().DoOnce(this);
            base.UpdateOnceBeforeFrame();

            if (!_iffMap.ContainsKey(Block.CubeGrid))
                _iffMap.Add(Block.CubeGrid, new HashSet<IffReflectorBlock>());
            _iffMap[Block.CubeGrid].Add(this);
        }

        public override void MarkForClose()
        {
            base.MarkForClose();
            if (!_iffMap.ContainsKey(Block.CubeGrid))
                return;

            _iffMap[Block.CubeGrid].Remove(this);
            if (_iffMap[Block.CubeGrid].Count == 0)
                _iffMap.Remove(Block.CubeGrid);
        }

        private static Dictionary<IMyCubeGrid, HashSet<IffReflectorBlock>> _iffMap = new Dictionary<IMyCubeGrid, HashSet<IffReflectorBlock>>();
        public static string[] GetIffCodes(IMyCubeGrid grid)
        {
            HashSet<IffReflectorBlock> map;
            if (!_iffMap.TryGetValue(grid, out map))
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
