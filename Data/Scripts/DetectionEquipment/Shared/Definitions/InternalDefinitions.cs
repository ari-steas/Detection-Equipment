using DetectionEquipment.Shared.Utils;
using System;
using System.Collections.Generic;
using VRageMath;
using static DetectionEquipment.Shared.Definitions.SensorDefinition;

namespace DetectionEquipment.Shared.Definitions
{
    internal static class InternalDefinitions
    {
        private static readonly Dictionary<string, SensorDefinition> Definitions = new Dictionary<string, SensorDefinition>()
        {
            // Vanilla Camera
            ["VanillaCamera"] = new SensorDefinition
            {
                BlockSubtypes = new[]
                {
                    "LargeCameraBlock",
                    "SmallCameraBlock",
                    "ActiveRadar_Simple",
                },
                Type = SensorType.Optical,
                MaxAperture = Math.PI/4,
                MinAperture = Math.PI/16,
                DetectionThreshold = 0.001,
                MaxPowerDraw = -1,
                Movement = null,
            },

            // SimpleActiveRadar
            ["SimpleActiveRadar"] = new SensorDefinition
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
            ["SimplePassiveRadar"] = new SensorDefinition
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

        public static void Register()
        {
            Log.Info("InternalDefinitions", "Registering...");
            foreach (var definitionKvp in Definitions)
                DefinitionManager.DefinitionApi.RegisterDefinition(definitionKvp.Key, definitionKvp.Value);
            Log.Info("InternalDefinitions", "Complete.");
        }
    }
}
