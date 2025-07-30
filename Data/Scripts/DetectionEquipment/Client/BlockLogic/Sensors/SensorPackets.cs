using DetectionEquipment.Server;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRageMath;
using static DetectionEquipment.Client.BlockLogic.Sensors.SensorUpdatePacket;

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
                ServerNetwork.SendToPlayer(new SensorUpdatePacket(sensor, FieldId.All), requesterId);
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
        [ProtoMember(3)] public FieldId Fields;
        [ProtoMember(4)] private float[] _values;

        public SensorUpdatePacket(long blockId, ClientSensorData sensor, FieldId fields) // TODO make this only update one value at a time
        {
            AttachedBlockId = blockId;
            Id = sensor.Id;
            Fields = fields;

            List<float> valuesSet = new List<float>();
            if ((fields & FieldId.Azimuth) != 0)
                valuesSet.Add(sensor.DesiredAzimuth);
            if ((fields & FieldId.Elevation) != 0)
                valuesSet.Add(sensor.DesiredElevation);
            if ((fields & FieldId.Aperture) != 0)
                valuesSet.Add(sensor.Aperture);
            if ((fields & FieldId.MinAzimuth) != 0)
                valuesSet.Add(sensor.MinAzimuth);
            if ((fields & FieldId.MaxAzimuth) != 0)
                valuesSet.Add(sensor.MaxAzimuth);
            if ((fields & FieldId.MinElevation) != 0)
                valuesSet.Add(sensor.MinElevation);
            if ((fields & FieldId.MaxElevation) != 0)
                valuesSet.Add(sensor.MaxElevation);
            if ((fields & FieldId.AllowMechanicalControl) != 0)
                valuesSet.Add(sensor.AllowMechanicalControl ? 1 : 0);
            _values = valuesSet.ToArray();
        }

        public SensorUpdatePacket(Server.SensorBlocks.BlockSensor sensor, FieldId fields)
        {
            AttachedBlockId = sensor.Block.EntityId;
            Id = sensor.Sensor.Id;
            Fields = fields;

            List<float> valuesSet = new List<float>();
            if ((fields & FieldId.Azimuth) != 0)
                valuesSet.Add((float) sensor.DesiredAzimuth);
            if ((fields & FieldId.Elevation) != 0)
                valuesSet.Add((float) sensor.DesiredElevation);
            if ((fields & FieldId.Aperture) != 0)
                valuesSet.Add((float) sensor.Aperture);
            if ((fields & FieldId.MinAzimuth) != 0)
                valuesSet.Add((float) sensor.MinAzimuth);
            if ((fields & FieldId.MaxAzimuth) != 0)
                valuesSet.Add((float) sensor.MaxAzimuth);
            if ((fields & FieldId.MinElevation) != 0)
                valuesSet.Add((float) sensor.MinElevation);
            if ((fields & FieldId.MaxElevation) != 0)
                valuesSet.Add((float) sensor.MaxElevation);
            if ((fields & FieldId.AllowMechanicalControl) != 0)
                valuesSet.Add(sensor.AllowMechanicalControl ? 1 : 0);
            _values = valuesSet.ToArray();
        }

        private SensorUpdatePacket()
        {
        }

        public void SetField(FieldId fieldId, ref float field)
        {
            // skip if this packet doesn't contain the field
            if ((Fields & fieldId) == 0)
                return;

            int valuesIdx = 0;
            int iterVal = 1;
            while (iterVal < (int) fieldId)
            {
                if (((int) Fields & iterVal) == iterVal) // count number of preceding occupied fields
                    valuesIdx++;
                iterVal <<= 1;
            }

            field = _values[valuesIdx];
        }

        public void SetField(FieldId fieldId, ref double field)
        {
            // skip if this packet doesn't contain the field
            if ((Fields & fieldId) == 0)
                return;

            int valuesIdx = 0;
            int iterVal = 1;
            while (iterVal < (int) fieldId)
            {
                if (((int) Fields & iterVal) == iterVal) // count number of preceding occupied fields
                    valuesIdx++;
                iterVal <<= 1;
            }

            field = _values[valuesIdx];
        }

        public void SetField(FieldId fieldId, ref bool field)
        {
            // skip if this packet doesn't contain the field
            if ((Fields & fieldId) == 0)
                return;

            int valuesIdx = 0;
            int iterVal = 1;
            while (iterVal < (int) fieldId)
            {
                if (((int) Fields & iterVal) == iterVal) // count number of preceding occupied fields
                    valuesIdx++;
                iterVal <<= 1;
            }

            field = _values[valuesIdx] > 0;
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

        [Flags]
        internal enum FieldId
        {
            None = 0,
            Azimuth = 1,
            Elevation = 2,
            Aperture = 4,
            MinAzimuth = 8,
            MaxAzimuth = 16,
            MinElevation = 32,
            MaxElevation = 64,
            AllowMechanicalControl = 128,

            All = int.MaxValue
        }
    }
}
