using System;
using Sandbox.Game;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Server;
using DetectionEquipment.Server.Tracking;
using VRage.ModAPI;
using VRageMath;
using RichHudFramework;
using VRage.Game.Models;

namespace DetectionEquipment.Shared.Utils
{
    internal static class TrackingUtils
    {
        public static Vector3D GetSunDirection() => MyVisualScriptLogicProvider.GetSunDirection();
        private static object _voxelCastLockObj = new object();

        /// <summary>
        /// Stores local cross-section cast directions for performance.
        /// </summary>
        public static Vector3[] VisibilityDirectionCache;
        public static IcoSphereConstructor CurrentConstructor;

        private static int _prevDivs = -1;

        public static void Load()
        {
            UpdateVisibilityCache(GlobalData.CrossSectionDetail.Value);
        }

        public static void Unload()
        {
            _prevDivs = -1;
            VisibilityDirectionCache = null;
            CurrentConstructor = null;
        }

        public static void UpdateVisibilityCache(int numDivisions)
        {
            if (numDivisions == _prevDivs)
                return;
            _prevDivs = numDivisions;

            CurrentConstructor = new IcoSphereConstructor(numDivisions);
            VisibilityDirectionCache = CurrentConstructor.Sphere.GenerateVertexSet();
            Log.Info("TrackingUtils", $"Updated VisibilityDirectionCache for {numDivisions} divisions. New size: {VisibilityDirectionCache.Length}.");
            
            if (ServerMain.I == null)
                return;

            foreach (var track in ServerMain.I.Tracks)
            {
                var gT = track.Value as GridTrack;
                if (gT == null)
                    continue;

                gT.NeedsRegenerateCache = true;

                if (GlobalData.DebugLevel >= 3)
                {
                    var matrix = gT.Grid.WorldMatrix;
                    matrix.Translation = gT.Grid.WorldAABB.Center;
                    CurrentConstructor.Sphere.DrawDebug(gT.Grid.LocalAABB.HalfExtents.Length() * gT.Grid.GridSize, matrix, Color.Blue, 30);
                }
            }
        }

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

        public static Vector3D FurthestCorner(this BoundingBoxD boundingBoxD, Vector3D position)
        {
            var delta = position - boundingBoxD.Center;

            return new Vector3D(
                delta.X < 0 ? boundingBoxD.Max.X : boundingBoxD.Min.X,
                delta.Y < 0 ? boundingBoxD.Max.Y : boundingBoxD.Min.Y,
                delta.Z < 0 ? boundingBoxD.Max.Z : boundingBoxD.Min.Z);
        }

        public static List<MyDataBroadcaster> GetAllRelayedBroadcasters(MyDataReceiver receiver, long identityId, bool mutual)
        {
            var rcvToCheck = GlobalObjectPools.DataReceiverPool.Pop();
            var broadcasters = GlobalObjectPools.DataBroadcasterPool.Pop();
            
            rcvToCheck.Enqueue(receiver);
            while (rcvToCheck.Count > 0)
            {
                var next = rcvToCheck.Dequeue();

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
                        broadcasters.Add(current);
                }
            }

            var list = broadcasters.ToList();
            broadcasters.Clear();
            GlobalObjectPools.DataBroadcasterPool.Push(broadcasters);

            rcvToCheck.Clear();
            GlobalObjectPools.DataReceiverPool.Push(rcvToCheck);

            return list;
        }

        public static List<MyDataBroadcaster> GetAllRelayedBroadcasters(IEnumerable<MyDataReceiver> receivers, long identityId, bool mutual)
        {
            var rcvToCheck = GlobalObjectPools.DataReceiverPool.Pop();
            var broadcasters = GlobalObjectPools.DataBroadcasterPool.Pop();

            foreach (var receiver in receivers)
                rcvToCheck.Enqueue(receiver);
            while (rcvToCheck.Count > 0)
            {
                var next = rcvToCheck.Dequeue();

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

                    if (canAccess && broadcasters.Add(current) && current.Receiver != null)
                        rcvToCheck.Enqueue(current.Receiver);
                }
            }

            var list = broadcasters.ToList();
            broadcasters.Clear();
            GlobalObjectPools.DataBroadcasterPool.Push(broadcasters);

            rcvToCheck.Clear();
            GlobalObjectPools.DataReceiverPool.Push(rcvToCheck);

            return list;
        }

        public static bool HasLoS(Vector3D thisPos, IMyEntity thisEnt, IMyEntity toCheck)
        {
            // Look for at least two visible corners
            bool oneHit = false;

            foreach (var cornerLocal in toCheck.LocalAABB.Corners)
            {
                Vector3D corner = Vector3D.Transform(cornerLocal, toCheck.WorldMatrix);

                // MyAPIGateway.Physics.CastRay(thisPos, toCheck.PositionComp.WorldAABB.GetCorner(i), castList);
                // threadsafe but only goes by AABBs
                bool isValid = IsBlocked(thisEnt, toCheck, corner, thisPos);

                if (!isValid)
                    continue;

                if (oneHit)
                {
                    return true;
                }
                oneHit = true;
            }

            if (oneHit && !IsBlocked(thisEnt, toCheck, toCheck.WorldAABB.Center, thisPos))
            {
                return true;
            }

            return false;
        }

        

        public static bool HasLoSDir(Vector3D direction, IMyEntity thisEnt, IMyEntity toCheck)
        {
            // Look for at least two visible corners
            bool oneHit = false;
            
            double distToRaycast = toCheck.WorldAABB.HalfExtents.Length();
            foreach (var cornerLocal in toCheck.LocalAABB.Corners)
            {
                Vector3D corner = Vector3D.Transform(cornerLocal, toCheck.WorldMatrix);
                // MyAPIGateway.Physics.CastRay(thisPos, toCheck.PositionComp.WorldAABB.GetCorner(i), castList);
                // threadsafe but only goes by AABBs
                bool isValid = IsBlocked(thisEnt, toCheck, corner, corner - distToRaycast * direction);

                if (!isValid)
                    continue;

                if (oneHit)
                {
                    return true;
                }
                oneHit = true;
            }

            if (oneHit && !IsBlocked(thisEnt, toCheck, toCheck.WorldAABB.Center, toCheck.WorldAABB.Center - distToRaycast * direction))
            {
                return true;
            }

            return false;
        }
        private static bool IsBlocked(IMyEntity thisEnt, IMyEntity toCheck, Vector3D from, Vector3D to)
        {
            var entityList = GlobalObjectPools.EntityLineOverlapPool.Pop();
            bool isValid = true;

            try
            {
                LineD raycast = new LineD(from, to);

                MyGamePruningStructure.GetTopmostEntitiesOverlappingRay(ref raycast, entityList);

                if (GlobalData.DebugLevel > 1)
                    DebugDraw.AddLine(raycast, Color.Cyan.SetAlphaPct(0.05f), 0);

                foreach (var segementOverlapResult in entityList)
                {
                    if (segementOverlapResult.Element == thisEnt || segementOverlapResult.Element == toCheck)
                        continue;

                    if (GlobalData.DebugLevel > 1)
                        DebugDraw.AddPoint(from + raycast.Direction * segementOverlapResult.Distance,
                            Color.Green.SetAlphaPct(1f), 0);

                    // raycast extents can be optimized further

                    Vector3D? result;
                    if (segementOverlapResult.Element is MyCubeGrid)
                    {
                        MyCubeGrid.MyCubeGridHitInfo intersect = new MyCubeGrid.MyCubeGridHitInfo();
                        if (((MyCubeGrid)segementOverlapResult.Element).GetIntersectionWithLine(ref raycast,
                                ref intersect))
                        {
                            if (GlobalData.DebugLevel > 1)
                                DebugDraw.AddPoint(intersect.Triangle.IntersectionPointInWorldSpace,
                                    Color.Red.SetAlphaPct(1f), 0);
                            isValid = false;
                            break;
                        }
                    }
                    else if (segementOverlapResult.Element is MyVoxelBase)
                    {
                        //if (segementOverlapResult.Element is MyPlanet)
                        //{
                        //    ((MyPlanet)segementOverlapResult.Element).PrefetchShapeOnRay(ref raycast);
                        //}

                        MyIntersectionResultLineTriangleEx? tri;
                        lock (_voxelCastLockObj) // vain attempt to limit concurrency exceptions caused by MyVoxelGeometry.GetCellLineIntersectionOctree()
                        {
                            isValid &= !((MyVoxelBase)segementOverlapResult.Element).GetIntersectionWithLine(ref raycast, out tri);
                        }

                        if (!isValid)
                        {
                            if (GlobalData.DebugLevel > 1)
                                DebugDraw.AddPoint(tri.Value.IntersectionPointInWorldSpace, Color.MediumVioletRed.SetAlphaPct(1f), 0);
                            break;
                        }
                    }
                    else if (segementOverlapResult.Element.GetIntersectionWithLine(ref raycast, out result))
                    {
                        if (GlobalData.DebugLevel > 1)
                            DebugDraw.AddPoint(result.Value, Color.Red.SetAlphaPct(1f), 0);
                        isValid = false;
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Exception("TrackingUtils", ex);
            }
            finally
            {
                entityList.Clear();
                GlobalObjectPools.EntityLineOverlapPool.Push(entityList);
            }

            return isValid;
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
