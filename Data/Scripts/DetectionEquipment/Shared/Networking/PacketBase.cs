using DetectionEquipment.Client.BlockLogic;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.BlockLogic.HudController;
using DetectionEquipment.Shared.Helpers;
using ProtoBuf;
using System;
using Sandbox.ModAPI;

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
    [ProtoInclude(GlobalData.ServerNetworkId + 11, typeof(IffHelper.IffSaltPacket))]
    [ProtoInclude(GlobalData.ServerNetworkId + 12, typeof(NetworkProfiler.NetworkProfilePacket))]
    [ProtoContract(UseProtoMembersOnly = true)]
    public abstract class PacketBase
    {
        /// <summary>
        /// Called whenever your packet is received.
        /// </summary>
        public abstract void Received(ulong senderSteamId, bool fromServer);

        /// <summary>
        /// Gets profiling info for this packet
        /// </summary>
        /// <returns></returns>
        public abstract PacketInfo GetInfo();

        public struct PacketInfo
        {
            public string Name => PacketType?.Name ?? PacketTypeName;

            public Type PacketType;
            /// <summary>
            /// optional if PacketType is not available
            /// </summary>
            public string PacketTypeName;
            public int PacketSize;
            public PacketInfo[] SubPackets;

            public static PacketInfo FromPacket(PacketBase packet)
            {
                return new PacketInfo
                {
                    PacketType = packet.GetType(),
                    PacketSize = MyAPIGateway.Utilities.SerializeToBinary(packet).Length
                };
            }

            public static PacketInfo FromPacket(PacketBase packet, params PacketInfo[] subPackets)
            {
                return new PacketInfo
                {
                    PacketType = packet.GetType(),
                    PacketSize = MyAPIGateway.Utilities.SerializeToBinary(packet).Length,
                    SubPackets = subPackets
                };
            }
        }
    }
}
