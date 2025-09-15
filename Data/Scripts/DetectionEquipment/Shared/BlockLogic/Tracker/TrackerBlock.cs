﻿using System;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.Structs;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Utils;

namespace DetectionEquipment.Shared.BlockLogic.Tracker
{
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
        public readonly SimpleSync<float> ResetAngleTime = new SimpleSync<float>(4);
        public readonly SimpleSync<bool> InvertAllowControl = new SimpleSync<bool>(false);

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
            InvertAllowControl.Component = this;
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
                    if (!(sensor.AllowMechanicalControl ^ InvertAllowControl.Value))
                        continue;

                    var target = GetFirstTarget(sensor);
                    if (target == null)
                    {
                        if (!LockDecay.ContainsKey(sensor) || LockDecay[sensor].RemainingDecayTime <= 0)
                        {
                            sensor.DesiredAzimuth = sensor.Definition.Movement.HomeAzimuth;
                            sensor.DesiredElevation = sensor.Definition.Movement.HomeElevation;
                            LockDecay.Remove(sensor);
                        }
                        else
                        {
                            LockDecay[sensor].RemainingDecayTime -= 1 / 60f;
                        }

                        continue;
                    }

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
                int min = int.MaxValue;
                bool hasValue = false;
                var prevTrack = new KeyValuePair<WorldDetectionInfo, int>();
                foreach (var info in _detectionTrackDict)
                {
                    if (info.Value < min)
                        min = info.Value;
                    if (info.Key.EntityId != prevLockSet.TrackId)
                        continue;
                    prevTrack = info;
                    hasValue = true;
                }

                if (hasValue && sensor.CanAimAt(prevTrack.Key.Position) && prevTrack.Value <= min)
                    return prevTrack.Key;
            }

            int numLocks = int.MaxValue;
            WorldDetectionInfo? bestTarget = null;
            var sensorGridSize = sensor.Block.CubeGrid.LocalAABB.Size.Length();
            foreach (var target in _detectionTrackDict.Reverse())
            {
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
