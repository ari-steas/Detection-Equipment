using DetectionEquipment.Shared.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
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
                foreach (var set in cache)
                {
                    if (set == latestSet)
                        continue;

                    foreach (var member in set)
                    {
                        bool typesMatch = AggregateTypes || member.DetectionType == info.DetectionType;
                        bool crossSectionsMatch = member.DetectionType != info.DetectionType || Math.Abs(member.CrossSection - info.CrossSection) <= Math.Max(member.CrossSection, info.CrossSection) * RCSThreshold;
                        double maxPositionDiff = Math.Max(member.Error, info.Error) * DistanceThreshold + MaxVelocity;
                        bool positionsMatch = Vector3D.DistanceSquared(member.Position, info.Position) <= maxPositionDiff * maxPositionDiff;

                        // Cross-section doesn't have to match if the sensors are different types.
                        if (!typesMatch || !crossSectionsMatch || !positionsMatch)
                            continue;

                        toCombine.Add(member);
                        break;
                    }
                }

                Vector3D averageVelocity = Vector3D.Zero;
                double velVariation = 0;

                if (toCombine.Count == 0)
                {
                    aggregatedDetections.Add(info);
                    continue;
                }

                if (toCombine.Count > 1)
                {
                    var velocities = new Vector3D[toCombine.Count - 1];
                    for (int i = 0; i < velocities.Length; i++)
                    {
                        //DebugDraw.AddLine(toCombine[i].Position, toCombine[i+1].Position, Color.White * ((float)i/velocities.Length), 0); // Position delta indicator
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

                var averagedInfo = WorldDetectionInfo.Average(toCombine);
                if (velVariation <= VelocityErrorThreshold * VelocityErrorThreshold)
                    averagedInfo.Position += averageVelocity * AggregationTime / 2;

                averagedInfo.Velocity = averageVelocity;
                averagedInfo.VelocityVariance = velVariation;

                //MyAPIGateway.Utilities.ShowNotification($"Vel: {averageVelocity.Length():N1} m/s (R={Math.Sqrt(velVariation)})", 1000/60);
                //DebugDraw.AddLine(averagedInfo.Position, averagedInfo.Position + averageVelocity, Color.Blue, 0);

                aggregatedDetections.Add(averagedInfo);
                toCombine.Clear();
            }

            _bufferDetections = aggregatedDetections;
        }

        private WorldDetectionInfo[] AggregateInfos(ICollection<WorldDetectionInfo> infos)
        {
            GroupInfos(infos);
            var aggregated = new WorldDetectionInfo[_groupsCache.Count];
            for (int i = 0; i < aggregated.Length; i++)
            {
                aggregated[i] = WorldDetectionInfo.Average(_groupsCache[i]);
                _groupsCache[i].Clear();
                GroupInfoBuffer.Push(_groupsCache[i]);
            }

            _groupsCache.Clear();
        
            return aggregated;
        }

        private List<HashSet<WorldDetectionInfo>> _groupsCache = new List<HashSet<WorldDetectionInfo>>();
        private static readonly Stack<HashSet<WorldDetectionInfo>> GroupInfoBuffer = new Stack<HashSet<WorldDetectionInfo>>();

        /// <summary>
        /// Groups detection info from a single moment in time.
        /// </summary>
        /// <param name="infos"></param>
        private void GroupInfos(ICollection<WorldDetectionInfo> infos)
        {
            // This is an *INCREDIBLY* hot loop, so we need to squeeze as much performance out as we possibly can.

            foreach (var info in infos)
            {
                // Check if any existing groups match RCS and position
                bool didMatch = false;
                foreach (var group in _groupsCache)
                {
                    foreach (var member in group)
                    {
                        bool typesMatch = AggregateTypes.Value || member.DetectionType == info.DetectionType;
                        // Cross-section doesn't have to match if the sensors are different types.
                        bool crossSectionsMatch = member.DetectionType != info.DetectionType || Math.Abs(member.CrossSection - info.CrossSection) <= Math.Max(member.CrossSection, info.CrossSection) * RCSThreshold.Value;
                        // If types or cross-sections don't match, it's likely that this group won't match at all.
                        if (!typesMatch || !crossSectionsMatch)
                            break;
                        
                        double maxPositionDiff = Math.Max(member.Error, info.Error) * DistanceThreshold.Value;
                        bool positionsMatch = Vector3D.DistanceSquared(member.Position, info.Position) <= maxPositionDiff * maxPositionDiff;

                        if (!positionsMatch)
                            continue;

                        didMatch = true;
                        break;
                    }

                    // Add to group if matched
                    if (!didMatch)
                        continue;
                    group.Add(info);
                    break;
                }

                // Otherwise create new group
                if (!didMatch)
                {
                    var list = GroupInfoBuffer.Count > 0
                        ? GroupInfoBuffer.Pop()
                        : new HashSet<WorldDetectionInfo>(UseAllSensors.Value
                            ? GridSensors.Sensors.Count
                            : ActiveSensors.Count);

                    list.Add(info);

                    _groupsCache.Add(list);
                }
            }
        }
    }
}
