using DetectionEquipment.Shared.Utils;
using System;
using System.Collections.Generic;
using VRageMath;
using static DetectionEquipment.Shared.Definitions.SensorDefinition;

namespace DetectionEquipment.Shared.Definitions
{
    internal static class InternalDefinitions
    {
        private static readonly Dictionary<string, SensorDefinition> SensorDefinitions = new Dictionary<string, SensorDefinition>
        {
            // Vanilla Camera
            ["VanillaCamera"] = new SensorDefinition
            {
                BlockSubtypes = new[]
                {
                    "LargeCameraBlock",
                    "SmallCameraBlock",
                },
                Type = SensorType.Infrared,
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

        private static readonly Dictionary<string, CountermeasureDefinition> CountermeasureDefinitions = new Dictionary<string, CountermeasureDefinition>
        {
            ["SimpleFlare"] = new CountermeasureDefinition
            {
                CountermeasureType = CountermeasureDefinition.CountermeasureTypeEnum.Infrared,
                MaxRange = 10000,
                FalloffScalar = 0.00001f,
                MinNoise = 0.01f,
                FalloffType = CountermeasureDefinition.FalloffTypeEnum.Linear,
                MinEffectAperture = (float) Math.PI,
                MaxEffectAperture = (float) Math.PI,
                MaxLifetime = 600,
                HasPhysics = true,
                DragMultiplier = 0.001f,
                ParticleEffect = "Smoke_Firework"
            }
        };

        private static readonly Dictionary<string, CountermeasureEmitterDefinition> CountermeasureEmitterDefinitions = new Dictionary<string, CountermeasureEmitterDefinition>
        {
            ["SimpleFlareEmitter"] = new CountermeasureEmitterDefinition
            {
                BlockSubtypes = new[]
                {
                    "FlareLauncher"
                },
                Muzzles = new[]
                {
                    "muzzle_01",
                    "muzzle_02",
                    "muzzle_03",
                    "muzzle_04",
                    "muzzle_05",
                    "muzzle_06",
                    "muzzle_07",
                    "muzzle_08",
                    "muzzle_09",
                    "muzzle_10",
                    "muzzle_11",
                    "muzzle_12",
                    "muzzle_13",
                    "muzzle_14",
                    "muzzle_15",
                    "muzzle_16",
                    "muzzle_17",
                    "muzzle_18",
                    "muzzle_19",
                    "muzzle_20",
                    "muzzle_21",
                    "muzzle_22",
                    "muzzle_23",
                    "muzzle_24",
                    "muzzle_25",
                },
                CountermeasureIds = new[]
                {
                    "SimpleFlare"
                },
                IsCountermeasureAttached = false,
                ShotsPerSecond = 15,
                EjectionVelocity = 50,
                FireParticle = "Smoke_Collector"
            }
        };

        public static void Register()
        {
            Log.Info("InternalDefinitions", "Registering...");
            foreach (var definitionKvp in SensorDefinitions)
                DefinitionManager.DefinitionApi.RegisterDefinition(definitionKvp.Key, definitionKvp.Value);
            foreach (var definitionKvp in CountermeasureDefinitions)
                DefinitionManager.DefinitionApi.RegisterDefinition(definitionKvp.Key, definitionKvp.Value);
            foreach (var definitionKvp in CountermeasureEmitterDefinitions)
                DefinitionManager.DefinitionApi.RegisterDefinition(definitionKvp.Key, definitionKvp.Value);
            Log.Info("InternalDefinitions", "Complete.");
        }
    }
}
