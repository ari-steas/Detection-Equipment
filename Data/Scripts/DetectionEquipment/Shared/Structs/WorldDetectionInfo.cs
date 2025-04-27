using DetectionEquipment.Shared.Definitions;
using System;
using System.Collections.Generic;
using VRage;
using VRageMath;

using WorldDetTuple = VRage.MyTuple<int, double, double, VRageMath.Vector3D, VRage.MyTuple<VRageMath.Vector3D, double>?, string[]>;

namespace DetectionEquipment.Shared.Structs
{
    internal struct WorldDetectionInfo : IComparable<WorldDetectionInfo>
    {
        public long EntityId;
        public double CrossSection, Error;
        public Vector3D Position;
        public Vector3D? Velocity;
        public double? VelocityVariance;
        public SensorDefinition.SensorType DetectionType;
        public string[] IffCodes;

        public WorldDetectionInfo(DetectionInfo info)
        {
            EntityId = info.Track.EntityId;
            CrossSection = info.CrossSection;
            Position = info.Sensor.Position + info.Bearing * info.Range;

            Error = Math.Tan(info.BearingError) * info.Range; // planar error; base width of right triangle
            Error *= Error;
            Error += info.RangeError * info.RangeError; // normal error
            Error = Math.Sqrt(Error);

            DetectionType = info.Sensor.Definition.Type;

            Velocity = null;
            VelocityVariance = null;
            IffCodes = info.IffCodes ?? Array.Empty<string>();
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

            long entityId = -1;
            double totalError = 0;
            var allCodes = new List<string>();
            foreach (var info in args)
            {
                entityId = info.EntityId;
                totalError += info.Error;
                foreach (var code in info.IffCodes)
                    allCodes.Add(code);
            }

            Vector3D averagePos = Vector3D.Zero;
            double totalCrossSection = 0;
            foreach (var info in args)
            {
                if (totalError > 0)
                    averagePos += info.Position * (info.Error / totalError);
                else
                    averagePos += info.Position;
                totalCrossSection += info.CrossSection;
            }

            if (totalError <= 0)
                averagePos /= args.Count;

            double avgDiff = 0;
            foreach (var info in args)
                avgDiff += Vector3D.DistanceSquared(info.Position, averagePos);
            avgDiff = Math.Sqrt(avgDiff) / args.Count;

            return new WorldDetectionInfo
            {
                EntityId = entityId,
                CrossSection = totalCrossSection / args.Count,
                Position = averagePos,
                Error = avgDiff,
                DetectionType = 0,
                IffCodes = allCodes.ToArray()
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
