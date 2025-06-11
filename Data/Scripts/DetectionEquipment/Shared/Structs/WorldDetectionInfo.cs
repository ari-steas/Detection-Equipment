using DetectionEquipment.Shared.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;

namespace DetectionEquipment.Shared.Structs
{
    internal struct WorldDetectionInfo : IComparable<WorldDetectionInfo>, IPackageable
    {
        public long EntityId;
        public double CrossSection;
        public double MaxRangeError;
        public double MaxBearingError;
        public Vector3D PositionOffset;
        public Vector3D? Velocity;
        public double? VelocityVariance;
        public SensorDefinition.SensorType DetectionType;
        public string[] IffCodes;
        public MyRelationsBetweenPlayers? Relations;
        public MyEntity Entity;

        public Vector3D Position => PositionOffset + Entity.PositionComp.WorldAABB.Center;
        public double SumError => MaxRangeError + MaxBearingError;

        public static WorldDetectionInfo Create(DetectionInfo info, AggregatorBlock aggregator = null)
        {
            var wInfo = new WorldDetectionInfo
            {
                EntityId = info.Track.EntityId,
                Entity = (info.Track as EntityTrack)?.Entity,
                CrossSection = info.CrossSection,
                MaxRangeError = info.MaxRangeError,
                MaxBearingError = info.MaxBearingError,
                PositionOffset = info.PositionOffset,
                DetectionType = info.Sensor.Definition.Type,
                Velocity = null,
                VelocityVariance = null,
                IffCodes = info.IffCodes ?? Array.Empty<string>(),
            };

            //wInfo.Error = Math.Tan(info.MaxBearingError) * info.Range; // planar error; base width of right triangle
            //wInfo.Error *= wInfo.Error;
            //wInfo.Error += info.MaxRangeError * info.MaxRangeError; // normal error
            //wInfo.Error = Math.Sqrt(wInfo.Error);
            wInfo.Relations = aggregator?.GetInfoRelations(wInfo);

            return wInfo;
        }

        public static WorldDetectionInfo Create(WorldDetectionInfo info)
        {
            return new WorldDetectionInfo
            {
                EntityId = info.EntityId,
                CrossSection = info.CrossSection,
                MaxRangeError = info.MaxRangeError,
                MaxBearingError = info.MaxBearingError,
                PositionOffset = info.PositionOffset,
                Velocity = info.Velocity,
                VelocityVariance = info.VelocityVariance,
                DetectionType = info.DetectionType,
                IffCodes = info.IffCodes,
                Relations = info.Relations,
                Entity = info.Entity,
            };
        }

        public override bool Equals(object obj)
        {
            if (!(obj is WorldDetectionInfo))
                return false;
            var info = (WorldDetectionInfo) obj;

            return info.EntityId == EntityId && PositionOffset.Equals(info.PositionOffset);
        }
        public override int GetHashCode() => EntityId.GetHashCode();

        public static WorldDetectionInfo Average(AggregatorBlock aggregator, params WorldDetectionInfo[] args) => Average(args, aggregator);

        public static WorldDetectionInfo Average(ICollection<WorldDetectionInfo> args, AggregatorBlock aggregator)
        {
            if (args.Count == 0)
                throw new Exception("No detection infos provided!");

            if (args.Count == 1)
                return args.First();

            SensorDefinition.SensorType? proposedType = null;
            MyEntity entity = null;
            var allCodes = new List<string>();
            foreach (var info in args)
            {
                if (info.Entity != null)
                    entity = info.Entity;
                foreach (var code in info.IffCodes)
                    if (!allCodes.Contains(code))
                        allCodes.Add(code);
                if (proposedType == null)
                    proposedType = info.DetectionType;
                else if (proposedType != info.DetectionType)
                    proposedType = SensorDefinition.SensorType.None;
            }

            double totalRangeError, totalBearingError, averageCrossSection;
            var averageRelPos = AveragePositions(args, aggregator, entity, out totalRangeError, out totalBearingError, out averageCrossSection);

            var wInfo = new WorldDetectionInfo
            {
                Entity = entity,
                EntityId = entity?.EntityId ?? -1,
                CrossSection = averageCrossSection,
                PositionOffset = averageRelPos,
                MaxRangeError = totalRangeError / args.Count,
                MaxBearingError = totalBearingError / args.Count,
                DetectionType = proposedType ?? SensorDefinition.SensorType.None,
                IffCodes = allCodes.ToArray(),
            };
            wInfo.Relations = aggregator?.GetInfoRelations(wInfo);

            return wInfo;
        }

        /// <summary>
        /// RETURNS RELATIVE OFFSET!
        /// </summary>
        /// <param name="args"></param>
        /// <param name="aggregator"></param>
        /// <param name="totalError"></param>
        /// <returns></returns>
        private static Vector3D AveragePositions(ICollection<WorldDetectionInfo> args, AggregatorBlock aggregator, MyEntity entity,
            out double totalRangeError, out double totalBearingError, out double averageCrossSection)
        {
            totalRangeError = 0;
            totalBearingError = 0;
            averageCrossSection = 0;

            foreach (var info in args)
            {
                totalRangeError += info.MaxRangeError;
                totalBearingError += info.MaxBearingError;
            }

            Vector3D averageBearing = Vector3D.Zero;
            double averageRange = 0;
            double bearingErrorPctSum = 0, rangeErrorPctSum = 0;
            Vector3D aggregatorPos = aggregator.Block.GetPosition();
            foreach (var info in args)
            {
                var relativeBearing = info.Position - aggregatorPos;
                var relativeRange = relativeBearing.Normalize();

                averageBearing += totalBearingError == 0
                    ? relativeBearing
                    : relativeBearing * (1 - (info.MaxBearingError / totalBearingError));
                bearingErrorPctSum += (1 - (info.MaxBearingError / totalBearingError));

                averageRange += totalRangeError == 0
                    ? relativeRange
                    : relativeRange * (1 - (info.MaxBearingError / totalRangeError));
                rangeErrorPctSum += (1 - (info.MaxRangeError / totalRangeError));

                averageCrossSection += info.CrossSection;
            }

            averageBearing /= totalBearingError == 0
                ? args.Count
                : bearingErrorPctSum;
            averageRange /= totalRangeError == 0
                ? args.Count
                : rangeErrorPctSum;
            averageCrossSection /= args.Count;

            return averageBearing * averageRange + aggregatorPos - entity.PositionComp.WorldAABB.Center;
        }

        public int CompareTo(WorldDetectionInfo other)
        {
            return other.CrossSection.CompareTo(this.CrossSection);
        }

        public int FieldCount => 10;
        public void Package(object[] fieldArray)
        {
            fieldArray[0] = EntityId;
            fieldArray[1] = DetectionType;
            fieldArray[2] = CrossSection;
            fieldArray[3] = MaxBearingError;
            fieldArray[4] = Position;
            fieldArray[5] = Velocity;
            fieldArray[6] = VelocityVariance;
            fieldArray[7] = IffCodes;
            fieldArray[8] = (int?) Relations;
            fieldArray[9] = MaxRangeError;
        }
    }
}
