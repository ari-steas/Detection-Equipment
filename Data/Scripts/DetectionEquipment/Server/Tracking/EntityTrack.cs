using DetectionEquipment.Shared.Utils;
using VRage.Game.Entity;
using VRageMath;

namespace DetectionEquipment.Server.Tracking
{
    internal class EntityTrack : ITrack
    {
        public Vector3D Position => Entity.PositionComp.WorldAABB.Center;
        public BoundingBoxD BoundingBox => Entity.PositionComp.WorldAABB;
        public long EntityId => Entity.EntityId;

        public readonly MyEntity Entity;

        public EntityTrack(MyEntity entity)
        {
            Entity = entity;
        }

        // Multiplying the projected area by 2 because grids inherently have an absurdly high RCS
        public virtual double ProjectedArea(Vector3D source, VisibilityType type) => 2 * Entity.PositionComp.LocalAABB.ProjectedArea(Vector3D.Transform(source, MatrixD.Invert(Entity.PositionComp.WorldMatrixRef)).Normalized());

        public virtual double InfraredVisibility(Vector3D source)
        {
            // Returns a value from 0.01 to 0.25 depending on the angle to the sun. If the source can see the lit side, the visibility approaches 0.5.
            return OpticalVisibility(source) * (0.135 + Vector3D.Dot(Vector3D.Normalize(Position - source), TrackingUtils.GetSunDirection()) / 8);
        }

        public virtual double InfraredVisibility(Vector3D source, double opticalVisibility)
        {
            // Returns a value from 0.01 to 0.25 depending on the angle to the sun. If the source can see the lit side, the visibility approaches 0.5.
            return opticalVisibility * (0.135 + Vector3D.Dot(Vector3D.Normalize(Position - source), TrackingUtils.GetSunDirection()) / 8);
        }

        public virtual double OpticalVisibility(Vector3D source)
        {
            return ProjectedArea(source, VisibilityType.Optical);
        }

        public virtual double RadarVisibility(Vector3D source)
        {
            return ProjectedArea(source, VisibilityType.Radar);
        }
    }
}
