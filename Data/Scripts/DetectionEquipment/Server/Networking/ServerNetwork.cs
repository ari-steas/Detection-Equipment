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
        public NetworkProfiler Profiler = new NetworkProfiler(true);


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

                // skip sending to self (server player)
                if (queuePair.Key == MyAPIGateway.Multiplayer.ServerId || queuePair.Key == 0)
                {
                    if (!MyAPIGateway.Utilities.IsDedicated)
                    {
                        foreach (var packet in queuePair.Value.ToArray())
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
                    }
                }
                else
                {
                    try
                    {
                        byte[] serialized = MyAPIGateway.Utilities.SerializeToBinary(queuePair.Value.ToArray());
                        MyAPIGateway.Multiplayer.SendMessageTo(GlobalData.ClientNetworkId, serialized, queuePair.Key);
                        if (Profiler.Active)
                            Profiler.LogUpPackets(queuePair.Value, serialized.Length);
                    }
                    catch (Exception ex)
                    {
                        Log.Exception("ServerNetwork", new Exception($"Failed to serialize packet containing {string.Join(", ", queuePair.Value.Select(p => p.GetType().Name).Distinct())}.", ex));
                    }
                }

                queuePair.Value.Clear();
            }

            Profiler.Update();
        }

        private void ReceivedPacket(ushort channelId, byte[] serialized, ulong senderSteamId, bool isSenderServer)
        {
            try
            {
                var packets = MyAPIGateway.Utilities.SerializeFromBinary<PacketBase[]>(serialized);
                foreach (var packet in packets)
                    packet.Received(senderSteamId, false);
                if (Profiler.Active)
                    Profiler.LogDownPackets(packets, serialized.Length);
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
            if (packet == null)
                return;

            if (Environment.CurrentManagedThreadId != GlobalData.MainThreadId || SharedMain.I.Ticks == 0)
            {
                // avoid thread contention and zero players connected on world load
                MyAPIGateway.Utilities.InvokeOnGameThread(() => SendToPlayerInternal(packet, playerSteamId));
                return;
            }

            if (!_packetQueue.ContainsKey(playerSteamId))
                _packetQueue[playerSteamId] = new HashSet<PacketBase>();
            _packetQueue[playerSteamId].Add(packet);
        }

        private void SendToEveryoneInternal(PacketBase packet)
        {
            if (packet == null)
                return;

            if (Environment.CurrentManagedThreadId != GlobalData.MainThreadId || SharedMain.I.Ticks == 0)
            {
                // avoid thread contention and zero players connected on world load
                MyAPIGateway.Utilities.InvokeOnGameThread(() => SendToEveryoneInternal(packet));
                return;
            }

            foreach (IMyPlayer p in GlobalData.Players)
            {
                if (!_packetQueue.ContainsKey(p.SteamUserId))
                    _packetQueue[p.SteamUserId] = new HashSet<PacketBase>();
                _packetQueue[p.SteamUserId].Add(packet);
            }
        }

        private void SendToEveryoneInSyncInternal(PacketBase packet, Vector3D position)
        {
            if (packet == null)
                return;

            if (Environment.CurrentManagedThreadId != GlobalData.MainThreadId || SharedMain.I.Ticks == 0)
            {
                // avoid thread contention and zero players connected on world load
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
