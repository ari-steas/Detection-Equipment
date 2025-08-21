using VRageMath;

namespace DetectionEquipment.Server.Tracking
{
    /// <summary>
    /// Generic tracking cache.
    /// </summary>
    internal interface ITrack
    {
        Vector3D Position { get; }
        BoundingBoxD BoundingBox { get; }
        long EntityId { get; }

        /// <summary>
        /// Visibility to optical cameras.
        /// </summary>
        /// <param name="source">Position of the sensor</param>
        /// <returns></returns>
        double OpticalVisibility(Vector3D source);
        /// <summary>
        /// Visibility to infrared cameras.
        /// </summary>
        /// <param name="source">Position of the sensor</param>
        /// <returns></returns>
        double InfraredVisibility(Vector3D source);
        /// <summary>
        /// Visibility to infrared cameras.
        /// </summary>
        /// <param name="source">Position of the sensor</param>
        /// <param name="opticalVisibility">Cached optical visibility</param>
        /// <returns></returns>
        double InfraredVisibility(Vector3D source, double opticalVisibility);
        /// <summary>
        /// Visibility to active radars.
        /// </summary>
        /// <param name="source">Position of the sensor</param>
        /// <returns></returns>
        double RadarVisibility(Vector3D source);
        /// <summary>
        /// Visibility to comms sensors.
        /// </summary>
        /// <param name="source">Position of the sensor</param>
        /// <returns></returns>
        double CommsVisibility(Vector3D source);
    }

    public enum VisibilityType
    {
        Optical = 0,
        Radar = 1,
    }
}
