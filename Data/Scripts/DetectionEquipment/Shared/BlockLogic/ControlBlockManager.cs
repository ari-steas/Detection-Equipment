using DetectionEquipment.Shared.Utils;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.BlockLogic.GenericControls;

namespace DetectionEquipment.Shared.BlockLogic
{
    internal class ControlBlockManager
    {
        public static ControlBlockManager I;
        public Dictionary<MyCubeBlock, IControlBlockBase> Blocks = new Dictionary<MyCubeBlock, IControlBlockBase>();
        public Dictionary<string, IBlockSelectControl> BlockControls = new Dictionary<string, IBlockSelectControl>();

        public readonly ObjectPool<Dictionary<long, List<WorldDetectionInfo>>> GroupsCacheBuffer = new ObjectPool<Dictionary<long, List<WorldDetectionInfo>>>(
            () => new Dictionary<long, List<WorldDetectionInfo>>(),
            dict => dict.Clear()
        );
        public readonly ObjectPool<List<WorldDetectionInfo>> GroupInfoBuffer = new ObjectPool<List<WorldDetectionInfo>>(
            () => new List<WorldDetectionInfo>(),
            list => list.Clear()
        );

        internal static void Load()
        {
            I = new ControlBlockManager();
            Log.Info("ControlBlockManager", "Ready.");
        }

        internal static void Unload()
        {
            I.Blocks = null;
            I = null;
            Log.Info("ControlBlockManager", "Unloaded.");
        }
    }
}
