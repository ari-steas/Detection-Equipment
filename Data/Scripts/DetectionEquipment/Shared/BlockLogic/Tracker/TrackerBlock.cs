using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.Structs;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI.Network;
using VRage.Sync;
using VRageMath;

namespace DetectionEquipment.Shared.BlockLogic.Tracker
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "DetectionTrackerBlock")]
    internal class TrackerBlock : ControlBlockBase<IMyConveyorSorter>
    {
        internal AggregatorBlock SourceAggregator
        {
            get
            {
                return TrackerControls.ActiveAggregators[this];
            }
            set
            {
                TrackerControls.ActiveAggregatorSelect.UpdateSelected(this, new long[] { value.Block.EntityId });
            }
        }

        internal HashSet<BlockSensor> ControlledSensors
        {
            get
            {
                return TrackerControls.ActiveSensors[this];
            }
            set
            {
                TrackerControls.ActiveSensorSelect.UpdateSelected(this, value.Select(sensor => sensor.Block.EntityId).ToArray());
            }
        }
        public MySync<float, SyncDirection.BothWays> ResetAngleTime;

        private Dictionary<WorldDetectionInfo, int> _detectionTrackDict = new Dictionary<WorldDetectionInfo, int>();
        public Dictionary<BlockSensor, float> LockDecay = new Dictionary<BlockSensor, float>();

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            ResetAngleTime.Value = 4;

            new TrackerControls().DoOnce(this);
        }

        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

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
                    if (LockDecay[sensor] <= 0)
                    {
                        sensor.DesiredAzimuth = 0;
                        sensor.DesiredElevation = 0;
                    }
                    else
                    {
                        LockDecay[sensor] -= 1 / 60f;
                    }
                    continue;
                }
                _detectionTrackDict[target.Value]++;
                sensor.AimAt(target.Value.Position);
                LockDecay[sensor] = ResetAngleTime;
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
