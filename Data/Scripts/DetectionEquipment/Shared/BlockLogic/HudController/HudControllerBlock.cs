using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Client.Interface;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.Networking;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.BlockLogic.Tracker;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using VRage.Game.Entity;

namespace DetectionEquipment.Shared.BlockLogic.HudController
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "DetectionHudControllerBlock")]
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class HudControllerBlock : ControlBlockBase<IMyConveyorSorter>
    {
        // DisplayNearby?
        // DisplayCockpit?
        // Friendlies / Neutrals
        internal AggregatorBlock SourceAggregator => HudControllerControls.ActiveAggregators[this];
        internal List<WorldDetectionInfo> Detections = new List<WorldDetectionInfo>();

        protected override ControlBlockSettingsBase GetSettings => new HudControllerSettings(this);

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;
            
            new HudControllerControls().DoOnce(this);
            base.UpdateOnceBeforeFrame();
        }

        public override void UpdateAfterSimulation()
        {
            if (!Block.IsWorking)
                return;

            try
            {
                if (!MyAPIGateway.Utilities.IsDedicated)
                    UpdateClient();

                if (MyAPIGateway.Session.IsServer)
                    UpdateServer();
            }
            catch (Exception ex)
            {
                Log.Exception("HudControllerBlock", ex);
            }
        }

        protected void UpdateServer()
        {
            if (MyAPIGateway.Session.GameplayFrameCounter % 7 != 0 || SourceAggregator == null)
                return;

            Detections.Clear();
            Detections.EnsureCapacity(SourceAggregator.DetectionSet.Count);
            foreach (var item in SourceAggregator.DetectionSet)
            {
                if (item.VelocityVariance != null && item.VelocityVariance > SourceAggregator.VelocityErrorThreshold)
                {
                    // Don't show velocity if we can't tell what it is.
                    var newInfo = WorldDetectionInfo.Create(item);
                    newInfo.Velocity = null;
                    newInfo.VelocityVariance = null;
                    Detections.Add(newInfo);
                }
                else
                    Detections.Add(item);
            }
                
            ServerNetwork.SendToEveryoneInSync(new HudUpdatePacket(this), Block.WorldMatrix.Translation);
        }

        protected void UpdateClient()
        {
            for (var i = 0; i < Detections.Count; i++)
            {
                var detection = Detections[i];
                if (detection.Velocity != null)
                    detection.Position += detection.Velocity.Value / 60f;
                Detections[i] = detection;
            }

            DetectionHud.UpdateDetections(Detections);
        }

        public override void MarkForClose()
        {
            base.MarkForClose();
            HudControllerControls.ActiveAggregators.Remove(this);
        }

        [ProtoContract]
        public class HudUpdatePacket : PacketBase
        {
            [ProtoMember(1)] private long _thisBlockId;
            [ProtoMember(2)] private List<WorldDetectionInfo> _detections;

            private HudUpdatePacket() { }

            public HudUpdatePacket(HudControllerBlock controller)
            {
                _thisBlockId = controller.Block.EntityId;
                _detections = controller.Detections;
            }

            public override void Received(ulong senderSteamId, bool fromServer)
            {
                if (MyAPIGateway.Session.IsServer && fromServer)
                    return;
                var controller = MyAPIGateway.Entities.GetEntityById(_thisBlockId)?.GameLogic?.GetAs<HudControllerBlock>();
                if (controller == null)
                    return;
                if (_detections == null)
                    controller.Detections.Clear();
                else
                {
                    for (var i = 0; i < _detections.Count; i++)
                    {
                        var info = _detections[i];
                        info.Entity = (MyEntity) MyAPIGateway.Entities.GetEntityById(info.EntityId);
                        _detections[i] = info;
                    }

                    controller.Detections = _detections;
                }
            }
        }
    }
}
