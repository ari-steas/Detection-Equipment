using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace DetectionEquipment.Server.Sensors
{
    internal class RadarSensor : ISensor
    {
        public Vector3D Position => MyAPIGateway.Session.Camera.WorldMatrix.Translation;

        public Vector3D Direction => MyAPIGateway.Session.Camera.WorldMatrix.Forward;

        public double Aperture { get; set; } = MathHelper.ToRadians(15);
        public double Power = 14000000;
        public double RecieverArea = 4.9 * 2.7;
        public double MinStableSignal = 30; // Minimum signal at which there is zero error, in dB

        public double PowerEfficiencyModifier = 0.00000000000000025;
        public double BearingErrorModifier { get; } = 0.1;
        public double RangeErrorModifier { get; } = 0.0001; //0.005;
        public double Bandwidth = 1.67E6;
        public double Frequency = 2800E6;
        public double Losses = 6.3; // 8dB

        public DetectionInfo? GetDetectionInfo(ITrack track)
        {
            return GetDetectionInfo(track, track.RadarVisibility(Position));
        }

        public DetectionInfo? GetDetectionInfo(ITrack track, double radarCrossSection)
        {
            double targetDistanceSq = Vector3D.DistanceSquared(Position, track.Position);
            double targetAngle = 0;
            if (track.BoundingBox.Intersects(new RayD(Position, Direction)) == null)
                targetAngle = Vector3D.Angle(Direction, track.BoundingBox.ClosestCorner(Position) - Position);

            double signalToNoiseRatio;
            {
                double lambda = 299792458 / Frequency;
                double outputDensity = (2 * Math.PI) / Aperture; // Inverse output density
                double gain = 4 * Math.PI * RecieverArea / (lambda * lambda) * MathHelper.Clamp(1 - targetAngle / Aperture, 0, 1) * outputDensity * outputDensity * outputDensity;

                // Can make this fancier if I want later.
                // https://www.ll.mit.edu/sites/default/files/outreach/doc/2018-07/lecture%202.pdf
                signalToNoiseRatio = MathUtils.ToDecibels((Power * PowerEfficiencyModifier * gain * gain * lambda * lambda * radarCrossSection) / (1984.40171 * targetDistanceSq * targetDistanceSq * 1.38E-23 * 950 * Bandwidth));
            }

            //MyAPIGateway.Utilities.ShowNotification($"Power: {Power/1000000:N1}MW -> {signalToNoiseRatio:F} dB", 1000/60);
            //MyAPIGateway.Utilities.ShowNotification($"{(MathHelper.Clamp(signalToNoiseRatio / MinStableSignal, 0, 1)) * 100:N0}% track integrity ({MathHelper.ToDegrees(Aperture):N0}° aperture)", 1000/60);

            if (signalToNoiseRatio < 0)
                return null;

            double maxBearingError = Aperture/2 + BearingErrorModifier * (1 - MathHelper.Clamp(signalToNoiseRatio / MinStableSignal, 0, 1));
            Vector3D bearing = MathUtils.RandomCone(Vector3D.Normalize(track.Position - Position), maxBearingError);

            double range = Math.Sqrt(targetDistanceSq);
            double maxRangeError = range * RangeErrorModifier * (1 - MathHelper.Clamp(signalToNoiseRatio / MinStableSignal, 0, 1));
            range += (2 * MathUtils.Random.NextDouble() - 1) * maxRangeError;

            return new DetectionInfo()
            {
                Track = track,
                Sensor = this,
                CrossSection = radarCrossSection,
                Bearing = bearing,
                BearingError = maxBearingError,
                Range = range,
                RangeError = maxRangeError,
            };
        }
    }
}
