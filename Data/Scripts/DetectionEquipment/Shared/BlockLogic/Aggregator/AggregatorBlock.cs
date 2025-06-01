using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.Aggregator.Datalink;
using DetectionEquipment.Shared.Structs;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using DetectionEquipment.Client.Networking;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Shared.BlockLogic.Aggregator
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "DetectionAggregatorBlock")]
    internal partial class AggregatorBlock : ControlBlockBase<IMyConveyorSorter>
    {
        public readonly SimpleSync<float> AggregationTime = new SimpleSync<float>(1);
        public readonly SimpleSync<float> VelocityErrorThreshold = new SimpleSync<float>(32); // Standard Deviation at which to ignore velocity estimation
        public readonly SimpleSync<bool> UseAllSensors = new SimpleSync<bool>(true);
        public readonly SimpleSync<int> DatalinkOutChannel = new SimpleSync<int>(0);
        public readonly SimpleSync<int> DatalinkInShareType = new SimpleSync<int>(1);
        public readonly SimpleSync<bool> DoWcTargeting = new SimpleSync<bool>(true);
        public readonly SimpleSync<bool> UseAllWeapons = new SimpleSync<bool>(true);
        private int _prevDatalinkOutChannel = -1;

        public float MaxVelocity = Math.Max(MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed, MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed) + 10;

        internal Queue<WorldDetectionInfo[]> DetectionCache = new Queue<WorldDetectionInfo[]>();

        private HashSet<WorldDetectionInfo> _bufferDetections = new HashSet<WorldDetectionInfo>();

        private Dictionary<int, HashSet<AggregatorBlock>> _bufferVisibleAggregators =
            new Dictionary<int, HashSet<AggregatorBlock>>();

        protected override ControlBlockSettingsBase GetSettings => new AggregatorSettings(this);
        protected override ITerminalControlAdder GetControls => new AggregatorControls();

        internal HashSet<BlockSensor> ActiveSensors => AggregatorControls.ActiveSensors[this];
        internal HashSet<IMyTerminalBlock> ActiveWeapons => AggregatorControls.ActiveWeapons[this];

        private int[] _datalinkInChannels = new[] { 0 };
        public int[] DatalinkInChannels
        {
            get
            {
                return _datalinkInChannels;
            }
            set
            {
                _datalinkInChannels = value;
                if (MyAPIGateway.Session.IsServer)
                    ServerNetwork.SendToEveryoneInSync(new AggregatorUpdatePacket(this), Block.GetPosition());
                else
                    ClientNetwork.SendToServer(new AggregatorUpdatePacket(this));
            }
        }

        public HashSet<WorldDetectionInfo> DetectionSet
        {
            get
            {
                if (_lastDetectionSetUpdate + 1 <= MyAPIGateway.Session.GameplayFrameCounter)
                    return UpdateAggregatedDetections();
                return _lastDetectionSet;
            }
        }

        public virtual MyRelationsBetweenPlayers GetInfoRelations(WorldDetectionInfo info)
        {
            // TODO: Script API for this
            if (info.DetectionType == SensorDefinition.SensorType.PassiveRadar) // Radar locks are probably enemies
                return MyRelationsBetweenPlayers.Enemies;
            return MyRelationsBetweenPlayers.Neutral; // we just don't know...
        }

        private int _lastDetectionSetUpdate = -1;
        private HashSet<WorldDetectionInfo> _lastDetectionSet = new HashSet<WorldDetectionInfo>(0);
        /// <summary>
        /// Gets all detections from this aggregator and datalink.
        /// </summary>
        /// <returns></returns>
        private HashSet<WorldDetectionInfo> UpdateAggregatedDetections()
        {
            List<WorldDetectionInfo> infosCache;
            if (!GroupInfoBuffer.TryPop(out infosCache))
                infosCache = new List<WorldDetectionInfo>();

            lock (_bufferDetections)
            {
                foreach (var info in _bufferDetections)
                    infosCache.Add(info);
            }

            foreach (var channel in _bufferVisibleAggregators)
            {
                if (!DatalinkInChannels.Contains(channel.Key))
                    continue;
                foreach (var aggregator in channel.Value)
                {
                    if (aggregator == this) continue;

                    var relations = Block.GetUserRelationToOwner(aggregator.Block.OwnerId);
                    if (relations == MyRelationsBetweenPlayerAndBlock.Enemies)
                        continue;

                    if (DatalinkInShareType != (int)ShareType.Unowned)
                    {
                        if (DatalinkInShareType.Value == (int)ShareType.Neutral &&
                            relations != MyRelationsBetweenPlayerAndBlock.Neutral &&
                            relations != MyRelationsBetweenPlayerAndBlock.FactionShare &&
                            relations != MyRelationsBetweenPlayerAndBlock.Owner)
                            continue;
                        if (DatalinkInShareType.Value == (int)ShareType.FactionOnly &&
                            relations != MyRelationsBetweenPlayerAndBlock.FactionShare &&
                            relations != MyRelationsBetweenPlayerAndBlock.Owner)
                            continue;
                        if (DatalinkInShareType.Value == (int)ShareType.OwnerOnly && relations != MyRelationsBetweenPlayerAndBlock.Owner)
                            continue;
                    }

                    lock (aggregator._bufferDetections)
                    {
                        foreach (var info in aggregator._bufferDetections)
                            infosCache.Add(info);
                    }
                }
                    
            }

            lock (_lastDetectionSet)
            {
                _lastDetectionSetUpdate = MyAPIGateway.Session.GameplayFrameCounter;
                _lastDetectionSet.Clear();
                foreach (var info in AggregateInfos(infosCache))
                {
                    var item = info;
                    _lastDetectionSet.Add(item);
                }

                infosCache.Clear();
                GroupInfoBuffer.Push(infosCache);

                return _lastDetectionSet;
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            AggregationTime.Component = this;
            VelocityErrorThreshold.Component = this;
            UseAllSensors.Component = this;
            DatalinkOutChannel.Component = this;
            DatalinkOutChannel.OnValueChanged = (value, fromNetwork) =>
            {
                DatalinkManager.RegisterAggregator(this, value, _prevDatalinkOutChannel);
                _prevDatalinkOutChannel = value;
            };
            DatalinkInShareType.Component = this;
            DoWcTargeting.Component = this;
            UseAllWeapons.Component = this;

            base.UpdateOnceBeforeFrame();
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;

            DatalinkManager.RegisterAggregator(this, DatalinkOutChannel.Value, _prevDatalinkOutChannel);
            _prevDatalinkOutChannel = DatalinkOutChannel.Value;
        }

        public override void Close()
        {
            base.Close();
            if (DatalinkOutChannel != null)
                DatalinkManager.RegisterAggregator(this, -1, DatalinkOutChannel);
        }

        private bool _isProcessing = false;
        private readonly List<WorldDetectionInfo[]> _parallelCache = new List<WorldDetectionInfo[]>();
        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            try
            {
                List<WorldDetectionInfo> infosCache;
                if (!GroupInfoBuffer.TryPop(out infosCache))
                    infosCache = new List<WorldDetectionInfo>();

                foreach (var sensor in UseAllSensors.Value ? GridSensors.Sensors : ActiveSensors)
                {
                    foreach (var sensorDetection in sensor.Detections)
                    {
                        var detection = WorldDetectionInfo.Create(sensorDetection, this);
                        infosCache.Add(detection);
                    }
                }

                DetectionCache.Enqueue(AggregateInfos(infosCache));
                infosCache.Clear();
                GroupInfoBuffer.Push(infosCache);
                while (DetectionCache.Count > AggregationTime * 60)
                    DetectionCache.Dequeue();

                if (!_isProcessing)
                {
                    _isProcessing = true;

                    _parallelCache.Clear();
                    _parallelCache.EnsureCapacity(DetectionCache.Count + 1);
                    foreach (var item in DetectionCache)
                        _parallelCache.Add(item);

                    MyAPIGateway.Parallel.Start(() =>
                    {
                        CalculateDetections(_parallelCache);
                        _isProcessing = false;
                    });
                }

                // testing //
                //MyAPIGateway.Utilities.ShowNotification($"Det: {AggregatedDetections.Count} Cache: {DetectionCache.Count}", 1000/60);
                //if (Block.ShowOnHUD && !MyAPIGateway.Utilities.IsDedicated)
                //{
                //    foreach (var detection in _bufferDetections)
                //    {
                //        DebugDraw.AddLine(Block.GetPosition(), detection.Position, Color.Green, 0);
                //        if (detection.Velocity != null)
                //            DebugDraw.AddLine(detection.Position, detection.Position + detection.Velocity.Value, Color.Blue, 0);
                //    }
                //}
            }
            catch (Exception ex)
            {
                Log.Exception("AggregatorBlock", ex, true); 
            }
        }

        public override void UpdateAfterSimulation10()
        {
            // This method is pretty slow, let's not call it often.
            _bufferVisibleAggregators = DatalinkManager.GetActiveDatalinkChannels(Block.CubeGrid, Block.OwnerId);
        }

        public enum ShareType
        {
            Unowned = 0,
            Neutral = 1,
            FactionOnly = 2,
            OwnerOnly = 3,
        }

        internal class AggregatorUpdatePacket : PacketBase
        {
            [ProtoMember(1)] private long _blockId;
            [ProtoMember(2)] private int[] _datalinkInChannels;

            public AggregatorUpdatePacket(AggregatorBlock block)
            {
                _blockId = block.Block.EntityId;
                _datalinkInChannels = block.DatalinkInChannels;
            }

            private AggregatorUpdatePacket()
            {
            }

            public override void Received(ulong senderSteamId, bool fromServer)
            {
                if (fromServer && MyAPIGateway.Session.IsServer)
                    return;

                var block = MyAPIGateway.Entities.GetEntityById(_blockId)?.GameLogic?.GetAs<AggregatorBlock>();
                if (block == null)
                    return;
                block._datalinkInChannels = _datalinkInChannels;

                if (MyAPIGateway.Session.IsServer)
                    ServerNetwork.SendToEveryoneInSync(this, block.Block.GetPosition());
            }
        }
    }
}
