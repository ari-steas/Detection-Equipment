using System;

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
    }
}
