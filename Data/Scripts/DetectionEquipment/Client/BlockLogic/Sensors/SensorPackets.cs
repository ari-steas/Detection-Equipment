using System.Collections.Generic;
using DetectionEquipment.Server;
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

        protected override IBlockLogic CreateClientLogic()
        {
            return new ClientSensorLogic(Ids, DefinitionIds);
        }

        protected override BlockLogicInitPacket CreateServerInitPacket(long blockEntityId)
        {
            var block = MyAPIGateway.Entities.GetEntityById(blockEntityId) as IMyCameraBlock;
            if (block == null)
                return null;

            var ids = new List<uint>();
            var defIds = new List<int>();
            foreach (var sensor in ServerMain.I.GridSensorMangers[block.CubeGrid].Sensors)
            {
                if (sensor.Block != block)
                    continue;
                ids.Add(sensor.Sensor.Id);
                defIds.Add(sensor.Definition.Id);
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

        public SensorUpdatePacket(ClientSensorData sensor)
        {
            Id = sensor.Id;
            Azimuth = sensor.Azimuth;
            Elevation = sensor.Elevation;
            Aperture = sensor.Aperture;
        }

        public SensorUpdatePacket(Server.SensorBlocks.BlockSensor sensor)
        {
            Id = sensor.Sensor.Id;
            Azimuth = (float) sensor.Azimuth;
            Elevation = (float) sensor.Elevation;
            Aperture = (float) sensor.Aperture;
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
    }
}
