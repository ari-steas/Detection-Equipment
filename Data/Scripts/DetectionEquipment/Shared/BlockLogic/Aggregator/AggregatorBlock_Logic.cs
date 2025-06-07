using DetectionEquipment.Shared.Structs;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Shared.Utils;
using VRageMath;

namespace DetectionEquipment.Shared.BlockLogic.Aggregator
{
    internal partial class AggregatorBlock
    {
        private void CalculateDetections(ICollection<WorldDetectionInfo[]> cache) // TODO: Improve performance of this method
        {
            //Dictionary<WorldDetectionInfo, int> weightedInfos = new Dictionary<WorldDetectionInfo, int>();
            var aggregatedDetections = new HashSet<WorldDetectionInfo>();

            if (cache.Count == 0)
            {
                lock (_bufferDetections)
                    _bufferDetections.Clear();
                return;
            }

            //int weight = 1;
            //foreach (var set in cache)
            //{
            //    weight++;
            //    foreach (var detection in set)
            //    {
            //        weightedInfos[detection] = weightedInfos.ContainsKey(detection) ? weightedInfos[detection] + weight : weight;
            //    }
            //}

            var latestSet = cache.First();
            List<WorldDetectionInfo> toCombine = new List<WorldDetectionInfo>();
            foreach (var info in latestSet)
            {
                toCombine.Add(info);
                foreach (var set in cache)
                {
                    if (set == latestSet)
                        continue;

                    foreach (var member in set)
                    {
                        //bool typesMatch = AggregateTypes || member.DetectionType == info.DetectionType;
                        //bool crossSectionsMatch = member.DetectionType != info.DetectionType || Math.Abs(member.CrossSection - info.CrossSection) <= Math.Max(member.CrossSection, info.CrossSection) * RcsThreshold;
                        //double maxPositionDiff = Math.Max(member.Error, info.Error) * DistanceThreshold + MaxVelocity;
                        //bool positionsMatch = Vector3D.DistanceSquared(member.Position, info.Position) <= maxPositionDiff * maxPositionDiff;
                        //
                        //// Cross-section doesn't have to match if the sensors are different types.
                        //if (!typesMatch || !crossSectionsMatch || !positionsMatch)
                        //    continue;
                        if (member.EntityId != info.EntityId)
                            continue;

                        toCombine.Add(member);
                        break;
                    }
                }

                Vector3D averageVelocity = Vector3D.Zero;
                double velVariation = 0;

                if (toCombine.Count == 1)
                {
                    aggregatedDetections.Add(info);
                    continue;
                }

                if (toCombine.Count > 1)
                {
                    var velocities = new Vector3D[toCombine.Count - 1];
                    for (int i = 0; i < velocities.Length; i++)
                    {
                        if (GlobalData.Debug)
                            DebugDraw.AddLine(toCombine[i].Position, toCombine[i+1].Position, Color.White * ((float)i/velocities.Length), 0); // Position delta indicator
                        velocities[i] = (toCombine[i + 1].Position - toCombine[i].Position) * 60;
                        averageVelocity += velocities[i];
                    }
                    averageVelocity /= velocities.Length;
                    double averageSpeed = averageVelocity.Length();

                    foreach (var velocity in velocities)
                    {
                        var len = velocity.Length();
                        velVariation += (len - averageSpeed) * (len - averageSpeed);
                    }

                    velVariation /= velocities.Length;
                }

                var averagedInfo = WorldDetectionInfo.Average(toCombine, this);
                if (velVariation <= VelocityErrorThreshold * VelocityErrorThreshold)
                    averagedInfo.Position += averageVelocity * AggregationTime / 2;

                averagedInfo.Velocity = averageVelocity;
                averagedInfo.VelocityVariance = velVariation;

                //MyAPIGateway.Utilities.ShowNotification($"Vel: {averageVelocity.Length():N1} m/s (R={Math.Sqrt(velVariation)})", 1000/60);
                if (GlobalData.Debug)
                    DebugDraw.AddLine(averagedInfo.Position, averagedInfo.Position + averageVelocity, Color.Blue, 0);

                aggregatedDetections.Add(averagedInfo);
                toCombine.Clear();
            }

            lock (_bufferDetections)
                _bufferDetections = aggregatedDetections;
        }

        private WorldDetectionInfo[] AggregateInfos(ICollection<WorldDetectionInfo> infos)
        {
            var groupCache = ControlBlockManager.I.GroupsCacheBuffer.Pull();

            // Group infos together
            foreach (var info in infos)
            {
                if (!groupCache.ContainsKey(info.EntityId))
                    groupCache[info.EntityId] = ControlBlockManager.I.GroupInfoBuffer.Pull();
                groupCache[info.EntityId].Add(info);
            }

            var aggregated = new WorldDetectionInfo[groupCache.Count];
            int i = 0;
            foreach (var set in groupCache.Values)
            {
                aggregated[i++] = WorldDetectionInfo.Average(set, this);
                ControlBlockManager.I.GroupInfoBuffer.Push(set);
            }

            ControlBlockManager.I.GroupsCacheBuffer.Push(groupCache);
        
            return aggregated;
        }
    }
}
