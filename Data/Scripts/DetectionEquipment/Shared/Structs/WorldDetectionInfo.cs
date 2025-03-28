﻿using DetectionEquipment.Shared.Definitions;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage;
using VRageMath;

using WorldDetTuple = VRage.MyTuple<int, double, double, VRageMath.Vector3D, VRage.MyTuple<VRageMath.Vector3D, double>?, string[]>;

namespace DetectionEquipment.Shared.Structs
{
    internal struct WorldDetectionInfo
    {
        public WorldDetectionInfo(DetectionInfo info)
        {
            CrossSection = info.CrossSection;
            Position = info.Sensor.Position + info.Bearing * info.Range;

            Error = Math.Tan(info.BearingError) * info.Range; // planar error; base width of right triangle
            Error *= Error;
            Error += info.RangeError * info.RangeError; // normal error
            Error = Math.Sqrt(Error);

            DetectionType = info.Sensor?.Definition.Type ?? 0;

            Velocity = null;
            VelocityVariance = null;
            IffCodes = info.IffCodes ?? Array.Empty<string>();
        }

        public double CrossSection, Error;
        public Vector3D Position;
        public Vector3D? Velocity;
        public double? VelocityVariance;
        public SensorDefinition.SensorType DetectionType;
        public string[] IffCodes;

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
            Velocity == null ? null : new MyTuple<Vector3D, double>?(new MyTuple<Vector3D, double>(Velocity.Value, VelocityVariance.Value)),
            IffCodes
            );

        public static WorldDetectionInfo Average(ICollection<WorldDetectionInfo> args)
        {
            if (args.Count == 0)
                throw new Exception("No detection infos provided!");

            double totalError = 0;
            HashSet<string> allCodes = new HashSet<string>();
            foreach (var info in args)
            {
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

            WorldDetectionInfo result = new WorldDetectionInfo()
            {
                CrossSection = totalCrossSection / args.Count,
                Position = averagePos,
                Error = avgDiff,
                DetectionType = 0,
                IffCodes = allCodes.ToArray()
            };

            return result;
        }

        public static WorldDetectionInfo AverageWeighted(ICollection<KeyValuePair<WorldDetectionInfo, int>> args)
        {
            if (args.Count == 0)
                throw new Exception("No detection infos provided!");

            double totalError = 0;
            double totalWeight = 0;
            double highestError = 0;

            HashSet<string> allCodes = new HashSet<string>();
            foreach (var info in args)
            {
                totalError += info.Key.Error;
                totalWeight += info.Value;
                if (info.Key.Error > highestError)
                    highestError = info.Key.Error;
                foreach (var code in info.Key.IffCodes)
                    allCodes.Add(code);
            }

            Vector3D averagePos = Vector3D.Zero;
            double avgCrossSection = 0;
            foreach (var info in args)
            {
                double infoWeightPct = info.Value / totalWeight;

                averagePos += info.Key.Position * infoWeightPct;

                avgCrossSection += info.Key.CrossSection * infoWeightPct;
            }

            double avgDiff = 0;
            foreach (var info in args)
                avgDiff += Vector3D.Distance(info.Key.Position, averagePos);
            avgDiff /= args.Count;

            WorldDetectionInfo result = new WorldDetectionInfo()
            {
                CrossSection = avgCrossSection,
                Position = averagePos,
                Error = avgDiff,
                DetectionType = 0,
                IffCodes = allCodes.ToArray()
            };

            return result;
        }
    }
}
