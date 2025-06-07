using DetectionEquipment.Shared.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using VRage.Game.Entity;
using VRageMath;

namespace DetectionEquipment.Shared.Structs
{
    internal struct WorldDetectionInfo : IComparable<WorldDetectionInfo>, IPackageable
    {
        public long EntityId;
        public double CrossSection;
        public double Error;
        public Vector3D Position;
        public Vector3D? Velocity;
        public double? VelocityVariance;
        public SensorDefinition.SensorType DetectionType;
        public string[] IffCodes;
        public MyRelationsBetweenPlayers? Relations;
        public MyEntity Entity;

        public static WorldDetectionInfo Create(DetectionInfo info, AggregatorBlock aggregator = null)
        {
            var wInfo = new WorldDetectionInfo
            {
                EntityId = info.Track.EntityId,
                Entity = (info.Track as EntityTrack)?.Entity,
                CrossSection = info.CrossSection,
                Position = info.Sensor.Position + info.Bearing * info.Range,
                DetectionType = info.Sensor.Definition.Type,
                Velocity = null,
                VelocityVariance = null,
                IffCodes = info.IffCodes ?? Array.Empty<string>(),
            };

            wInfo.Error = Math.Tan(info.BearingError) * info.Range; // planar error; base width of right triangle
            wInfo.Error *= wInfo.Error;
            wInfo.Error += info.RangeError * info.RangeError; // normal error
            wInfo.Error = Math.Sqrt(wInfo.Error);
            wInfo.Relations = aggregator?.GetInfoRelations(wInfo);

            return wInfo;
        }

        public static WorldDetectionInfo Create(WorldDetectionInfo info)
        {
            return new WorldDetectionInfo
            {
                EntityId = info.EntityId,
                CrossSection = info.CrossSection,
                Error = info.Error,
                Position = info.Position,
                Velocity = info.Velocity,
                VelocityVariance = info.VelocityVariance,
                DetectionType = info.DetectionType,
                IffCodes = info.IffCodes,
                Relations = info.Relations,
                Entity = info.Entity,
            };
        }

        public override bool Equals(object obj) => obj is WorldDetectionInfo && Position.Equals(((WorldDetectionInfo)obj).Position);
        public override int GetHashCode() => EntityId.GetHashCode();

        public override string ToString()
        {
            return $"UID: {EntityId}\nPosition: {Position.ToString("N0")} +-{Error:N1}m\nIFF: {(IffCodes.Length == 0 ? "N/A" : string.Join(" | ", IffCodes))}";
        }

        public static WorldDetectionInfo Average(AggregatorBlock aggregator, params WorldDetectionInfo[] args) => Average(args, aggregator);

        public static WorldDetectionInfo Average(ICollection<WorldDetectionInfo> args, AggregatorBlock aggregator)
        {
            if (args.Count == 0)
                throw new Exception("No detection infos provided!");

            if (args.Count == 1)
                return args.First();

            SensorDefinition.SensorType? proposedType = null;
            MyEntity entity = null;
            double totalError = 0;
            double minError = double.MaxValue;
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
            foreach (var info in args) // TODO weighted average error
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

            var wInfo = new WorldDetectionInfo
            {
                Entity = entity,
                EntityId = entity?.EntityId ?? -1,
                CrossSection = totalCrossSection / args.Count,
                Position = averagePos,
                Error = minError,
                DetectionType = proposedType ?? SensorDefinition.SensorType.None,
                IffCodes = allCodes.ToArray(),
            };
            wInfo.Relations = aggregator?.GetInfoRelations(wInfo);

            return wInfo;
        }

        public int CompareTo(WorldDetectionInfo other)
        {
            return other.CrossSection.CompareTo(this.CrossSection);
        }

        public int FieldCount => 9;
        public void Package(object[] fieldArray)
        {
            fieldArray[0] = EntityId;
            fieldArray[1] = DetectionType;
            fieldArray[2] = CrossSection;
            fieldArray[3] = Error;
            fieldArray[4] = Position;
            fieldArray[5] = Velocity;
            fieldArray[6] = VelocityVariance;
            fieldArray[7] = IffCodes;
            fieldArray[8] = (int?) Relations;
        }
    }
}
