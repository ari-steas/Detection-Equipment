using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.Aggregator.Datalink;
using DetectionEquipment.Shared.Structs;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Client.Networking;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.Networking;
using ProtoBuf;
using VRage.Game.Components;
using VRage.Game.ModAPI.Network;
using VRage.Sync;
using VRage.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.Aggregator
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "DetectionAggregatorBlock")]
    internal partial class AggregatorBlock : ControlBlockBase<IMyConveyorSorter>
    {
        public MySync<float, SyncDirection.BothWays> AggregationTime;
        public MySync<float, SyncDirection.BothWays> DistanceThreshold;
        public MySync<float, SyncDirection.BothWays> VelocityErrorThreshold; // Standard Deviation at which to ignore velocity estimation
        public MySync<float, SyncDirection.BothWays> RCSThreshold;
        public MySync<bool, SyncDirection.BothWays> AggregateTypes;
        public MySync<bool, SyncDirection.BothWays> UseAllSensors;
        public MySync<int, SyncDirection.BothWays> DatalinkOutChannel;
        private int _prevDatalinkOutChannel = -1;

        public float MaxVelocity = Math.Max(MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed, MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed) + 10;

        internal Queue<WorldDetectionInfo[]> DetectionCache = new Queue<WorldDetectionInfo[]>();

        private HashSet<WorldDetectionInfo> _bufferDetections = new HashSet<WorldDetectionInfo>();

        private Dictionary<int, HashSet<AggregatorBlock>> _bufferVisibleAggregators =
            new Dictionary<int, HashSet<AggregatorBlock>>();

        protected override ControlBlockSettingsBase GetSettings => new AggregatorSettings(this);

        internal HashSet<BlockSensor> ActiveSensors
        {
            get
            {
                return AggregatorControls.ActiveSensors[this];
            }
            set
            {
                AggregatorControls.ActiveSensorSelect.UpdateSelected(this, value.Select(sensor => sensor.Block.EntityId).ToArray());
            }
        }

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

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            DatalinkOutChannel.ValueChanged += sync =>
            {
                DatalinkManager.RegisterAggregator(this, sync.Value, _prevDatalinkOutChannel);
                _prevDatalinkOutChannel = sync.Value;
            };

            new AggregatorControls().DoOnce(this);
            base.UpdateOnceBeforeFrame();
            NeedsUpdate |= MyEntityUpdateEnum.EACH_10TH_FRAME;

            DatalinkManager.RegisterAggregator(this, DatalinkOutChannel.Value, _prevDatalinkOutChannel);
            _prevDatalinkOutChannel = DatalinkOutChannel.Value;
        }

        public override void MarkForClose()
        {
            base.MarkForClose();
            DatalinkManager.RegisterAggregator(this, -1, DatalinkOutChannel);
        }

        public HashSet<WorldDetectionInfo> GetAggregatedDetections()
        {
            foreach (var info in _bufferDetections)
                _infosCache.Add(info);

            foreach (var channel in _bufferVisibleAggregators)
            {
                if (!DatalinkInChannels.Contains(channel.Key))
                    continue;
                foreach (var aggregator in channel.Value)
                    if (aggregator != this)
                        foreach (var info in aggregator._bufferDetections)
                            _infosCache.Add(info);
            }

            var detectionSet = AggregateInfos(_infosCache).ToHashSet();
            _infosCache.Clear();

            return detectionSet;
        }

        private bool _isProcessing = false;
        private readonly List<WorldDetectionInfo[]> _parallelCache = new List<WorldDetectionInfo[]>();
        private readonly List<WorldDetectionInfo> _infosCache = new List<WorldDetectionInfo>();
        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

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

            foreach (var sensor in UseAllSensors.Value ? GridSensors.Sensors : ActiveSensors)
            {
                foreach (var sensorDetection in sensor.Detections)
                {
                    var detection = new WorldDetectionInfo(sensorDetection);
                    //DebugDraw.AddLine(sensor.Sensor.Position, detection.Position, Color.Red, 0);
                    _infosCache.Add(detection);
                }
            }

            DetectionCache.Enqueue(AggregateInfos(_infosCache));
            _infosCache.Clear();
            while (DetectionCache.Count > AggregationTime * 60)
                DetectionCache.Dequeue();

            // testing //
            //MyAPIGateway.Utilities.ShowNotification($"Det: {AggregatedDetections.Count} Cache: {DetectionCache.Count}", 1000/60);
            //foreach (var detection in AggregatedDetections)
            //{
            //    MyAPIGateway.Utilities.ShowMessage("", detection.ToString());
            //    DebugDraw.AddLine(Block.GetPosition(), detection.Position, Color.Green, 0);
            //    if (detection.Velocity != null)
            //        DebugDraw.AddLine(detection.Position, detection.Position + detection.Velocity.Value, Color.Blue, 0);
            //}
        }

        public override void UpdateAfterSimulation10()
        {
            // This method is pretty slow, let's not call it often.
            _bufferVisibleAggregators = DatalinkManager.GetActiveDatalinkChannels(Block.CubeGrid, Block.OwnerId);
        }

        internal class AggregatorUpdatePacket : PacketBase
        {
            [ProtoMember(1)] private long BlockId;
            [ProtoMember(2)] private int[] DatalinkInChannels;

            public AggregatorUpdatePacket(AggregatorBlock block)
            {
                BlockId = block.Block.EntityId;
                DatalinkInChannels = block.DatalinkInChannels;
            }

            private AggregatorUpdatePacket()
            {
            }

            public override void Received(ulong senderSteamId, bool fromServer)
            {
                if (fromServer && MyAPIGateway.Session.IsServer)
                    return;

                var block = MyAPIGateway.Entities.GetEntityById(BlockId)?.GameLogic?.GetAs<AggregatorBlock>();
                if (block == null)
                    return;
                block._datalinkInChannels = DatalinkInChannels;

                if (MyAPIGateway.Session.IsServer)
                    ServerNetwork.SendToEveryoneInSync(this, block.Block.GetPosition());
            }
        }
    }
}
