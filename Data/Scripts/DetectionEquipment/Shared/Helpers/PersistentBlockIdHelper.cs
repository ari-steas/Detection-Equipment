using System;
using System.Collections.Generic;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DetectionEquipment.Shared.Helpers
{
    internal static class PersistentBlockIdHelper
    {
        // GridEntityId => <UniqueId, Block>
        private static Dictionary<long, Dictionary<long, IMyCubeBlock>> BlockIds;
        private static Random Random;

        public static void Load()
        {
            BlockIds = new Dictionary<long, Dictionary<long, IMyCubeBlock>>();
            Random = new Random();

            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                OnEntityAdd(e);
                return false;
            });
        }

        public static void Unload()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            BlockIds = null;
            Random = null;
        }

        public static long GetOrCreatePersistentId(this IMyCubeBlock block)
        {
            if (block == null)
                throw new ArgumentNullException(nameof(block));

            Dictionary<long, IMyCubeBlock> gridIds;
            if (BlockIds.TryGetValue(block.CubeGrid.EntityId, out gridIds))
            {
                foreach (var blockKvp in gridIds)
                {
                    if (blockKvp.Value != block)
                        continue;
                    return blockKvp.Key;
                }
            }

            return TryAssignId(block);
        }

        public static long AssignPersistentId(this IMyCubeBlock block) => TryAssignId(block);

        public static IMyCubeBlock GetBlockByPersistentId(this IMyCubeGrid grid, long id)
        {
            Dictionary<long, IMyCubeBlock> gridIds;
            IMyCubeBlock block;
            if (!BlockIds.TryGetValue(grid.EntityId, out gridIds) || !gridIds.TryGetValue(id, out block))
                return null;
            return block;
        }

        private static void OnEntityAdd(IMyEntity obj)
        {
            var grid = obj as IMyCubeGrid;
            if (grid?.Physics == null)
                return;

            grid.OnBlockAdded += OnBlockAdded;
            foreach (var block in grid.GetFatBlocks<IMyCubeBlock>())
                OnBlockAdded(block.SlimBlock);
            grid.OnMarkForClose += OnGridClose;
        }

        private static void OnBlockAdded(IMySlimBlock slim)
        {
            var block = slim.FatBlock;
            string persistentIdStr = null;
            long persistentId;
            // IF block is null OR storage has no id OR id is invalid, return.
            if (block == null || !(block.Storage?.TryGetValue(GlobalData.PersistentBlockIdGuid, out persistentIdStr) ?? false) || !long.TryParse(persistentIdStr, out persistentId))
                return;
            TryAssignId(block, persistentId);
        }

        private static void OnGridClose(IMyEntity grid)
        {
            BlockIds.Remove(grid.EntityId);
        }

        private static long TryAssignId(IMyCubeBlock block, long persistentId = -1)
        {
            Dictionary<long, IMyCubeBlock> gridIds;
            if (!BlockIds.TryGetValue(block.CubeGrid.EntityId, out gridIds))
            {
                gridIds = new Dictionary<long, IMyCubeBlock>();
                BlockIds[block.CubeGrid.EntityId] = gridIds;
            }

            while (persistentId == -1 || gridIds.ContainsKey(persistentId))
                persistentId = Random.NextLong();

            gridIds.Add(persistentId, block);
            if (block.Storage == null)
                block.Storage = new MyModStorageComponent();
            block.Storage[GlobalData.PersistentBlockIdGuid] = persistentId.ToString();
            return persistentId;
        }
    }
}
