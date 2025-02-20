using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using VRage;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace DetectionEquipment.Server.PBApi
{
    internal static class PbApiMethods
    {
        public static ImmutableDictionary<string, Delegate> SafeMethods => ImmutableDictionary.CreateRange(_methods);

        private static Dictionary<string, Delegate> _methods = new Dictionary<string, Delegate>()
        {
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
            ["GetSensorDefinition"] = new Func<uint, MyTuple<int, double, double, MyTuple<double, double, double, double, double, double>?, double, double>>(GetSensorDefinition),
            ["GetSensorDetections"] = new Func<uint, MyTuple<double, double, double, double, Vector3D>[]>(GetSensorDetections),
            ["RegisterInvokeOnDetection"] = new Action<uint, Action<MyTuple<double, double, double, double, Vector3D>>>(RegisterInvokeOnDetection),
            ["UnregisterInvokeOnDetection"] = new Action<uint, Action<MyTuple<double, double, double, double, Vector3D>>>(UnregisterInvokeOnDetection),
        };

        private static uint[] GetSensorIds(IMyCubeBlock block)
        {
            return ServerMain.I.GridSensorMangers[(MyCubeGrid) block.CubeGrid].BlockSensorIdMap[(MyCubeBlock) block];
        }

        private static bool HasSensor(IMyCubeBlock block)
        {
            return ServerMain.I.GridSensorMangers[(MyCubeGrid) block.CubeGrid].BlockSensorIdMap.ContainsKey((MyCubeBlock) block);
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

        private static MyTuple<int, double, double, MyTuple<double, double, double, double, double, double>?, double, double> GetSensorDefinition(uint id)
        {
            var d = ServerMain.I.SensorIdMap[id].Definition;
            return new MyTuple<int, double, double, MyTuple<double, double, double, double, double, double>?, double, double>(
                (int) d.Type,
                d.MaxAperture,
                d.MinAperture,
                d.Movement == null ? null : new MyTuple<double, double, double, double, double, double>?(new MyTuple<double, double, double, double, double, double>(
                    d.Movement.MinAzimuth,
                    d.Movement.MaxAzimuth,
                    d.Movement.MinElevation,
                    d.Movement.MaxElevation,
                    d.Movement.AzimuthRate,
                    d.Movement.ElevationRate
                    )),
                d.DetectionThreshold,
                d.MaxPowerDraw
                );
        }

        private static MyTuple<double, double, double, double, Vector3D>[] GetSensorDetections(uint id)
        {
            var detections = ServerMain.I.BlockSensorIdMap[id].Detections;
            MyTuple<double, double, double, double, Vector3D>[] tupleSet = new MyTuple<double, double, double, double, Vector3D>[detections.Count];
            int i = 0;
            foreach (var detection in detections)
            {
                tupleSet[i] = new MyTuple<double, double, double, double, Vector3D>(detection.CrossSection, detection.Range, detection.RangeError, detection.BearingError, detection.Bearing);
                i++;
            }
            return tupleSet;
        }

        private static void RegisterInvokeOnDetection(uint id, Action<MyTuple<double, double, double, double, Vector3D>> action)
        {
            ServerMain.I.SensorIdMap[id].OnDetection += action;
        }

        private static void UnregisterInvokeOnDetection(uint id, Action<MyTuple<double, double, double, double, Vector3D>> action)
        {
            ServerMain.I.SensorIdMap[id].OnDetection -= action;
        }

    }
}
