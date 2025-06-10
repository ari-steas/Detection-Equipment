using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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

        private ConcurrentDictionary<long, DetectionInfo> _queuedRadarHits = new ConcurrentDictionary<long, DetectionInfo>();
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
        }

        private PassiveRadarSensor() { }

        public double Aperture { get; set; } = Math.PI;

        public DetectionInfo? GetDetectionInfo(VisibilitySet visibilitySet)
        {
            var track = visibilitySet.Track;
            if (!Enabled || track == null)
                return null;

            if (!_queuedRadarHits.ContainsKey(track.EntityId))
                return null;
            var data = _queuedRadarHits[track.EntityId];
            _queuedRadarHits.Remove(track.EntityId);
            data.Track = track;

            OnDetection?.Invoke(ObjectPackager.Package(data));
            //MyAPIGateway.Utilities.ShowMessage($"{data.CrossSection:N0}", data.ToString());

            return data;
        }

        private static readonly HashSet<PassiveRadarSensor> Sensors = new HashSet<PassiveRadarSensor>();
        public static void NotifyOnRadarHit(IMyEntity entity, RadarSensor sensor)
        {
            foreach (var passiveSensor in Sensors)
            {
                if (passiveSensor.AttachedEntity?.GetTopMostParent() != entity.GetTopMostParent())
                    continue;

                Vector3D bearing = sensor.Position - passiveSensor.Position;
                double angleToTarget = Vector3D.Angle(passiveSensor.Direction, bearing);
                if (angleToTarget > passiveSensor.Aperture)
                    continue;

                double signalToNoiseRatio = sensor.SignalRatioAtTarget(passiveSensor.Position, passiveSensor.Aperture < Math.PI ? passiveSensor.Definition.RadarProperties.ReceiverArea * Math.Cos(angleToTarget) : passiveSensor.Definition.RadarProperties.ReceiverArea);

                double range = bearing.Normalize();

                if (signalToNoiseRatio < 0)
                    continue;

                double maxBearingError = passiveSensor.Definition.BearingErrorModifier * (1 - MathHelper.Clamp(signalToNoiseRatio / passiveSensor.Definition.DetectionThreshold, 0, 1));
                bearing = MathUtils.RandomCone(bearing, maxBearingError);

                double maxRangeError = range * passiveSensor.Definition.RangeErrorModifier * (1 - MathHelper.Clamp(signalToNoiseRatio / passiveSensor.Definition.DetectionThreshold, 0, 1));
                range += (2 * MathUtils.Random.NextDouble() - 1) * maxRangeError;

                passiveSensor._queuedRadarHits[sensor.AttachedEntity.EntityId] = new DetectionInfo
                (
                    null, // this is set later
                    passiveSensor,
                    signalToNoiseRatio,
                    range,
                    maxRangeError,
                    bearing,
                    maxBearingError
                );
            }
        }
    }
}
