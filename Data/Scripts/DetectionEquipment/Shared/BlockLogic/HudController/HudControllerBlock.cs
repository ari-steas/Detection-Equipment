using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.Networking;
using VRage.Game.Components;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using DetectionEquipment.Client.Interface.DetectionHud;
using VRageMath;
using MyAPIGateway = Sandbox.ModAPI.MyAPIGateway;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.BlockLogic.HudController;
using VRage.Game.ModAPI;

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
        internal List<HudDetectionInfo> Detections = new List<HudDetectionInfo>();

        public SimpleSync<bool> AlwaysDisplay = new SimpleSync<bool>(false);
        public SimpleSync<float> CombineAngle = new SimpleSync<float>((float)MathHelper.ToRadians(2.5));
        public SimpleSync<bool> ShowSelf = new SimpleSync<bool>(false);

        protected override ControlBlockSettingsBase GetSettings => new HudControllerSettings(this);
        protected override ITerminalControlAdder GetControls => new HudControllerControls();

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            AlwaysDisplay.Component = this;
            CombineAngle.Component = this;

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
            if (MyAPIGateway.Session.GameplayFrameCounter % 3 != 0 || SourceAggregator == null)
                return;

            Detections.Clear();
            Detections.EnsureCapacity(SourceAggregator.DetectionSet.Count);
            var blockParent = ShowSelf.Value ? null : Block.CubeGrid.GetTopMostParent(typeof(IMyCubeGrid));
            foreach (var item in SourceAggregator.DetectionSet)
            {
                if (!ShowSelf.Value && item.Entity is IMyCubeGrid && item.Entity.GetTopMostParent(typeof(IMyCubeGrid)) == blockParent)
                    continue;

                var newInfo = new HudDetectionInfo(item);
                if (item.VelocityVariance != null && item.VelocityVariance > SourceAggregator.VelocityErrorThreshold)
                {
                    // Don't show velocity if we can't tell what it is.
                    newInfo.Velocity = null;
                    newInfo.VelocityVariance = null;
                    Detections.Add(newInfo);
                }
                else
                    Detections.Add(newInfo);
            }

            // Only send to players in relevant cockpits
            var topmostParent = Block.GetTopMostParent();
            var updatePacket = new HudUpdatePacket(this);
            foreach (var player in GlobalData.Players)
                if (player.Controller.ControlledEntity?.Entity.GetTopMostParent() == topmostParent)
                    ServerNetwork.SendToPlayer(updatePacket, player.SteamUserId);
        }

        protected void UpdateClient()
        {
            // Only show HUD blocks on controlled grid.
            if (MyAPIGateway.Session.Player?.Controller.ControlledEntity?.Entity.GetTopMostParent() ==
                Block.GetTopMostParent())
            {
                DetectionHud.AlwaysShow = AlwaysDisplay.Value;
                DetectionHud.CombineAngle = CombineAngle.Value;
                DetectionHud.UpdateDetections(Detections);
            }

            for (var i = 0; i < Detections.Count; i++)
            {
                var detection = Detections[i];
                detection.Position += (detection.Entity?.Physics?.LinearVelocity ?? detection.Velocity ?? Vector3.Zero) / 60f;
                Detections[i] = detection;
            }
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
            [ProtoMember(2)] private HudDetectionInfoPackage[] _detections;

            private HudUpdatePacket() { }

            public HudUpdatePacket(HudControllerBlock controller)
            {
                _thisBlockId = controller.Block.EntityId;
                _detections = new HudDetectionInfoPackage[controller.Detections.Count];
                for (var i = 0; i < controller.Detections.Count; i++)
                    _detections[i] = (HudDetectionInfoPackage)controller.Detections[i];
            }

            public override void Received(ulong senderSteamId, bool fromServer)
            {
                if (MyAPIGateway.Session.IsServer && fromServer)
                    return;
                var controller = MyAPIGateway.Entities.GetEntityById(_thisBlockId)?.GameLogic?.GetAs<HudControllerBlock>();
                if (controller == null)
                    return;

                controller.Detections.Clear();

                if (_detections != null)
                {
                    foreach (var info in _detections)
                        controller.Detections.Add((HudDetectionInfo)info);
                }
            }
        }
    }
}
