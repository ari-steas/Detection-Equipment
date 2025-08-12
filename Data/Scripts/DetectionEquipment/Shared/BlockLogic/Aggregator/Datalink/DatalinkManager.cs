using DetectionEquipment.Shared.Utils;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.Aggregator.Datalink
{
    internal static class DatalinkManager
    {
        public static Dictionary<IMyCubeGrid, Dictionary<int, HashSet<AggregatorBlock>>> ActiveDatalinkChannels;
        private static Dictionary<IMyCubeGrid, HashSet<IMyFunctionalBlock>> _antennaCache;

        public static void Load()
        {
            ActiveDatalinkChannels = new Dictionary<IMyCubeGrid, Dictionary<int, HashSet<AggregatorBlock>>>();
            _antennaCache = new Dictionary<IMyCubeGrid, HashSet<IMyFunctionalBlock>>();

            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                if (e is IMyCubeGrid)
                    OnEntityAdd(e);
                return false;
            });

            Log.Info("DatalinkManager", "Loaded.");
        }

        public static void Unload()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;

            _antennaCache = null;
            ActiveDatalinkChannels = null;
            Log.Info("DatalinkManager", "Unloaded.");
        }

        private static void OnEntityAdd(IMyEntity e)
        {
            var grid = e as IMyCubeGrid;
            if (grid == null)
                return;
            _antennaCache.Add(grid, new HashSet<IMyFunctionalBlock>());
            grid.OnClosing += OnGridClose;
            grid.OnBlockAdded += OnBlockAdded;
            grid.OnBlockRemoved += OnBlockRemoved;
            foreach (var block in grid.GetFatBlocks<IMyFunctionalBlock>())
                OnBlockAdded(block.SlimBlock);
        }

        private static void OnBlockAdded(IMySlimBlock b)
        {
            var functional = b.FatBlock as IMyFunctionalBlock;
            if (functional == null || !(functional is IMyRadioAntenna || functional is IMyLaserAntenna))
                return;
            HashSet<IMyFunctionalBlock> set;
            if (!_antennaCache.TryGetValue(functional.CubeGrid, out set))
                return; // either grid is dead or something has gone wrong
            set.Add(functional);
        }

        private static void OnBlockRemoved(IMySlimBlock b)
        {
            var functional = b.FatBlock as IMyFunctionalBlock;
            if (functional == null || !(functional is IMyRadioAntenna || functional is IMyLaserAntenna))
                return;
            HashSet<IMyFunctionalBlock> set;
            if (!_antennaCache.TryGetValue(functional.CubeGrid, out set))
                return; // either grid is dead or something has gone wrong
            set.Remove(functional);
        }

        private static void OnGridClose(IMyEntity e)
        {
            _antennaCache.Remove((IMyCubeGrid)e);
        }

        public static Dictionary<int, HashSet<AggregatorBlock>> GetActiveDatalinkChannels(IMyCubeGrid grid, long ownerIdentityId)
        {
            var receivers = new List<MyDataReceiver>();
      
            var connectedGrids = new HashSet<IMyCubeGrid>();
            grid.GetGridGroup(GridLinkTypeEnum.Logical).GetGrids(connectedGrids);
            foreach (var cGrid in connectedGrids)
            {
                HashSet<IMyFunctionalBlock> antennaSet;
                if (!_antennaCache.TryGetValue(cGrid, out antennaSet) || antennaSet.Count == 0)
                    continue;
                foreach (var block in antennaSet)
                {
                    if (block.Enabled)
                        receivers.Add(block.Components.Get<MyDataReceiver>());
                }
            }

            foreach (var receiver in TrackingUtils.GetAllRelayedBroadcasters(receivers, ownerIdentityId, false))
                if (receiver.Entity is IMyCubeBlock)
                    connectedGrids.Add(((IMyCubeBlock)receiver.Entity).CubeGrid);

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
