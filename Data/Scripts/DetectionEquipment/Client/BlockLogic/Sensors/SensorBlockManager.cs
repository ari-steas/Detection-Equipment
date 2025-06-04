using System.Collections.Generic;
using DetectionEquipment.Client.Networking;
using DetectionEquipment.Shared.Definitions;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DetectionEquipment.Client.BlockLogic.Sensors
{
    internal static class SensorBlockManager
    {
        public static Dictionary<uint, ClientSensorLogic> BlockSensorIdMap;
        public static Dictionary<IMyCubeGrid, List<IMyCubeBlock>> SensorBlocks;

        public static void Load()
        {
            BlockSensorIdMap = new Dictionary<uint, ClientSensorLogic>();
            SensorBlocks = new Dictionary<IMyCubeGrid, List<IMyCubeBlock>>();

            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                OnEntityAdd(e);
                return false;
            });
        }

        public static void Unload()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;

            BlockSensorIdMap = null;
            SensorBlocks = null;
        }

        private static void OnEntityAdd(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;
            if (grid == null)
                return;

            grid.OnBlockAdded += OnBlockAdded;
            foreach (var block in grid.GetFatBlocks<IMyCubeBlock>())
                OnBlockAdded(block.SlimBlock);
        }

        private static void OnBlockAdded(IMySlimBlock slim)
        {
            var block = slim.FatBlock;
            if (block == null)
                return;

            if (DefinitionManager.GetSensorDefinitions(block).Count == 0)
                return;

            ClientNetwork.SendToServer(new SensorInitPacket
            {
                AttachedBlockId = block.EntityId
            });
        }
    }
}
