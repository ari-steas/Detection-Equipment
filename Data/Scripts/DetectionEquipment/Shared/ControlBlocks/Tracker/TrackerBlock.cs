using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.ControlBlocks.Aggregator;
using DetectionEquipment.Shared.Structs;
using Sandbox.Common.ObjectBuilders;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI.Network;
using VRage.Sync;
using VRageMath;

namespace DetectionEquipment.Shared.ControlBlocks.Tracker
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "DetectionTrackerBlock")]
    internal class TrackerBlock : ControlBlockBase
    {
        internal AggregatorBlock SourceAggregator = null;
        internal List<BlockSensor> ControlledSensors = new List<BlockSensor>();
        public MySync<float, SyncDirection.BothWays> ResetAngleTime;
        public MySync<long[], SyncDirection.BothWays> ActiveSensors;
        public MySync<long, SyncDirection.BothWays> ActiveAggregator;

        private Dictionary<WorldDetectionInfo, int> _detectionTrackDict = new Dictionary<WorldDetectionInfo, int>();
        private Dictionary<BlockSensor, float> _lockDecay = new Dictionary<BlockSensor, float>();

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            ResetAngleTime.Value = 4;
            ActiveSensors.Value = Array.Empty<long>();
            ActiveSensors.ValueChanged += sync =>
            {
                ControlledSensors.Clear();
                _lockDecay.Clear();
                foreach (var sensor in GridSensors.Sensors)
                {
                    for (int i = 0; i < sync.Value.Length; i++)
                    {
                        if (sensor.Block.EntityId != sync.Value[i])
                            continue;
                        ControlledSensors.Add(sensor);
                        _lockDecay[sensor] = 0;
                        break;
                    }
                };
            };
            ActiveAggregator.Value = -1;
            ActiveAggregator.ValueChanged += sync =>
            {
                if (sync.Value == -1)
                {
                    SourceAggregator = null;
                    return;
                }
                SourceAggregator = ControlBlockManager.I.Blocks.Values.FirstOrDefault(b => b.Block.EntityId == sync.Value) as AggregatorBlock;
            };

            SourceAggregator = (AggregatorBlock)ControlBlockManager.I.Blocks.Values.FirstOrDefault(b => b is AggregatorBlock && b.Block.CubeGrid == Block.CubeGrid);

            new TrackerControls().DoOnce();
        }

        public override void UpdateAfterSimulation()
        {
            if (SourceAggregator == null)
                return;

            _detectionTrackDict.Clear();
            foreach (var detection in SourceAggregator.GetAggregatedDetections())
                _detectionTrackDict[detection] = 0;

            foreach (var sensor in ControlledSensors)
            {
                var target = GetFirstTarget(sensor, _detectionTrackDict);
                if (target == null)
                {
                    if (_lockDecay[sensor] <= 0)
                    {
                        sensor.DesiredAzimuth = 0;
                        sensor.DesiredElevation = 0;
                    }
                    else
                    {
                        _lockDecay[sensor] -= 1 / 60f;
                    }
                    continue;
                }
                _detectionTrackDict[target.Value]++;
                sensor.AimAt(target.Value.Position);
                _lockDecay[sensor] = ResetAngleTime;
            }
        }

        private WorldDetectionInfo? GetFirstTarget(BlockSensor sensor, Dictionary<WorldDetectionInfo, int> targetDict)
        {
            int numLocks = int.MaxValue;
            WorldDetectionInfo? bestTarget = null;
            foreach (var target in targetDict)
            {
                if (!sensor.CanAimAt(target.Key.Position))
                    continue;

                if (Vector3D.Angle(sensor.Sensor.Direction, target.Key.Position - sensor.Sensor.Position) < 0.5)
                {
                    bestTarget = target.Key;
                    break;
                }

                if (target.Value < numLocks)
                {
                    numLocks = target.Value;
                    bestTarget = target.Key;
                }
                if (numLocks == 0)
                    break;
            }

            return bestTarget;
        }
    }
}
