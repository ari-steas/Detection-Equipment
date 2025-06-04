using DetectionEquipment.Client.BlockLogic;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.BlockLogic.HudController;
using ProtoBuf;

namespace DetectionEquipment.Shared.Networking
{
    [ProtoInclude(GlobalData.ServerNetworkId + 2, typeof(BlockSelectControlPacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 3, typeof(AggregatorBlock.AggregatorUpdatePacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 4, typeof(CountermeasurePacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 5, typeof(CountermeasureEmitterPacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 6, typeof(SimpleSyncManager.InternalSimpleSyncBothWays))]
    [ProtoInclude(GlobalData.ServerNetworkId + 7, typeof(WcTargetingPacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 8, typeof(HudControllerBlock.HudUpdatePacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 9, typeof(BlockLogicInitPacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 10, typeof(BlockLogicUpdatePacket))]
    [ProtoContract(UseProtoMembersOnly = true)]
    public abstract class PacketBase
    {
        /// <summary>
        /// Called whenever your packet is received.
        /// </summary>
        public abstract void Received(ulong senderSteamId, bool fromServer);
    }
}
