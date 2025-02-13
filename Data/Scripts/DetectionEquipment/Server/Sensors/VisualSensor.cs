using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace DetectionEquipment.Server.Sensors
{
    internal class VisualSensor : ISensor
    {
        public Vector3D Position => MyAPIGateway.Session.Camera.WorldMatrix.Translation;
        public Vector3D Direction => MyAPIGateway.Session.Camera.WorldMatrix.Forward;
        public double Aperture { get; set; } = MathHelper.ToRadians(15);

        public bool IsInfrared = false;
        public double BearingErrorModifier { get; } = 0.1;
        public double RangeErrorModifier { get; } = 10;
        public double MinVisibility => 0.01;

        public VisualSensor(bool isInfrared)
        {
            IsInfrared = isInfrared;
        }

        private VisualSensor()
        {

        }

        public DetectionInfo? GetDetectionInfo(ITrack track)
        {
            return GetDetectionInfo(track, IsInfrared ? track.InfraredVisibility(Position) : track.OpticalVisibility(Position));
        }

        public DetectionInfo? GetDetectionInfo(ITrack track, double visibility)
        {
            double range = Vector3D.Distance(track.Position, Position);
            double targetAngle = 0;
            if (track.BoundingBox.Intersects(new RayD(Position, Direction)) == null)
                targetAngle = Vector3D.Angle(Direction, track.BoundingBox.ClosestCorner(Position) - Position);

            double targetSizeRatio = Math.Tan(Math.Sqrt(visibility/Math.PI) / range) / Aperture;

            //MyAPIGateway.Utilities.ShowNotification($"{targetSizeRatio*100:F1}% ({MathHelper.ToDegrees(Aperture):N0}° aperture)", 1000/60);
            if (targetAngle > Aperture || targetSizeRatio < MinVisibility)
                return null;

            double errorScalar = 1 - MathHelper.Clamp(targetSizeRatio, 0, 1);

            double maxBearingError = Aperture/2 * BearingErrorModifier * errorScalar;
            Vector3D bearing = MathUtils.RandomCone(Vector3D.Normalize(track.Position - Position), maxBearingError);

            double maxRangeError = Math.Sqrt(range) * RangeErrorModifier * errorScalar;
            range += (2 * MathUtils.Random.NextDouble() - 1) * maxRangeError;

            return new DetectionInfo()
            {
                Track = track,
                Sensor = this,
                CrossSection = visibility,
                Bearing = bearing,
                BearingError = maxBearingError,
                Range = range,
                RangeError = maxRangeError,
            };
        }
    }
}
