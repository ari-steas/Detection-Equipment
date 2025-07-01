using Sandbox.Game;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace DetectionEquipment.Shared.Utils
{
    internal static class TrackingUtils
    {
        public static Vector3D GetSunDirection() => MyVisualScriptLogicProvider.GetSunDirection();

        /// <summary>
        /// Stores cardinal and ordinal directions for performance
        /// </summary>
        public static Dictionary<Vector3D, double> VisibilityCacheBase = new Dictionary<Vector3D, double>
        {
            [Vector3D.Forward] = 0,
            [Vector3D.Normalize(Vector3D.Forward + Vector3D.Right)] = 0,
            [Vector3D.Right] = 0,
            [Vector3D.Normalize(Vector3D.Right + Vector3D.Backward)] = 0,
            [Vector3D.Backward] = 0,
            [Vector3D.Normalize(Vector3D.Backward + Vector3D.Left)] = 0,
            [Vector3D.Left] = 0,
            [Vector3D.Normalize(Vector3D.Left + Vector3D.Forward)] = 0,

            [Vector3D.Normalize(Vector3D.Forward + Vector3D.Up)] = 0,
            [Vector3D.Up] = 0,
            [Vector3D.Normalize(Vector3D.Up + Vector3D.Backward)] = 0,
            [Vector3D.Normalize(Vector3D.Backward + Vector3D.Down)] = 0,
            [Vector3D.Down] = 0,
            [Vector3D.Normalize(Vector3D.Down + Vector3D.Forward)] = 0,

            [Vector3D.Normalize(Vector3D.Up + Vector3D.Left)] = 0,
            [Vector3D.Normalize(Vector3D.Up + Vector3D.Right)] = 0,
            [Vector3D.Normalize(Vector3D.Down + Vector3D.Left)] = 0,
            [Vector3D.Normalize(Vector3D.Down + Vector3D.Right)] = 0,
        };

        public static void MinComponents(ref Vector3D a, Vector3D b)
        {
            if (a.X > b.X)
                a.X = b.X;
            if (a.Y > b.Y)
                a.Y = b.Y;
            if (a.Z > b.Z)
                a.Z = b.Z;
        }

        public static void MaxComponents(ref Vector3D a, Vector3D b)
        {
            if (a.X < b.X)
                a.X = b.X;
            if (a.Y < b.Y)
                a.Y = b.Y;
            if (a.Z < b.Z)
                a.Z = b.Z;
        }

        public static Vector3D ClosestCorner(this BoundingBoxD boundingBoxD, Vector3D position)
        {
            //double closestDSq = double.MaxValue;
            //Vector3D closest = Vector3D.Zero;
            //for (int i = 0; i < BoundingBoxD.NUMBER_OF_CORNERS; ++i)
            //{
            //    Vector3D corner = boundingBoxD.GetCorner(i);
            //    double distSq = Vector3D.DistanceSquared(corner, position);
            //    if (distSq > closestDSq)
            //        continue;
            //    closestDSq = distSq;
            //    closest = corner;
            //}
            var delta = position - boundingBoxD.Center;

            return new Vector3D(
                delta.X > 0 ? boundingBoxD.Max.X : boundingBoxD.Min.X,
                delta.Y > 0 ? boundingBoxD.Max.Y : boundingBoxD.Min.Y,
                delta.Z > 0 ? boundingBoxD.Max.Z : boundingBoxD.Min.Z);
        }

        private static readonly HashSet<MyDataBroadcaster> Broadcasters = new HashSet<MyDataBroadcaster>();
        private static readonly Queue<MyDataReceiver> BroadcastToCheck = new Queue<MyDataReceiver>();
        public static List<MyDataBroadcaster> GetAllRelayedBroadcasters(MyDataReceiver receiver, long identityId, bool mutual)
        {
            BroadcastToCheck.Enqueue(receiver);
            while (BroadcastToCheck.Count > 0)
            {
                var next = BroadcastToCheck.Dequeue();

                foreach (MyDataBroadcaster current in next.BroadcastersInRange)
                {
                    if (current.Closed ||
                        mutual &&
                        (
                            current.Receiver == null || 
                            next.Broadcaster == null ||
                            !current.Receiver.BroadcastersInRange.Contains(next.Broadcaster))
                        )
                        continue;

                    if (current.Receiver != null && current.CanBeUsedByPlayer(identityId))
                        Broadcasters.Add(current);
                }
            }

            var list = Broadcasters.ToList();
            Broadcasters.Clear();
            return list;
        }

        public static List<MyDataBroadcaster> GetAllRelayedBroadcasters(IEnumerable<MyDataReceiver> receivers, long identityId, bool mutual)
        {
            foreach (var receiver in receivers)
                BroadcastToCheck.Enqueue(receiver);
            while (BroadcastToCheck.Count > 0)
            {
                var next = BroadcastToCheck.Dequeue();

                foreach (MyDataBroadcaster current in next.BroadcastersInRange)
                {
                    if (current.Closed ||
                        mutual &&
                        (
                            current.Receiver == null || 
                            next.Broadcaster == null ||
                            !current.Receiver.BroadcastersInRange.Contains(next.Broadcaster))
                        )
                        continue;

                    // Prevent neutrals from seeing all broadcasters
                    bool canAccess = (identityId != 0 || current.Owner == 0) && current.CanBeUsedByPlayer(identityId);

                    if (canAccess && Broadcasters.Add(current) && current.Receiver != null)
                        BroadcastToCheck.Enqueue(current.Receiver);
                }
            }

            var list = Broadcasters.ToList();
            Broadcasters.Clear();
            return list;
        }

        //public static DetectionInfo AverageDetection(params DetectionInfo[] args)
        //{
        //    double totalBearingError = args.Sum(info => info.BearingError);
        //    double totalRangeError = args.Sum(info => info.RangeError);
        //
        //    Vector3D averageBearing = Vector3D.Zero;
        //    double averageRange = 0;
        //    foreach (var info in args)
        //    {
        //        averageBearing += info.Bearing * (info.BearingError/totalBearingError);
        //        averageRange += info.Range * (info.RangeError/totalRangeError);
        //    }
        //
        //    DetectionInfo result = new DetectionInfo()
        //    {
        //        Sensor = args[0].Sensor,
        //        Track = args[0].Track,
        //        Bearing = averageBearing.Normalized(),
        //        BearingError = totalBearingError / args.Length,
        //        Range = averageRange,
        //        RangeError = totalRangeError / args.Length,
        //    };
        //
        //    return result;
        //}
        //
        //public static DetectionInfo AverageDetection(ICollection<DetectionInfo> args)
        //{
        //    if (args.Count == 0)
        //        throw new Exception("No detection infos provided!");
        //
        //    double totalBearingError = 0;
        //    double totalRangeError = 0;
        //
        //    foreach (var info in args)
        //    {
        //        totalBearingError += info.BearingError;
        //        totalRangeError += info.RangeError;
        //    }
        //
        //    Vector3D averageBearing = Vector3D.Zero;
        //    double averageRange = 0;
        //    double totalCrossSecton = 0;
        //    foreach (var info in args)
        //    {
        //        averageBearing += info.Bearing * (info.BearingError/totalBearingError);
        //        averageRange += info.Range * (info.RangeError/totalRangeError);
        //    }
        //
        //    DetectionInfo result = new DetectionInfo()
        //    {
        //        Sensor = args.First().Sensor,
        //        Track = args.First().Track,
        //        Bearing = averageBearing.Normalized(),
        //        BearingError = totalBearingError / args.Count,
        //        Range = averageRange,
        //        RangeError = totalRangeError / args.Count,
        //        CrossSection = totalCrossSecton / args.Count,
        //    };
        //
        //    return result;
        //}
    }
}
