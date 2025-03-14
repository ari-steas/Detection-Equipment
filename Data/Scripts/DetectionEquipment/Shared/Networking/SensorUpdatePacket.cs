using DetectionEquipment.Client.Sensors;
using DetectionEquipment.Server;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System;
using System.Linq;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.Networking
{
    /// <summary>
    /// Updates an existing sensor.
    /// </summary>
    internal class SensorUpdatePacket : PacketBase
    {
        public uint Id;
        public float Azimuth;
        public float Elevation;
        public float Aperture;

        public SensorUpdatePacket(BlockSensor sensor)
        {
            Id = sensor.Sensor.Id;
            Azimuth = (float) sensor.DesiredAzimuth;
            Elevation = (float) sensor.DesiredElevation;
            Aperture = (float) sensor.Sensor.Aperture;
        }

        public SensorUpdatePacket(ClientBlockSensor.ClientSensorData sensor)
        {
            Id = sensor.Id;
            Azimuth = sensor.DesiredAzimuth;
            Elevation = sensor.DesiredElevation;
            Aperture = sensor.Aperture;
        }

        public override void Received(ulong senderSteamId, bool fromServer)
        {
            if (!fromServer)
            {
                var blockSensor = ServerMain.I.BlockSensorIdMap[Id];
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

    internal class SensorInitPacket : PacketBase
    {
        public uint Id;
        public long AttachedBlockId;
        public int DefinitionId;

        public SensorInitPacket(BlockSensor sensor)
        {
            Id = sensor.Sensor.Id;
            AttachedBlockId = sensor.Block.EntityId;
            DefinitionId = sensor.Definition.Id;
        }

        /// <summary>
        /// Constructs a new sensor init request for a given block id.
        /// </summary>
        /// <param name="blockId"></param>
        public SensorInitPacket(long blockId)
        {
            AttachedBlockId = blockId;
        }

        public override void Received(ulong senderSteamId, bool fromServer)
        {
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
                    if (sensor.Block == block)
                        ServerNetwork.SendToPlayer(new SensorInitPacket(sensor), senderSteamId);
            }
        }
    }
}
