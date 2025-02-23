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
        public float AggregationTime = 1;
        public float DistanceThreshold = 2f;

        public float MaxVelocity = Math.Max(MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed, MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed) + 10;
        public float RCSThreshold = 1f;

        internal Queue<WorldDetectionInfo[]> DetectionCache = new Queue<WorldDetectionInfo[]>();

        public HashSet<WorldDetectionInfo> GetAggregatedDetections()
        {
            Dictionary<WorldDetectionInfo, int> weightedInfos = new Dictionary<WorldDetectionInfo, int>();

            int weight = 1;
            foreach (var set in DetectionCache)
            {
                weight++;
                foreach (var detection in set)
                {
                    weightedInfos[detection] = weightedInfos.ContainsKey(detection) ? weightedInfos[detection] + weight : weight;
                }
            }

            var uncombinableInfos = new HashSet<WorldDetectionInfo>();
            var alreadyChecked = new HashSet<WorldDetectionInfo>();
            var toCombine = new Dictionary<WorldDetectionInfo, int>();
            while (true)
            {
                int prevCount = weightedInfos.Count;
                foreach (var info in weightedInfos.Keys.ToArray())
                {
                    if (alreadyChecked.Contains(info))
                        continue;
                    alreadyChecked.Add(info);

                    int totalWeight = weightedInfos[info];
                    foreach (var subDetection in weightedInfos.Keys)
                    {
                        if (subDetection.Equals(info) || uncombinableInfos.Contains(subDetection) || alreadyChecked.Contains(subDetection))
                            continue;

                        var maxDistanceError = MathHelper.Max(info.Error, subDetection.Error) * DistanceThreshold + MaxVelocity;

                        if (Math.Abs(subDetection.CrossSection - info.CrossSection)/info.CrossSection <= RCSThreshold && Vector3D.DistanceSquared(info.Position, subDetection.Position) < maxDistanceError * maxDistanceError)
                        {
                            toCombine.Add(subDetection, weightedInfos[subDetection]);
                            alreadyChecked.Add(subDetection);
                            totalWeight += weightedInfos[subDetection];

                            DebugDraw.AddLine(info.Position, subDetection.Position, Color.Blue * (weightedInfos[subDetection]/(float) DetectionCache.Count), 0);
                        }
                    }

                    if (toCombine.Count > 0)
                    {
                        toCombine.Add(info, weightedInfos[info]);

                        foreach (var subDetection in toCombine.Keys)
                            weightedInfos.Remove(subDetection);

                        weightedInfos.Add(WorldDetectionInfo.AverageWeighted(toCombine), totalWeight);
                    }
                    else
                    {
                        weightedInfos.Remove(info);
                        uncombinableInfos.Add(info);
                    }
                }

                toCombine.Clear();
                alreadyChecked.Clear();

                if (prevCount == weightedInfos.Count)
                    break;
            }

            return uncombinableInfos;
        }

        public override void UpdateAfterSimulation()
        {
            HashSet<WorldDetectionInfo> infos = new HashSet<WorldDetectionInfo>();
            foreach (var sensor in GridSensors.Sensors)
            {
                foreach (var sensorDetection in sensor.Detections)
                {
                    var detection = new WorldDetectionInfo(sensorDetection);
                    DebugDraw.AddLine(sensor.Sensor.Position, detection.Position, Color.Red, 0);
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
