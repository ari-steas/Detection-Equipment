using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using VRageMath;

using LocalDetTuple = VRage.MyTuple<double, double, double, double, VRageMath.Vector3D, string[]>;

namespace DetectionEquipment.Shared.Structs
{
    internal struct DetectionInfo
    {
        public ITrack Track;
        public ISensor Sensor;
        public double CrossSection, Range, RangeError, BearingError;
        public Vector3D Bearing;
        public string[] IffCodes;

        public LocalDetTuple Tuple => new LocalDetTuple(CrossSection, Range, RangeError, BearingError, Bearing, IffCodes);

        public override string ToString()
        {
            return $"Range: {Range:N0} +-{RangeError:N1}m\nBearing: {Bearing.ToString("N0")} +-{MathHelper.ToDegrees(BearingError):N1}°\nIFF: {(IffCodes.Length == 0 ? "N/A" : string.Join(" | ", IffCodes))}";
        }
    }
}
