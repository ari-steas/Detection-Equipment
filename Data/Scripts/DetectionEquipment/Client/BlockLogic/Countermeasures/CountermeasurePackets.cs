using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DetectionEquipment.Client.BlockLogic.Sensors;
using DetectionEquipment.Server;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.ModAPI;
using VRageMath;

namespace DetectionEquipment.Client.BlockLogic.Countermeasures
{
    [ProtoContract]
    internal class CountermeasureInitPacket : BlockLogicInitPacket
    {
        [ProtoMember(2)] public List<uint> Ids;
        [ProtoMember(3)] public List<int> DefinitionIds;

        protected override IBlockLogic CreateClientLogic()
        {
            return new ClientCountermeasureLogic(Ids, DefinitionIds);
        }

        protected override BlockLogicInitPacket CreateServerInitPacket(long blockEntityId, ulong requesterId)
        {
            var ids = new List<uint>();
            var defIds = new List<int>();
            foreach (var logic in Server.Countermeasures.CountermeasureManager.CountermeasureEmitterIdMap)
            {
                if (logic.Value.Block.EntityId != blockEntityId)
                    continue;
                ids.Add(logic.Key);
                defIds.Add(logic.Value.Definition.Id);

                // prep and send update packets, receive order doesn't matter
                ServerNetwork.SendToPlayer(new CountermeasureUpdatePacket(logic.Value), requesterId);
            }

            if (ids.Count == 0)
            {
                //Log.Exception("CountermeasurePacket", new Exception($"Failed to create emitter for {blockEntityId}!"));
                return null;
            }

            return new CountermeasureInitPacket
            {
                AttachedBlockId = blockEntityId,
                Ids = ids,
                DefinitionIds = defIds,
            };
        }
    }

    [ProtoContract]
    internal class CountermeasureUpdatePacket : BlockLogicUpdatePacket
    {
        [ProtoMember(2)] public uint Id;
        [ProtoMember(3)] public bool Firing;
        [ProtoMember(4)] public bool Reloading;

        public CountermeasureUpdatePacket(long blockId, ClientCountermeasureLogic emitter)
        {
            AttachedBlockId = blockId;
            Id = emitter.Id;
            Firing = emitter.Firing;
            Reloading = emitter.Reloading;
        }

        public CountermeasureUpdatePacket(Server.Countermeasures.CountermeasureEmitterBlock emitter)
        {
            AttachedBlockId = emitter.Block.EntityId;
            Id = emitter.Id;
            Firing = emitter.Firing;
            Reloading = emitter.Reloading;
        }

        private CountermeasureUpdatePacket()
        {
        }

        protected override void TryUpdateLogicClient()
        {
            ClientCountermeasureLogic logic;
            if (!BlockLogicManager.CanUpdateLogic(AttachedBlockId, this, out logic))
                return;
            logic.UpdateFromNetwork(this);
        }

        protected override Vector3D TryUpdateLogicServer()
        {
            Server.Countermeasures.CountermeasureEmitterBlock blockEmitter;
            if (!Server.Countermeasures.CountermeasureManager.CountermeasureEmitterIdMap.TryGetValue(Id, out blockEmitter))
                return Vector3D.PositiveInfinity;
            blockEmitter.UpdateFromPacket(this);
            return blockEmitter.Block.GetPosition();
        }

        public override bool CanUpdate(IBlockLogic logic)
        {
            return logic is ClientCountermeasureLogic;
        }
    }
}
