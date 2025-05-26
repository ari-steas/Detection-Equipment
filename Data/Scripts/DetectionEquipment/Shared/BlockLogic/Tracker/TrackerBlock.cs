using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.Structs;
using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Shared.Networking;
using VRage.Game.Components;
using VRageMath;
using DetectionEquipment.Shared.BlockLogic.GenericControls;

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
                TrackerControls.ActiveAggregatorSelect.UpdateSelected(this, new[] { value.Block.EntityId });
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
        public SimpleSync<float> ResetAngleTime = new SimpleSync<float>(4);

        private SortedDictionary<WorldDetectionInfo, int> _detectionTrackDict = new SortedDictionary<WorldDetectionInfo, int>();
        public Dictionary<BlockSensor, float> LockDecay = new Dictionary<BlockSensor, float>();

        protected override ControlBlockSettingsBase GetSettings => new TrackerSettings(this);
        protected override ITerminalControlAdder GetControls => new TrackerControls();

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;
            ResetAngleTime.Component = this;
            base.UpdateOnceBeforeFrame();
        }

        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            if (SourceAggregator == null || !Block.IsWorking)
                return;

            _detectionTrackDict.Clear();
            foreach (var detection in SourceAggregator.DetectionSet)
                _detectionTrackDict[detection] = 0;

            foreach (var sensor in ControlledSensors)
            {
                var target = GetFirstTarget(sensor, _detectionTrackDict);
                if (target == null)
                {
                    if (LockDecay[sensor] <= 0)
                    {
                        sensor.DesiredAzimuth = sensor.Definition.Movement.HomeAzimuth;
                        sensor.DesiredElevation = sensor.Definition.Movement.HomeElevation;
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
