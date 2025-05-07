using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.Structs;

namespace DetectionEquipment.Shared.BlockLogic
{
    internal class ControlBlockManager
    {
        public static ControlBlockManager I;
        public Dictionary<MyCubeBlock, IControlBlockBase> Blocks = new Dictionary<MyCubeBlock, IControlBlockBase>();
        public Dictionary<string, IBlockSelectControl> BlockControls = new Dictionary<string, IBlockSelectControl>();

        internal static void Load()
        {
            I = new ControlBlockManager();
            AggregatorBlock.GroupInfoBuffer = new Stack<List<WorldDetectionInfo>>();
            Log.Info("ControlBlockManager", "Ready.");
        }

        internal static void Unload()
        {
            AggregatorBlock.GroupInfoBuffer = null;
            I.Blocks = null;
            I = null;
            Log.Info("ControlBlockManager", "Unloaded.");
        }
    }
}
