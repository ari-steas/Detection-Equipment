using DetectionEquipment.Server;
using DetectionEquipment.Server.SensorBlocks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;

namespace DetectionEquipment.Shared.ControlBlocks
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "DetectionTrackerBlock")]
    internal class TrackerBlock : ControlBlockBase
    {
        internal AggregatorBlock SourceAggregator;
        internal List<BlockSensor> ControlledSensors = new List<BlockSensor>();
        public float ResetAngleTime = 4;

        private Dictionary<WorldDetectionInfo, int> _detectionTrackDict = new Dictionary<WorldDetectionInfo, int>();
        private Dictionary<BlockSensor, float> _lockDecay = new Dictionary<BlockSensor, float>();

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            SourceAggregator = (AggregatorBlock) ControlBlockManager.I.Blocks.Values.FirstOrDefault(b => b is AggregatorBlock && b.Block.CubeGrid == Block.CubeGrid);
            ControlledSensors = ServerMain.I.GridSensorMangers[(MyCubeGrid)Block.CubeGrid].Sensors.ToList();
            foreach (var sensor in ControlledSensors)
            {
                _lockDecay[sensor] = 0;
            }
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
                        _lockDecay[sensor] -= 1/60f;
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
