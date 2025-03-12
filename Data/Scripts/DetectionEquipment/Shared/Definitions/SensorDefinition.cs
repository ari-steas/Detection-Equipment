using System;
using VRage;
using SensorDefTuple = VRage.MyTuple<int, double, double, VRage.MyTuple<double, double, double, double, double, double>?, double, double>;

namespace DetectionEquipment.Shared.Definitions
{
    public struct SensorDefinition
    {
        public string[] BlockSubtypes;
        public SensorType Type;
        public double MaxAperture;
        public double MinAperture;
        public SensorMovementDefinition Movement;
        public double DetectionThreshold;
        public double MaxPowerDraw;

        public class SensorMovementDefinition
        {
            public string AzimuthPart;
            public string ElevationPart;
            public double MinAzimuth;
            public double MaxAzimuth;
            public double MinElevation;
            public double MaxElevation;
            public double AzimuthRate;
            public double ElevationRate;

            public bool CanRotateFull => MaxAzimuth >= Math.PI && MinAzimuth <= -Math.PI;
            public bool CanElevateFull => MaxElevation >= Math.PI && MinElevation <= -Math.PI;
        }

        public enum SensorType
        {
            None = 0,
            Radar = 1,
            PassiveRadar = 2,
            Optical = 3,
            Infrared = 4,
        }

        public static explicit operator SensorDefTuple(SensorDefinition d) => new MyTuple<int, double, double, MyTuple<double, double, double, double, double, double>?, double, double>(
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
}
