using DetectionEquipment.Shared;
using DetectionEquipment.Shared.ExternalApis;
using DetectionEquipment.Shared.Utils;
using RichHudFramework;
using Sandbox.Game.Entities;
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
        public float[] OpticalVisibilityCache = new float[TrackingUtils.VisibilityDirectionCache.Length];

        /// <summary>
        /// Cache cardinal and ordinal directions for active radar.
        /// </summary>
        public float[] RadarVisibilityCache = new float[TrackingUtils.VisibilityDirectionCache.Length];

        private HashSet<Vector3I> UpdatedCells = new HashSet<Vector3I>();
        public bool NeedsRegenerateCache = true;
        public bool NeedsUpdate => UpdatedCells.Count > 0 || NeedsRegenerateCache;

        private readonly HashSet<IMyThrust> _thrustCache = new HashSet<IMyThrust>();
        private readonly HashSet<IMyFunctionalBlock> _broadcasterCache = new HashSet<IMyFunctionalBlock>();
        private readonly HashSet<MyCubeBlock> _wcWeaponCache = new HashSet<MyCubeBlock>();
        private int _lastThrustCacheUpdate = -1;

        public GridTrack(IMyCubeGrid grid) : base((MyEntity)grid)
        {
            Grid = grid;

            foreach (var block in Grid.GetFatBlocks<IMyFunctionalBlock>())
                OnBlockAdded(block.SlimBlock);
            UpdatedCells.Clear();
            Grid.OnBlockAdded += OnBlockAdded;
            Grid.OnBlockRemoved += OnBlockRemoved;
        }

        private void OnBlockAdded(IMySlimBlock block)
        {
            UpdatedCells.Add(block.Position);
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
        }

        private void OnBlockRemoved(IMySlimBlock block)
        {
            UpdatedCells.Add(block.Position);
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
        }

        /// <summary>
        /// Generates the entire visibility cache.
        /// </summary>
        public void RegenerateVisibilityCache()
        {
            if (OpticalVisibilityCache.Length < TrackingUtils.VisibilityDirectionCache.Length)
                OpticalVisibilityCache = new float[TrackingUtils.VisibilityDirectionCache.Length];
            if (RadarVisibilityCache.Length < TrackingUtils.VisibilityDirectionCache.Length)
                RadarVisibilityCache = new float[TrackingUtils.VisibilityDirectionCache.Length];

            MyAPIGateway.Parallel.For(0, TrackingUtils.VisibilityDirectionCache.Length-1, i =>
            {
                var direction = TrackingUtils.VisibilityDirectionCache[i];
                CalculateRcsLocal(direction, out RadarVisibilityCache[i], out OpticalVisibilityCache[i]);

                if (GlobalData.DebugLevel >= 3)
                {
                    float cSize = Grid.LocalAABB.HalfExtents.Length() * Grid.GridSize;
                    var globalVertexOffset = Vector3D.Rotate(cSize * direction, Grid.WorldMatrix);
                    var globalTailOffset = Vector3D.Rotate((cSize + RadarVisibilityCache[i]) * direction, Grid.WorldMatrix);
                    DebugDraw.AddLine(Grid.WorldAABB.Center - globalVertexOffset, Grid.WorldAABB.Center - globalTailOffset, Color.Red, 30);
                }
            });

            UpdatedCells.Clear();
            NeedsRegenerateCache = false;

            if (GlobalData.DebugLevel >= 1)
            {
                Log.Info("GridTrack", $"{Grid.EntityId} regenerated visibility cache.");
            }
        }

        public void UpdateVisibilityCache()
        {
            if (NeedsRegenerateCache)
            {
                RegenerateVisibilityCache();
                return;
            }

            UpdateVisibilityCache(UpdatedCells);
            UpdatedCells.Clear();
        }

        /// <summary>
        /// Updates a portion of the visibility cache.
        /// </summary>
        private void UpdateVisibilityCache(ICollection<Vector3I> blockPositions)
        {
            float maxCastLength = Grid.LocalAABB.HalfExtents.Length();
            var grid = (MyCubeGrid) Grid;
            var directionsNeedingUpdate = GlobalObjectPools.IntPool.Pop();

            MyAPIGateway.Parallel.For(0, TrackingUtils.VisibilityDirectionCache.Length-1, i =>
            {
                var localDirection = TrackingUtils.VisibilityDirectionCache[i];
                var globalCastOffset = Vector3D.Rotate(maxCastLength * localDirection, Grid.WorldMatrix);
                foreach (var blockPosition in blockPositions)
                {
                    var blockPos = Grid.GridIntegerToWorld(blockPosition);
                    LineD castLine = new LineD(blockPos - globalCastOffset, blockPos);
                    var firstCastResult = grid.RayCastBlocks(castLine.From, castLine.To);
                    if (firstCastResult != null && firstCastResult != blockPosition) // fast check failed, need to do slow check
                    {
                        MyCubeGrid.MyCubeGridHitInfo intersect = new MyCubeGrid.MyCubeGridHitInfo();
                        if (grid.GetIntersectionWithLine(ref castLine, ref intersect) &&
                            intersect.Position != blockPosition)
                            continue;
                    }

                    if (GlobalData.DebugLevel >= 4)
                        DebugDraw.AddLine(castLine, Color.Green, 2);

                    lock (directionsNeedingUpdate)
                    {
                        directionsNeedingUpdate.Add(i);
                        return;
                    }
                }
            });

            // TODO: Only run a few of these per tick
            MyAPIGateway.Parallel.ForEach(directionsNeedingUpdate, directionIdx =>
            {
                CalculateRcsLocal(TrackingUtils.VisibilityDirectionCache[directionIdx], out RadarVisibilityCache[directionIdx], out OpticalVisibilityCache[directionIdx]);
            });

            if (GlobalData.DebugLevel >= 1)
            {
                Log.Info("GridTrack", $"{Grid.EntityId} updated visibility cache for {directionsNeedingUpdate.Count} directions.");
            }

            directionsNeedingUpdate.Clear();
            GlobalObjectPools.IntPool.Push(directionsNeedingUpdate);
        }

        protected override double ProjectedArea(Vector3D source, VisibilityType type)
        {
            var cache = type == VisibilityType.Radar ? RadarVisibilityCache : OpticalVisibilityCache;

            Vector3D sourceDirection = Vector3D.Rotate(Vector3D.Normalize(source - Grid.WorldAABB.Center), MatrixD.Invert(Entity.PositionComp.WorldMatrixRef));

            // select direction closest to source
            double bestVisibility = 0, bestWeight = double.MinValue;
            for (var i = 0; i < TrackingUtils.VisibilityDirectionCache.Length; i++)
            {
                var direction = TrackingUtils.VisibilityDirectionCache[i];

                double weight = -Vector3D.Dot(sourceDirection, direction);
                if (weight < bestWeight)
                    continue;

                bestVisibility = cache[i];
                bestWeight = weight;
            }

            return bestVisibility;
        }

        /// <summary>
        /// Combines radar and optical visibility; saves performance in some cases.
        /// </summary>
        public void RadarAndOpticalVisibility(Vector3D source, out double rcs, out double vcs)
        {
            Vector3D sourceDirection = Vector3D.Rotate(Vector3D.Normalize(source - Grid.WorldAABB.Center), MatrixD.Invert(Entity.PositionComp.WorldMatrixRef));
            rcs = 0;
            vcs = 0;

            // select direction closest to source
            double bestWeight = double.MinValue;
            Vector3 bestLine = Vector3.Zero;
            for (var i = 0; i < TrackingUtils.VisibilityDirectionCache.Length; i++)
            {
                var direction = TrackingUtils.VisibilityDirectionCache[i];

                double weight = -Vector3D.Dot(sourceDirection, direction);
                if (weight < bestWeight)
                    continue;

                rcs = RadarVisibilityCache[i];
                vcs = OpticalVisibilityCache[i];
                bestWeight = weight;

                if (GlobalData.DebugLevel >= 3)
                    bestLine = direction;
            }

            if (GlobalData.DebugLevel >= 3)
            {
                float cSize = Grid.LocalAABB.HalfExtents.Length() * Grid.GridSize;
                var globalVertexOffset = Vector3D.Rotate(cSize * bestLine, Grid.WorldMatrix);
                var globalTailOffset = Vector3D.Rotate((float)(cSize + rcs) * bestLine, Grid.WorldMatrix);
                DebugDraw.AddLine(Grid.WorldAABB.Center - globalVertexOffset, source, Color.Red, 0);
                DebugDraw.AddLine(Grid.WorldAABB.Center - globalVertexOffset, Grid.WorldAABB.Center, Color.Red, 0);
                DebugDraw.AddLine(Grid.WorldAABB.Center - globalVertexOffset, Grid.WorldAABB.Center - globalTailOffset, Color.Green, 0);
            }
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
            Vector3 localDirection = Vector3D.Rotate(globalDirection, MatrixD.Invert(Grid.WorldMatrix));

            float rcs, vcs;
            CalculateRcsLocal(localDirection, out rcs, out vcs);
            radarCrossSection = rcs;
            visualCrossSection = vcs;
        }

        protected virtual void CalculateRcsLocal(Vector3 localDirection, out float radarCrossSection, out float visualCrossSection)
        {
            // Estimate the max cast size
            Vector3D minCheck, maxCheck;

            double maxCastLength = Grid.LocalAABB.HalfExtents.Length();
            double checkArea = CalcCheckArea(localDirection, out minCheck, out maxCheck);
            int scaleMultiplier = (int) MathUtils.Clamp(Math.Round(Math.Pow(checkArea / (32 * Grid.GridSize * Grid.GridSize * Grid.GridSize), 1/3d)), 1, double.MaxValue);

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


            radarCrossSection = (float)(totalRcs * Grid.GridSize * Grid.GridSize * scaleMultiplier * scaleMultiplier);
            visualCrossSection = (float)(totalVcs * Grid.GridSize * Grid.GridSize * scaleMultiplier * scaleMultiplier);

            // Failsafe for all raycasts missing
            if (radarCrossSection == 0 || visualCrossSection == 0)
            {
                var globalDirection = Vector3D.Rotate(localDirection, Grid.WorldMatrix);

                radarCrossSection = (float) base.ProjectedArea(Grid.WorldAABB.Center - globalDirection, VisibilityType.Radar) * GlobalData.RcsModifier;
                visualCrossSection = (float) base.ProjectedArea(Grid.WorldAABB.Center - globalDirection, VisibilityType.Optical) * GlobalData.VcsModifier;
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
                    try
                    {
                        // this just throws an exception sometimes and I don't have the whitelist access to fix it
                        if (!grid.GetIntersectionWithLine(ref castLine, ref intersect))
                            continue;
                    }
                    catch (Exception ex)
                    {
                        Log.Info("GridTrack", "Handled exception in (MyCubeGrid).GetIntersectionWithLine:");
                        Log.Exception("GridTrack", ex);
                        continue;
                    }
                    

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
