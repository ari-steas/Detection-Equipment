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
            ["DetEq_VanillaCamera"] = new SensorDefinition
            {
                BlockSubtypes = new[]
                {
                    "LargeCameraBlock",
                    "LargeCameraTopMounted",
                    "SmallCameraBlock",
                    "SmallCameraTopMounted",
                },
                Type = SensorType.Infrared,
                MaxAperture = Math.PI/4,
                MinAperture = Math.PI/16,
                DetectionThreshold = 0.00001,
                BearingErrorModifier = 0.05,
                RangeErrorModifier = 0.05,
                MaxPowerDraw = -1,
                Movement = null,
            },

            // Gimbal Camera
            ["DetEq_GimbalCamera"] = new SensorDefinition
            {
                BlockSubtypes = new[]
                {
                    "GimbalCamera",
                },
                Type = SensorType.Optical,
                SensorEmpty = "GimbalCameraEmpty",
                MaxAperture = MathHelper.ToRadians(30),
                MinAperture = MathHelper.ToRadians(1),
                DetectionThreshold = 0.001,
                BearingErrorModifier = 0.05,
                RangeErrorModifier = 1,
                MaxPowerDraw = -1,
                Movement = new SensorMovementDefinition
                {
                    AzimuthPart = "gimbalcam_azimuth",
                    AzimuthRate = 2 * Math.PI,
                    MaxAzimuth = Math.PI,
                    MinAzimuth = -Math.PI,

                    ElevationPart = "gimbalcam_elevation",
                    ElevationRate = 1 * Math.PI,
                    MaxElevation = Math.PI/2,
                    MinElevation = -Math.PI/8,
                },
            },

            // SimpleActiveRadar
            ["DetEq_SimpleActiveRadar"] = new SensorDefinition
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
                BearingErrorModifier = 0.1,
                RangeErrorModifier = 0.0001,
                Movement = new SensorMovementDefinition
                {
                    AzimuthPart = "azimuth",
                    AzimuthRate = 1.5 * Math.PI,
                    MaxAzimuth = Math.PI,
                    MinAzimuth = -Math.PI,

                    ElevationPart = "elevation",
                    ElevationRate = 1.5 * Math.PI,
                    MaxElevation = Math.PI/2,
                    MinElevation = -Math.PI/8,
                },
                RadarProperties = new RadarPropertiesDefinition
                {
                    ReceiverArea = 4.9 * 2.7,
                    PowerEfficiencyModifier = 0.00000000000025,
                    Bandwidth = 1.67E6,
                    Frequency = 2800E6,
                }
            },

            // Small Gimbal Radar
            ["DetEq_SmallFixedRadar"] = new SensorDefinition
            {
                BlockSubtypes = new[]
                {
                    "SmallFixedRadar",
                },
                Type = SensorType.Radar,
                MaxAperture = MathHelper.ToRadians(15),
                MinAperture = MathHelper.ToRadians(1),
                DetectionThreshold = 30,
                MaxPowerDraw = 1000000,
                BearingErrorModifier = 0.1,
                RangeErrorModifier = 0.0001,
                Movement = new SensorMovementDefinition
                {
                    AzimuthPart = "smallfixedradar_azimuth",
                    AzimuthRate = 2 * Math.PI,
                    MaxAzimuth = MathHelper.ToRadians(35),
                    MinAzimuth = -MathHelper.ToRadians(35),

                    ElevationPart = "smallfixedradar_elevation",
                    ElevationRate = 2 * Math.PI,
                    MaxElevation = MathHelper.ToRadians(35),
                    MinElevation = -MathHelper.ToRadians(35),
                },
                RadarProperties = new RadarPropertiesDefinition
                {
                    ReceiverArea = 0.5 * 0.5,
                    PowerEfficiencyModifier = 0.00000000000025,
                    Bandwidth = 1.67E6,
                    Frequency = 2800E6,
                }
            },

            // SimplePassiveRadar
            ["DetEq_SimplePassiveRadar"] = new SensorDefinition
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
                BearingErrorModifier = 0.1,
                RangeErrorModifier = 0.0001,
                Movement = null,
                RadarProperties = new RadarPropertiesDefinition
                {
                    ReceiverArea = 2.5 * 2.5,
                }
            },

            // Antennae
            ["DetEq_AntennaSnooperLarge"] = new SensorDefinition
            {
                BlockSubtypes = new[]
                {
                    "LargeBlockRadioAntenna",
                    "LargeBlockCompactRadioAntenna",
                    "LargeBlockRadioAntennaDish",
                },
                Type = SensorType.Antenna,
                MaxAperture = Math.PI,
                MinAperture = Math.PI,
                DetectionThreshold = 30,
                MaxPowerDraw = -1,
                BearingErrorModifier = 0.1,
                RangeErrorModifier = 0.0001,
                Movement = null,
                RadarProperties = new RadarPropertiesDefinition
                {
                    ReceiverArea = 2.5 * 2.5,
                }
            },
        };

        private static readonly Dictionary<string, CountermeasureDefinition> CountermeasureDefinitions = new Dictionary<string, CountermeasureDefinition>
        {
            ["DetEq_SimpleFlare"] = new CountermeasureDefinition
            {
                CountermeasureType = CountermeasureDefinition.CountermeasureTypeEnum.Infrared,
                MaxRange = 10000,
                FalloffScalar = 0.00001f,
                MinNoise = 0.01f,
                FalloffType = CountermeasureDefinition.FalloffTypeEnum.Linear,
                MinEffectAperture = (float) Math.PI,
                MaxEffectAperture = (float) Math.PI,
                MaxLifetime = 300,
                HasPhysics = true,
                DragMultiplier = 0.001f,
                ParticleEffect = "Smoke_Firework",
            },
            ["DetEq_SimpleChaff"] = new CountermeasureDefinition
            {
                CountermeasureType = CountermeasureDefinition.CountermeasureTypeEnum.Radar,
                MaxRange = 10000,
                FalloffScalar = 1.0E10f,
                MinNoise = 0f,
                FalloffType = CountermeasureDefinition.FalloffTypeEnum.Quadratic,
                MinEffectAperture = (float) Math.PI,
                MaxEffectAperture = (float) Math.PI,
                MaxLifetime = 240,
                HasPhysics = true,
                DragMultiplier = 0.001f,
                ParticleEffect = "SimpleChaffParticle",
            },
            ["DetEq_SimpleAreaJammer"] = new CountermeasureDefinition
            {
                CountermeasureType = CountermeasureDefinition.CountermeasureTypeEnum.Radar,
                MaxRange = 50000,
                FalloffScalar = 1.0E12f,
                MinNoise = 0f,
                FalloffType = CountermeasureDefinition.FalloffTypeEnum.Quadratic,
                MinEffectAperture = (float) Math.PI,
                MaxEffectAperture = (float) Math.PI,
                MaxLifetime = uint.MaxValue,
                HasPhysics = false,
                DragMultiplier = 0f,
                ParticleEffect = "",

                //ApplyDrfmToOtherTargets = true,
                //MaxDrfmRange = 50000,
                //ApplyOutsideSensorCone = false,
                //DrfmEffects = (sensorId, counterId, emitter, targetId, targetCrossSection, targetRange, maxRangeErr, targetBearing, maxBearingErr, iffCodes) =>
                //{
                //    Log.Info("SimpleAreaJammer", $"DrfmEffects invoked! {sensorId} {counterId} {emitter?.CustomName ?? "NULLEMM"}, {targetId}, {targetCrossSection}, {targetRange}, {maxRangeErr}, {targetBearing}, {maxBearingErr}, {iffCodes.Length}");
                //    return new MyTuple<double, double, double, Vector3D, double, string[]>(0, 0, 0, targetBearing, 0, iffCodes);
                //}
            }
        };

        private static readonly Dictionary<string, CountermeasureEmitterDefinition> CountermeasureEmitterDefinitions = new Dictionary<string, CountermeasureEmitterDefinition>
        {
            ["DetEq_SimpleFlareEmitter"] = new CountermeasureEmitterDefinition
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
                    "DetEq_SimpleFlare"
                },
                IsCountermeasureAttached = false,
                ShotsPerSecond = 15,
                EjectionVelocity = 50,
                FireParticle = "Muzzle_Flash_Autocannon",
                ActivePowerDraw = 0,
                MagazineSize = 25,
                MagazineItem = "DetEq_FlareMagazine",
                ReloadTime = 12,
                InventorySize = 0.240f,
            },
            ["DetEq_SimpleChaffEmitter"] = new CountermeasureEmitterDefinition
            {
                BlockSubtypes = new[]
                {
                    "ChaffLauncher"
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
                    "DetEq_SimpleChaff"
                },
                IsCountermeasureAttached = false,
                ShotsPerSecond = 15,
                EjectionVelocity = 100,
                FireParticle = "Muzzle_Flash_Autocannon",
                ActivePowerDraw = 0,
                MagazineSize = 25,
                MagazineItem = "DetEq_ChaffMagazine",
                ReloadTime = 12,
                InventorySize = 0.240f,
            },
            ["DetEq_SimpleJammer"] = new CountermeasureEmitterDefinition
            {
                BlockSubtypes = new[]
                {
                    "SimpleJammer"
                },
                Muzzles = new[]
                {
                    "muzzle",
                },
                CountermeasureIds = new[]
                {
                    "DetEq_SimpleAreaJammer"
                },
                IsCountermeasureAttached = true,
                ShotsPerSecond = 60,
                EjectionVelocity = 0,
                FireParticle = "",
                ActivePowerDraw = 50,
                InventorySize = 0,
            }
        };

        private static readonly Dictionary<string, ControlBlockDefinition> ControlBlockDefinitions = new Dictionary<string, ControlBlockDefinition>
        {
            ["DetEq_Aggregator"] = new ControlBlockDefinition
            {
                SubtypeIds = new[]
                {
                    "DetectionAggregatorBlock", "DetectionAggregatorBlock_Small"
                },
                Type = ControlBlockDefinition.LogicType.Aggregator
            },
            ["DetEq_IffAggregator"] = new ControlBlockDefinition
            {
                SubtypeIds = new[]
                {
                    "DetectionIffAggregatorBlock", "DetectionIffAggregatorBlock_Small"
                },
                Type = ControlBlockDefinition.LogicType.IffAggregator
            },
            ["DetEq_HudController"] = new ControlBlockDefinition
            {
                SubtypeIds = new[]
                {
                    "DetectionHudControllerBlock", "DetectionHudControllerBlock_Small"
                },
                Type = ControlBlockDefinition.LogicType.HudController
            },
            ["DetEq_IffReflector"] = new ControlBlockDefinition
            {
                SubtypeIds = new[]
                {
                    "IffReflector", "IffReflector_Small", "TorpIFF"
                },
                Type = ControlBlockDefinition.LogicType.IffReflector
            },
            ["DetEq_Searcher"] = new ControlBlockDefinition
            {
                SubtypeIds = new[]
                {
                    "DetectionSearchBlock", "DetectionSearchBlock_Small"
                },
                Type = ControlBlockDefinition.LogicType.Searcher
            },
            ["DetEq_Tracker"] = new ControlBlockDefinition
            {
                SubtypeIds = new[]
                {
                    "DetectionTrackerBlock", "DetectionTrackerBlock_Small"
                },
                Type = ControlBlockDefinition.LogicType.Tracker
            },
        };

        public static void Register()
        {
            Log.Info("InternalDefinitions", "Registering...");
            Log.IncreaseIndent();

            foreach (var definitionKvp in SensorDefinitions)
            {
                if (DefinitionManager.DefinitionApi.HasDefinition<SensorDefinition>(definitionKvp.Key))
                    continue;

                DefinitionManager.DefinitionApi.RegisterDefinition(definitionKvp.Key, definitionKvp.Value);
                var delegates = definitionKvp.Value.GenerateDelegates();
                if (delegates != null)
                    DefinitionManager.DefinitionApi.RegisterDelegates<SensorDefinition>(definitionKvp.Key, delegates);
            }

            foreach (var definitionKvp in CountermeasureDefinitions)
            {
                if (DefinitionManager.DefinitionApi.HasDefinition<CountermeasureDefinition>(definitionKvp.Key))
                    continue;

                DefinitionManager.DefinitionApi.RegisterDefinition(definitionKvp.Key, definitionKvp.Value);
                var delegates = definitionKvp.Value.GenerateDelegates();
                if (delegates != null)
                    DefinitionManager.DefinitionApi.RegisterDelegates<CountermeasureDefinition>(definitionKvp.Key, delegates);
            }

            foreach (var definitionKvp in CountermeasureEmitterDefinitions)
            {
                if (DefinitionManager.DefinitionApi.HasDefinition<CountermeasureEmitterDefinition>(definitionKvp.Key))
                    continue;

                DefinitionManager.DefinitionApi.RegisterDefinition(definitionKvp.Key, definitionKvp.Value);
                var delegates = definitionKvp.Value.GenerateDelegates();
                if (delegates != null)
                    DefinitionManager.DefinitionApi.RegisterDelegates<CountermeasureEmitterDefinition>(definitionKvp.Key, delegates);
            }

            foreach (var definitionKvp in ControlBlockDefinitions)
            {
                if (DefinitionManager.DefinitionApi.HasDefinition<ControlBlockDefinition>(definitionKvp.Key))
                    continue;

                DefinitionManager.DefinitionApi.RegisterDefinition(definitionKvp.Key, definitionKvp.Value);
                var delegates = definitionKvp.Value.GenerateDelegates();
                if (delegates != null)
                    DefinitionManager.DefinitionApi.RegisterDelegates<ControlBlockDefinition>(definitionKvp.Key, delegates);
            }

            Log.DecreaseIndent();
            Log.Info("InternalDefinitions", "Complete.");
        }
    }
}
