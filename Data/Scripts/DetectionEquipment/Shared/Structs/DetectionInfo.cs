using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using VRageMath;

namespace DetectionEquipment.Shared.Structs
{
    internal struct DetectionInfo
    {
        public ITrack Track;
        public ISensor Sensor;
        public double CrossSection, Range, RangeError, BearingError;
        public Vector3D Bearing;
        public string[] IffCodes;

        public object[] DataSet => new object[]
        {
            CrossSection,
            Range,
            RangeError,
            BearingError,
            Bearing,
            IffCodes,
            Track.EntityId,
            Sensor.Id,
        };

        public override string ToString()
        {
            return $"Range: {Range:N0} +-{RangeError:N1}m\nBearing: {Bearing.ToString("N0")} +-{MathHelper.ToDegrees(BearingError):N1}°\nIFF: {(IffCodes.Length == 0 ? "N/A" : string.Join(" | ", IffCodes))}";
        }

        public override int GetHashCode()
        {
            return Track.EntityId.GetHashCode();
        }
    }
}
