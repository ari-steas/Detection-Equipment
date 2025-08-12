using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DetectionEquipment.Client.Networking;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRageMath;

namespace DetectionEquipment.Shared.Networking
{
    internal class NetworkProfiler
    {
        public readonly bool IsServer;
        public bool Active => RemainingDuration > 0;
        public float RemainingDuration { get; private set; } = 0;
        public float TotalDuration { get; private set; } = 0;
        private ulong LastRequester;

        public static bool CanUserRequestProfile(ulong steamId) => MyAPIGateway.Session.IsUserAdmin(steamId) || AlwaysAllowedSteamIds.Contains(steamId);
        private static readonly ulong[] AlwaysAllowedSteamIds =
        {
            0, // localhost
            76561198274566684u, // aristeas
        };

        private long _startTick;
        private ulong _totalBytesDown = 0;
        private Dictionary<long, PacketBase.PacketInfo> _downPacketLog;
        private ulong _totalBytesUp = 0;
        private Dictionary<long, PacketBase.PacketInfo> _upPacketLog;

        public NetworkProfiler(bool isServer)
        {
            IsServer = isServer;
        }

        public void Update()
        {
            if (!Active)
                return;

            RemainingDuration -= 1 / 60f;

            if (!Active)
                Stop();
        }

        public void LogUpPackets(ICollection<PacketBase> packets, int size)
        {
            if (!Active)
                return;

            _totalBytesUp += (ulong) size;

            PacketBase.PacketInfo[] subPackets = new PacketBase.PacketInfo[packets.Count];
            int i = 0;
            foreach (var packet in packets)
            {
                subPackets[i++] = packet.GetInfo();
            }

            _upPacketLog.Add(DateTime.UtcNow.Ticks, new PacketBase.PacketInfo
            {
                PacketType = packets.GetType(),
                PacketSize = size,
                SubPackets = subPackets
            });
        }

        public void LogDownPackets(ICollection<PacketBase> packets, int size)
        {
            if (!Active)
                return;

            _totalBytesDown += (ulong) size;

            PacketBase.PacketInfo[] subPackets = new PacketBase.PacketInfo[packets.Count];
            int i = 0;
            foreach (var packet in packets)
            {
                subPackets[i++] = packet.GetInfo();
            }

            _downPacketLog.Add(DateTime.UtcNow.Ticks, new PacketBase.PacketInfo
            {
                PacketType = packets.GetType(),
                PacketSize = size,
                SubPackets = subPackets
            });
        }

        public void Activate(float duration, ulong requester = 0)
        {
            if (Active)
                return;

            TotalDuration = duration;
            RemainingDuration = duration;
            _startTick = DateTime.UtcNow.Ticks;
            _downPacketLog = new Dictionary<long, PacketBase.PacketInfo>();
            _upPacketLog = new Dictionary<long, PacketBase.PacketInfo>();
            LastRequester = requester;

            Log.Info("NetworkProfiler", $"{(IsServer ? "SERVER" : "CLIENT")} network profiling activated for {duration}s.");
            MiscUtils.SafeChat("DetEq", $"{(IsServer ? "SERVER" : "CLIENT")} network profiling activated for {duration}s.");

            if (!MyAPIGateway.Utilities.IsDedicated && CanUserRequestProfile(MyAPIGateway.Session.Player.SteamUserId))
            {
                ClientNetwork.SendToServer(new NetworkProfilePacket(TotalDuration));
                Log.Info("NetworkProfiler", "Requested server network profiling.");
                MiscUtils.SafeChat("DetEq", "Requested server network profiling.");
            }
        }

        public void Stop()
        {
            RemainingDuration = 0;

            Log.Info("NetworkProfiler", $"{(IsServer ? "SERVER" : "CLIENT")} network profiling ended.");
            MiscUtils.SafeChat("DetEq", $"{(IsServer ? "SERVER" : "CLIENT")} network profiling ended.");

            string results = FormatResults();
            DisplayResults(results);
            if (MyAPIGateway.Session.IsServer && LastRequester != 0)
            {
                ServerNetwork.SendToPlayer(new NetworkProfilePacket(TotalDuration, results), LastRequester);
            }

            TotalDuration = 0;
            _totalBytesDown = 0;
            _totalBytesUp = 0;
            _downPacketLog = null; // reallocate to decrease memory usage
            _upPacketLog = null;
        }




        private string FormatResults()
        {
            ulong lowestBytesDown = 0;
            string downLog = FormatPacketLog(_downPacketLog, out lowestBytesDown);

            ulong lowestBytesUp = 0;
            string upLog = FormatPacketLog(_upPacketLog, out lowestBytesUp);
            

            StringBuilder fullSb = new StringBuilder();
            fullSb.AppendLine($"{(IsServer ? "SERVER" : "CLIENT")} Profiling Results");
            fullSb.AppendLine($"=====================================================");
            fullSb.AppendLine();
            fullSb.AppendLine($"Duration: {TotalDuration:N}s");
            fullSb.AppendLine($"Sent Packets: {_upPacketLog.Count}");
            fullSb.AppendLine($"    Total size: {_totalBytesUp:N0}b");
            fullSb.AppendLine($"    Logged size: {lowestBytesUp:N0}b"); // at lowest level
            fullSb.AppendLine($"    Estimated efficiency: {100*((double) lowestBytesUp / _totalBytesUp):N0}%");
            fullSb.AppendLine();
            fullSb.AppendLine($"Received Packets: {_upPacketLog.Count} - {_totalBytesUp:N0}b");
            fullSb.AppendLine($"    Total size: {_totalBytesDown:N0}b");
            fullSb.AppendLine($"    Logged size: {lowestBytesDown:N0}b"); // at lowest level
            fullSb.AppendLine($"    Estimated efficiency: {100*((double) lowestBytesDown / _totalBytesDown):N0}%");
            fullSb.AppendLine();
            fullSb.AppendLine($"RESULTS");
            fullSb.AppendLine($"=====================================================");
            fullSb.AppendLine();
            fullSb.AppendLine();
            fullSb.AppendLine($"   DOWN");
            fullSb.AppendLine(downLog);
            fullSb.AppendLine();
            fullSb.AppendLine();
            fullSb.AppendLine($"   UP");
            fullSb.AppendLine(upLog);

            return fullSb.ToString();
        }

        private string FormatPacketLog(Dictionary<long, PacketBase.PacketInfo> log, out ulong lowestBytesSum)
        {
            lowestBytesSum = 0;
            StringBuilder timelineBuilder = new StringBuilder();
            Dictionary<string, AveragingPacketInfo> averagePackets = new Dictionary<string, AveragingPacketInfo>();

            int i = 0;
            foreach (var tickRoot in log)
            {
                timelineBuilder.AppendLine($"   {(++i == log.Count ? "└" : "├")} {(double)(tickRoot.Key - _startTick) / TimeSpan.TicksPerSecond:N}s | {tickRoot.Value.SubPackets.Length} packets | {tickRoot.Value.PacketSize:N0}b");

                foreach (var packet in tickRoot.Value.SubPackets)
                {
                    if (averagePackets.ContainsKey(packet.Name))
                        averagePackets[packet.Name].Add(packet);
                    else
                        averagePackets.Add(packet.Name, new AveragingPacketInfo(packet));
                }
            }

            StringBuilder fullBuilder = new StringBuilder();

            fullBuilder.AppendLine($"Packet Types:");
            foreach (var type in averagePackets.Values)
                fullBuilder.AppendLine(type.Format("    "));
            fullBuilder.AppendLine();
            fullBuilder.AppendLine();
            fullBuilder.AppendLine($"Timeline:");
            fullBuilder.Append(timelineBuilder);
            fullBuilder.AppendLine();

            return fullBuilder.ToString();
        }

        private struct AveragingPacketInfo
        {
            public string Name;
            public long TotalSize;
            public int TotalCount;
            public Dictionary<string, long> SubpacketTotalSize;

            public AveragingPacketInfo(PacketBase.PacketInfo packet)
            {
                Name = packet.Name;
                TotalSize = packet.PacketSize;
                TotalCount = 1;
                SubpacketTotalSize = new Dictionary<string, long>(packet.SubPackets.Length);
                foreach (var subpacket in packet.SubPackets)
                    SubpacketTotalSize[subpacket.Name] = subpacket.PacketSize;
            }

            public void Add(PacketBase.PacketInfo packet)
            {
                TotalSize += packet.PacketSize;
                TotalCount++;
                foreach (var subpacket in packet.SubPackets)
                    SubpacketTotalSize[subpacket.Name] += subpacket.PacketSize;
            }

            public string Format(string linePrefix)
            {
                StringBuilder sb = new StringBuilder($"{Name} - {TotalCount} Instances");
                sb.AppendLine($"{linePrefix}Average Size: {(double)TotalSize / TotalCount:N1}b ({TotalSize:N0}b total)");
                int i = 0;
                foreach (var subpacket in SubpacketTotalSize)
                    sb.AppendLine($"{linePrefix}   {(++i == SubpacketTotalSize.Count ? "└" : "├")} {subpacket.Key}: {(double)subpacket.Value / TotalCount:N1}b ({subpacket.Value:N0}b total)");

                return sb.ToString();
            }
        }

        /*
         * int depth = 1;
         *  depthStack.Clear();
         *  foreach (var sPacket in tickRoot.Value.SubPackets)
         *      infoStack.Push(sPacket);
         *  
         *  while (infoStack.Count > 0)
         *  {
         *      while (depthStack.Peek() <= 0)
         *          depthStack.Pop();
         *  
         *      var current = infoStack.Pop();
         *      // write text
         *      sb.Append(' ', 4 * depth - 1);
         *      sb.AppendLine($"├ {}");
         *  
         *      depth++;
         *      depthStack.Push(current.SubPackets.Length);
         *      foreach (var subPacket in current.SubPackets)
         *          infoStack.Push(subPacket);
         *  
         *      depthStack.Push(depthStack.Pop() - 1);
         *  }
         */

        private static void DisplayResults(string results)
        {
            Log.Info("NetworkProfiler", "\n\n\n" + results + "\n\n\n");
            MiscUtils.SafeChat("NetworkProfiler", "Results sent to log file.");
            // TODO
        }

        internal class NetworkProfilePacket : PacketBase
        {
            [ProtoMember(1)] private float _duration;
            [ProtoMember(2)] private string _results;

            internal NetworkProfilePacket(float duration)
            {
                _duration = duration;
            }

            internal NetworkProfilePacket(float duration, string results)
            {
                _duration = duration;
                _results = results;
            }

            private NetworkProfilePacket()
            {

            }

            public override void Received(ulong senderSteamId, bool fromServer)
            {
                if (MyAPIGateway.Session.IsServer)
                {
                    string requesterName = senderSteamId == 0
                        ? "LocalHost"
                        : GlobalData.Players.FirstOrDefault(p => p.SteamUserId == senderSteamId)?.DisplayName ?? "UNKNOWN";
                    Log.Info("NetworkProfiler", $"Network profiling requested by \"{requesterName}\" (SteamId {senderSteamId}) for {_duration:N}s.");

                    // prevent non-admins from requesting profile results
                    if (ServerNetwork.I.Profiler.Active || !CanUserRequestProfile(senderSteamId))
                        return;

                    MyVisualScriptLogicProvider.SendChatMessageColored($"Network profiling requested by \"{requesterName}\" for {_duration:N}s - performance may drop.", Color.Red, "Detection Equipment");
                    ServerNetwork.I.Profiler.Activate(_duration, senderSteamId);
                    return;
                }

                DisplayResults(_results);
            }

            public override PacketInfo GetInfo()
            {
                return PacketInfo.FromPacket(this,
                    new PacketInfo
                    {
                        PacketTypeName = nameof(_duration),
                        PacketSize = sizeof(float)
                    },
                    new PacketInfo
                    {
                        PacketTypeName = nameof(_results),
                        PacketSize = _results.Length * sizeof(char)
                    }
                );
            }
        }
    }
}
