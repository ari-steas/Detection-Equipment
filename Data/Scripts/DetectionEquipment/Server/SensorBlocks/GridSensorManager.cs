using DetectionEquipment.Server.Networking;
using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Serialization;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
﻿using System;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Shared.BlockLogic;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Server.SensorBlocks
{
    internal class GridSensorManager
    {
        public readonly IMyCubeGrid Grid;
        public HashSet<BlockSensor> Sensors = new HashSet<BlockSensor>();
        public Dictionary<IMyCubeBlock, List<BlockSensor>> BlockSensorMap = new Dictionary<IMyCubeBlock, List<BlockSensor>>();
        private readonly HashSet<AggregatorBlock> _aggregators = new HashSet<AggregatorBlock>();
        private HashSet<VisibilitySet> _trackVisibility = new HashSet<VisibilitySet>();
        private bool _hasRadar = false;

        private readonly Dictionary<IMyGridGroupData, List<VisibilitySet>> _combineBuffer = new Dictionary<IMyGridGroupData, List<VisibilitySet>>();
        private bool _isUpdateComplete = true;

        public static void ScanTargetsAction(MyCubeGrid mainGrid, BoundingSphereD sphere, List<MyEntity> targets)
        {
            // Vanilla WC targeting
            if (!GlobalData.OverrideWcTargeting)
            {
                if (GlobalData.MaxWcMagicTargetingRange > 0 && sphere.Radius > GlobalData.MaxWcMagicTargetingRange)
                    sphere.Radius = GlobalData.MaxWcMagicTargetingRange;
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, targets);
            }

            // Check all aggregators on this grid and subgrids
            GridSensorManager gridSensors;
            var allGrids = new List<IMyCubeGrid>();
            mainGrid.GetGridGroup(GridLinkTypeEnum.Logical).GetGrids(allGrids);
            foreach (var grid in allGrids)
            {
                if (ServerMain.I.GridSensorMangers.TryGetValue(grid, out gridSensors))
                {
                    var gridPos = grid.WorldMatrix.Translation;
                    foreach (var aggregator in gridSensors._aggregators)
                    {
                        if (!aggregator.DoWcTargeting.Value)
                            continue;

                        foreach (var target in aggregator.DetectionSet)
                        {
                            var err = target.SumError / Vector3D.Distance(gridPos, target.Position);

                            if (GlobalData.DebugLevel > 1)
                            {
                                DebugDraw.AddLine(gridPos, target.Position, Color.Maroon, 10/6f);
                                //MyAPIGateway.Utilities.ShowNotification($"CHK {target.EntityId} | {err * 100:F}/{GlobalData.MinLockForWcTarget*100:F}% err", 100000/60);
                            }

                            if (err > GlobalData.MinLockForWcTarget)
                                continue;
                            if (target.Entity != null && !targets.Contains(target.Entity))
                                targets.Add(target.Entity);
                        }
                    }
                    //MyAPIGateway.Utilities.ShowNotification($"Check Target {((IMyCubeGrid)grid).CustomName} - {targets.Count} found.", 100000/60);
                    //Log.Info("GridSensorManager", $"Check Target {((IMyCubeGrid)grid).CustomName} - {targets.Count} found.");
                }
            }

            ServerNetwork.SendToEveryoneInSync(new WcTargetingPacket(mainGrid, targets), mainGrid.WorldMatrix.Translation);
        }

        public static bool ValidateWeaponTarget(IMyTerminalBlock weapon, int weaponId, MyEntity target)
        {
            if (!GlobalData.OverrideWcTargeting)
                return true;

            foreach (var aggregatorSet in AggregatorControls.ActiveWeapons)
            {
                if (!aggregatorSet.Key.UseAllWeapons && !aggregatorSet.Value.Contains(weapon))
                    continue;
                var detSet = aggregatorSet.Key.DetectionSet;
                lock (detSet)
                {
                    foreach (var item in detSet)
                        if (item.Entity == target)
                            return true;
                }
            }
            return false;
        }

        public GridSensorManager(IMyCubeGrid grid)
        {
            Grid = grid;
            Grid.OnBlockAdded += OnBlockAdded;
            Grid.OnBlockRemoved += OnBlockRemoved;

            grid.GetBlocks(null, b =>
            {
                OnBlockAdded(b);
                return false;
            });
        }
        // TODO: Combine GridSensorManagers on subgrids.
        
        public void Update()
        {
            if (Sensors.Count == 0)
                return;

            if (_isUpdateComplete)
            {
                _isUpdateComplete = false;
                MyAPIGateway.Parallel.Start(UpdateTracks);
            }

            var gridPos = Grid.WorldAABB.Center;
            foreach (var track in _trackVisibility)
                track.Update(gridPos);

            MyAPIGateway.Parallel.ForEach(Sensors, sensor =>
            {
                sensor.Update(_trackVisibility);
            });
        }

        private void UpdateTracks()
        {
            var tracksBuffer = GlobalObjectPools.TrackSharedPool.Pop();
            var internalVisibility = new HashSet<VisibilitySet>(_trackVisibility.Count);
            foreach (var trackKvp in tracksBuffer)
            {
                // 500km max track range if radar is present, 50km otherwise
                if (Vector3D.DistanceSquared(trackKvp.Value.Position, Grid.WorldAABB.Center) > (_hasRadar ? GlobalData.MaxSensorRange * GlobalData.MaxSensorRange : GlobalData.MaxVisualSensorRange * GlobalData.MaxVisualSensorRange))
                    continue;

                // move this before LOS check to save a few more raycasts
                var gT = trackKvp.Value as GridTrack;
                if (gT?.Grid.IsInSameLogicalGroupAs(Grid) ?? false) // skip grids attached to self
                    continue;

                // check sensor visibility before registering tracks
                bool cont = false;
                foreach (var sensor in Sensors)
                {
                    if (sensor.Block == null || sensor.Sensor == null || !sensor.Sensor.Enabled)
                        continue;

                    double targetAngle = Vector3D.Angle(sensor.Sensor.Direction, trackKvp.Value.Position - sensor.Sensor.Position);
                    if (targetAngle <= sensor.Sensor.Aperture || trackKvp.Value.BoundingBox.Intersects(new RayD(sensor.Sensor.Position, sensor.Sensor.Direction)) != null)
                    {
                        cont = true;
                        break;
                    }
                }
                if (!cont)
                {
                    continue;
                }

                if (!TrackingUtils.HasLoS(Grid.WorldAABB.ClosestCorner(trackKvp.Key.PositionComp.WorldAABB.Center), Grid, trackKvp.Key))
                    continue;

                if (gT != null)
                {
                    var topmost = gT.Grid.GetGridGroup(GridLinkTypeEnum.Physical);
                    if (!_combineBuffer.ContainsKey(topmost))
                        _combineBuffer[topmost] = new List<VisibilitySet>();
                    _combineBuffer[topmost].Add(new VisibilitySet(Grid, gT));
                }
                else
                {
                    internalVisibility.Add(new VisibilitySet(Grid, gT));
                }
            }

            GlobalObjectPools.TrackSharedPool.Push(tracksBuffer);

            foreach (var combineKvp in _combineBuffer)
            {
                if (combineKvp.Value.Count > 1)
                    internalVisibility.Add(new VisibilitySet(combineKvp.Value));
                else
                    internalVisibility.Add(combineKvp.Value[0]);
            }
            _combineBuffer.Clear();

            lock (_trackVisibility)
            {
                _trackVisibility = internalVisibility;
            }
            _isUpdateComplete = true;
        }

        public void Close()
        {
            Grid.OnBlockAdded -= OnBlockAdded;
            Grid.OnBlockRemoved -= OnBlockRemoved;

            foreach (var sensor in Sensors)
                sensor.Close();
        }


        private void OnBlockAdded(IMySlimBlock obj)
        {
            var cubeBlock = obj.FatBlock as IMyFunctionalBlock;
            if (cubeBlock == null) return;

            try
            {
                List<uint> ids = new List<uint>();
                var sensors = DefinitionManager.TryCreateSensors(cubeBlock);

                bool didPopulate = sensors.Count > 0;
                if (didPopulate)
                    BlockSensorMap.Add(cubeBlock, sensors);

                foreach (var newSensor in sensors)
                {
                    Sensors.Add(newSensor);
                    ids.Add(newSensor.Sensor.Id);
                    if (!didPopulate)
                        BlockSensorMap[cubeBlock].Add(newSensor);

                    _hasRadar |= newSensor.Sensor is RadarSensor || newSensor.Sensor is PassiveRadarSensor;
                }

                if (ids.Count > 0)
                {
                    BlockSensorSettings.LoadBlockSettings(cubeBlock, sensors);
                    BlockSensorSettings.SaveBlockSettings(cubeBlock, new BlockSensorSettings(sensors));

                    if (cubeBlock is IMyCameraBlock)
                    {
                        var resourceSink = (MyResourceSinkComponent)cubeBlock.ResourceSink;
                        resourceSink.SetRequiredInputFuncByType(GlobalData.ElectricityId, () => BlockSensor.GetPowerDraw(cubeBlock));
                        resourceSink.Update();
                        cubeBlock.EnabledChanged += b => resourceSink.Update();
                    }
                }

                var aggregator = ControlBlockManager.GetLogic<AggregatorBlock>(cubeBlock);
                if (aggregator != null)
                {
                    _aggregators.Add(aggregator);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("GridSensorManager", ex, true);
            }
        }

        private void OnBlockRemoved(IMySlimBlock obj)
        {
            try
            {
                var cubeBlock = obj.FatBlock;
                if (cubeBlock != null)
                {
                    Sensors.RemoveWhere(sensor => sensor.Block == cubeBlock);
                    BlockSensorMap.Remove(cubeBlock);

                    _hasRadar = Sensors.Any(s => s.Sensor is RadarSensor || s.Sensor is PassiveRadarSensor);

                    var aggregator = ControlBlockManager.GetLogic<AggregatorBlock>(cubeBlock);
                    if (aggregator != null)
                    {
                        _aggregators.Remove(aggregator);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("GridSensorManager", ex, true);
            }
        }

        /// <summary>
        /// Buffer class for ITrack data.
        /// </summary>
        public struct VisibilitySet
        {
            public ITrack Track;
            public double RadarVisibility;
            public double OpticalVisibility;
            public double InfraredVisibility;

            public Vector3D ClosestCorner;
            public BoundingBoxD BoundingBox;
            public Vector3D Position;
            private int _lastUpdate;

            public VisibilitySet(IMyCubeGrid thisGrid, ITrack track)
            {
                Track = track;
                if (track is GridTrack)
                {
                    ((GridTrack)track).RadarAndOpticalVisibility(thisGrid.WorldAABB.Center, out RadarVisibility, out OpticalVisibility);
                }
                else
                {
                    RadarVisibility = track.RadarVisibility(thisGrid.WorldAABB.Center);
                    OpticalVisibility = track.OpticalVisibility(thisGrid.WorldAABB.Center);
                }
                InfraredVisibility = track.InfraredVisibility(thisGrid.WorldAABB.Center, OpticalVisibility);

                BoundingBox = Track.BoundingBox;
                ClosestCorner = BoundingBox.ClosestCorner(thisGrid.WorldAABB.Center);
                Position = BoundingBox.Center;

                _lastUpdate = MyAPIGateway.Session.GameplayFrameCounter;
            }

            public VisibilitySet(ICollection<VisibilitySet> toAverage)
            {
                double largestVisibility = 0;
                Track = null;
                RadarVisibility = 0;
                OpticalVisibility = 0;
                InfraredVisibility = 0;
                ClosestCorner = Vector3D.Zero;
                BoundingBox = default(BoundingBoxD);
                Position = Vector3D.Zero;
                _lastUpdate = MyAPIGateway.Session.GameplayFrameCounter;

                foreach (var visibilitySet in toAverage)
                {
                    if (largestVisibility < visibilitySet.RadarVisibility + visibilitySet.OpticalVisibility)
                    {
                        Track = visibilitySet.Track;
                        largestVisibility = visibilitySet.RadarVisibility + visibilitySet.OpticalVisibility;
                        ClosestCorner = visibilitySet.ClosestCorner;
                        BoundingBox = visibilitySet.BoundingBox;
                        Position = visibilitySet.Position;
                    }
                    RadarVisibility += visibilitySet.RadarVisibility;
                    OpticalVisibility += visibilitySet.OpticalVisibility;
                    InfraredVisibility += visibilitySet.InfraredVisibility;
                }
            }

            public void Update(Vector3D gridPosition)
            {
                if (_lastUpdate == MyAPIGateway.Session.GameplayFrameCounter || Track == null)
                    return;

                BoundingBox = Track.BoundingBox;
                ClosestCorner = BoundingBox.ClosestCorner(gridPosition);
                Position = BoundingBox.Center;
                _lastUpdate = MyAPIGateway.Session.GameplayFrameCounter;
            }
        }
    }
}
