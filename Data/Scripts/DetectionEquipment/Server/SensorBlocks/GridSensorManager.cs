﻿using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Serialization;
using ParallelTasks;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Server.SensorBlocks
{
    internal class GridSensorManager
    {
        public readonly IMyCubeGrid Grid;
        public HashSet<BlockSensor> Sensors = new HashSet<BlockSensor>();
        public Dictionary<IMyCubeBlock, uint[]> BlockSensorIdMap = new Dictionary<IMyCubeBlock, uint[]>();
        public HashSet<VisibilitySet> TrackVisibility = new HashSet<VisibilitySet>();
        public bool HasRadar = false;

        private Dictionary<IMyGridGroupData, List<VisibilitySet>> _combineBuffer = new Dictionary<IMyGridGroupData, List<VisibilitySet>>();
        private bool _isUpdateComplete = true;

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
                var tracksBuffer = new Dictionary<IMyEntity, ITrack>(ServerMain.I.Tracks);
                MyAPIGateway.Parallel.Start(() =>
                {
                    var internalVisibility = new HashSet<VisibilitySet>(TrackVisibility.Count);
                    foreach (var trackKvp in tracksBuffer)
                    {
                        // 500km max track range if radar is present, 50km otherwise
                        if (Vector3D.DistanceSquared(trackKvp.Value.Position, Grid.WorldAABB.Center) > (HasRadar ? 500000d * 500000d : 50000d * 50000d))
                            continue;

                        var gT = trackKvp.Value as GridTrack;
                        if (gT?.Grid?.GetTopMostParent() == Grid.GetTopMostParent())
                            continue;

                        if (gT != null)
                        {
                            var topmost = gT.Grid.GetGridGroup(GridLinkTypeEnum.Physical);
                            if (!_combineBuffer.ContainsKey(topmost))
                                _combineBuffer[topmost] = new List<VisibilitySet>();
                            _combineBuffer[topmost].Add(new VisibilitySet(Grid, trackKvp.Value));
                        }
                        else
                        {
                            internalVisibility.Add(new VisibilitySet(Grid, trackKvp.Value));
                        }
                    }

                    foreach (var combineKvp in _combineBuffer)
                    {
                        if (combineKvp.Value.Count > 1)
                            internalVisibility.Add(new VisibilitySet(combineKvp.Value));
                        else
                            internalVisibility.Add(combineKvp.Value[0]);
                    }
                    _combineBuffer.Clear();

                    lock (TrackVisibility)
                    {
                        TrackVisibility = internalVisibility;
                    }
                    _isUpdateComplete = true;
                });
            }

            MyAPIGateway.Parallel.ForEach(Sensors, sensor =>
            {
                sensor.Update(TrackVisibility);
            });
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

            List<uint> ids = new List<uint>();
            var sensors = DefinitionManager.TryCreateSensors(cubeBlock);
            foreach (var newSensor in sensors)
            {
                Sensors.Add(newSensor);
                ids.Add(newSensor.Sensor.Id);
            }

            if (ids.Count > 0)
            {
                BlockSensorIdMap[cubeBlock] = ids.ToArray();
                BlockSensorSettings.LoadBlockSettings(cubeBlock, sensors);
                BlockSensorSettings.SaveBlockSettings(cubeBlock, sensors);
            }
        }

        private void OnBlockRemoved(IMySlimBlock obj)
        {
            var cubeBlock = obj.FatBlock;
            if (cubeBlock != null)
            {
                Sensors.RemoveWhere(sensor => sensor.Block == cubeBlock);
                BlockSensorIdMap.Remove(cubeBlock);
            }
        }

        public struct VisibilitySet
        {
            public ITrack Track;
            public double RadarVisibility;
            public double OpticalVisibility;
            public double InfraredVisibility;

            public VisibilitySet(IMyCubeGrid grid, ITrack track)
            {
                Track = track;
                if (track is GridTrack)
                {
                    ((GridTrack)track).CalculateRcs(Vector3D.Normalize(((GridTrack)track).Grid.WorldAABB.Center - grid.WorldAABB.Center), out RadarVisibility, out OpticalVisibility);
                }
                else
                {
                    RadarVisibility = track.RadarVisibility(grid.WorldAABB.Center);
                    OpticalVisibility = track.OpticalVisibility(grid.WorldAABB.Center);
                }
                InfraredVisibility = track.InfraredVisibility(grid.WorldAABB.Center, OpticalVisibility);
            }

            public VisibilitySet(ICollection<VisibilitySet> toAverage)
            {
                double largestVisibility = 0;
                Track = null;
                RadarVisibility = 0;
                OpticalVisibility = 0;
                InfraredVisibility = 0;

                foreach (var visibilitySet in toAverage)
                {
                    if (largestVisibility < visibilitySet.RadarVisibility + visibilitySet.OpticalVisibility)
                    {
                        Track = visibilitySet.Track;
                        largestVisibility = visibilitySet.RadarVisibility + visibilitySet.OpticalVisibility;
                    }
                    RadarVisibility += visibilitySet.RadarVisibility;
                    OpticalVisibility += visibilitySet.OpticalVisibility;
                    InfraredVisibility += visibilitySet.InfraredVisibility;
                }
            }
        }
    }
}
