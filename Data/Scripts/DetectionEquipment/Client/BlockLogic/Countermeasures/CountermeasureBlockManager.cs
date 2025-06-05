using DetectionEquipment.Client.BlockLogic.Sensors;
using DetectionEquipment.Client.Networking;
using DetectionEquipment.Shared.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DetectionEquipment.Shared.Networking;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DetectionEquipment.Client.BlockLogic.Countermeasures
{
    internal static class CountermeasureBlockManager
    {
        public static void Load()
        {
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
        }

        private static void OnEntityAdd(IMyEntity ent)
        {
            var grid = ent as IMyCubeGrid;
            if (grid == null)
                return;

            grid.OnBlockAdded += OnBlockAdded;
            foreach (var block in grid.GetFatBlocks<IMyCubeBlock>())
                OnBlockAdded(block.SlimBlock);
        }

        private static void OnBlockAdded(IMySlimBlock slim)
        {
            var block = slim.FatBlock;
            if (block == null)
                return;

            if (DefinitionManager.GetCountermeasureEmitterDefinitions(block).Count == 0)
                return;

            ClientNetwork.SendToServer(new CountermeasureInitPacket
            {
                AttachedBlockId = block.EntityId
            });
        }
    }
}
