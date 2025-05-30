﻿using Sandbox.ModAPI;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading;
using VRage.Game.ModAPI;
using VRageMath;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Utils;
using DetectionEquipment.Shared.Networking;
using VRage.Utils;

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
            SimpleSyncManager.Init();

            Log.Info("ServerNetwork", "Ready.");
        }

        public void UnloadData()
        {
            SimpleSyncManager.Close();
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
                    packet.Received(senderSteamId, false);
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
            if (Environment.CurrentManagedThreadId != GlobalData.MainThreadId)
            {
                // avoid thread contention
                MyAPIGateway.Utilities.InvokeOnGameThread(() => SendToPlayerInternal(packet, playerSteamId));
                return;
            }

            if (playerSteamId == MyAPIGateway.Multiplayer.ServerId || playerSteamId == 0)
            {
                if (!MyAPIGateway.Utilities.IsDedicated)
                {
                    try
                    {
                        packet.Received(0, true);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception("ServerNetwork", ex);
                    }
                }
                return;
            }

            if (!_packetQueue.ContainsKey(playerSteamId))
                _packetQueue[playerSteamId] = new HashSet<PacketBase>();
            _packetQueue[playerSteamId].Add(packet);
        }

        private void SendToEveryoneInternal(PacketBase packet)
        {
            if (Environment.CurrentManagedThreadId != GlobalData.MainThreadId)
            {
                // avoid thread contention
                MyAPIGateway.Utilities.InvokeOnGameThread(() => SendToEveryoneInternal(packet));
                return;
            }

            foreach (IMyPlayer p in GlobalData.Players)
            {
                // skip sending to self (server player) or back to sender
                if (p.SteamUserId == MyAPIGateway.Multiplayer.ServerId || p.SteamUserId == 0)
                {
                    if (!MyAPIGateway.Utilities.IsDedicated)
                    {
                        try
                        {
                            packet.Received(0, true);
                        }
                        catch (Exception ex)
                        {
                            Log.Exception("ServerNetwork", ex);
                        }
                    }
                    continue;
                }

                if (!_packetQueue.ContainsKey(p.SteamUserId))
                    _packetQueue[p.SteamUserId] = new HashSet<PacketBase>();
                _packetQueue[p.SteamUserId].Add(packet);
            }
        }

        private void SendToEveryoneInSyncInternal(PacketBase packet, Vector3D position)
        {
            if (Environment.CurrentManagedThreadId != GlobalData.MainThreadId)
            {
                // avoid thread contention
                MyAPIGateway.Utilities.InvokeOnGameThread(() => SendToEveryoneInSyncInternal(packet, position));
                return;
            }

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
