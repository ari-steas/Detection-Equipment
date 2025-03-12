using DetectionEquipment.Server.SensorBlocks;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRageMath;
using static DetectionEquipment.Shared.Definitions.SensorDefinition;

namespace DetectionEquipment.Shared.Definitions
{
    internal static class DefinitionManager
    {
        public static List<SensorDefinition> Definitions = new List<SensorDefinition>()
        {
            // Vanilla Camera
            new SensorDefinition
            {
                BlockSubtypes = new[]
                {
                    "LargeCameraBlock",
                    "SmallCameraBlock"
                },
                Type = SensorType.Optical,
                MaxAperture = Math.PI/4,
                MinAperture = Math.PI/16,
                DetectionThreshold = 0.001,
                MaxPowerDraw = -1,
                Movement = null,
            },

            // SimpleActiveRadar
            new SensorDefinition
            {
                BlockSubtypes = new[]
                {
                    "ActiveRadar_Simple",
                },
                Type = SensorType.Radar,
                MaxAperture = MathHelper.ToRadians(15),
                MinAperture = MathHelper.ToRadians(1),
                DetectionThreshold = 30,
                MaxPowerDraw = 14000000,
                Movement = new SensorMovementDefinition
                {
                    AzimuthPart = "azimuth",
                    AzimuthRate = 4 * Math.PI / 60,
                    MaxAzimuth = Math.PI,
                    MinAzimuth = -Math.PI,

                    ElevationPart = "elevation",
                    ElevationRate = 2 * Math.PI,
                    MaxElevation = Math.PI/2,
                    MinElevation = -Math.PI/8,
                },
            },

            // SimplePassiveRadar
            new SensorDefinition
            {
                BlockSubtypes = new[]
                {
                    "PassiveRadar_Simple",
                },
                Type = SensorType.PassiveRadar,
                MaxAperture = Math.PI,
                MinAperture = Math.PI,
                DetectionThreshold = 30,
                MaxPowerDraw = -1,
                Movement = null,
            },
        };

        public static List<BlockSensor> TryCreateSensors(IMyFunctionalBlock block)
        {
            var sensors = new List<BlockSensor>();
            foreach (var definition in Definitions)
            {
                if (!definition.BlockSubtypes.Contains(block.BlockDefinition.SubtypeName))
                    continue;
                sensors.Add(new BlockSensor(block, definition));
            }
            return sensors;
        }
    }
}
