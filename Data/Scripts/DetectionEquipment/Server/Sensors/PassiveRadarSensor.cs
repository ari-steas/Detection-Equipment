using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Helpers;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using DetectionEquipment.Server.Countermeasures;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;

namespace DetectionEquipment.Server.Sensors
{
    internal class PassiveRadarSensor : ISensor
    {
        public bool Enabled { get; set; } = true;
        public uint Id { get; private set; }
        public readonly IMyEntity AttachedEntity;
        public SensorDefinition Definition { get; private set; }
        public Action<object[]> OnDetection { get; set; } = null;
        public Vector3D Position { get; set; }
        public Vector3D Direction { get; set; }

        private ConcurrentDictionary<long, RadarSensor> _queuedRadarHits = new ConcurrentDictionary<long, RadarSensor>();
        public double CountermeasureNoise { get; set; } = 0;


        public PassiveRadarSensor(IMyEntity attachedEntity, SensorDefinition definition)
        {
            Id = ServerMain.I.HighestSensorId++;
            AttachedEntity = attachedEntity;
            Sensors.Add(this);
            Definition = definition;
            Aperture = definition.MaxAperture;

            ServerMain.I.SensorIdMap[Id] = this;
        }

        public void Close()
        {
            Sensors.Remove(this);
            ServerMain.I.SensorIdMap.Remove(Id);
            _queuedRadarHits = null;
        }

        private PassiveRadarSensor() { }

        public double Aperture { get; set; } = Math.PI;

        public DetectionInfo? GetDetectionInfo(VisibilitySet visibilitySet)
        {
            var track = visibilitySet.Track;
            if (!Enabled || track == null)
                return null;

            RadarSensor sensor;
            if (!_queuedRadarHits.TryGetValue(track.EntityId, out sensor))
                return null;
            _queuedRadarHits.Remove(track.EntityId);

            Vector3D targetBearing = sensor.Position - Position;
            double targetRange = targetBearing.Normalize();
            double targetAngle = Math.Acos(Vector3D.Dot(Direction, targetBearing));
            if (targetAngle > Aperture)
                return null;

            double signalToNoiseRatio = sensor.SignalRatioAtTarget(Position, Aperture < Math.PI ? Definition.RadarProperties.ReceiverArea * Math.Cos(targetAngle) : Definition.RadarProperties.ReceiverArea);

            if (signalToNoiseRatio < 0)
                return null;

            double trackCrossSection = signalToNoiseRatio;
            double trackRange = targetRange;
            double maxRangeError = targetRange * Definition.RangeErrorModifier * (1 - MathHelper.Clamp(signalToNoiseRatio / Definition.DetectionThreshold, 0, 1));
            Vector3D trackBearing = targetBearing;
            double maxBearingError = Definition.BearingErrorModifier * (1 - MathHelper.Clamp(signalToNoiseRatio / Definition.DetectionThreshold, 0, 1));
            var iffCodes = track is GridTrack ? IffHelper.GetIffCodes(((GridTrack)track).Grid, SensorDefinition.SensorType.PassiveRadar) : Array.Empty<string>();

            CountermeasureManager.ApplyDrfm(this, track, ref trackCrossSection, ref trackRange, ref maxRangeError, ref trackBearing, ref maxBearingError, ref iffCodes);

            trackBearing = MathUtils.RandomCone(trackBearing, maxBearingError);
            trackRange += (2 * MathUtils.Random.NextDouble() - 1) * maxRangeError;

            var data = new DetectionInfo
            (
                track,
                this,
                trackCrossSection,
                trackRange,
                maxRangeError,
                trackBearing,
                maxBearingError,
                iffCodes
            );

            OnDetection?.Invoke(ObjectPackager.Package(data));

            return data;
        }

        private static readonly HashSet<PassiveRadarSensor> Sensors = new HashSet<PassiveRadarSensor>();
        public static void NotifyOnRadarHit(IMyEntity entity, RadarSensor sensor)
        {
            foreach (var passiveSensor in Sensors)
            {
                if (passiveSensor.AttachedEntity?.GetTopMostParent(typeof(IMyCubeGrid)) != entity.GetTopMostParent(typeof(IMyCubeGrid)))
                    continue;

                passiveSensor._queuedRadarHits[sensor.AttachedEntity.EntityId] = sensor;
            }
        }
    }
}
