using DetectionEquipment.Server.Sensors;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace DetectionEquipment.Shared.ControlBlocks
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "DetectionAggregatorBlock")]
    internal class AggregatorBlock : ControlBlockBase
    {
        public float AggregationTime = 1f;
        public float DistanceThreshold = 2f;
        public float VelocityErrorThreshold = 32f; // Standard Deviation at which to ignore velocity estimation

        public float MaxVelocity = Math.Max(MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed, MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed) + 10;
        public float RCSThreshold = 1f;

        internal Queue<WorldDetectionInfo[]> DetectionCache = new Queue<WorldDetectionInfo[]>();

        public HashSet<WorldDetectionInfo> GetAggregatedDetections()
        {
            Dictionary<WorldDetectionInfo, int> weightedInfos = new Dictionary<WorldDetectionInfo, int>();
            HashSet<WorldDetectionInfo> combined = new HashSet<WorldDetectionInfo>();

            int weight = 1;
            foreach (var set in DetectionCache)
            {
                weight++;
                foreach (var detection in set)
                {
                    weightedInfos[detection] = weightedInfos.ContainsKey(detection) ? weightedInfos[detection] + weight : weight;
                }
            }

            var latestSet = DetectionCache.Peek();
            List<WorldDetectionInfo> toCombine = new List<WorldDetectionInfo>();
            foreach (var info in latestSet)
            {
                foreach (var set in DetectionCache)
                {
                    if (set == latestSet)
                        continue;
                    
                    foreach (var member in set)
                    {
                        bool typesMatch = member.DetectionType == info.DetectionType;
                        bool crossSectionsMatch = Math.Abs(member.CrossSection - info.CrossSection) <= Math.Max(member.CrossSection, info.CrossSection) * RCSThreshold;
                        double maxPositionDiff = Math.Max(member.Error, info.Error) * DistanceThreshold + MaxVelocity;
                        bool positionsMatch = Vector3D.DistanceSquared(member.Position, info.Position) <= maxPositionDiff * maxPositionDiff;

                        // Cross-section doesn't have to match if the sensors are different types.
                        if (!typesMatch || !crossSectionsMatch || !positionsMatch)
                            continue;

                        toCombine.Add(member);
                        break; // TODO calculate velocity from average position delta
                    }
                }

                Vector3D averageVelocity = Vector3D.Zero;
                double velVariation = 0;

                if (toCombine.Count == 0)
                {
                    combined.Add(info);
                    continue;
                }

                if (toCombine.Count > 1)
                {
                    var velocities = new Vector3D[toCombine.Count - 1];
                    for (int i = 0; i < velocities.Length; i++)
                    {
                        DebugDraw.AddLine(toCombine[i].Position, toCombine[i+1].Position, Color.White * ((float)i/velocities.Length), 0);
                        velocities[i] = (toCombine[i+1].Position - toCombine[i].Position) * 60;
                        averageVelocity += velocities[i];
                    }
                    averageVelocity /= velocities.Length;
                    double averageSpeed = averageVelocity.Length();

                    foreach (var velocity in velocities)
                    {
                        var len = velocity.Length();
                        velVariation += (len - averageSpeed) * (len - averageSpeed);
                    }

                    velVariation = velVariation/velocities.Length;
                }

                var averagedInfo = WorldDetectionInfo.Average(toCombine);
                if (velVariation <= VelocityErrorThreshold * VelocityErrorThreshold)
                    averagedInfo.Position += averageVelocity * AggregationTime/2;

                MyAPIGateway.Utilities.ShowNotification($"Vel: {averageVelocity.Length():N1} m/s (R={Math.Sqrt(velVariation)})", 1000/60);
                DebugDraw.AddLine(averagedInfo.Position, averagedInfo.Position + averageVelocity, Color.Blue, 0);
                
                combined.Add(averagedInfo);
                toCombine.Clear();
            }

            return combined;
        }

        public override void UpdateAfterSimulation()
        {
            HashSet<WorldDetectionInfo> infos = new HashSet<WorldDetectionInfo>();
            foreach (var sensor in GridSensors.Sensors)
            {
                foreach (var sensorDetection in sensor.Detections)
                {
                    var detection = new WorldDetectionInfo(sensorDetection);
                    //DebugDraw.AddLine(sensor.Sensor.Position, detection.Position, Color.Red, 0);
                    infos.Add(detection);
                }
            }

            DetectionCache.Enqueue(AggregateInfos(infos));
            while (DetectionCache.Count > AggregationTime * 60)
                DetectionCache.Dequeue();


            // testing //
            var dets = GetAggregatedDetections();
            MyAPIGateway.Utilities.ShowNotification($"Det: {dets.Count} Cache: {DetectionCache.Count}", 1000/60);
            foreach (var detection in dets)
            {
                MyAPIGateway.Utilities.ShowMessage("", detection.ToString());
                DebugDraw.AddLine(Block.GetPosition(), detection.Position, Color.Green, 0);
            }
        }

        private WorldDetectionInfo[] AggregateInfos(ICollection<WorldDetectionInfo> infos)
        {
            var groups = GroupInfos(infos);
            var aggregated = new WorldDetectionInfo[groups.Count];
            for (int i = 0; i < aggregated.Length; i++)
            {
                aggregated[i] = WorldDetectionInfo.Average(groups[i]);
            }
            MyAPIGateway.Utilities.ShowNotification($"Aggregated {infos.Count} -> {aggregated.Length}", 1000/60);
            return aggregated;
        }

        /// <summary>
        /// Groups detection info from a single moment in time.
        /// </summary>
        /// <param name="infos"></param>
        private List<HashSet<WorldDetectionInfo>> GroupInfos(ICollection<WorldDetectionInfo> infos)
        {
            var groups = new List<HashSet<WorldDetectionInfo>>();

            foreach (var info in infos)
            {
                // Check if any existing groups match RCS and position
                bool didMatch = false;
                foreach (var group in groups)
                {
                    foreach (var member in group)
                    {
                        bool typesMatch = member.DetectionType == info.DetectionType;
                        bool crossSectionsMatch = Math.Abs(member.CrossSection - info.CrossSection) <= Math.Max(member.CrossSection, info.CrossSection) * RCSThreshold;
                        double maxPositionDiff = Math.Max(member.Error, info.Error) * DistanceThreshold;
                        bool positionsMatch = Vector3D.DistanceSquared(member.Position, info.Position) <= maxPositionDiff * maxPositionDiff;

                        // Cross-section doesn't have to match if the sensors are different types.
                        if (!typesMatch || !crossSectionsMatch || !positionsMatch)
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
                    groups.Add(new HashSet<WorldDetectionInfo>()
                    {
                        info
                    });
                }
            }

            return groups;
        }

        public override bool IsSerialized()
        {
            return base.IsSerialized();
        }
    }
}
