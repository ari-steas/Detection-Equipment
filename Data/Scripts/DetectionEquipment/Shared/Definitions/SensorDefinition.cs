using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DetectionEquipment.Shared.Definitions
{
    public struct SensorDefinition
    {
        public string[] BlockSubtypes;
        public SensorType Type;
        public double MaxAperture;
        public double MinAperture;
        public SensorMovementDefinition? Movement;
        public double DetectionThreshold;
        public double MaxPowerDraw;

        public struct SensorMovementDefinition
        {
            public string AzimuthPart;
            public string ElevationPart;
            public double MinAzimuth;
            public double MaxAzimuth;
            public double MinElevation;
            public double MaxElevation;
            public double AzimuthRate;
            public double ElevationRate;
        }

        public enum SensorType
        {
            Radar = 0,
            PassiveRadar = 1,
            Optical = 2,
            Infrared = 3,
        }
    }
}
