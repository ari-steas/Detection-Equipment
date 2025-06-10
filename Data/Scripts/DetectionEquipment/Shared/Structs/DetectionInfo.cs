using System;
using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using VRageMath;

namespace DetectionEquipment.Shared.Structs
{
    internal struct DetectionInfo : IPackageable
    {
        public ITrack Track;
        public readonly ISensor Sensor;
        public readonly double CrossSection;
        /// <summary>
        /// Error in meters
        /// </summary>
        public readonly double MaxRangeError, MaxBearingError;
        /// <summary>
        /// Relative offset from target entity position
        /// </summary>
        public readonly Vector3D PositionOffset;
        public readonly string[] IffCodes;

        public Vector3D Position => PositionOffset + Track.Position;

        public DetectionInfo(ITrack track, ISensor sensor, double crossSection, double range, double maxRangeError, Vector3D bearing, double maxBearingError, string[] iffCodes = null)
        {
            Track = track;
            Sensor = sensor;
            CrossSection = crossSection;
            MaxRangeError = maxRangeError;
            PositionOffset = range * bearing + sensor.Position - track.Position; // faking it because the detection-to-track pipeline can take a few ticks
            MaxBearingError = Math.Tan(maxBearingError) * range;
            IffCodes = iffCodes ?? Array.Empty<string>();
        }

        public override int GetHashCode()
        {
            return Track.EntityId.GetHashCode();
        }

        public void GetBearingRange(out Vector3D bearing, out double range)
        {
            bearing = Position - Sensor.Position;
            range = bearing.Normalize();
        }

        public int FieldCount => 8;
        public void Package(object[] fieldArray)
        {
            Vector3D bearing;
            double range;
            GetBearingRange(out bearing, out range);

            fieldArray[0] = CrossSection;
            fieldArray[1] = range;
            fieldArray[2] = MaxRangeError;
            fieldArray[3] = MaxBearingError;
            fieldArray[4] = bearing;
            fieldArray[5] = IffCodes;
            fieldArray[6] = Track.EntityId;
            fieldArray[7] = Sensor.Id;
        }
    }
}
