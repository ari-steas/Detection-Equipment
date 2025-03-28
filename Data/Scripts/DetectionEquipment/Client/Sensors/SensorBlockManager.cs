﻿using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Client.Sensors
{
    internal static class SensorBlockManager
    {
        public static Dictionary<uint, ClientBlockSensor> BlockSensorIdMap;
        public static Dictionary<IMyCubeGrid, HashSet<IMyCubeBlock>> GridBlockSensorsMap;

        // These two fields could cause memory leaks with a high rate of packet loss.
        // Howver, they are quite important to prevent low-latency client<->server sensor init from failing.
        private static Dictionary<IMyCameraBlock, List<SensorInitPacket>> DelayedInitPackets;
        private static Dictionary<uint, SensorUpdatePacket> DelayedUpdatePackets;

        public static void Init()
        {
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                OnEntityAdd(e);
                return false;
            });
            BlockSensorIdMap = new Dictionary<uint, ClientBlockSensor>();
            GridBlockSensorsMap = new Dictionary<IMyCubeGrid, HashSet<IMyCubeBlock>>();
            DelayedInitPackets = new Dictionary<IMyCameraBlock, List<SensorInitPacket>>();
            DelayedUpdatePackets = new Dictionary<uint, SensorUpdatePacket>();
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
            GridBlockSensorsMap = null;
            DelayedInitPackets = null;
            DelayedUpdatePackets = null;
            Log.Info("SensorBlockManager", "Unloaded.");
        }

        public static void TryRegisterSensor(IMyCameraBlock block, SensorInitPacket packet)
        {
            var logic = block.GameLogic?.GetAs<ClientBlockSensor>();
            if (logic != null)
                logic.RegisterSensor(packet);
            else
            {
                if (!DelayedInitPackets.ContainsKey(block))
                    DelayedInitPackets[block] = new List<SensorInitPacket>() { packet };
                else
                    DelayedInitPackets[block].Add(packet);
            }
        }

        public static void TryUpdateSensor(uint logicId, SensorUpdatePacket packet)
        {
            if (BlockSensorIdMap.ContainsKey(logicId))
                BlockSensorIdMap[logicId].UpdateFromPacket(packet);
            else
                DelayedUpdatePackets[logicId] = packet;
        }

        private static void OnEntityAdd(VRage.ModAPI.IMyEntity obj)
        {
            var grid = obj as IMyCubeGrid;
            if (grid?.Physics == null)
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
            logic.UpdateOnceBeforeFrame();
            
            if (GridBlockSensorsMap.ContainsKey(block.CubeGrid))
                GridBlockSensorsMap[block.CubeGrid].Add(fatblock);
            else
                GridBlockSensorsMap[block.CubeGrid] = new HashSet<IMyCubeBlock>() { fatblock };
            logic.OnClose += () => GridBlockSensorsMap[block.CubeGrid].Remove(fatblock);

            if (DelayedInitPackets.ContainsKey(fatblock))
            {
                foreach (var initPacket in DelayedInitPackets[fatblock])
                {
                    logic.RegisterSensor(initPacket);
                    if (DelayedUpdatePackets.ContainsKey(initPacket.Id))
                    {
                        logic.UpdateFromPacket(DelayedUpdatePackets[initPacket.Id]);
                        DelayedUpdatePackets.Remove(initPacket.Id);
                    }
                }
                DelayedInitPackets.Remove(fatblock);
            }
        }
    }
}
