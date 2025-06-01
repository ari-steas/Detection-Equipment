using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using RichHudFramework;
using Sandbox.Game.Entities;
using VRage;
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

        private HashSet<IMyThrust> ThrusterCache = new HashSet<IMyThrust>();
        private Dictionary<Vector3, float> ThrustCache = new Dictionary<Vector3, float>();
        private int LastThrustCacheUpdate = -1;

        public GridTrack(IMyCubeGrid grid) : base((MyEntity)grid)
        {
            Grid = grid;
            foreach (var thrust in Grid.GetFatBlocks<IMyThrust>())
                ThrusterCache.Add(thrust);
            Grid.OnBlockAdded += block =>
            {
                if (block.FatBlock is IMyThrust)
                    ThrusterCache.Add((IMyThrust)block.FatBlock);
            };
            Grid.OnBlockRemoved += block =>
            {
                if (block.FatBlock is IMyThrust)
                    ThrusterCache.Remove((IMyThrust)block.FatBlock);
            };
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
            return InfraredVisibility(source, ProjectedArea(source, VisibilityType.Optical));
        }

        public override double InfraredVisibility(Vector3D source, double opticalVisibility)
        {
            // Combine:
            // -   Base visibility (sun heating).
            // -   Power usage per surface area times visible area; a smaller object with the same power draw as a larger object is hotter.
            // -   Thruster wattage for visible thrust directions, times the dot product (thruster facing the sensor is more visible)
            // Then divide by distance squared (inverse square falloff).

            double heatWattage;
            {
                // Grid power is in MW by default, convert to watts
                double powerDraw = Grid.ResourceDistributor.TotalRequiredInputByType(MyResourceDistributorComponent.ElectricityId, Grid) * 1000000;
                heatWattage = powerDraw / Grid.LocalAABB.SurfaceArea() * opticalVisibility;
            }

            double thrustWattage = 0;
            {
                var normal = Vector3D.Normalize(Position - source);
                var gridThrust = GetGridThrust();
                foreach (var direction in gridThrust)
                {
                    var dotProduct = MathHelper.Clamp(Vector3D.Rotate(-direction.Key, Grid.WorldMatrix).Dot(normal), 0, 1);
                    // Direction.Value is in Newtons. We want to find N*m/s.
                    // To get meters, divide force by mass (and multiply seconds squared, which is one, ignored.)
                    // Time interval is one second - divide by 1, ignored.
                    var thisWattage = direction.Value * direction.Value / Grid.Physics.Mass;

                    // However, this doesn't work on static grids because their mass is infinite. In this case, we just convert from newtons to watts by scaling by a small ion thruster's efficiency. yikes.
                    if (float.IsNaN(thisWattage) || float.IsInfinity(thisWattage))
                        thisWattage = direction.Value*9.72222222f;

                    thrustWattage += dotProduct * thisWattage;
                }
            }
            // base visibility is already divided by distancesq
            return base.InfraredVisibility(source, opticalVisibility) + (heatWattage + thrustWattage) / Vector3D.DistanceSquared(source, Position);
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

        private Dictionary<Vector3, float> GetGridThrust()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter == LastThrustCacheUpdate)
                return ThrustCache;

            ThrustCache.Clear();

            foreach (var thrust in ThrusterCache) // TODO cache thruster blocks
            {
                if (ThrustCache.ContainsKey(thrust.LocalMatrix.Forward))
                    ThrustCache[thrust.LocalMatrix.Forward] += thrust.CurrentThrust;
                else
                    ThrustCache.Add(thrust.LocalMatrix.Forward, thrust.CurrentThrust);
            }

            LastThrustCacheUpdate = MyAPIGateway.Session.GameplayFrameCounter;
            return ThrustCache;
        }

        public virtual void CalculateRcs(Vector3D globalDirection, out double radarCrossSection, out double visualCrossSection)
        {
            globalDirection.Normalize();
            Vector3D direction = Vector3D.Rotate(globalDirection, MatrixD.Invert(Grid.WorldMatrix));

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

            double checkArea = (maxCheck.X - minCheck.X) * (maxCheck.Y - minCheck.Y);
            int scaleMultiplier = (int) MathUtils.Clamp(Math.Round(Math.Pow(checkArea / 500, 1/3d)), 1, double.MaxValue);

            for (double x = minCheck.X; x <= maxCheck.X; x += Grid.GridSize * scaleMultiplier) // Check every two blocks for performance's sake
            {
                for (double y = minCheck.Y; y <= maxCheck.Y; y += Grid.GridSize * scaleMultiplier) // Check every two blocks for performance's sake
                {
                    var vecOffset = Vector3D.Rotate(new Vector3D(x, y, 0), rotationMatrix);

                    var from = Vector3D.Rotate(direction * -maxCastLength + vecOffset, Grid.WorldMatrix) + gridPos;
                    var to = Vector3D.Rotate(direction * maxCastLength + vecOffset, Grid.WorldMatrix) + gridPos;

                    //if (GlobalData.Debug)
                    //    DebugDraw.AddLine(from, to, Color.Gray.SetAlphaPct(0.05f), 0);

                    var result = Grid.RayCastBlocks(from, to);
                    if (result != null)
                        visited.Add(result.Value);
                }
            }

            double totalRcs = 0;
            double totalVcs = 0;

            MyAPIGateway.Parallel.ForEach(visited, hitPos =>
            {
                var globalHitPos = Vector3D.Transform(hitPos * Grid.GridSize, Grid.WorldMatrix);
                if (GlobalData.Debug)
                    DebugDraw.AddPoint(globalHitPos, Color.Gray.SetAlphaPct(0.1f), 0);
                Vector3D from = globalHitPos - globalDirection * Grid.GridSize * 3;
                Vector3D to = globalHitPos + globalDirection * maxCastLength; // cast several blocks deep to limit certain cheese tactics
            
                IHitInfo hitInfo;
                if (!MyAPIGateway.Physics.CastRay(from, to, out hitInfo, 15) || hitInfo == null ||
                    hitInfo.HitEntity != Grid)
                {
                    if (GlobalData.Debug)
                        DebugDraw.AddLine(from, to, Color.Red, 0);
                    return;
                }

                var block = Grid.GetCubeBlock(hitPos);

                if (GlobalData.Debug)
                    DebugDraw.AddLine(from, to, Color.Green, 0);
                // Armor blocks have half the RCS of components, and light armor has half the RCS of heavy armor.
                totalVcs += 1;
                double scaledRcs = Math.Abs(Vector3D.Dot(globalDirection, hitInfo.Normal));
                if (block?.FatBlock == null)
                {
                    if (GlobalData.LowRcsSubtypes.Contains(block?.BlockDefinition.Id.SubtypeName))
                        totalRcs += scaledRcs / 2;
                    else
                        totalRcs += scaledRcs;
                }
                else
                    totalRcs += scaledRcs * 2;
            });

            radarCrossSection = totalRcs * Grid.GridSize * Grid.GridSize * scaleMultiplier * scaleMultiplier;
            visualCrossSection = totalVcs * Grid.GridSize * Grid.GridSize * scaleMultiplier * scaleMultiplier;

            // Failsafe for all raycasts missing
            if (radarCrossSection == 0 || visualCrossSection == 0)
            {
                radarCrossSection = base.ProjectedArea(Grid.WorldAABB.Center - globalDirection, VisibilityType.Radar);
                visualCrossSection = base.ProjectedArea(Grid.WorldAABB.Center - globalDirection, VisibilityType.Optical);
            }
        }
    }
}
