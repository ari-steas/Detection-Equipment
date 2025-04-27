using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Structs;
using System;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;
using LocalDetTuple = VRage.MyTuple<double, double, double, double, VRageMath.Vector3D, string[]>;


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
        double CountermeasureNoise { get; set; }

        Action<LocalDetTuple> OnDetection { get; set; }

        DetectionInfo? GetDetectionInfo(VisibilitySet visibility);
        void Close();
    }
}
