using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using VRageMath;

namespace DetectionEquipment.Shared.Structs
{
    internal struct DetectionInfo : IPackageable
    {
        public ITrack Track;
        public ISensor Sensor;
        public double CrossSection, Range, RangeError, BearingError;
        public Vector3D Bearing;
        public string[] IffCodes;

        public override string ToString()
        {
            return $"Range: {Range:N0} +-{RangeError:N1}m\nBearing: {Bearing.ToString("N0")} +-{MathHelper.ToDegrees(BearingError):N1}°\nIFF: {(IffCodes.Length == 0 ? "N/A" : string.Join(" | ", IffCodes))}";
        }

        public override int GetHashCode()
        {
            return Track.EntityId.GetHashCode();
        }

        public int FieldCount => 8;
        public void Package(object[] fieldArray)
        {
            fieldArray[0] = CrossSection;
            fieldArray[1] = Range;
            fieldArray[2] = RangeError;
            fieldArray[3] = BearingError;
            fieldArray[4] = Bearing;
            fieldArray[5] = IffCodes;
            fieldArray[6] = Track.EntityId;
            fieldArray[7] = Sensor.Id;
        }
    }
}
