using DetectionEquipment.Server.Tracking;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace DetectionEquipment.Server.Sensors
{
    internal interface ISensor
    {
        /// <summary>
        /// Sensor position in global space
        /// </summary>
        Vector3D Position { get; }
        /// <summary>
        /// Forward direction of sensor in global space
        /// </summary>
        Vector3D Direction { get; }
        /// <summary>
        /// Visibility cone radius in radians
        /// </summary>
        double Aperture { get; set; }

        double BearingErrorModifier { get; }
        double RangeErrorModifier { get; }

        DetectionInfo? GetDetectionInfo(ITrack track);
        DetectionInfo? GetDetectionInfo(ITrack track, double visibility);
    }
}
