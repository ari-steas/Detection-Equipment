using VRageMath;

namespace DetectionEquipment.Server.Tracking
{
    internal class MunitionTrack : ITrack
    {
        public Vector3D Position { get; }
        public BoundingBoxD BoundingBox { get; }
        public long EntityId => 0;

        public MunitionTrack(Vector3D position)
        {
            Position = position;
            BoundingBox = new BoundingBoxD(Position - Vector3D.Half, Position + Vector3D.Half);
        }

        public double OpticalVisibility(Vector3D source) => 1;

        public double InfraredVisibility(Vector3D source) => 1;

        public double InfraredVisibility(Vector3D source, double opticalVisibility) => 1;

        public double RadarVisibility(Vector3D source) => 1;

        public double CommsVisibility(Vector3D source) => 0;
    }
}
