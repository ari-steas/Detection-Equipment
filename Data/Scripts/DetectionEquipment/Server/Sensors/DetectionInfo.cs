using DetectionEquipment.Server.Tracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace DetectionEquipment.Server.Sensors
{
    internal struct DetectionInfo
    {
        public ITrack Track;
        public ISensor Sensor;
        public double CrossSection, Range, RangeError, BearingError;
        public Vector3D Bearing;

        public override string ToString()
        {
            return $"Range: {Range:N0} +-{RangeError:N1}m\nBearing: {Bearing.ToString("N0")} +-{MathHelper.ToDegrees(BearingError):N1}°";
        }
    }
}
