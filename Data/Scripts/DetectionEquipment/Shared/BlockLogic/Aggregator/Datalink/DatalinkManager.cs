using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.Aggregator.Datalink
{
    internal static class DatalinkManager
    {
        public static Dictionary<IMyCubeGrid, Dictionary<int, HashSet<AggregatorBlock>>> ActiveDatalinkChannels;

        public static void Load()
        {
            ActiveDatalinkChannels = new Dictionary<IMyCubeGrid, Dictionary<int, HashSet<AggregatorBlock>>>();
            Log.Info("DatalinkManager", "Loaded.");
        }

        public static void Unload()
        {
            ActiveDatalinkChannels = null;
            Log.Info("DatalinkManager", "Unloaded.");
        }



        public static Dictionary<int, HashSet<AggregatorBlock>> GetActiveDatalinkChannels(IMyCubeGrid grid, long ownerIdentityId)
        {
            var recievers = new List<MyDataReceiver>();
            foreach (var block in ((MyCubeGrid)grid).GetFatBlocks()) // TODO: Cache antennas.
                if (block is IMyRadioAntenna || block is IMyLaserAntenna && ((IMyFunctionalBlock)block).Enabled)
                    recievers.Add(block.Components.Get<MyDataReceiver>());

            var connectedGrids = new HashSet<IMyCubeGrid>();
            grid.GetGridGroup(GridLinkTypeEnum.Logical).GetGrids(connectedGrids);

            foreach (var reciever in TrackingUtils.GetAllRelayedBroadcasters(recievers, ownerIdentityId, false))
                if (reciever.Entity is IMyCubeBlock)
                    connectedGrids.Add(((IMyCubeBlock)reciever.Entity).CubeGrid);

            var fullSet = new Dictionary<int, HashSet<AggregatorBlock>>();

            //Log.Info("DatalinkManager", "CHK " + grid.CustomName);
            //Log.IncreaseIndent();

            foreach (var connectedGrid in connectedGrids)
            {
                //Log.Info("GRID", connectedGrid.CustomName);
                Dictionary<int, HashSet<AggregatorBlock>> gridSet;
                if (!ActiveDatalinkChannels.TryGetValue(connectedGrid, out gridSet))
                    continue;
                foreach (var channel in gridSet)
                {
                    if (fullSet.ContainsKey(channel.Key))
                    {
                        foreach (var setItem in channel.Value)
                            fullSet[channel.Key].Add(setItem);
                    }
                    else
                        fullSet[channel.Key] = channel.Value;
                }
            }

            //Log.DecreaseIndent();

            return fullSet;
        }

        public static void RegisterAggregator(AggregatorBlock logic, int id, int prevId = -1)
        {
            if (logic == null || id == prevId) return;

            if (prevId != -1)
            {
                Dictionary<int, HashSet<AggregatorBlock>> set;
                HashSet<AggregatorBlock> subSet;
                if (ActiveDatalinkChannels.TryGetValue(logic.Block.CubeGrid, out set) && set.TryGetValue(prevId, out subSet))
                {
                    subSet.Remove(logic);
                    if (subSet.Count == 0)
                        set.Remove(prevId);
                    if (set.Count == 0)
                        ActiveDatalinkChannels.Remove(logic.Block.CubeGrid);

                    //Log.Info("DatalinkManager", "Unregistered aggregator with previous ID " + prevId);
                }
            }

            if (id != -1)
            {
                Dictionary<int, HashSet<AggregatorBlock>> set;
                if (!ActiveDatalinkChannels.TryGetValue(logic.Block.CubeGrid, out set))
                {
                    set = new Dictionary<int, HashSet<AggregatorBlock>>();
                    ActiveDatalinkChannels[logic.Block.CubeGrid] = set;
                }

                HashSet<AggregatorBlock> subSet;
                if (!set.TryGetValue(id, out subSet))
                {
                    subSet = new HashSet<AggregatorBlock>();
                    set[id] = subSet;
                }
                subSet.Add(logic);

                //Log.Info("DatalinkManager", $"Registered aggregator {logic.Block.EntityId} with ID " + id);
            }
        }

        public static void UnregisterAggregator(AggregatorBlock logic)
        {
            //Log.Info("DatalinkManager", "Unregistered aggregator " + logic.Block.EntityId);
            if (logic == null)
                return;

            foreach (var channel in ActiveDatalinkChannels.Values)
                foreach (var idSet in channel.Values)
                    idSet.Remove(logic);
        }
    }
}
