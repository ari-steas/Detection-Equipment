using System.Collections.Generic;
using DetectionEquipment.Server;
using DetectionEquipment.Server.Networking;
using ProtoBuf;
using Sandbox.ModAPI;
using VRageMath;

namespace DetectionEquipment.Client.BlockLogic.Sensors
{
    [ProtoContract]
    internal class SensorInitPacket : BlockLogicInitPacket
    {
        [ProtoMember(2)] public List<uint> Ids;
        [ProtoMember(3)] public List<int> DefinitionIds;

        public SensorInitPacket()
        {
        }

        protected override IBlockLogic CreateClientLogic()
        {
            return new ClientSensorLogic(Ids, DefinitionIds);
        }

        protected override BlockLogicInitPacket CreateServerInitPacket(long blockEntityId, ulong requesterId)
        {
            var block = MyAPIGateway.Entities.GetEntityById(blockEntityId) as IMyCameraBlock;
            if (block == null)
                return null;



            // send the actual init packet
            var ids = new List<uint>();
            var defIds = new List<int>();
            foreach (var sensor in ServerMain.I.BlockSensorIdMap.Values)
            {
                if (sensor.Block != block)
                    continue;
                ids.Add(sensor.Sensor.Id);
                defIds.Add(sensor.Definition.Id);

                // prep and send update packets, receive order doesn't matter
                ServerNetwork.SendToPlayer(new SensorUpdatePacket(sensor), requesterId);
            }
            return new SensorInitPacket
            {
                AttachedBlockId = blockEntityId,
                Ids = ids,
                DefinitionIds = defIds
            };
        }
    }

    [ProtoContract]
    internal class SensorUpdatePacket : BlockLogicUpdatePacket
    {
        [ProtoMember(2)] public uint Id;
        [ProtoMember(3)] public float Azimuth;
        [ProtoMember(4)] public float Elevation;
        [ProtoMember(5)] public float Aperture;
        [ProtoMember(6)] public float MinAzimuth;
        [ProtoMember(7)] public float MaxAzimuth;
        [ProtoMember(8)] public float MinElevation;
        [ProtoMember(9)] public float MaxElevation;
        [ProtoMember(10)] public bool AllowMechanicalControl;

        public SensorUpdatePacket(long blockId, ClientSensorData sensor) // TODO make this only update one value at a time
        {
            AttachedBlockId = blockId;
            Id = sensor.Id;
            Azimuth = sensor.DesiredAzimuth;
            Elevation = sensor.DesiredElevation;
            Aperture = sensor.Aperture;
            MinAzimuth = sensor.MinAzimuth;
            MaxAzimuth = sensor.MaxAzimuth;
            MinElevation = sensor.MinElevation;
            MaxElevation = sensor.MaxElevation;
            AllowMechanicalControl = sensor.AllowMechanicalControl;
        }

        public SensorUpdatePacket(Server.SensorBlocks.BlockSensor sensor)
        {
            AttachedBlockId = sensor.Block.EntityId;
            Id = sensor.Sensor.Id;
            Azimuth = (float) sensor.DesiredAzimuth;
            Elevation = (float) sensor.DesiredElevation;
            Aperture = (float) sensor.Aperture;
            MinAzimuth = (float) sensor.MinAzimuth;
            MaxAzimuth = (float) sensor.MaxAzimuth;
            MinElevation = (float) sensor.MinElevation;
            MaxElevation = (float) sensor.MaxElevation;
            AllowMechanicalControl = sensor.AllowMechanicalControl;
        }

        private SensorUpdatePacket()
        {
        }

        protected override void TryUpdateLogicClient()
        {
            ClientSensorLogic logic;
            if (!BlockLogicManager.CanUpdateLogic(AttachedBlockId, this, out logic))
                return;
            logic.UpdateFromNetwork(this);
        }

        protected override Vector3D TryUpdateLogicServer()
        {
            Server.SensorBlocks.BlockSensor blockSensor;
            if (!ServerMain.I.BlockSensorIdMap.TryGetValue(Id, out blockSensor))
                return Vector3D.PositiveInfinity;
            blockSensor.UpdateFromPacket(this);
            return blockSensor.Block.GetPosition();
        }

        public override bool CanUpdate(IBlockLogic logic)
        {
            return logic is ClientSensorLogic;
        }
    }
}
