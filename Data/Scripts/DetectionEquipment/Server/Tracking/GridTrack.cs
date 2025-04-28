using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace DetectionEquipment.Server.Tracking
{
    internal class GridTrack : EntityTrack
    {
        public readonly IMyCubeGrid Grid;

        /// <summary>
        /// Cache cardinal and ordinal directions for visible and IR sensors.
        /// </summary>
        public OrderedDictionary<Vector3D, double> OpticalVisibilityCache = new OrderedDictionary<Vector3D, double>(TrackingUtils.VisibilityCacheBase);

        /// <summary>
        /// Cache cardinal and ordinal directions for active radar.
        /// </summary>
        public OrderedDictionary<Vector3D, double> RadarVisibilityCache = new OrderedDictionary<Vector3D, double>(TrackingUtils.VisibilityCacheBase);

        /// <summary>
        /// Next visibility cache item to be updated.
        /// </summary>
        protected int NextCacheUpdate = 0;


        public GridTrack(IMyCubeGrid grid) : base((MyEntity)grid)
        {
            Grid = grid;
        }

        public override double ProjectedArea(Vector3D source, VisibilityType type)
        {
            var cache = type == VisibilityType.Radar ? RadarVisibilityCache : OpticalVisibilityCache;

            double totalVisibility = 0;
            Vector3D sourceNormal = Vector3D.Transform(source, MatrixD.Invert(Entity.PositionComp.WorldMatrixRef)).Normalized();

            // We're calculating a weighted average between all "visible" angles. TODO: More accurate way to find this
            var directionRelStrength = new Dictionary<Vector3D, double>();
            double totalStrength = 0;
            foreach (var direction in cache.Keys)
            {
                if (Vector3D.Dot(sourceNormal, direction) <= 0) // Ignore directions that aren't visible to the emitter
                    continue;

                var invDirection = -direction;
                Vector3D.ProjectOnVector(ref invDirection, ref sourceNormal);
                double strength = invDirection.LengthSquared(); // Prefer directions better aligned to the source
                directionRelStrength[direction] = strength;
                totalStrength += strength;
            }

            foreach (var direction in directionRelStrength.Keys)
                totalVisibility += cache[direction] * (directionRelStrength[direction] / totalStrength);

            return totalVisibility;
        }

        public override double InfraredVisibility(Vector3D source)
        {
            // Retrieve power draw and add to base visibility. Scale by inverse of base visibility to simulate heat distribution; a smaller object with the same power draw as a larger object is hotter.
            float powerDraw = Grid.ResourceDistributor.TotalRequiredInputByType(MyResourceDistributorComponent.ElectricityId, Grid);
            return base.InfraredVisibility(source) + powerDraw * ProjectedArea(source, VisibilityType.Optical);
        }

        public override double InfraredVisibility(Vector3D source, double opticalVisibility)
        {
            float powerDraw = Grid.ResourceDistributor.TotalRequiredInputByType(MyResourceDistributorComponent.ElectricityId, Grid);
            return base.InfraredVisibility(source) + powerDraw * opticalVisibility;
        }

        // TODO: Scale visible and infrared visibility by thrust output.

        /// <summary>
        /// Update a portion of the visibility caches.
        /// </summary>
        /// <param name="updatePercent">How much of the caches to update, ranging from 0 to 1.</param>
        public void UpdateVisibilityCache(double updatePercent)
        {
            if (Grid.Physics == null)
                return;

            int itemsToUpdate = (int)Math.Ceiling(updatePercent * RadarVisibilityCache.Count);
            MyAPIGateway.Utilities.ShowNotification($"Updating {itemsToUpdate} items...", 1000);

            // TODO: Move this into a Parallel.For
            for (int i = 0; i < itemsToUpdate; i++)
            {
                if (NextCacheUpdate + i >= RadarVisibilityCache.Count) // leaving this as an if statement for readability. Saves a bit on performance.
                    UpdateVisibilityCache((NextCacheUpdate + i) % RadarVisibilityCache.Count);
                else
                    UpdateVisibilityCache(NextCacheUpdate + i);
            }

            NextCacheUpdate += itemsToUpdate;
            if (NextCacheUpdate >= RadarVisibilityCache.Count)
                NextCacheUpdate %= RadarVisibilityCache.Count;
        }

        protected virtual void UpdateVisibilityCache(int index)
        {
            Vector3D direction = -RadarVisibilityCache.GetKey(index);

            // Estimate the max cast size
            Vector3D minCheck = Vector3D.MaxValue, maxCheck = Vector3D.MinValue;
            MatrixD rotationMatrix = MatrixD.CreateFromDir(direction, direction == Vector3D.Up ? Vector3D.Backward : (direction == Vector3D.Down ? Vector3D.Forward : Vector3D.Up));
            foreach (var corner in Entity.PositionComp.LocalAABB.Corners)
            {
                var vec = Vector3D.Rotate(corner, rotationMatrix);
                if (minCheck.X > vec.X)
                    minCheck.X = vec.X;
                if (minCheck.Y > vec.Y)
                    minCheck.Y = vec.Y;
                if (minCheck.Z > vec.Z)
                    minCheck.Z = vec.Z;

                if (maxCheck.X < vec.X)
                    maxCheck.X = vec.X;
                if (maxCheck.Y < vec.Y)
                    maxCheck.Y = vec.Y;
                if (maxCheck.Z < vec.Z)
                    maxCheck.Z = vec.Z;
            }

            var invRMatrix = MatrixD.Invert(rotationMatrix);
            minCheck = Vector3D.Rotate(minCheck, rotationMatrix);
            maxCheck = Vector3D.Rotate(maxCheck, rotationMatrix);

            minCheck = Vector3D.Rotate(Vector3D.ProjectOnPlane(ref minCheck, ref direction), invRMatrix);
            maxCheck = Vector3D.Rotate(Vector3D.ProjectOnPlane(ref maxCheck, ref direction), invRMatrix);
            
            {
                // Min and max can get mixed up a bit
                var bufferMin = minCheck;
                minCheck.MinComponents(maxCheck);
                maxCheck.MaxComponents(bufferMin);
                bufferMin = maxCheck - minCheck;
                minCheck = -bufferMin/2;
                maxCheck = bufferMin/2;
            }

            //MyAPIGateway.Utilities.ShowNotification($"Min: {minCheck.ToString("F")}", 1000);
            //MyAPIGateway.Utilities.ShowNotification($"Max: {maxCheck.ToString("F")}", 1000);

            var gridPos = Grid.WorldAABB.Center;
            //DebugDraw.AddLine(gridPos, Vector3D.Rotate(direction * 10, Grid.WorldMatrix) + gridPos, Color.Blue, 1);

            // Cast for occupied cells, if there's a hit then do a physics cast.
            HashSet<Vector3I> visited = new HashSet<Vector3I>();
            double maxCastLength = Grid.LocalAABB.HalfExtents.Length();
            for (double x = minCheck.X; x <= maxCheck.X; x += Grid.GridSize)
            {
                for (double y = minCheck.Y; y <= maxCheck.Y; y += Grid.GridSize)
                {
                    var vecOffset = Vector3D.Rotate(new Vector3D(x, y, 0), rotationMatrix);

                    var from = Vector3D.Rotate(direction * -maxCastLength + vecOffset, Grid.WorldMatrix) + gridPos;
                    var to = Vector3D.Rotate(direction * maxCastLength + vecOffset, Grid.WorldMatrix) + gridPos;

                    var result = Grid.RayCastBlocks(from, to);
                    if (result != null)
                        visited.Add(result.Value);
                }
            }

            double totalRcs = 0;
            double totalVcs = 0;
            var globalDirection = Vector3D.Rotate(-direction, Grid.WorldMatrix);

            MyAPIGateway.Parallel.ForEach(visited, hitPos =>
            {
                Vector3D from = Vector3D.Transform(hitPos * Grid.GridSize, Grid.WorldMatrix) - direction * maxCastLength;
                Vector3D to = direction * maxCastLength + from;
            
                IHitInfo hitInfo;
                if (MyAPIGateway.Physics.CastRay(from, to, out hitInfo, 15))
                {
                    DebugDraw.AddLine(hitInfo.Position, hitInfo.Position + hitInfo.Normal, Color.Green, 1);
                    totalVcs += 1;
                    totalRcs += Math.Abs(Vector3D.Dot(globalDirection, hitInfo.Normal));
                }
            });

            totalRcs *= Grid.GridSize * Grid.GridSize;
            totalVcs *= Grid.GridSize * Grid.GridSize;
            RadarVisibilityCache.SetValue(index, totalRcs);
            OpticalVisibilityCache.SetValue(index, totalVcs);

            MyAPIGateway.Utilities.ShowNotification($"Side RCS: {totalRcs:N0} m^2", 1000);
        }

        public virtual void CalculateRcs(Vector3D globalDirection, out double radarCrossSection, out double visualCrossSection)
        {
            Vector3D direction = -Vector3D.Rotate(globalDirection, MatrixD.Invert(Grid.WorldMatrix));

            // Estimate the max cast size
            Vector3D minCheck = Vector3D.MaxValue, maxCheck = Vector3D.MinValue;
            MatrixD rotationMatrix = MatrixD.CreateFromDir(direction, direction == Vector3D.Up ? Vector3D.Backward : (direction == Vector3D.Down ? Vector3D.Forward : Vector3D.Up));
            foreach (var corner in Entity.PositionComp.LocalAABB.Corners)
            {
                var vec = Vector3D.Rotate(corner, rotationMatrix);
                if (minCheck.X > vec.X)
                    minCheck.X = vec.X;
                if (minCheck.Y > vec.Y)
                    minCheck.Y = vec.Y;
                if (minCheck.Z > vec.Z)
                    minCheck.Z = vec.Z;

                if (maxCheck.X < vec.X)
                    maxCheck.X = vec.X;
                if (maxCheck.Y < vec.Y)
                    maxCheck.Y = vec.Y;
                if (maxCheck.Z < vec.Z)
                    maxCheck.Z = vec.Z;
            }

            var invRMatrix = MatrixD.Invert(rotationMatrix);
            minCheck = Vector3D.Rotate(minCheck, rotationMatrix);
            maxCheck = Vector3D.Rotate(maxCheck, rotationMatrix);

            minCheck = Vector3D.Rotate(Vector3D.ProjectOnPlane(ref minCheck, ref direction), invRMatrix);
            maxCheck = Vector3D.Rotate(Vector3D.ProjectOnPlane(ref maxCheck, ref direction), invRMatrix);
            
            {
                // Min and max can get mixed up a bit
                var bufferMin = minCheck;
                minCheck.MinComponents(maxCheck);
                maxCheck.MaxComponents(bufferMin);
                bufferMin = maxCheck - minCheck;
                minCheck = -bufferMin/2;
                maxCheck = bufferMin/2;
            }

            var gridPos = Grid.WorldAABB.Center;
            //DebugDraw.AddLine(gridPos, Vector3D.Rotate(direction * 10, Grid.WorldMatrix) + gridPos, Color.Blue, 0);

            // Cast for occupied cells, if there's a hit then do a physics cast.
            HashSet<Vector3I> visited = new HashSet<Vector3I>();
            double maxCastLength = Grid.LocalAABB.HalfExtents.Length();
            for (double x = minCheck.X; x <= maxCheck.X; x += Grid.GridSize * 2) // Check every two blocks for performance's sake
            {
                for (double y = minCheck.Y; y <= maxCheck.Y; y += Grid.GridSize * 2) // Check every two blocks for performance's sake
                {
                    var vecOffset = Vector3D.Rotate(new Vector3D(x, y, 0), rotationMatrix);

                    var from = Vector3D.Rotate(direction * -maxCastLength + vecOffset, Grid.WorldMatrix) + gridPos;
                    var to = Vector3D.Rotate(direction * maxCastLength + vecOffset, Grid.WorldMatrix) + gridPos;

                    var result = Grid.RayCastBlocks(from, to);
                    if (result != null)
                        visited.Add(result.Value);
                }
            }

            double totalRcs = 0;
            double totalVcs = 0;

            MyAPIGateway.Parallel.ForEach(visited, hitPos =>
            {
                Vector3D from = Vector3D.Transform(hitPos * Grid.GridSize, Grid.WorldMatrix) - globalDirection * maxCastLength;
                Vector3D to = globalDirection * maxCastLength + from;
            
                IHitInfo hitInfo;
                if (!MyAPIGateway.Physics.CastRay(from, to, out hitInfo, 15))
                    return;

                if (hitInfo == null || hitInfo.HitEntity != Grid)
                    return;

                var block = Grid.GetCubeBlock(hitPos);

                //DebugDraw.AddLine(hitInfo.Position, hitInfo.Position + hitInfo.Normal, Color.Green, 0);
                // Armor blocks have half the RCS of components, and light armor has half the RCS of heavy armor.
                totalVcs += 1;
                if (block?.FatBlock == null)
                {
                    if (GlobalData.LowRcsSubtypes.Contains(block?.BlockDefinition.Id.SubtypeName))
                        totalRcs += Math.Abs(Vector3D.Dot(globalDirection, hitInfo.Normal)) / 2;
                    else
                        totalRcs += Math.Abs(Vector3D.Dot(globalDirection, hitInfo.Normal));
                }
                else
                    totalRcs += Math.Abs(Vector3D.Dot(globalDirection, hitInfo.Normal)) * 2;
            });

            radarCrossSection = totalRcs * Grid.GridSize * Grid.GridSize;
            visualCrossSection = totalVcs * Grid.GridSize * Grid.GridSize;
        }
    }
}
