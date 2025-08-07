using DetectionEquipment.Server;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Client.BlockLogic.Sensors;
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
        [ProtoMember(5)] public bool[] AllowMechanicalControl;
        [ProtoMember(6)] public float[] MinAzimuth;
        [ProtoMember(7)] public float[] MaxAzimuth;
        [ProtoMember(8)] public float[] MinElevation;
        [ProtoMember(9)] public float[] MaxElevation;

        private BlockSensorSettings() { }

        internal BlockSensorSettings(ClientSensorLogic sensorBlock)
        {
            var sensors = sensorBlock.Sensors.Values.ToList();

            DefinitionIdMap = new int[sensors.Count];
            Azimuth = new float[sensors.Count];
            Elevation = new float[sensors.Count];
            Aperture = new float[sensors.Count];
            AllowMechanicalControl = new bool[sensors.Count];
            MinAzimuth = new float[sensors.Count];
            MaxAzimuth = new float[sensors.Count];
            MinElevation = new float[sensors.Count];
            MaxElevation = new float[sensors.Count];

            for (int i = 0; i < sensors.Count; i++)
            {
                DefinitionIdMap[i] = sensors[i].Definition.Id;
                Azimuth[i] = sensors[i].DesiredAzimuth;
                Elevation[i] = sensors[i].DesiredElevation;
                Aperture[i] = sensors[i].Aperture;
                AllowMechanicalControl[i] = sensors[i].AllowMechanicalControl;
                MinAzimuth[i] = sensors[i].MinAzimuth;
                MaxAzimuth[i] = sensors[i].MaxAzimuth;
                MinElevation[i] = sensors[i].MinElevation;
                MaxElevation[i] = sensors[i].MaxElevation;
            }
        }

        internal BlockSensorSettings(List<BlockSensor> sensors)
        {
            DefinitionIdMap = new int[sensors.Count];
            Azimuth = new float[sensors.Count];
            Elevation = new float[sensors.Count];
            Aperture = new float[sensors.Count];
            AllowMechanicalControl = new bool[sensors.Count];
            MinAzimuth = new float[sensors.Count];
            MaxAzimuth = new float[sensors.Count];
            MinElevation = new float[sensors.Count];
            MaxElevation = new float[sensors.Count];

            for (int i = 0; i < sensors.Count; i++)
            {
                DefinitionIdMap[i] = sensors[i].Definition.Id;
                Azimuth[i] = (float) sensors[i].DesiredAzimuth;
                Elevation[i] = (float) sensors[i].DesiredElevation;
                Aperture[i] = (float) sensors[i].Aperture;
                AllowMechanicalControl[i] = sensors[i].AllowMechanicalControl;
                MinAzimuth[i] = (float) sensors[i].MinAzimuth;
                MaxAzimuth[i] = (float) sensors[i].MaxAzimuth;
                MinElevation[i] = (float) sensors[i].MinElevation;
                MaxElevation[i] = (float) sensors[i].MaxElevation;
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
            if (!block.Storage.TryGetValue(GlobalData.SensorSettingsGuid, out rawData))
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

                //int loadedCount = 0;
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

                    // backwards-compatibility for settings
                    if (loadedSettings.MinAzimuth != null)
                    {
                        sensor.AllowMechanicalControl = loadedSettings.AllowMechanicalControl[idx];
                        sensor.MinAzimuth = loadedSettings.MinAzimuth[idx];
                        sensor.MaxAzimuth = loadedSettings.MaxAzimuth[idx];
                        sensor.MinElevation = loadedSettings.MinElevation[idx];
                        sensor.MaxElevation = loadedSettings.MaxElevation[idx];
                    }
                    else
                    {
                        sensor.AllowMechanicalControl = true;
                        sensor.MinAzimuth = sensor.Definition.Movement?.MinAzimuth ?? 0;
                        sensor.MaxAzimuth = sensor.Definition.Movement?.MaxAzimuth ?? 0;
                        sensor.MinElevation = sensor.Definition.Movement?.MinElevation ?? 0;
                        sensor.MaxElevation = sensor.Definition.Movement?.MaxElevation ?? 0;
                    }
                    

                    //loadedCount++;
                }

                //Log.Info("BlockSensorSettings", $"Loaded {loadedCount} of {sensors.Count} sensor data(s) for block {block.EntityId}.");
            }
            catch (Exception e)
            {
                Log.Exception($"LoadSensorBlockSettings::{block.BlockDefinition.SubtypeName}", e);
            }
        }

        private static void LoadDefaultSettings(IMyCubeBlock block, List<BlockSensor> sensors)
        {
            foreach (var sensor in sensors)
            {
                sensor.LoadDefaultSettings();
            }
        }

        internal static void SaveBlockSettings(IMyCubeBlock block)
        {
            GridSensorManager manager;
            if (!ServerMain.I.GridSensorMangers.TryGetValue(block.CubeGrid, out manager))
                return;

            List<BlockSensor> sensors;
            if (!manager.BlockSensorMap.TryGetValue(block, out sensors) || sensors.Count == 0)
                return;

            SaveBlockSettings(block, sensors);
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

            block.Storage.SetValue(GlobalData.SensorSettingsGuid, Convert.ToBase64String(MyAPIGateway.Utilities.SerializeToBinary(settings)));
            //Log.Info("BlockSensorSettings", $"Saved {sensors.Count} sensor data(s) for block {block.EntityId}.");
        }
    }
}
