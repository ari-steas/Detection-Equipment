using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Server.Sensors;
using VRageMath;
using DetectionEquipment.Shared.BlockLogic.Aggregator;

using IMyCubeBlock = VRage.Game.ModAPI.Ingame.IMyCubeBlock;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;
using VRage.Scripting.MemorySafeTypes;
using DetectionEquipment.Shared.Utils;
using DetectionEquipment.Shared.BlockLogic;
using DetectionEquipment.Shared.BlockLogic.IffReflector;
using DetectionEquipment.Shared.Structs;

namespace DetectionEquipment.Server.PBApi
{
    internal static class PbApiMethods
    {
        public static ImmutableDictionary<string, Delegate> SafeMethods => ImmutableDictionary.CreateRange(Methods);

        private static readonly Dictionary<string, Delegate> Methods = new Dictionary<string, Delegate>
        {
            // Utils
            ["ReturnFieldArray"] = new Action<object[]>(ObjectPackager.Return),

            // Sensors
            ["GetSensorIds"] = new Func<IMyCubeBlock, uint[]>(GetSensorIds),
            ["HasSensor"] = new Func<IMyCubeBlock, bool>(HasSensor),
            ["GetSensorPosition"] = new Func<uint, Vector3D>(GetSensorPosition),
            ["GetSensorDirection"] = new Func<uint, Vector3D>(GetSensorDirection),
            ["GetSensorAperture"] = new Func<uint, double>(GetSensorAperture),
            ["SetSensorAperture"] = new Action<uint, double>(SetSensorAperture),
            ["GetSensorAzimuth"] = new Func<uint, double>(GetSensorAzimuth),
            ["SetSensorAzimuth"] = new Action<uint, double>(SetSensorAzimuth),
            ["GetSensorElevation"] = new Func<uint, double>(GetSensorElevation),
            ["SetSensorElevation"] = new Action<uint, double>(SetSensorElevation),
            ["GetSensorDefinition"] = new Func<uint, object[]>(GetSensorDefinition),
            ["GetSensorDetections"] = new Func<uint, object[][]>(GetSensorDetections),
            ["RegisterInvokeOnDetection"] = new Action<uint, Action<object[]>>(RegisterInvokeOnDetection),
            ["UnregisterInvokeOnDetection"] = new Action<uint, Action<object[]>>(UnregisterInvokeOnDetection),

            // Aggregator
            ["HasAggregator"] = new Func<IMyCubeBlock, bool>(HasAggregator),
            ["GetAggregatorTime"] = new Func<IMyCubeBlock, float>(GetAggregatorTime),
            ["SetAggregatorTime"] = new Action<IMyCubeBlock, float>(SetAggregatorTime),
            ["GetAggregatorVelocity"] = new Func<IMyCubeBlock, float>(GetAggregatorVelocity),
            ["SetAggregatorVelocity"] = new Action<IMyCubeBlock, float>(SetAggregatorVelocity),
            ["GetAggregatorInfo"] = new Func<IMyCubeBlock, object[][]>(GetAggregatorInfo),
            ["GetAggregatorUseAllSensors"] = new Func<IMyCubeBlock, bool>(GetAggregatorUseAllSensors),
            ["SetAggregatorUseAllSensors"] = new Action<IMyCubeBlock, bool>(SetAggregatorUseAllSensors),
            ["GetAggregatorActiveSensors"] = new Func<IMyCubeBlock, MemorySafeList<IMyTerminalBlock>>(GetAggregatorActiveSensors),
            ["SetAggregatorActiveSensors"] = new Action<IMyCubeBlock, MemorySafeList<IMyTerminalBlock>>(SetAggregatorActiveSensors),

            // IFF Reflector
            ["HasReflector"] = new Func<IMyCubeBlock, bool>(HasReflector),
            ["GetIffCode"] = new Func<IMyCubeBlock, string>(GetIffCode),
            ["SetIffCode"] = new Action<IMyCubeBlock, string>(SetIffCode),
            ["GetIffReturnHashed"] = new Func<IMyCubeBlock, bool>(GetIffReturnHashed),
            ["SetIffReturnHashed"] = new Action<IMyCubeBlock, bool>(SetIffReturnHashed),
        };

        #region Sensors
        private static uint[] GetSensorIds(IMyCubeBlock block)
        {
            List<BlockSensor> sensors;
            if (!ServerMain.I.GridSensorMangers[(MyCubeGrid)block.CubeGrid].BlockSensorMap
                    .TryGetValue((MyCubeBlock)block, out sensors))
                return Array.Empty<uint>();

            var ids = new uint[sensors.Count];
            for (var i = 0; i < sensors.Count; i++)
                ids[i] = sensors[i].Sensor.Id;
            return ids;
        }

        private static bool HasSensor(IMyCubeBlock block)
        {
            return ServerMain.I.GridSensorMangers[(MyCubeGrid) block.CubeGrid].BlockSensorMap.ContainsKey((MyCubeBlock) block);
        }

        private static Vector3D GetSensorPosition(uint id)
        {
            return ServerMain.I.SensorIdMap[id].Position;
        }

        private static Vector3D GetSensorDirection(uint id)
        {
            return ServerMain.I.SensorIdMap[id].Direction;
        }

        private static double GetSensorAperture(uint id)
        {
            return ServerMain.I.SensorIdMap[id].Aperture;
        }

        private static void SetSensorAperture(uint id, double value)
        {
            ServerMain.I.SensorIdMap[id].Aperture = value;
        }

        private static double GetSensorAzimuth(uint id)
        {
            return ServerMain.I.BlockSensorIdMap[id].Azimuth;
        }

        private static void SetSensorAzimuth(uint id, double value)
        {
            ServerMain.I.BlockSensorIdMap[id].DesiredAzimuth = value;
        }

        private static double GetSensorElevation(uint id)
        {
            return ServerMain.I.BlockSensorIdMap[id].Elevation;
        }

        private static void SetSensorElevation(uint id, double value)
        {
            ServerMain.I.BlockSensorIdMap[id].DesiredElevation = value;
        }

        private static object[] GetSensorDefinition(uint id)
        {
            ISensor sensor;
            if (!ServerMain.I.SensorIdMap.TryGetValue(id, out sensor))
                return null;
            return ObjectPackager.Package(sensor.Definition);
        }

        private static object[][] GetSensorDetections(uint id)
        {
            var detections = ServerMain.I.BlockSensorIdMap[id].Detections;
            var tupleSet = new object[detections.Count][];
            int i = 0;
            foreach (var detection in detections)
            {
                tupleSet[i] = ObjectPackager.Package(detection);
                i++;
            }
            return tupleSet;
        }

        private static void RegisterInvokeOnDetection(uint id, Action<object[]> action)
        {
            ServerMain.I.SensorIdMap[id].OnDetection += action;
        }

        private static void UnregisterInvokeOnDetection(uint id, Action<object[]> action)
        {
            ServerMain.I.SensorIdMap[id].OnDetection -= action;
        }
        #endregion

        #region Aggregator

        private static bool HasAggregator(IMyCubeBlock block)
        {
            return ((MyCubeBlock)block).GameLogic.GetAs<AggregatorBlock>() != null;
        }
        private static float GetAggregatorTime(IMyCubeBlock block)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control))
                return -1;
            return (control as AggregatorBlock)?.AggregationTime.Value ?? -1;
        }
        private static void SetAggregatorTime(IMyCubeBlock block, float value)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control) || !(control is AggregatorBlock))
                return;
            ((AggregatorBlock)control).AggregationTime.Value = value;
        }
        private static float GetAggregatorVelocity(IMyCubeBlock block)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control))
                return -1;
            return (control as AggregatorBlock)?.VelocityErrorThreshold.Value ?? -1;
        }
        private static void SetAggregatorVelocity(IMyCubeBlock block, float value)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control) || !(control is AggregatorBlock))
                return;
            ((AggregatorBlock)control).VelocityErrorThreshold.Value = value;
        }
        private static object[][] GetAggregatorInfo(IMyCubeBlock block)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control) || !(control is AggregatorBlock))
                return null;

            var set = ((AggregatorBlock)control).DetectionSet;
            var toReturn = new object[set.Count][];

            int i = 0;
            foreach (var value in set)
            {
                toReturn[i] = ObjectPackager.Package(value);
                i++;
            }

            return toReturn;
        }

        private static bool GetAggregatorUseAllSensors(IMyCubeBlock block)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control))
                return false;
            return (control as AggregatorBlock)?.UseAllSensors ?? false;
        }

        private static void SetAggregatorUseAllSensors(IMyCubeBlock block, bool value)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control) || !(control is AggregatorBlock))
                return;
            ((AggregatorBlock)control).UseAllSensors.Value = value;
        }

        private static MemorySafeList<IMyTerminalBlock> GetAggregatorActiveSensors(IMyCubeBlock block)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control))
                return null;
            var aggregator = control as AggregatorBlock;
            if (aggregator == null)
                return null;

            var active = new MemorySafeList<IMyTerminalBlock>(aggregator.ActiveSensors.Count);
            foreach (var sensor in aggregator.ActiveSensors)
                active.Add(sensor.Block);

            return active;
        }

        private static void SetAggregatorActiveSensors(IMyCubeBlock block, MemorySafeList<IMyTerminalBlock> value)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control) || !(control is AggregatorBlock))
                return;

            var valid = new List<long>();
            var gridSensors = control.GridSensors.Sensors;
            // Validate entityIds
            foreach (var sensor in gridSensors)
            {
                foreach (var valBlock in value)
                {
                    if (sensor.Block != valBlock)
                        continue;
                    valid.Add(valBlock.EntityId);
                    break;
                }
            }

            AggregatorControls.ActiveSensorSelect.UpdateSelected(control, valid.ToArray());
        }

        #endregion

        #region Iff Reflector

        private static bool HasReflector(IMyCubeBlock block)
        {
            return ((MyCubeBlock)block).GameLogic.GetAs<IffReflectorBlock>() != null;
        }
        private static string GetIffCode(IMyCubeBlock block)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control) || !(control is IffReflectorBlock))
                return null;
            IffReflectorBlock reflector = (IffReflectorBlock)control;
            return reflector.IffCode;
        }
        private static void SetIffCode(IMyCubeBlock block, string value)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control) || !(control is IffReflectorBlock))
                return;
            IffReflectorBlock reflector = (IffReflectorBlock)control;
            reflector.IffCode.Value = value.RemoveChars(',', '#', '&').Trim();
        }
        private static bool GetIffReturnHashed(IMyCubeBlock block)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control) || !(control is IffReflectorBlock))
                return false;
            IffReflectorBlock reflector = (IffReflectorBlock)control;
            return reflector.ReturnHash;
        }
        private static void SetIffReturnHashed(IMyCubeBlock block, bool value)
        {
            IControlBlockBase control;
            if (!ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock) block, out control) || !(control is IffReflectorBlock))
                return;
            IffReflectorBlock reflector = (IffReflectorBlock)control;
            reflector.ReturnHash.Value = value;
        }

        #endregion
    }
}
