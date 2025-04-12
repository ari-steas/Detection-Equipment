using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI;
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

        private SortedDictionary<WorldDetectionInfo, int> _detectionTrackDict = new SortedDictionary<WorldDetectionInfo, int>();
        public Dictionary<BlockSensor, float> LockDecay = new Dictionary<BlockSensor, float>();

        protected override ControlBlockSettingsBase GetSettings => new TrackerSettings(this);

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;
            new TrackerControls().DoOnce(this);
            base.UpdateOnceBeforeFrame();
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

        private WorldDetectionInfo? GetFirstTarget(BlockSensor sensor, IDictionary<WorldDetectionInfo, int> targetDict)
        {
            int numLocks = int.MaxValue;
            WorldDetectionInfo? bestTarget = null;
            var sensorGridSize = sensor.Block.CubeGrid.LocalAABB.Size.Length();
            foreach (var target in targetDict.Reverse())
            {
                if (!sensor.CanAimAt(target.Key.Position))
                    continue;

                var thisGridHit = sensor.Block.CubeGrid.RayCastBlocks(sensor.Sensor.Position + Vector3D.Normalize(target.Key.Position - sensor.Sensor.Position) * sensorGridSize, sensor.Sensor.Position);
                //DebugDraw.AddLine(sensor.Sensor.Position + Vector3D.Normalize(target.Key.Position - sensor.Sensor.Position) * sensorGridSize, sensor.Sensor.Position, Color.Blue, 4);
                if (thisGridHit != sensor.Block.Position)
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
            }

            return bestTarget;
        }
    }
}
