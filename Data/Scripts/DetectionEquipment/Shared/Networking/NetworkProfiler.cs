using System;
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
        private List<PacketBase.PacketInfo> _downPacketLog;
        private ulong _totalBytesUp = 0;
        private List<PacketBase.PacketInfo> _upPacketLog;

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

            _upPacketLog.Add(new PacketBase.PacketInfo
            {
                Timestamp = DateTime.UtcNow.Ticks,
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

            _downPacketLog.Add(new PacketBase.PacketInfo
            {
                Timestamp = DateTime.UtcNow.Ticks,
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
            _downPacketLog = new List<PacketBase.PacketInfo>();
            _upPacketLog = new List<PacketBase.PacketInfo>();
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
            const float timelineInterval = 1;

            TimelineTick[] timeline = new TimelineTick[(int)Math.Ceiling(TotalDuration/timelineInterval)];
            long lowestBytesDown = 0;
            string downLog = FormatPacketLog(_downPacketLog, timelineInterval, false, out lowestBytesDown, ref timeline);

            long lowestBytesUp = 0;
            string upLog = FormatPacketLog(_upPacketLog, timelineInterval, true, out lowestBytesUp, ref timeline);
            
            StringBuilder timelineBuilder = new StringBuilder();
            timelineBuilder.AppendLine("TIME | PACKET COUNT | SIZE (bytes)");
            for (int i = 0; i < timeline.Length; i++)
            {
                TimelineTick tick = timeline[i];
                timelineBuilder.AppendLine($"{(i+1) * timelineInterval:N}s | {tick.UpCount:N0}u {tick.DownCount:N0}d | {tick.UpSize:N0}b u {tick.DownSize:N0}b d");
            }

            StringBuilder fullSb = new StringBuilder();
            fullSb.AppendLine($"{(IsServer ? "SERVER" : "CLIENT")} Profiling Results");
            fullSb.AppendLine($"=====================================================");
            fullSb.AppendLine();
            fullSb.AppendLine($"Duration: {TotalDuration:N}s");
            fullSb.AppendLine($"Sent Packets: {_upPacketLog.Count}");
            fullSb.AppendLine($"    Total size: {_totalBytesUp:N0}b");
            fullSb.AppendLine($"    Logged size: {lowestBytesUp:N0}b"); // at lowest level
            fullSb.AppendLine($"    Estimated efficiency: {100*((double) lowestBytesUp / _totalBytesUp):N}%");
            fullSb.AppendLine();
            fullSb.AppendLine($"Received Packets: {_downPacketLog.Count}");
            fullSb.AppendLine($"    Total size: {_totalBytesDown:N0}b");
            fullSb.AppendLine($"    Logged size: {lowestBytesDown:N0}b"); // at lowest level
            fullSb.AppendLine($"    Estimated efficiency: {100*((double) lowestBytesDown / _totalBytesDown):N}%");
            fullSb.AppendLine();
            fullSb.AppendLine($"DOWN RESULTS PER-PACKET\n=====================================================");
            fullSb.AppendLine(downLog);
            fullSb.AppendLine();
            fullSb.AppendLine($"UP RESULTS PER-PACKET\n=====================================================");
            fullSb.AppendLine(upLog);
            fullSb.AppendLine();
            fullSb.AppendLine($"TIMELINE");
            fullSb.Append(timelineBuilder);

            return fullSb.ToString();
        }

        private string FormatPacketLog(List<PacketBase.PacketInfo> log, float timelineInterval, bool isUp, out long lowestBytesSum, ref TimelineTick[] timeline)
        {
            lowestBytesSum = 0;
            Dictionary<string, AveragingPacketInfo> averagePackets = new Dictionary<string, AveragingPacketInfo>();

            int i = 0;
            foreach (var tickRoot in log)
            {
                TimelineTick tick;
                if (isUp)
                {
                    tick = new TimelineTick
                    {
                        UpCount = tickRoot.SubPackets.Length,
                        UpSize = tickRoot.PacketSize
                    };
                }
                else
                {
                    tick = new TimelineTick
                    {
                        DownCount = tickRoot.SubPackets.Length,
                        DownSize = tickRoot.PacketSize
                    };
                }

                timeline[(int)MathHelper.Clamp(Math.Ceiling((double)(tickRoot.Timestamp - _startTick) / TimeSpan.TicksPerSecond / timelineInterval)-1, 0, timeline.Length-1)] += tick;

                foreach (var packet in tickRoot.SubPackets)
                {
                    if (averagePackets.ContainsKey(packet.Name))
                        averagePackets[packet.Name].Add(packet);
                    else
                        averagePackets.Add(packet.Name, new AveragingPacketInfo(packet));
                }
            }

            StringBuilder fullBuilder = new StringBuilder();
            
            fullBuilder.AppendLine($"Packet Types:");
            foreach (var type in new SortedSet<AveragingPacketInfo>(averagePackets.Values).Reverse())
            {
                fullBuilder.AppendLine(type.Format("    "));
                lowestBytesSum += type.SubpacketTotalSize.Values.Sum();
            }
            fullBuilder.AppendLine();

            return fullBuilder.ToString();
        }

        private struct TimelineTick
        {
            public int UpCount, DownCount;
            public long UpSize, DownSize;

            public static TimelineTick operator +(TimelineTick left, TimelineTick right) => new TimelineTick
            {
                UpCount = left.UpCount + right.UpCount,
                UpSize = left.UpSize + right.UpSize,
                DownCount = left.DownCount + right.DownCount,
                DownSize = left.DownSize + right.DownSize,
            };
        }

        private class AveragingPacketInfo : IComparable<AveragingPacketInfo>
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
                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"{Name} - {TotalCount} Instance(s)");
                sb.AppendLine($"{linePrefix}Average Size: {(double)TotalSize / TotalCount:N1}b ({TotalSize:N0}b total)");
                sb.AppendLine($"{linePrefix}Estimated Efficiency: {100*(double)SubpacketTotalSize.Values.Sum() / TotalSize:N}%");
                sb.AppendLine($"{linePrefix}Fields");
                int i = 0;
                foreach (var subpacket in SubpacketTotalSize)
                    sb.AppendLine($"{linePrefix}{(++i == SubpacketTotalSize.Count ? "└" : "├")}─ {subpacket.Key}: {(double)subpacket.Value / TotalCount:N1}b ({subpacket.Value:N0}b total)");

                return sb.ToString();
            }

            public int CompareTo(AveragingPacketInfo other)
            {
                if (other == null) return 1;

                return TotalSize.CompareTo(other.TotalSize);
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

        [ProtoContract]
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
                    if (_duration <= 0 || ServerNetwork.I.Profiler.Active || !CanUserRequestProfile(senderSteamId))
                        return;

                    MyVisualScriptLogicProvider.SendChatMessageColored($"Network profiling requested by \"{requesterName}\" for {_duration:N}s - performance may drop.", Color.Red, "Detection Equipment");
                    ServerNetwork.I.Profiler.Activate(_duration, senderSteamId);
                    return;
                }

                MiscUtils.SafeChat("NetworkProfiler", "Received results from server.");
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
                        PacketSize = _results == null ? 0 : _results.Length * sizeof(char)
                    }
                );
            }
        }
    }
}
