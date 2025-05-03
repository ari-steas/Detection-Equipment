using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using ProtoBuf;

namespace DetectionEquipment.Shared.Networking
{
    [ProtoInclude(GlobalData.ServerNetworkId + 0, typeof(SensorUpdatePacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 1, typeof(SensorInitPacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 2, typeof(BlockSelectControlPacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 3, typeof(AggregatorBlock.AggregatorUpdatePacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 4, typeof(CountermeasurePacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 5, typeof(CountermeasureEmitterPacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 6, typeof(SimpleSyncManager.InternalSimpleSyncBothWays))]
    [ProtoContract(UseProtoMembersOnly = true)]
    public abstract class PacketBase
    {
        /// <summary>
        /// Called whenever your packet is recieved.
        /// </summary>
        public abstract void Received(ulong senderSteamId, bool fromServer);
    }
}
