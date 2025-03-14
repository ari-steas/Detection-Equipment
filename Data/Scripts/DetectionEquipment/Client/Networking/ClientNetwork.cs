using Sandbox.ModAPI;
using System.Collections.Generic;
using System;
using System.Linq;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Utils;

namespace DetectionEquipment.Client.Networking
{
    internal class ClientNetwork
    {
        public static ClientNetwork I;
        // We only need one because it's only being sent to the server.
        private readonly HashSet<PacketBase> _packetQueue = new HashSet<PacketBase>();

        public void LoadData()
        {
            I = this;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GlobalData.ClientNetworkId, ReceivedPacket);

            Log.Info("ClientNetwork", "Initialized.");
        }

        public void UnloadData()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(GlobalData.ClientNetworkId, ReceivedPacket);
            Log.Info("ClientNetwork", "Closed.");
        }

        public void Update()
        {
            if (_packetQueue.Count > 0)
            {
                MyAPIGateway.Multiplayer.SendMessageToServer(GlobalData.ServerNetworkId, MyAPIGateway.Utilities.SerializeToBinary(_packetQueue.ToArray()));
                _packetQueue.Clear();
            }
        }

        void ReceivedPacket(ushort channelId, byte[] serialized, ulong senderSteamId, bool isSenderServer)
        {
            try
            {
                PacketBase[] packets = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase[]>(serialized);
                foreach (var packet in packets)
                    packet.Received(senderSteamId, false);
            }
            catch (Exception ex)
            {
                Log.Exception("ClientNetwork", ex);
            }
        }


        public static void SendToServer(PacketBase packet) =>
            I?.SendToServerInternal(packet);

        private void SendToServerInternal(PacketBase packet)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                packet.Received(0, false);
                return;
            }

            _packetQueue.Add(packet);
        }
    }
}
