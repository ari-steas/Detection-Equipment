using DetectionEquipment.Client.Sensors;
using DetectionEquipment.Server;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Linq;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.Networking
{
    /// <summary>
    /// Updates an existing sensor.
    /// </summary>
    [ProtoContract]
    internal class SensorUpdatePacket : PacketBase
    {
        [ProtoMember(1)] public uint Id;
        [ProtoMember(2)] public float Azimuth;
        [ProtoMember(3)] public float Elevation;
        [ProtoMember(4)] public float Aperture;

        public SensorUpdatePacket(BlockSensor sensor)
        {
            Id = sensor.Sensor.Id;
            Azimuth = (float) sensor.DesiredAzimuth;
            Elevation = (float) sensor.DesiredElevation;
            Aperture = (float) sensor.Sensor.Aperture;

            Log.Info("SensorUpdatePacket", "Prepping server SensorUpdatePacket");
        }

        public SensorUpdatePacket(ClientBlockSensor.ClientSensorData sensor)
        {
            Id = sensor.Id;
            Azimuth = sensor.DesiredAzimuth;
            Elevation = sensor.DesiredElevation;
            Aperture = sensor.Aperture;

            Log.Info("SensorUpdatePacket", "Prepping client SensorUpdatePacket");
        }

        private SensorUpdatePacket() { }

        public override void Received(ulong senderSteamId, bool fromServer)
        {
            Log.Info("SensorUpdatePacket", "Recieved SensorUpdatePacket");
            if (!fromServer)
            {
                BlockSensor blockSensor;
                if (!ServerMain.I.BlockSensorIdMap.TryGetValue(Id, out blockSensor))
                    return;
                blockSensor.UpdateFromPacket(this);

                ServerNetwork.SendToEveryoneInSync(this, blockSensor.Block.GetPosition());
            }
            else
            {
                var blockSensor = SensorBlockManager.BlockSensorIdMap[Id];
                blockSensor.UpdateFromPacket(this);
            }
        }
    }

    [ProtoContract]
    internal class SensorInitPacket : PacketBase
    {
        [ProtoMember(1)] public uint Id;
        [ProtoMember(2)] public long AttachedBlockId;
        [ProtoMember(3)] public int DefinitionId;

        public SensorInitPacket(BlockSensor sensor)
        {
            Id = sensor.Sensor.Id;
            AttachedBlockId = sensor.Block.EntityId;
            DefinitionId = sensor.Definition.Id;
            Log.Info("SensorInitPacket", "Prepping server SensorInitPacket");
        }

        /// <summary>
        /// Constructs a new sensor init request for a given block id.
        /// </summary>
        /// <param name="blockId"></param>
        public SensorInitPacket(long blockId)
        {
            AttachedBlockId = blockId;
            Log.Info("SensorInitPacket", "Prepping client SensorInitPacket request");
        }

        private SensorInitPacket() { }

        public override void Received(ulong senderSteamId, bool fromServer)
        {
            Log.Info("SensorInitPacket", "Recieved SensorInitPacket from " + (fromServer ? "server" : "client"));
            var block = MyAPIGateway.Entities.GetEntityById(AttachedBlockId) as IMyCameraBlock;
            if (block == null)
            {
                Log.Exception("SensorInitPacket", new Exception($"Invalid EntityId \"{AttachedBlockId}\" for sensor \"{Id}\"!"));
                return;
            }

            if (fromServer)
            {
                block.GameLogic.GetAs<ClientBlockSensor>().RegisterSensor(this);
            }
            else
            {
                foreach (var sensor in ServerMain.I.GridSensorMangers[block.CubeGrid].Sensors)
                {
                    if (sensor.Block != block)
                        continue;
                    ServerNetwork.SendToPlayer(new SensorInitPacket(sensor), senderSteamId);
                    MyAPIGateway.Utilities.InvokeOnGameThread(() => ServerNetwork.SendToPlayer(new SensorUpdatePacket(sensor), senderSteamId)); // Wait a tick before sending update packet to ensure it arrives after
                }
                    
                Log.Info("SensorInitPacket", "Sent requested init packets to " + senderSteamId);
            }
        }
    }
}
