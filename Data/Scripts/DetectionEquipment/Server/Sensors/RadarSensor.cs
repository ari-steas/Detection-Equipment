using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.BlockLogic.IffReflector;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System;
using VRage;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;

namespace DetectionEquipment.Server.Sensors
{
    internal class RadarSensor : ISensor
    {
        public uint Id { get; private set; }
        public readonly Func<long> AttachedEntityId;
        public Vector3D Position { get; set; } = Vector3D.Zero;
        public Vector3D Direction { get; set; } = Vector3D.Forward;


        public RadarSensor(long attachedEntityId, SensorDefinition definition)
        {
            Id = ServerMain.I.HighestSensorId++;
            AttachedEntityId = () => attachedEntityId;
            Definition = definition;
            
            ServerMain.I.SensorIdMap[Id] = this;
        }

        public RadarSensor(IMyEntity entity, SensorDefinition definition)
        {
            Id = ServerMain.I.HighestSensorId++;
            AttachedEntityId = () => ((MyEntity)entity).GetTopMostParent().EntityId;
            Definition = definition;
            
            ServerMain.I.SensorIdMap[Id] = this;
        }

        private RadarSensor() { }

        public void Close()
        {
            ServerMain.I.SensorIdMap.Remove(Id);
        }

        public SensorDefinition Definition { get; private set; }
        public Action<MyTuple<double, double, double, double, Vector3D, string[]>> OnDetection { get; set; } = null;

        public double Aperture { get; set; } = MathHelper.ToRadians(15);
        public double Power = 14000000;
        public double ReceiverArea = 4.9 * 2.7;

        /// <summary>
        /// Minimum signal at which there is zero error, in dB
        /// </summary>
        public double MinStableSignal = 30;

        public double PowerEfficiencyModifier = 0.00000000000000025;
        public double BearingErrorModifier { get; set; } = 0.1;
        public double RangeErrorModifier { get; set; } = 0.0001; //0.005;
        public double Bandwidth = 1.67E6;
        public double Frequency = 2800E6;
        public double CountermeasureNoise { get; set; } = 0;

        public DetectionInfo? GetDetectionInfo(VisibilitySet visibilitySet)
        {
            var track = visibilitySet.Track;
            if (track == null) return null;

            double targetAngle = 0;
            if (visibilitySet.BoundingBox.Intersects(new RayD(Position, Direction)) == null)
                targetAngle = Vector3D.Angle(Direction, visibilitySet.ClosestCorner - Position);
            if (targetAngle > Aperture)
                return null;

            double targetDistanceSq = Vector3D.DistanceSquared(Position, visibilitySet.Position);

            double signalToNoiseRatio;
            {
                double lambda = 299792458 / Frequency;
                double outputDensity = (2 * Math.PI) / Aperture; // Inverse output density
                double receiverAreaAtAngle = Aperture < Math.PI ? ReceiverArea * Math.Cos(targetAngle) : ReceiverArea; // If the aperture is more than 180 degrees, assume that it's a spheroid.

                //            4 * pi * receiverArea
                // ----------------------------------------------
                // lambda^2 * angleOffsetScalar * outputDensity^3
                double gain = 4 * Math.PI * receiverAreaAtAngle / (lambda * lambda) * MathHelper.Clamp(1 - targetAngle / Aperture, 0, 1) * outputDensity * outputDensity * outputDensity;

                // Can make this fancier if I want later.
                // https://www.ll.mit.edu/sites/default/files/outreach/doc/2018-07/lecture%202.pdf
                signalToNoiseRatio = MathUtils.ToDecibels(
                    // netpower * gain^2 * lambda^2 * rcs
                    (Power * PowerEfficiencyModifier * gain * gain * lambda * lambda * visibilitySet.RadarVisibility)
                    /
                    // 4pi^3 * range^4 * boltzmann * systemNoise * bandwidth
                    (1984.40171 * targetDistanceSq * targetDistanceSq * 1.38E-23 * (950 + CountermeasureNoise) * Bandwidth)
                    );
            }

            //MyAPIGateway.Utilities.ShowNotification($"Power: {Power/1000000:N1}MW -> {signalToNoiseRatio:F} dB", 1000/60);
            //MyAPIGateway.Utilities.ShowNotification($"{(signalToNoiseRatio / MinStableSignal) * 100:N0}% track integrity", 1000/60);

            if (track is EntityTrack)
                PassiveRadarSensor.NotifyOnRadarHit(((EntityTrack)track).Entity, this);

            if (signalToNoiseRatio < 0)
            {
                DebugDraw.AddLine(Position, visibilitySet.Position, Color.Blue, 0);
                return null;
            }

            double maxBearingError = BearingErrorModifier * (1 - MathHelper.Clamp(signalToNoiseRatio / MinStableSignal, 0, 1));
            Vector3D bearing = MathUtils.RandomCone(Vector3D.Normalize(visibilitySet.Position - Position), maxBearingError);

            double range = Math.Sqrt(targetDistanceSq);
            double maxRangeError = range * RangeErrorModifier * (1 - MathHelper.Clamp(signalToNoiseRatio / MinStableSignal, 0, 1));
            range += (2 * MathUtils.Random.NextDouble() - 1) * maxRangeError;

            var iffCodes = track is GridTrack ? IffReflectorBlock.GetIffCodes(((GridTrack)track).Grid) : Array.Empty<string>();

            OnDetection?.Invoke(new MyTuple<double, double, double, double, Vector3D, string[]>(visibilitySet.RadarVisibility, range, maxRangeError, maxBearingError, bearing, iffCodes));

            return new DetectionInfo
            {
                Track = track,
                Sensor = this,
                CrossSection = visibilitySet.RadarVisibility,
                Bearing = bearing,
                BearingError = maxBearingError,
                Range = range,
                RangeError = maxRangeError,
                IffCodes = iffCodes
            };
        }

        public double SignalRatioAtTarget(Vector3D targetPos, double crossSection)
        {
            double targetDistanceSq = Vector3D.DistanceSquared(Position, targetPos);
            double targetAngle = Vector3D.Angle(Direction, targetPos - Position);

            double lambda = 299792458 / Frequency;
            double outputDensity = (2 * Math.PI) / Aperture; // Inverse output density
            double gain = 4 * Math.PI * ReceiverArea / (lambda * lambda) * MathHelper.Clamp(1 - targetAngle / Aperture, 0, 1) * outputDensity * outputDensity * outputDensity;

            // https://www.ll.mit.edu/sites/default/files/outreach/doc/2018-07/lecture%202.pdf
            return MathUtils.ToDecibels((Power * PowerEfficiencyModifier * gain * crossSection) / (4 * Math.PI * targetDistanceSq * 1.38E-23 * 950 * Bandwidth));
        }
    }
}
