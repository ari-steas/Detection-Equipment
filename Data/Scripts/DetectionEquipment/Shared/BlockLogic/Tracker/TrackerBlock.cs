using System;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.Structs;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Utils;
using VRage.Game.Entity;

namespace DetectionEquipment.Shared.BlockLogic.Tracker
{
    internal class TrackerBlock : ControlBlockBase<IMyConveyorSorter>, ISensorControlBlock
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
        public readonly SimpleSync<float> ResetAngleTime = new SimpleSync<float>(4);
        public readonly SimpleSync<int> MaxSensorsPerLock  = new SimpleSync<int>(0);
        public readonly SimpleSync<bool> TrackAllies = new SimpleSync<bool>(false);
        public readonly SimpleSync<bool> TrackEnemies = new SimpleSync<bool>(true);
        public readonly SimpleSync<bool> TrackNeutrals = new SimpleSync<bool>(true);
        public SimpleSync<bool> InvertAllowControl { get; } = new SimpleSync<bool>(false);
        public SimpleSync<int> ControlPriority { get; }  = new SimpleSync<int>(0);

        private readonly SortedDictionary<WorldDetectionInfo, int> _detectionTrackDict = new SortedDictionary<WorldDetectionInfo, int>();
        public readonly Dictionary<BlockSensor, LockSet> LockDecay = new Dictionary<BlockSensor, LockSet>();

        protected override ControlBlockSettingsBase GetSettings => new TrackerSettings(this);
        protected override ITerminalControlAdder GetControls => new TrackerControls();

        public TrackerBlock(IMyFunctionalBlock block) : base(block)
        {
        }

        public override void Init()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;
            ResetAngleTime.Component = this;
            MaxSensorsPerLock.Component = this;
            TrackAllies.Component = this;
            TrackEnemies.Component = this;
            TrackNeutrals.Component = this;
            InvertAllowControl.Component = this;
            ControlPriority.Component = this;
            base.Init();
        }

        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            try
            {
                if (SourceAggregator == null || !Block.IsWorking)
                    return;

                _detectionTrackDict.Clear();
                lock (SourceAggregator.DetectionSet)
                {
                    foreach (var detection in SourceAggregator.DetectionSet)
                        _detectionTrackDict[detection] = 0;
                }

                foreach (var sensor in ControlledSensors)
                {
                    // Peek control; idle behavior should not take control.
                    if (!(sensor.AllowMechanicalControl ^ InvertAllowControl.Value) || !sensor.PeekTakeControl(this))
                        continue;

                    var target = GetFirstTarget(sensor);
                    if (target == null)
                    {
                        // idle behavior should not take control
                        if (!LockDecay.ContainsKey(sensor) || LockDecay[sensor].RemainingDecayTime <= 0)
                        {
                            if (!sensor.IsBeingControlled())
                            {
                                sensor.DesiredAzimuth = sensor.Definition.Movement.HomeAzimuth;
                                sensor.DesiredElevation = sensor.Definition.Movement.HomeElevation;
                            }
                            LockDecay.Remove(sensor);
                        }
                        else
                        {
                            sensor.TryTakeControl(this);
                            LockDecay[sensor].RemainingDecayTime -= 1 / 60f;
                        }

                        continue;
                    }

                    sensor.TryTakeControl(this);
                    _detectionTrackDict[target.Value]++;
                    sensor.AimAt(target.Value.Position);
                    LockDecay[sensor] = new LockSet(target.Value.EntityId, ResetAngleTime);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("TrackerBlock", ex, true);
            }
        }

        private WorldDetectionInfo? GetFirstTarget(BlockSensor sensor)
        {
            LockSet prevLockSet;
            if (LockDecay.TryGetValue(sensor, out prevLockSet))
            {
                int minLockCt = int.MaxValue;
                bool hasValue = false;
                var prevTrack = new KeyValuePair<WorldDetectionInfo, int>();
                foreach (var info in _detectionTrackDict)
                {
                    if (MaxSensorsPerLock.Value > 0 && info.Value >= MaxSensorsPerLock.Value)
                        continue;

                    if (info.Value < minLockCt)
                        minLockCt = info.Value;
                    if (info.Key.EntityId != prevLockSet.TrackId)
                        continue;
                    prevTrack = info;
                    hasValue = true;
                }

                bool isIffValid = true;
                switch (prevTrack.Key.Relations)
                {
                    case MyRelationsBetweenPlayers.Allies:
                        isIffValid = TrackAllies.Value;
                        break;
                    case MyRelationsBetweenPlayers.Neutral:
                        isIffValid = TrackNeutrals.Value;
                        break;
                    case MyRelationsBetweenPlayers.Enemies:
                        isIffValid = TrackEnemies.Value;
                        break;
                    // no check by default
                }

                if (hasValue && isIffValid && sensor.CanAimAt(prevTrack.Key.Position) && prevTrack.Value <= minLockCt)
                    return prevTrack.Key;
            }

            int numLocks = int.MaxValue;
            WorldDetectionInfo? bestTarget = null;
            //var sensorGridSize = sensor.Block.CubeGrid.LocalAABB.Size.Length();
            foreach (var target in _detectionTrackDict.Reverse())
            {
                switch (target.Key.Relations)
                {
                    case MyRelationsBetweenPlayers.Allies:
                        if (!TrackAllies.Value)
                            continue;
                        break;
                    case MyRelationsBetweenPlayers.Neutral:
                        if (!TrackNeutrals.Value)
                            continue;
                        break;
                    case MyRelationsBetweenPlayers.Enemies:
                        if (!TrackEnemies.Value)
                            continue;
                        break;
                    // no check by default
                }

                if (MaxSensorsPerLock.Value > 0 && target.Value >= MaxSensorsPerLock.Value)
                    continue;

                if (!sensor.CanAimAt(target.Key.Position))
                    continue;

                // TODO re-introduce this
                // commented out to avoid crash on dediserver start related to list overflow in MyGridIntersection.Calculate()

                //var thisGridHit = sensor.Block.CubeGrid.RayCastBlocks(sensor.Sensor.Position + Vector3D.Normalize(target.Key.Position - sensor.Sensor.Position) * sensorGridSize, sensor.Sensor.Position);
                ////DebugDraw.AddLine(sensor.Sensor.Position + Vector3D.Normalize(target.Key.Position - sensor.Sensor.Position) * sensorGridSize, sensor.Sensor.Position, Color.Blue, 4);
                //if (thisGridHit != sensor.Block.Position)
                //    continue;

                //if (Vector3D.Angle(sensor.Sensor.Direction, target.Key.Position - sensor.Sensor.Position) < sensor.Sensor.Aperture && target.Value <= targetDict.Values.Min())
                //{
                //    bestTarget = target.Key;
                //    break;
                //}

                if (target.Value < numLocks)
                {
                    numLocks = target.Value;
                    bestTarget = target.Key;
                }
            }

            return bestTarget;
        }

        internal class LockSet
        {
            public long TrackId;
            public float RemainingDecayTime;

            public LockSet(long trackId, float remainingDecayTime)
            {
                TrackId = trackId;
                RemainingDecayTime = remainingDecayTime;
            }
        }
    }
}
