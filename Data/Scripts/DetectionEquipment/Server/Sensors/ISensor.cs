using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared.Definitions;
using System;
using VRage;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;

namespace DetectionEquipment.Server.Sensors
{
    internal interface ISensor
    {
        uint Id { get; }

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
        SensorDefinition Definition { get; }

        double BearingErrorModifier { get; set; }
        double RangeErrorModifier { get; set; }

        Action<MyTuple<double, double, double, double, Vector3D, string[]>> OnDetection { get; set; }

        DetectionInfo? GetDetectionInfo(ITrack track);
        DetectionInfo? GetDetectionInfo(ITrack track, double visibility);
        DetectionInfo? GetDetectionInfo(VisibilitySet visibility);
        void Close();
    }
}
