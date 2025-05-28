using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Structs;
using System;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;


namespace DetectionEquipment.Server.Sensors
{
    internal interface ISensor
    {
        bool Enabled { get; set; }
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

        double CountermeasureNoise { get; set; }

        Action<object[]> OnDetection { get; set; }

        DetectionInfo? GetDetectionInfo(VisibilitySet visibility);
        void Close();
    }
}
