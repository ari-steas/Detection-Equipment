using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Client.Sensors
{
    internal static class SensorBlockManager
    {
        public static Dictionary<uint, ClientBlockSensor> BlockSensorIdMap;

        public static void Init()
        {
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                OnEntityAdd(e);
                return false;
            });
            BlockSensorIdMap = new Dictionary<uint, ClientBlockSensor>();
            Log.Info("SensorBlockManager", "Initialized.");
        }

        public static void Update()
        {
            foreach (var sensor in BlockSensorIdMap.Values)
                sensor.UpdateAfterSimulation(); // we're not properly registering the gamelogic so methods must be called manually
        }

        public static void Unload()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            BlockSensorIdMap = null;
            Log.Info("SensorBlockManager", "Unloaded.");
        }

        private static void OnEntityAdd(VRage.ModAPI.IMyEntity obj)
        {
            var grid = obj as IMyCubeGrid;
            if (grid == null)
                return;
            grid.OnBlockAdded += OnBlockAdded;
            foreach (var block in grid.GetFatBlocks<IMyCameraBlock>())
                OnBlockAdded(block.SlimBlock);
        }

        private static void OnBlockAdded(IMySlimBlock block)
        {
            var fatblock = block.FatBlock as IMyCameraBlock;
            if (fatblock == null)
                return;
            if (DefinitionManager.GetDefinitions(fatblock).Count == 0)
                return;

            var logic = new ClientBlockSensor(fatblock);
            MyAPIGateway.Utilities.InvokeOnGameThread(logic.UpdateOnceBeforeFrame);
        }
    }
}
