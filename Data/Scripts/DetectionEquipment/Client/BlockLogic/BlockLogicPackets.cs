using DetectionEquipment.Client.BlockLogic.Sensors;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using VRageMath;

namespace DetectionEquipment.Client.BlockLogic
{
    [ProtoContract]
    [ProtoInclude(567 + 0, typeof(SensorInitPacket))]
    internal abstract class BlockLogicInitPacket : PacketBase
    {
        [ProtoMember(1)] public long AttachedBlockId;

        protected abstract IBlockLogic CreateClientLogic();
        protected abstract BlockLogicInitPacket CreateServerInitPacket(long blockEntityId, ulong requesterId);

        public override void Received(ulong senderSteamId, bool fromServer)
        {
            if (GlobalData.Debug)
                Log.Info("BlockLogicInitPacket", "Received BlockLogicInitPacket from " + (fromServer ? "server" : "client"));

            if (fromServer)
            {
                var logic = CreateClientLogic();
                if (logic != null)
                    BlockLogicManager.RegisterLogic(AttachedBlockId, logic);
            }
            else
            {
                ServerNetwork.SendToPlayer(CreateServerInitPacket(AttachedBlockId, senderSteamId), senderSteamId);
            }
        }
    }

    [ProtoContract]
    [ProtoInclude(567 + 0, typeof(SensorUpdatePacket))]
    internal abstract class BlockLogicUpdatePacket : PacketBase
    {
        [ProtoMember(1)] public long AttachedBlockId;

        protected abstract void TryUpdateLogicClient();
        protected abstract Vector3D TryUpdateLogicServer();
        public abstract bool CanUpdate(IBlockLogic logic);

        public override void Received(ulong senderSteamId, bool fromServer)
        {
            //if (GlobalData.Debug)
            //    Log.Info("BlockLogicUpdatePacket", "Received BlockLogicUpdatePacket from " + (fromServer ? "server" : "client"));

            if (fromServer)
            {
                TryUpdateLogicClient();
            }
            else
            {
                var blockPos = TryUpdateLogicServer();
                if (blockPos.IsValid())
                    ServerNetwork.SendToEveryoneInSync(this, blockPos);
            }
        }
    }
}
