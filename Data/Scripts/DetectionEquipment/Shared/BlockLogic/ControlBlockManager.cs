using DetectionEquipment.Shared.Utils;
using System.Collections.Generic;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Definitions;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic
{
    internal class ControlBlockManager
    {
        public static ControlBlockManager I;
        public Dictionary<IMyCubeBlock, IControlBlockBase> Blocks = new Dictionary<IMyCubeBlock, IControlBlockBase>(); // assume only one IControlBlockBase is ever present on a given block.
        public Dictionary<string, IBlockSelectControl> BlockControls = new Dictionary<string, IBlockSelectControl>();

        public long TerminalSelectedBlock = 0;

        public readonly ObjectPool<Dictionary<long, List<WorldDetectionInfo>>> GroupsCacheBuffer = new ObjectPool<Dictionary<long, List<WorldDetectionInfo>>>(
            () => new Dictionary<long, List<WorldDetectionInfo>>(),
            null,
            dict => dict.Clear()
        );
        public readonly ObjectPool<List<WorldDetectionInfo>> GroupInfoBuffer = new ObjectPool<List<WorldDetectionInfo>>(
            () => new List<WorldDetectionInfo>(),
            null,
            list => list.Clear()
        );

        internal static void Load()
        {
            I = new ControlBlockManager();
            if (!MyAPIGateway.Utilities.IsDedicated)
                MyAPIGateway.TerminalControls.CustomControlGetter += CustomControlGetter;
            Log.Info("ControlBlockManager", "Ready.");

            SerializationNotifier.OnSerialize += I.OnSerialize;

            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                OnEntityAdd(e);
                return false;
            });
        }

        private static void OnEntityAdd(IMyEntity obj)
        {
            IMyCubeGrid grid = obj as IMyCubeGrid;
            if (grid?.Physics == null)
                return;
            foreach (var block in grid.GetFatBlocks<IMyFunctionalBlock>())
                DefinitionManager.TryCreateControlBlock(block);
            grid.OnBlockAdded += OnBlockAdded;
        }

        private static void OnBlockAdded(IMySlimBlock block)
        {
            var func = block.FatBlock as IMyFunctionalBlock;
            if (func == null)
                return;
            DefinitionManager.TryCreateControlBlock(func);
        }


        private void OnSerialize(IMyEntity obj)
        {
            IControlBlockBase logic;
            var block = obj as IMyCubeBlock;
            if (block == null || !Blocks.TryGetValue(block, out logic))
                return;
            logic.Serialize();
        }

        public void Register(IMyCubeBlock block, IControlBlockBase logic)
        {
            // delay a tick to give everything else time to init properly
            MyAPIGateway.Utilities.InvokeOnGameThread(() =>
            {
                logic.Init();
                Blocks.Add(block, logic);
                block.OnMarkForClose += logic.MarkForClose;
            });
        }

        internal void Update()
        {
            bool update10 = MyAPIGateway.Session.GameplayFrameCounter % 10 == 0;
            foreach (var logic in Blocks.Values)
            {
                logic.UpdateAfterSimulation();
                if (update10) logic.UpdateAfterSimulation10();
            }
        }

        internal static void Unload()
        {
            SerializationNotifier.OnSerialize -= I.OnSerialize;
            I.Blocks = null;
            if (!MyAPIGateway.Utilities.IsDedicated)
                MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            I = null;
            Log.Info("ControlBlockManager", "Unloaded.");
        }

        internal static TLogic GetLogic<TLogic>(IMyCubeBlock block) where TLogic : class, IControlBlockBase
        {
            IControlBlockBase logic;
            if (block == null || !I.Blocks.TryGetValue(block, out logic))
                return null;
            return logic as TLogic;
        }

        private static void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            I.TerminalSelectedBlock = block.EntityId;
        }
    }
}
