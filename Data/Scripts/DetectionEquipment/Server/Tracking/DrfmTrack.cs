using VRageMath;

namespace DetectionEquipment.Server.Tracking
{
    internal class DrfmTrack : ITrack
    {
        public Vector3D Position { get; private set; }
        public BoundingBoxD BoundingBox { get; private set; }
        public long EntityId { get; private set; }

        public DrfmTrack(Vector3D position, long id)
        {
            Position = position;
            BoundingBox = new BoundingBoxD(Position, Position);
            EntityId = id;
        }

        public double OpticalVisibility(Vector3D source) => 0;

        public double InfraredVisibility(Vector3D source) => 0;

        public double InfraredVisibility(Vector3D source, double opticalVisibility) => 0;

        public double RadarVisibility(Vector3D source) => 0;
        public double CommsVisibility(Vector3D source) => 0;
    }
}
