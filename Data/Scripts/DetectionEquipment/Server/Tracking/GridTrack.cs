using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using RichHudFramework;
using Sandbox.Game.Entities;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using DetectionEquipment.Shared.ExternalApis;

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

        private readonly HashSet<IMyThrust> _thrustCache = new HashSet<IMyThrust>();
        private readonly HashSet<IMyFunctionalBlock> _broadcasterCache = new HashSet<IMyFunctionalBlock>();
        private readonly HashSet<MyCubeBlock> _wcWeaponCache = new HashSet<MyCubeBlock>();
        private int _lastThrustCacheUpdate = -1;

        public GridTrack(IMyCubeGrid grid) : base((MyEntity)grid)
        {
            Grid = grid;

            foreach (var block in Grid.GetFatBlocks<IMyFunctionalBlock>())
            {
                if (block is IMyThrust)
                    _thrustCache.Add((IMyThrust)block);
                if (block is IMyRadioAntenna || block is IMyBeacon)
                    _broadcasterCache.Add(block);
                else if (ApiManager.WcApi.IsReady && ApiManager.WcApi.HasCoreWeapon((MyEntity)block))
                {
                    _wcWeaponCache.Add((MyCubeBlock) block);
                }
            }
            Grid.OnBlockAdded += block =>
            {
                if (block.FatBlock == null)
                    return;

                if (block.FatBlock is IMyThrust)
                    _thrustCache.Add((IMyThrust)block.FatBlock);
                if (block.FatBlock is IMyRadioAntenna || block.FatBlock is IMyBeacon)
                    _broadcasterCache.Add((IMyFunctionalBlock)block.FatBlock);
                else if (ApiManager.WcApi.IsReady && ApiManager.WcApi.HasCoreWeapon((MyEntity)block.FatBlock))
                {
                    _wcWeaponCache.Add((MyCubeBlock)block.FatBlock);
                }
            };
            Grid.OnBlockRemoved += block =>
            {
                if (block.FatBlock == null)
                    return;

                if (block.FatBlock is IMyThrust)
                    _thrustCache.Remove((IMyThrust)block.FatBlock);
                if (block.FatBlock is IMyRadioAntenna || block.FatBlock is IMyBeacon)
                    _broadcasterCache.Remove((IMyFunctionalBlock)block.FatBlock);
                else if (ApiManager.WcApi.IsReady && ApiManager.WcApi.HasCoreWeapon((MyEntity)block.FatBlock))
                {
                    _wcWeaponCache.Remove((MyCubeBlock) block.FatBlock);
                }
            };
        }

        protected override double ProjectedArea(Vector3D source, VisibilityType type)
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
            // -   Weapon heat (if applicable) per visible area
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

            double wcHeatWattage = 0;
            {
                if (ApiManager.WcApi.IsReady)
                {
                    const double stefanBoltzmann = 5.6704E-8;

                    foreach (var weapon in _wcWeaponCache)
                    {
                        // arbitrary conversion from Wc's Proprietary Heat Measurement Unit™ to degrees K
                        double wcTemperature = ApiManager.WcApi.GetHeatLevel(weapon) * GlobalData.WcHeatToDegreeConversionRatio + 298;

                        // I (aristeas) reworked this to follow the blackbody radiation formula; thanks for writing the original, Nerd
                        // if we ever want to make this a bit fancier, can add an emissivity multiplier
                        
                        // stefanBoltzmann * temp^4 * area
                        // "room temperature" is counted elsewhere, so it's removed
                        double weaponWattage = stefanBoltzmann *
                                               (wcTemperature * wcTemperature * wcTemperature * wcTemperature - 7886150416d) *
                                               weapon.PositionComp.LocalAABB.SurfaceArea();
                        wcHeatWattage += weaponWattage;

                        //MyAPIGateway.Utilities.ShowNotification($"{((IMyTerminalBlock)weapon).CustomName}: {wcTemperature-273.15:N0}°C ({weaponWattage:N0}W)", 1000/60);
                    }

                    // scale by grid's area to visible area ratio; while it would be better to check for each weapon's visibility individually, that would involve a Performance Overhead (horror)
                    wcHeatWattage = wcHeatWattage / Grid.LocalAABB.SurfaceArea() * opticalVisibility;
                    //MyAPIGateway.Utilities.ShowNotification($"Sum: {wcHeatWattage:N0}W", 1000/60);
                }
            }

            double visFromHeat = (heatWattage + thrustWattage + wcHeatWattage) / Vector3D.DistanceSquared(source, Position);

            // base visibility is already divided by distancesq
            return base.InfraredVisibility(source, opticalVisibility) + visFromHeat;
        }

        public override double CommsVisibility(Vector3D source)
        {
            double strongestCaster = 0;

            foreach (var broadcaster in _broadcasterCache)
            {
                var antenna = broadcaster as IMyRadioAntenna;
                if (broadcaster.Enabled && (antenna == null || antenna.IsBroadcasting))
                {
                    strongestCaster += 1000000 * broadcaster.ResourceSink.CurrentInputByType(GlobalData.ElectricityId);
                }
            }

            if (strongestCaster == 0)
                return 0;

            return strongestCaster;
        }

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
                TrackingUtils.MinComponents(ref minCheck, maxCheck);
                TrackingUtils.MaxComponents(ref maxCheck, bufferMin);
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
            var thrustCache = new Dictionary<Vector3, float>(6);
            if (MyAPIGateway.Session.GameplayFrameCounter == _lastThrustCacheUpdate)
                return thrustCache;

            thrustCache.Clear();

            foreach (var thrust in _thrustCache)
            {
                if (thrustCache.ContainsKey(thrust.LocalMatrix.Forward))
                    thrustCache[thrust.LocalMatrix.Forward] += thrust.CurrentThrust;
                else
                    thrustCache.Add(thrust.LocalMatrix.Forward, thrust.CurrentThrust);
            }

            _lastThrustCacheUpdate = MyAPIGateway.Session.GameplayFrameCounter;
            return thrustCache;
        }

        public virtual void CalculateRcs(Vector3D globalDirection, out double radarCrossSection, out double visualCrossSection)
        {
            globalDirection.Normalize();
            Vector3D localDirection = Vector3D.Rotate(globalDirection, MatrixD.Invert(Grid.WorldMatrix));

            // Estimate the max cast size
            Vector3D minCheck, maxCheck;

            double maxCastLength = Grid.LocalAABB.HalfExtents.Length();
            double checkArea = CalcCheckArea(localDirection, out minCheck, out maxCheck);
            int scaleMultiplier = (int) MathUtils.Clamp(Math.Round(Math.Pow(checkArea / 500, 1/3d)), 1, double.MaxValue);

            double totalRcs = 0;
            double totalVcs = 0;

            // Cast for occupied cells using a weird and messed up custom method (based around block bounding boxes)
            foreach (var hitInfo in GenerateHitNormals(localDirection, minCheck, maxCheck, maxCastLength, scaleMultiplier))
            {
                totalVcs += 1;
                double scaledRcs = Math.Abs(hitInfo.DotProduct);

                if (GlobalData.LowRcsSubtypes.Contains(hitInfo.Block.BlockDefinition.Id.SubtypeName))
                    totalRcs += scaledRcs * GlobalData.LightRcsModifier;
                else if (hitInfo.Block.FatBlock == null)
                    totalRcs += scaledRcs * GlobalData.HeavyRcsModifier;
                else
                    totalRcs += scaledRcs * GlobalData.FatblockRcsModifier;
            }


            radarCrossSection = totalRcs * Grid.GridSize * Grid.GridSize * scaleMultiplier * scaleMultiplier;
            visualCrossSection = totalVcs * Grid.GridSize * Grid.GridSize * scaleMultiplier * scaleMultiplier;

            // Failsafe for all raycasts missing
            if (radarCrossSection == 0 || visualCrossSection == 0)
            {
                radarCrossSection = base.ProjectedArea(Grid.WorldAABB.Center - globalDirection, VisibilityType.Radar) * GlobalData.RcsModifier;
                visualCrossSection = base.ProjectedArea(Grid.WorldAABB.Center - globalDirection, VisibilityType.Optical) * GlobalData.VcsModifier;
            }
        }

        /// <summary>
        /// Estimates raycast bounds to save on performance.
        /// </summary>
        /// <param name="localDir"></param>
        /// <param name="minCheck"></param>
        /// <param name="maxCheck"></param>
        private double CalcCheckArea(Vector3D localDir, out Vector3D minCheck, out Vector3D maxCheck)
        {
            minCheck = Vector3D.MaxValue;
            maxCheck = Vector3D.MinValue;

            var suggestedPlaneUp = Vector3D.Cross(localDir, Vector3D.Dot(localDir, Vector3.Right) < 0.5 ? Vector3D.Right : Vector3D.Up);
            suggestedPlaneUp.Normalize();

            var lookMatrix = MatrixD.CreateFromDir(localDir, suggestedPlaneUp);
            var invLookMatrix = MatrixD.Invert(lookMatrix);

            foreach (var corner in Entity.PositionComp.LocalAABB.Corners)
            {
                Vector3D vec = corner - Entity.PositionComp.LocalAABB.Center;
                // project on plane
                vec = vec - Vector3D.Dot(vec, localDir) * localDir;

                if (GlobalData.DebugLevel > 2)
                    DebugDraw.AddLine(Grid.WorldAABB.Center, Vector3D.Rotate(vec, Grid.WorldMatrix) + Grid.WorldAABB.Center, Color.DeepPink, 0);

                vec = Vector3D.Rotate(vec, invLookMatrix);
                
                TrackingUtils.MinComponents(ref minCheck, vec);
                TrackingUtils.MaxComponents(ref maxCheck, vec);
            }

            if (GlobalData.DebugLevel > 2)
            {
                DebugDraw.AddPoint(Vector3D.Rotate(minCheck, lookMatrix * Grid.WorldMatrix) + Grid.WorldAABB.Center, Color.Green, 0);
                DebugDraw.AddPoint(Vector3D.Rotate(maxCheck, lookMatrix * Grid.WorldMatrix) + Grid.WorldAABB.Center, Color.Red, 0);
            }

            return (maxCheck.X - minCheck.X) * (maxCheck.Y - minCheck.Y);
        }

        private IEnumerable<CustomHitInfo> GenerateHitNormals(Vector3D localDir, Vector3D minCheck, Vector3D maxCheck, double maxCastLength, int scaleMultiplier)
        {
            var grid = (MyCubeGrid)Grid;
            var gridPos = Grid.WorldAABB.Center;
            var lookMatrix = MatrixD.CreateFromDir(localDir, Vector3D.Cross(localDir, Vector3D.Dot(localDir, Vector3.Right) < 0.5 ? Vector3D.Right : Vector3D.Up));

            minCheck.X += Grid.GridSize / 2;
            minCheck.Y += Grid.GridSize / 2;
            maxCheck.X -= Grid.GridSize / 2;
            maxCheck.Y -= Grid.GridSize / 2;

            for (double x = minCheck.X; x <= maxCheck.X; x += Grid.GridSize * scaleMultiplier)
            {
                for (double y = minCheck.Y; y <= maxCheck.Y; y += Grid.GridSize * scaleMultiplier)
                {
                    var vecOffset = Vector3D.Rotate(new Vector3D(x, y, 0), lookMatrix);

                    LineD castLine = new LineD(
                        Vector3D.Rotate(localDir * -maxCastLength + vecOffset, Grid.WorldMatrix) + gridPos,
                        Vector3D.Rotate(localDir * maxCastLength + vecOffset, Grid.WorldMatrix) + gridPos);


                    if (GlobalData.DebugLevel > 3)
                        DebugDraw.AddLine(castLine, Color.Gray.SetAlphaPct(0.05f), 0);

                    MyCubeGrid.MyCubeGridHitInfo intersect = new MyCubeGrid.MyCubeGridHitInfo();
                    if (!grid.GetIntersectionWithLine(ref castLine, ref intersect))
                        continue;

                    if (GlobalData.DebugLevel > 2)
                        DebugDraw.AddLine(intersect.Triangle.IntersectionPointInWorldSpace, intersect.Triangle.IntersectionPointInWorldSpace + intersect.Triangle.NormalInWorldSpace, Color.LimeGreen, 0);
                        
                    yield return new CustomHitInfo(Grid.GetCubeBlock(intersect.Position), Math.Abs(Vector3D.Dot(localDir, intersect.Triangle.NormalInObjectSpace)));
                }
            }
        }

        private struct CustomHitInfo
        {
            public IMySlimBlock Block;
            public double DotProduct;

            public CustomHitInfo(IMySlimBlock block, double dotProduct)
            {
                Block = block;
                DotProduct = dotProduct;
            }
        }
    }
}
