using System;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Client.Sensors
{
    internal static class SensorBlockManager
    {
        public static Dictionary<uint, ClientBlockSensor> BlockSensorIdMap;
        public static Dictionary<IMyCubeGrid, HashSet<IMyCubeBlock>> GridBlockSensorsMap;
        private static List<uint> _deadSensors;

        // These two fields could cause memory leaks with a high rate of packet loss.
        // Howver, they are quite important to prevent low-latency client<->server sensor init from failing.
        private static Dictionary<IMyCameraBlock, List<SensorInitPacket>> _delayedInitPackets;
        private static Dictionary<uint, SensorUpdatePacket> _delayedUpdatePackets;

        public static void Init()
        {
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.OnEntityAdd += OnEntityRemove;
            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                OnEntityAdd(e);
                return false;
            });
            BlockSensorIdMap = new Dictionary<uint, ClientBlockSensor>();
            GridBlockSensorsMap = new Dictionary<IMyCubeGrid, HashSet<IMyCubeBlock>>();
            _deadSensors = new List<uint>();
            _delayedInitPackets = new Dictionary<IMyCameraBlock, List<SensorInitPacket>>();
            _delayedUpdatePackets = new Dictionary<uint, SensorUpdatePacket>();

            Log.Info("SensorBlockManager", "Initialized.");
        }

        public static void Update()
        {
            foreach (var sensor in BlockSensorIdMap)
            {
                sensor.Value.UpdateAfterSimulation(); // we're not properly registering the gamelogic so methods must be called manually
                if (sensor.Value.Block.Closed)
                    _deadSensors.Add(sensor.Key);
            }

            foreach (var id in _deadSensors)
                BlockSensorIdMap.Remove(id);
        }

        public static void Unload()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityRemove;
            BlockSensorIdMap = null;
            GridBlockSensorsMap = null;
            _delayedInitPackets = null;
            _delayedUpdatePackets = null;
            Log.Info("SensorBlockManager", "Unloaded.");
        }

        public static void TryRegisterSensor(IMyCameraBlock block, SensorInitPacket packet)
        {
            var logic = block.GameLogic?.GetAs<ClientBlockSensor>();
            if (logic != null)
                logic.RegisterSensor(packet);
            else
            {
                if (!_delayedInitPackets.ContainsKey(block))
                    _delayedInitPackets[block] = new List<SensorInitPacket> { packet };
                else
                    _delayedInitPackets[block].Add(packet);
            }
        }

        public static void TryUpdateSensor(uint logicId, SensorUpdatePacket packet)
        {
            if (BlockSensorIdMap.ContainsKey(logicId))
                BlockSensorIdMap[logicId].UpdateFromPacket(packet);
            else
                _delayedUpdatePackets[logicId] = packet;
        }

        private static void OnEntityAdd(VRage.ModAPI.IMyEntity obj)
        {
            try
            {
                var grid = obj as IMyCubeGrid;
                if (grid?.Physics == null)
                    return;
                grid.OnBlockAdded += OnBlockAdded;
                foreach (var block in grid.GetFatBlocks<IMyCameraBlock>())
                    OnBlockAdded(block.SlimBlock);
            }
            catch (Exception ex)
            {
                Log.Exception("SensorBlockManager", ex, true);
            }
        }

        private static void OnEntityRemove(VRage.ModAPI.IMyEntity obj)
        {
            try
            {
                var grid = obj as IMyCubeGrid;
                if (grid == null)
                    return;
                GridBlockSensorsMap.Remove(grid);
            }
            catch (Exception ex)
            {
                Log.Exception("SensorBlockManager", ex, true);
            }
        }

        private static void OnBlockAdded(IMySlimBlock block)
        {
            var fatblock = block.FatBlock as IMyCameraBlock;
            if (fatblock == null)
                return;
            if (DefinitionManager.GetSensorDefinitions(fatblock).Count == 0)
                return;

            var logic = new ClientBlockSensor(fatblock);
            logic.UpdateOnceBeforeFrame();
            
            if (GridBlockSensorsMap.ContainsKey(block.CubeGrid))
                GridBlockSensorsMap[block.CubeGrid].Add(fatblock);
            else
                GridBlockSensorsMap[block.CubeGrid] = new HashSet<IMyCubeBlock> { fatblock };
            logic.OnClose += () => GridBlockSensorsMap[block.CubeGrid].Remove(fatblock);

            if (_delayedInitPackets.ContainsKey(fatblock))
            {
                foreach (var initPacket in _delayedInitPackets[fatblock])
                {
                    logic.RegisterSensor(initPacket);
                    if (_delayedUpdatePackets.ContainsKey(initPacket.Id))
                    {
                        logic.UpdateFromPacket(_delayedUpdatePackets[initPacket.Id]);
                        _delayedUpdatePackets.Remove(initPacket.Id);
                    }
                }
                _delayedInitPackets.Remove(fatblock);
            }
        }
    }
}
