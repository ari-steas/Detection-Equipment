using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.ModAPI;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;

namespace DetectionEquipment.Server.Sensors
{
    internal class PassiveRadarSensor : ISensor
    {
        public readonly IMyEntity AttachedEntity;
        public SensorDefinition Definition { get; private set; }
        public Vector3D Position { get; set; }
        public Vector3D Direction { get; set; }

        private Dictionary<long, DetectionInfo> _queuedRadarHits = new Dictionary<long, DetectionInfo>();

        public PassiveRadarSensor(IMyEntity attachedEntity, SensorDefinition definition)
        {
            AttachedEntity = attachedEntity;
            Sensors.Add(this);
            Definition = definition;
        }

        public void Close()
        {
            Sensors.Remove(this);
        }

        private PassiveRadarSensor() { }

        public double Aperture { get; set; } = Math.PI;
        public double RecieverArea { get; set; } = 2.5*2.5;

        public double BearingErrorModifier { get; set; } = 0.1;

        public double RangeErrorModifier { get; set; } = 0.0001;
        public double MinStableSignal = 30; // Minimum signal at which there is zero error, in dB

        public DetectionInfo? GetDetectionInfo(ITrack track)
        {
            return GetDetectionInfo(track, 0);
        }

        public DetectionInfo? GetDetectionInfo(VisibilitySet visibilitySet)
        {
            return GetDetectionInfo(visibilitySet.Track, 0);
        }

        public DetectionInfo? GetDetectionInfo(ITrack track, double visibility)
        {
            if (!_queuedRadarHits.ContainsKey(track.EntityId))
                return null;
            var data = _queuedRadarHits[track.EntityId];
            _queuedRadarHits.Remove(track.EntityId);
            data.Track = track;

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

                double signalToNoiseRatio = sensor.SignalRatioAtTarget(passiveSensor.Position, passiveSensor.Aperture < Math.PI ? passiveSensor.RecieverArea * Math.Cos(angleToTarget) : passiveSensor.RecieverArea);

                double range = bearing.Normalize();

                if (signalToNoiseRatio < 0)
                    continue;

                double maxBearingError = passiveSensor.BearingErrorModifier * (1 - MathHelper.Clamp(signalToNoiseRatio / passiveSensor.MinStableSignal, 0, 1));
                bearing = MathUtils.RandomCone(bearing, maxBearingError);

                double maxRangeError = range * passiveSensor.RangeErrorModifier * (1 - MathHelper.Clamp(signalToNoiseRatio / passiveSensor.MinStableSignal, 0, 1));
                range += (2 * MathUtils.Random.NextDouble() - 1) * maxRangeError;

                passiveSensor._queuedRadarHits[sensor.AttachedEntityId()] = new DetectionInfo()
                {
                    Sensor = passiveSensor,
                    CrossSection = signalToNoiseRatio,
                    Bearing = bearing,
                    BearingError = maxBearingError,
                    Range = range,
                    RangeError = maxRangeError,
                };
            }
        }
    }
}
