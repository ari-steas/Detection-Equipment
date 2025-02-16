using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Server.SensorBlocks
{
    internal class GridSensorManager
    {
        public readonly IMyCubeGrid Grid;
        public HashSet<BlockSensor> Sensors = new HashSet<BlockSensor>();
        public HashSet<VisibilitySet> TrackVisibility = new HashSet<VisibilitySet>();
        public bool HasRadar = false;

        private Dictionary<IMyGridGroupData, List<VisibilitySet>> _combineBuffer = new Dictionary<IMyGridGroupData, List<VisibilitySet>>();

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

        
        public void Update()
        {
            MyAPIGateway.Utilities.ShowNotification("Sensors: " + Sensors.Count, 1000/60);
            TrackVisibility.Clear();
            _combineBuffer.Clear();
            if (Sensors.Count == 0)
                return;

            foreach (var track in ServerMain.I.Tracks.Values)
            {
                // 500km max track range if radar is present, 50km otherwise
                if (Vector3D.DistanceSquared(track.Position, Grid.WorldAABB.Center) > (HasRadar ? 500000d * 500000d : 50000d * 50000d))
                    continue;

                GridTrack gT = track as GridTrack;
                if (gT?.Grid?.GetTopMostParent() == Grid.GetTopMostParent())
                    continue;

                if (gT != null)
                {
                    var topmost = gT.Grid.GetGridGroup(GridLinkTypeEnum.Physical);
                    if (!_combineBuffer.ContainsKey(topmost))
                        _combineBuffer[topmost] = new List<VisibilitySet>();
                    _combineBuffer[topmost].Add(new VisibilitySet(Grid, track));
                }
                else
                {
                    TrackVisibility.Add(new VisibilitySet(Grid, track));
                }
            }

            foreach (var combineKvp in _combineBuffer)
            {
                if (combineKvp.Value.Count > 1)
                    TrackVisibility.Add(new VisibilitySet(combineKvp.Value));
                else
                    TrackVisibility.Add(combineKvp.Value[0]);
            }

            foreach (var sensor in Sensors)
                sensor.Update(TrackVisibility);
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
            var cubeBlock = obj.FatBlock;
            if (cubeBlock == null) return;

            if (cubeBlock is IMyCameraBlock)
            {
                var sensor = new VisualSensor(false)
                {
                    Aperture = MathHelper.ToRadians(45),
                };
                Sensors.Add(new BlockSensor<VisualSensor>(sensor, cubeBlock));
            }
        }

        private void OnBlockRemoved(IMySlimBlock obj)
        {
            var cubeBlock = obj.FatBlock;
            if (cubeBlock != null)
                Sensors.RemoveWhere(sensor => sensor.Block == cubeBlock);
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
