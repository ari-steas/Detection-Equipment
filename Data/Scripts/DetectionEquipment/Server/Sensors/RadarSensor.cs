using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using System;
using DetectionEquipment.Server.Countermeasures;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;
using DetectionEquipment.Shared.Helpers;
using Sandbox.ModAPI;

namespace DetectionEquipment.Server.Sensors
{
    internal class RadarSensor : ISensor
    {
        public bool Enabled { get; set; } = true;
        public uint Id { get; private set; }
        public readonly IMyEntity AttachedEntity;
        public Vector3D Position { get; set; } = Vector3D.Zero;
        public Vector3D Direction { get; set; } = Vector3D.Forward;


        public RadarSensor(IMyEntity entity, SensorDefinition definition)
        {
            Id = ServerMain.I.HighestSensorId++;
            AttachedEntity = entity.GetTopMostParent(typeof(IMyCubeGrid));
            Definition = definition;
            Aperture = definition.MaxAperture;
            
            ServerMain.I.SensorIdMap[Id] = this;
        }

        private RadarSensor() { }

        public void Close()
        {
            ServerMain.I.SensorIdMap.Remove(Id);
        }

        public SensorDefinition Definition { get; private set; }
        public Action<object[]> OnDetection { get; set; } = null;

        public double Aperture { get; set; } = MathHelper.ToRadians(15);
        public double CountermeasureNoise { get; set; } = 0;

        public bool GetDetectionInfo(VisibilitySet visibilitySet, out DetectionInfo detection)
        {
            if (!Enabled)
            {
                detection = default(DetectionInfo);
                return false;
            }

            var track = visibilitySet.Track;
            if (track == null)
            {
                detection = default(DetectionInfo);
                return false;
            }

            Vector3D targetBearing = visibilitySet.Position - Position;
            double targetRange = targetBearing.Normalize();

            double targetAngle = 0;
            if (visibilitySet.BoundingBox.Intersects(new RayD(Position, Direction)) == null)
                targetAngle = Math.Acos(Vector3D.Dot(Direction, targetBearing));

            if (targetAngle > Aperture)
            {
                detection = default(DetectionInfo);
                return false;
            }

            double signalToNoiseRatio;
            {
                const double c = 299792458d;
                const double fourPi3 = 64 * Math.PI * Math.PI * Math.PI;
                const double bmConstant = 1.38E-23;
                const double inherentNoise = 950;

                double lambdaSq = c / Definition.RadarProperties.Frequency;
                lambdaSq *= lambdaSq;
                double outputDensity = 2 * Math.PI / Aperture; // Inverse output density

                // If the aperture is more than 180 degrees, assume that it's a spheroid.
                double receiverAreaAtAngle = Definition.RadarProperties.ReceiverArea;
                if (Aperture <= Math.PI && Definition.RadarProperties.AccountForRadarAngle)
                    receiverAreaAtAngle *= Math.Cos(targetAngle);

                //   4 * pi * receiverArea * angleOffsetScalar * outputDensity^3
                // ---------------------------------------------------------------
                //                            lambda^2
                double gain =
                    4 * Math.PI * receiverAreaAtAngle *
                    (Definition.RadarProperties.AccountForRadarAngle
                        ? MathHelper.Clamp(1 - targetAngle / Aperture, 0, 1)
                        : 1) * outputDensity * outputDensity * outputDensity
                    / lambdaSq;

                // Can make this fancier if I want later.
                // https://www.ll.mit.edu/sites/default/files/outreach/doc/2018-07/lecture%202.pdf
                //            power_net * gain^2 * lambda^2 * rcs
                // --------------------------------------------------------
                //  (4pi)^3 * range^4 * boltzmann * noise_system * bandwidth
                signalToNoiseRatio = MathUtils.ToDecibels(
                    Definition.MaxPowerDraw * Definition.RadarProperties.PowerEfficiencyModifier * gain * gain * lambdaSq * visibilitySet.RadarVisibility
                    /
                    (fourPi3 * targetRange * targetRange * targetRange * targetRange * bmConstant * (inherentNoise + CountermeasureNoise) * Definition.RadarProperties.Bandwidth)
                    );
            }

            //MyAPIGateway.Utilities.ShowNotification($"Power: {Power/1000000:N1}MW -> {signalToNoiseRatio:F} dB", 1000/60);
            //MyAPIGateway.Utilities.ShowNotification($"{(signalToNoiseRatio / MinStableSignal) * 100:N0}% track integrity", 1000/60);

            if (track is EntityTrack)
                PassiveRadarSensor.NotifyOnRadarHit(((EntityTrack)track).Entity, this);

            if (signalToNoiseRatio < 0)
            {
                detection = default(DetectionInfo);
                return false;
            }

            double errorScalar = 1 - MathHelper.Clamp(signalToNoiseRatio / Definition.DetectionThreshold, 0, 1);

            double trackCrossSection = visibilitySet.RadarVisibility;
            double trackRange = targetRange;
            double maxRangeError = targetRange * Definition.RangeErrorModifier * errorScalar;
            Vector3D trackBearing = targetBearing;
            double maxBearingError = Definition.BearingErrorModifier * errorScalar;
            var gridTrack = track as GridTrack;
            var iffCodes = gridTrack != null ? IffHelper.GetIffCodes(gridTrack.Grid, SensorDefinition.SensorType.Radar) : Array.Empty<string>();

            CountermeasureManager.ApplyDrfm(this, track, ref trackCrossSection, ref trackRange, ref maxRangeError, ref trackBearing, ref maxBearingError, ref iffCodes);

            trackBearing = MathUtils.RandomCone(trackBearing, maxBearingError);
            trackRange += (2 * MathUtils.Random.NextDouble() - 1) * maxRangeError;

            detection = new DetectionInfo
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

            OnDetection?.Invoke(ObjectPackager.Package(detection));

            return true;
        }

        public double SignalRatioAtTarget(Vector3D targetPos, double crossSection)
        {
            double targetDistanceSq = Vector3D.DistanceSquared(Position, targetPos);
            double targetAngle = Vector3D.Angle(Direction, targetPos - Position);

            double lambda = 299792458 / Definition.RadarProperties.Frequency;
            double outputDensity = (2 * Math.PI) / Aperture; // Inverse output density
            double gain = 4 * Math.PI * Definition.RadarProperties.ReceiverArea / (lambda * lambda) * MathHelper.Clamp(1 - targetAngle / Aperture, 0, 1) * outputDensity * outputDensity * outputDensity;

            // https://www.ll.mit.edu/sites/default/files/outreach/doc/2018-07/lecture%202.pdf
            return MathUtils.ToDecibels((Definition.MaxPowerDraw * Definition.RadarProperties.PowerEfficiencyModifier * gain * crossSection) / (4 * Math.PI * targetDistanceSq * 1.38E-23 * 950 * Definition.RadarProperties.Bandwidth));
        }
    }
}
