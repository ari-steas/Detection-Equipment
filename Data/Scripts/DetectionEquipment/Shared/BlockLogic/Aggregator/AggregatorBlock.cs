﻿using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.Structs;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI.Network;
using VRage.Sync;
using VRageMath;

namespace DetectionEquipment.Shared.BlockLogic.Aggregator
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "DetectionAggregatorBlock")]
    internal class AggregatorBlock : ControlBlockBase<IMyConveyorSorter>
    {
        public MySync<float, SyncDirection.BothWays> AggregationTime;
        public MySync<float, SyncDirection.BothWays> DistanceThreshold;
        public MySync<float, SyncDirection.BothWays> VelocityErrorThreshold; // Standard Deviation at which to ignore velocity estimation
        public MySync<float, SyncDirection.BothWays> RCSThreshold;
        public MySync<bool, SyncDirection.BothWays> AggregateTypes;
        public MySync<bool, SyncDirection.BothWays> UseAllSensors;
        public MySync<long[], SyncDirection.BothWays> ActiveSensors;

        public float MaxVelocity = Math.Max(MyDefinitionManager.Static.EnvironmentDefinition.LargeShipMaxSpeed, MyDefinitionManager.Static.EnvironmentDefinition.SmallShipMaxSpeed) + 10;

        internal Queue<WorldDetectionInfo[]> DetectionCache = new Queue<WorldDetectionInfo[]>();

        private bool _isBufferValid = false;
        private HashSet<WorldDetectionInfo> _bufferDetections = new HashSet<WorldDetectionInfo>();

        internal HashSet<BlockSensor> ActiveSensorBlocks = new HashSet<BlockSensor>();

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (Block?.CubeGrid?.Physics == null || !MyAPIGateway.Session.IsServer) // ignore projected and other non-physical grids
                return;

            AggregationTime.Value = 1f;
            DistanceThreshold.Value = 2f;
            VelocityErrorThreshold.Value = 32f;
            RCSThreshold.Value = 1f;
            AggregateTypes.Value = true;
            UseAllSensors.Value = true;
            ActiveSensors.Value = Array.Empty<long>();
            ActiveSensors.ValueChanged += sync =>
            {
                ActiveSensorBlocks.Clear();
                foreach (var sensor in GridSensors.Sensors)
                {
                    for (int i = 0; i < sync.Value.Length; i++)
                    {
                        if (sensor.Block.EntityId != sync.Value[i])
                            continue;
                        ActiveSensorBlocks.Add(sensor);
                        break;
                    }
                };
            };

            new AggregatorControls().DoOnce();
        }

        public HashSet<WorldDetectionInfo> GetAggregatedDetections()
        {
            if (_isBufferValid)
                return _bufferDetections;

            Dictionary<WorldDetectionInfo, int> weightedInfos = new Dictionary<WorldDetectionInfo, int>();
            var aggregatedDetections = new HashSet<WorldDetectionInfo>();

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

            _isBufferValid = true;
            _bufferDetections = aggregatedDetections;
            return aggregatedDetections;
        }

        public override void UpdateAfterSimulation()
        {
            _isBufferValid = false;
            _bufferDetections.Clear();

            HashSet<WorldDetectionInfo> infos = new HashSet<WorldDetectionInfo>();
            foreach (var sensor in UseAllSensors.Value ? GridSensors.Sensors : ActiveSensorBlocks)
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
            //MyAPIGateway.Utilities.ShowNotification($"Det: {AggregatedDetections.Count} Cache: {DetectionCache.Count}", 1000/60);
            //foreach (var detection in AggregatedDetections)
            //{
            //    MyAPIGateway.Utilities.ShowMessage("", detection.ToString());
            //    DebugDraw.AddLine(Block.GetPosition(), detection.Position, Color.Green, 0);
            //    if (detection.Velocity != null)
            //        DebugDraw.AddLine(detection.Position, detection.Position + detection.Velocity.Value, Color.Blue, 0);
            //}
        }

        private WorldDetectionInfo[] AggregateInfos(ICollection<WorldDetectionInfo> infos)
        {
            var groups = GroupInfos(infos);
            var aggregated = new WorldDetectionInfo[groups.Count];
            for (int i = 0; i < aggregated.Length; i++)
            {
                aggregated[i] = WorldDetectionInfo.Average(groups[i]);
            }

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
                        bool typesMatch = AggregateTypes || member.DetectionType == info.DetectionType;
                        bool crossSectionsMatch = member.DetectionType != info.DetectionType || Math.Abs(member.CrossSection - info.CrossSection) <= Math.Max(member.CrossSection, info.CrossSection) * RCSThreshold;
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
