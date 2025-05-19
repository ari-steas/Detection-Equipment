using DetectionEquipment.Shared.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Server.Tracking;
using ProtoBuf;
using VRage;
using VRage.Game.Entity;
using VRageMath;

using WorldDetTuple = VRage.MyTuple<int, double, double, VRageMath.Vector3D, VRage.MyTuple<VRageMath.Vector3D, double>?, string[]>;

namespace DetectionEquipment.Shared.Structs
{
    [ProtoContract]
    internal struct WorldDetectionInfo : IComparable<WorldDetectionInfo>
    {
        [ProtoMember(1)] public long EntityId;
        [ProtoMember(2)] public double CrossSection;
        [ProtoMember(3)] public double Error;
        [ProtoMember(4)] public Vector3D Position;
        [ProtoMember(5)] public Vector3D? Velocity;
        [ProtoMember(6)] public double? VelocityVariance;
        [ProtoMember(7)] public SensorDefinition.SensorType DetectionType;
        [ProtoMember(8)] public string[] IffCodes;
        [ProtoMember(9)] public MyRelationsBetweenPlayers? Relations;
        public MyEntity Entity;

        public static WorldDetectionInfo Create(DetectionInfo info)
        {
            var wInfo = new WorldDetectionInfo();
            wInfo.EntityId = info.Track.EntityId;
            var eTrack = info.Track as EntityTrack;
            wInfo.Entity = eTrack?.Entity;

            wInfo.CrossSection = info.CrossSection;
            wInfo.Position = info.Sensor.Position + info.Bearing * info.Range;

            wInfo.Error = Math.Tan(info.BearingError) * info.Range; // planar error; base width of right triangle
            wInfo.Error *= wInfo.Error;
            wInfo.Error += info.RangeError * info.RangeError; // normal error
            wInfo.Error = Math.Sqrt(wInfo.Error);

            wInfo.DetectionType = info.Sensor.Definition.Type;

            wInfo.Velocity = null;
            wInfo.VelocityVariance = null;
            wInfo.IffCodes = info.IffCodes ?? Array.Empty<string>();
            wInfo.Relations = null;
            return wInfo;
        }

        public static WorldDetectionInfo Create(WorldDetectionInfo info)
        {
            return new WorldDetectionInfo
            {
                Entity = info.Entity,
                EntityId = info.EntityId,
                CrossSection = info.CrossSection,
                Error = info.Error,
                Position = info.Position,
                Velocity = info.Velocity,
                VelocityVariance = info.VelocityVariance,
                DetectionType = info.DetectionType,
                IffCodes = info.IffCodes,
            };
        }

        public override bool Equals(object obj) => obj is WorldDetectionInfo && Position.Equals(((WorldDetectionInfo)obj).Position);
        public override int GetHashCode() => Position.GetHashCode();

        public override string ToString()
        {
            return $"Position: {Position.ToString("N0")} +-{Error:N1}m\nIFF: {(IffCodes.Length == 0 ? "N/A" : string.Join(" | ", IffCodes))}";
        }

        //public override bool Equals(object obj)
        //{
        //    if (obj.GetType() != typeof(WorldDetectionInfo)) return false;
        //    var i = (WorldDetectionInfo)obj;
        //
        //    return CrossSection == i.CrossSection && Error == i.Error && Position == i.Position;
        //}

        public WorldDetTuple Tuple => new WorldDetTuple(
            (int)DetectionType,
            CrossSection,
            Error,
            Position,
            Velocity == null ? null : new MyTuple<Vector3D, double>?(new MyTuple<Vector3D, double>(Velocity.Value, VelocityVariance ?? 0)),
            IffCodes
            );

        public static WorldDetectionInfo Average(params WorldDetectionInfo[] args) => Average((ICollection<WorldDetectionInfo>) args);

        public static WorldDetectionInfo Average(ICollection<WorldDetectionInfo> args)
        {
            if (args.Count == 0)
                throw new Exception("No detection infos provided!");

            if (args.Count == 1)
                return args.First();

            SensorDefinition.SensorType? proposedType = null;
            MyEntity entity = null;
            double totalError = 0;
            double minError = Double.MaxValue;
            var allCodes = new List<string>();
            foreach (var info in args)
            {
                if (info.Entity != null)
                    entity = info.Entity;
                totalError += info.Error;
                if (info.Error < minError) minError = info.Error;
                foreach (var code in info.IffCodes)
                    if (!allCodes.Contains(code))
                        allCodes.Add(code);
                if (proposedType == null)
                    proposedType = info.DetectionType;
                else if (proposedType != info.DetectionType)
                    proposedType = SensorDefinition.SensorType.None;
            }

            Vector3D averagePos = Vector3D.Zero;
            double totalCrossSection = 0;
            double pctSum = 0;
            foreach (var info in args)
            {
                pctSum += 1 - (info.Error / totalError);
                if (totalError > 0)
                    averagePos += info.Position * (1 - (info.Error / totalError));
                else
                    averagePos += info.Position;
                totalCrossSection += info.CrossSection;
            }

            if (totalError > 0)
                averagePos /= pctSum;
            else
                averagePos /= args.Count;

            //double avgDiff = 0;
            //foreach (var info in args)
            //    avgDiff += Vector3D.DistanceSquared(info.Position, averagePos);
            //avgDiff = Math.Sqrt(avgDiff) / args.Count;

            return new WorldDetectionInfo
            {
                EntityId = entity?.EntityId ?? -1,
                Entity = entity,
                CrossSection = totalCrossSection / args.Count,
                Position = averagePos,
                Error = minError,
                DetectionType = proposedType ?? SensorDefinition.SensorType.None,
                IffCodes = allCodes.ToArray(),
            };
        }

        //public static WorldDetectionInfo AverageWeighted(ICollection<KeyValuePair<WorldDetectionInfo, int>> args)
        //{
        //    if (args.Count == 0)
        //        throw new Exception("No detection infos provided!");
        //
        //    double totalError = 0;
        //    double totalWeight = 0;
        //    double highestError = 0;
        //
        //    HashSet<string> allCodes = new HashSet<string>();
        //    foreach (var info in args)
        //    {
        //        totalError += info.Key.Error;
        //        totalWeight += info.Value;
        //        if (info.Key.Error > highestError)
        //            highestError = info.Key.Error;
        //        foreach (var code in info.Key.IffCodes)
        //            allCodes.Add(code);
        //    }
        //
        //    Vector3D averagePos = Vector3D.Zero;
        //    double avgCrossSection = 0;
        //    foreach (var info in args)
        //    {
        //        double infoWeightPct = info.Value / totalWeight;
        //
        //        averagePos += info.Key.Position * infoWeightPct;
        //
        //        avgCrossSection += info.Key.CrossSection * infoWeightPct;
        //    }
        //
        //    double avgDiff = 0;
        //    foreach (var info in args)
        //        avgDiff += Vector3D.Distance(info.Key.Position, averagePos);
        //    avgDiff /= args.Count;
        //
        //    WorldDetectionInfo result = new WorldDetectionInfo()
        //    {
        //        CrossSection = avgCrossSection,
        //        Position = averagePos,
        //        Error = avgDiff,
        //        DetectionType = 0,
        //        IffCodes = allCodes.ToArray()
        //    };
        //
        //    return result;
        //}

        public int CompareTo(WorldDetectionInfo other)
        {
            return other.CrossSection.CompareTo(this.CrossSection);
        }
    }
}
