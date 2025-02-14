using DetectionEquipment.Server.Tracking;
using VRageMath;

namespace DetectionEquipment.Server.Sensors
{
    internal interface ISensor
    {
        /// <summary>
        /// Sensor position in global space
        /// </summary>
        Vector3D Position { get; set; }
        /// <summary>
        /// Forward direction of sensor in global space
        /// </summary>
        Vector3D Direction { get; set; }
        /// <summary>
        /// Visibility cone radius in radians
        /// </summary>
        double Aperture { get; set; }

        double BearingErrorModifier { get; set; }
        double RangeErrorModifier { get; set; }

        DetectionInfo? GetDetectionInfo(ITrack track);
        DetectionInfo? GetDetectionInfo(ITrack track, double visibility);
    }
}
