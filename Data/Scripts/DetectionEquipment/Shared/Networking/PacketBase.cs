using DetectionEquipment.Shared.BlockLogic.GenericControls;
using ProtoBuf;

namespace DetectionEquipment.Shared.Networking
{
    [ProtoInclude(100, typeof(SensorUpdatePacket))]
    [ProtoInclude(101, typeof(SensorInitPacket))]
    [ProtoInclude(102, typeof(BlockSelectControlPacket))]
    [ProtoContract(UseProtoMembersOnly = true)]
    public abstract class PacketBase
    {
        /// <summary>
        /// Called whenever your packet is recieved.
        /// </summary>
        /// <param name="SenderSteamId"></param>
        public abstract void Received(ulong senderSteamId, bool fromServer);
    }
}
