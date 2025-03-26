using DetectionEquipment.Client.Sensors;
using DetectionEquipment.Server;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.Serialization
{
    [ProtoContract]
    public class BlockSensorSettings
    {
        [ProtoMember(1)] public int[] DefinitionIdMap;
        [ProtoMember(2)] public float[] Azimuth;
        [ProtoMember(3)] public float[] Elevation;
        [ProtoMember(4)] public float[] Aperture;

        private BlockSensorSettings() { }

        internal BlockSensorSettings(ClientBlockSensor sensorBlock)
        {
            var sensors = sensorBlock.Sensors.Values.ToList();

            DefinitionIdMap = new int[sensors.Count];
            Azimuth = new float[sensors.Count];
            Elevation = new float[sensors.Count];
            Aperture = new float[sensors.Count];

            for (int i = 0; i < sensors.Count; i++)
            {
                DefinitionIdMap[i] = sensors[i].Definition.Id;
                Azimuth[i] = sensors[i].Azimuth;
                Elevation[i] = sensors[i].Elevation;
                Aperture[i] = sensors[i].Aperture;
            }
        }

        internal BlockSensorSettings(List<BlockSensor> sensors)
        {
            DefinitionIdMap = new int[sensors.Count];
            Azimuth = new float[sensors.Count];
            Elevation = new float[sensors.Count];
            Aperture = new float[sensors.Count];

            for (int i = 0; i < sensors.Count; i++)
            {
                DefinitionIdMap[i] = sensors[i].Definition.Id;
                Azimuth[i] = (float) sensors[i].DesiredAzimuth;
                Elevation[i] = (float) sensors[i].DesiredElevation;
                Aperture[i] = (float) sensors[i].Aperture;
            }
        }

        internal static void LoadBlockSettings(IMyCubeBlock block, List<BlockSensor> sensors)
        {
            if (block.Storage == null)
            {
                LoadDefaultSettings(block, sensors);
                //Log.Info("BlockSensorSettings", $"Failed to load {block.EntityId} sensor data because storage is null.");
                return;
            }


            string rawData;
            if (!block.Storage.TryGetValue(GlobalData.SettingsGuid, out rawData))
            {
                LoadDefaultSettings(block, sensors);
                //Log.Info("BlockSensorSettings", $"Failed to load {block.EntityId} sensor data because storage does not contain relevant data.");
                return;
            }

            try
            {
                var loadedSettings = MyAPIGateway.Utilities.SerializeFromBinary<BlockSensorSettings>(Convert.FromBase64String(rawData));

                if (loadedSettings == null)
                    return;

                int loadedCount = 0;
                foreach (var sensor in sensors)
                {
                    // Find sensor index in settings
                    int idx = -1;
                    for (int i = 0; i < loadedSettings.DefinitionIdMap.Length; i++)
                    {
                        if (loadedSettings.DefinitionIdMap[i] != sensor.Definition.Id)
                            continue;
                        idx = i;
                        break;
                    }
                    if (idx == -1)
                        continue; // Load default settings for missing sensor

                    sensor.DesiredAzimuth = loadedSettings.Azimuth[idx];
                    sensor.DesiredElevation = loadedSettings.Elevation[idx];
                    sensor.Aperture = loadedSettings.Aperture[idx];
                    loadedCount++;
                }

                //Log.Info("BlockSensorSettings", $"Loaded {loadedCount} of {sensors.Count} sensor data(s) for block {block.EntityId}.");
            }
            catch (Exception e)
            {
                Log.Exception("LoadSensorBlockSettings", e);
            }
        }

        private static void LoadDefaultSettings(IMyCubeBlock block, List<BlockSensor> sensors)
        {
            // do nothing for now
        }

        internal static void SaveBlockSettings(IMyCubeBlock block)
        {
            GridSensorManager manager;
            if (!ServerMain.I.GridSensorMangers.TryGetValue(block.CubeGrid, out manager))
                return;

            uint[] ids;
            if (!manager.BlockSensorIdMap.TryGetValue(block, out ids))
                return;

            var sensorSet = manager.Sensors.Where(sensor => ids.Contains(sensor.Sensor.Id));
            if (!sensorSet.Any())
                return;

            SaveBlockSettings(block, sensorSet.ToList());
        }

        internal static void SaveBlockSettings(IMyCubeBlock block, List<BlockSensor> sensors)
        {
            var settings = new BlockSensorSettings(sensors);

            if (block == null)
                return; // called too soon or after it was already closed, ignore

            if (block.Storage == null)
            {
                block.Storage = new MyModStorageComponent();
                //Log.Info("BlockSensorSettings", $"Created new storage component for block {block.EntityId}.");
            }

            block.Storage.SetValue(GlobalData.SettingsGuid, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(settings)));
            //Log.Info("BlockSensorSettings", $"Saved {sensors.Count} sensor data(s) for block {block.EntityId}.");
        }
    }
}
