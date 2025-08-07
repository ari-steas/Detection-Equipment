using DetectionEquipment.Shared.Utils;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace DetectionEquipment.Shared.BlockLogic
{
    internal class ControlBlockManager
    {
        public static ControlBlockManager I;
        public Dictionary<MyCubeBlock, IControlBlockBase> Blocks = new Dictionary<MyCubeBlock, IControlBlockBase>();
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
        }

        internal static void Unload()
        {
            I.Blocks = null;
            if (!MyAPIGateway.Utilities.IsDedicated)
                MyAPIGateway.TerminalControls.CustomControlGetter -= CustomControlGetter;
            I = null;
            Log.Info("ControlBlockManager", "Unloaded.");
        }

        private static void CustomControlGetter(IMyTerminalBlock block, List<IMyTerminalControl> controls)
        {
            I.TerminalSelectedBlock = block.EntityId;
        }
    }
}
