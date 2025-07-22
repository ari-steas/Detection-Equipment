using System.Collections.Generic;
using DetectionEquipment.Client.BlockLogic.Countermeasures;
using DetectionEquipment.Client.BlockLogic.Sensors;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.BlockLogic;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DetectionEquipment.Client.BlockLogic
{
    internal class BlockLogicManager
    {
        private static BlockLogicManager _;
        private static Dictionary<long, List<IBlockLogic>> Logics = new Dictionary<long, List<IBlockLogic>>();
        private static Dictionary<long, List<IBlockLogic>> DelayedInitLogics = new Dictionary<long, List<IBlockLogic>>();
        private static Dictionary<long, List<BlockLogicUpdatePacket>> DelayedUpdateLogics = new Dictionary<long, List<BlockLogicUpdatePacket>>();

        public static void Load()
        {
            _ = new BlockLogicManager();
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemoved;

            SensorBlockManager.Load();
            CountermeasureBlockManager.Load();
        }

        public static void Unload()
        {
            SensorBlockManager.Unload();
            CountermeasureBlockManager.Unload();

            MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemoved;
            _ = null;
        }

        public static void UpdateAfterSimulation()
        {
            RegisterDelayedInits();

            lock (Logics)
            {
                foreach (var set in Logics)
                    foreach (var logic in set.Value)
                        logic.UpdateAfterSimulation();
            }
        }

        private static void RegisterDelayedInits()
        {
            var emptySets = new List<long>();
            foreach (var set in DelayedInitLogics)
            {
                for (int i = set.Value.Count - 1; i >= 0; i--)
                {
                    var logic = set.Value[i];
                    if (TryRegisterLogicInternal(set.Key, logic))
                        set.Value.RemoveAt(i);
                }
                if (set.Value.Count == 0)
                    emptySets.Add(set.Key);
            }

            foreach (var empty in emptySets)
                DelayedInitLogics.Remove(empty);
        }

        public static bool RegisterLogic<TLogic>(long blockId, TLogic logic) where TLogic : class, IBlockLogic
        {
            lock (Logics)
            {
                // don't double up on logics
                if ((Logics.ContainsKey(blockId) && Logics[blockId].Contains(logic)) || (DelayedInitLogics.ContainsKey(blockId) && DelayedInitLogics[blockId].Contains(logic)))
                    return false;
            }

            if (TryRegisterLogicInternal(blockId, logic))
                return true;

            if (DelayedInitLogics.ContainsKey(blockId))
                DelayedInitLogics[blockId].Add(logic);
            else
                DelayedInitLogics[blockId] = new List<IBlockLogic> { logic };

            return false;
        }

        private static bool TryRegisterLogicInternal<TLogic>(long blockId, TLogic logic) where TLogic : class, IBlockLogic
        {
            var block = MyAPIGateway.Entities.GetEntityById(blockId) as IMyTerminalBlock;
            if (block?.CubeGrid?.Physics == null)
            {
                if (GlobalData.DebugLevel > 0)
                    Log.Info("BlockLogicManager", $"Delayed registering {logic.GetType().Name} on {blockId}...");
                return false;
            }

            logic.Register(block);
            lock (Logics)
            {
                if (Logics.ContainsKey(blockId))
                    Logics[blockId].Add(logic);
                else
                    Logics[blockId] = new List<IBlockLogic> { logic };
            }

            List<BlockLogicUpdatePacket> updateSet;
            if (DelayedUpdateLogics.TryGetValue(blockId, out updateSet))
            {
                for (int i = updateSet.Count - 1; i >= 0; i--)
                {
                    var updatePacket = updateSet[i];
                    if (!updatePacket.CanUpdate(logic))
                        continue;

                    logic.UpdateFromNetwork(updatePacket);
                    if (GlobalData.DebugLevel > 0)
                        Log.Info("BlockLogicManager", $"Updated {logic.GetType().Name} from network on {blockId}.");
                    updateSet.RemoveAt(i);
                    // todo optimize this, there should only be one logic of each type per block!
                }
            }
            if (GlobalData.DebugLevel > 0)
                Log.Info("BlockLogicManager", $"Registered {logic.GetType().Name} on {blockId}.");
            return true;
        }

        public static bool CanUpdateLogic<TLogic>(long blockId, BlockLogicUpdatePacket packet, out TLogic logic) where TLogic : class, IBlockLogic
        {
            logic = GetLogic<TLogic>(blockId);
            if (logic == null)
            {
                if (DelayedUpdateLogics.ContainsKey(blockId))
                    DelayedUpdateLogics[blockId].Add(packet);
                else
                    DelayedUpdateLogics[blockId] = new List<BlockLogicUpdatePacket> { packet };
                if (GlobalData.DebugLevel > 0)
                    Log.Info("BlockLogicManager", $"Delayed updating {typeof(TLogic).Name} on {blockId}...");
                return false;
            }

            return true;
        }

        public static void OnEntityRemoved(IMyEntity ent)
        {
            var block = ent as IMyTerminalBlock;
            if (block == null)
                return;
            CloseLogic(block);
        }

        public static void CloseLogic(IMyTerminalBlock block)
        {
            List<IBlockLogic> set;
            lock (Logics)
            {
                if (!Logics.TryGetValue(block.EntityId, out set))
                    return;
            }

            foreach (var logic in set)
            {
                if (!logic.IsClosed)
                {
                    logic.Close();
                    logic.IsClosed = true;
                }
            }

            lock (Logics)
                Logics.Remove(block.EntityId);
        }

        public static TLogic GetLogic<TLogic>(long blockId) where TLogic : class, IBlockLogic
        {
            List<IBlockLogic> set;
            lock (Logics)
            {
                if (!Logics.TryGetValue(blockId, out set))
                    return null;
            }

            var type = typeof(TLogic);
            foreach (var logic in set)
                if (logic.GetType() == type)
                    return (TLogic)logic;
            return null;
        }

        public static bool HasLogic(long blockId)
        {
            if (Logics == null)
                return false;

            lock (Logics)
            {
                return Logics.ContainsKey(blockId);
            }
        }
    }

    internal static class BlockLogicHelpers
    {
        public static TLogic GetLogic<TLogic>(this IMyCubeBlock block) where TLogic : class, IBlockLogic => BlockLogicManager.GetLogic<TLogic>(block.EntityId);
        public static bool HasLogic(this IMyCubeBlock block) => BlockLogicManager.HasLogic(block.EntityId);
    }
}
