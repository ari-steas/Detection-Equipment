using Sandbox.ModAPI;
using System.Collections.Generic;
using System;
using System.Linq;
using VRage.Game.ModAPI;
using VRageMath;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Utils;
using DetectionEquipment.Shared.Networking;

namespace DetectionEquipment.Server.Networking
{
    internal class ServerNetwork
    {
        public static ServerNetwork I;
        private readonly Dictionary<ulong, HashSet<PacketBase>> _packetQueue = new Dictionary<ulong, HashSet<PacketBase>>();


        public void LoadData()
        {
            I = this;
            MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(GlobalData.ServerNetworkId, ReceivedPacket);

            Log.Info("ServerNetwork", "Initialized.");
        }

        public void UnloadData()
        {
            MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(GlobalData.ServerNetworkId, ReceivedPacket);
            I = null;
            Log.Info("ServerNetwork", "Closed.");
        }

        public void Update()
        {
            foreach (var queuePair in _packetQueue)
            {
                if (queuePair.Value.Count == 0)
                    continue;

                MyAPIGateway.Multiplayer.SendMessageTo(GlobalData.ClientNetworkId, MyAPIGateway.Utilities.SerializeToBinary(queuePair.Value.ToArray()), queuePair.Key);
                queuePair.Value.Clear();
            }
        }

        private void ReceivedPacket(ushort channelId, byte[] serialized, ulong senderSteamId, bool isSenderServer)
        {
            try
            {
                foreach (var packet in MyAPIGateway.Utilities.SerializeFromBinary<PacketBase[]>(serialized))
                    packet.Received(senderSteamId, true);
            }
            catch (Exception ex)
            {
                Log.Exception("ServerNetwork", ex);
            }
        }






        public static void SendToPlayer(PacketBase packet, ulong playerSteamId) =>
            I?.SendToPlayerInternal(packet, playerSteamId);
        public static void SendToEveryone(PacketBase packet) =>
            I?.SendToEveryoneInternal(packet);
        public static void SendToEveryoneInSync(PacketBase packet, Vector3D position) =>
            I?.SendToEveryoneInSyncInternal(packet, position);


        private void SendToPlayerInternal(PacketBase packet, ulong playerSteamId)
        {
            if (playerSteamId == MyAPIGateway.Multiplayer.ServerId || playerSteamId == 0)
            {
                if (!MyAPIGateway.Utilities.IsDedicated)
                    packet.Received(0, true);
                return;
            }

            if (!_packetQueue.ContainsKey(playerSteamId))
                _packetQueue[playerSteamId] = new HashSet<PacketBase>();
            _packetQueue[playerSteamId].Add(packet);
        }

        private void SendToEveryoneInternal(PacketBase packet)
        {
            foreach (IMyPlayer p in GlobalData.Players)
            {
                // skip sending to self (server player) or back to sender
                if (p.SteamUserId == MyAPIGateway.Multiplayer.ServerId || p.SteamUserId == 0)
                {
                    if (!MyAPIGateway.Utilities.IsDedicated)
                        packet.Received(0, true);
                    continue;
                }

                if (!_packetQueue.ContainsKey(p.SteamUserId))
                    _packetQueue[p.SteamUserId] = new HashSet<PacketBase>();
                _packetQueue[p.SteamUserId].Add(packet);
            }
        }

        private void SendToEveryoneInSyncInternal(PacketBase packet, Vector3D position)
        {
            List<ulong> toSend = new List<ulong>();
            foreach (var player in GlobalData.Players)
                if (Vector3D.DistanceSquared(player.GetPosition(), position) <= GlobalData.SyncRangeSq) // TODO: Sync this based on camera position
                    toSend.Add(player.SteamUserId);

            if (toSend.Count == 0)
                return;

            foreach (var clientSteamId in toSend)
                SendToPlayerInternal(packet, clientSteamId);
        }
    }
}
