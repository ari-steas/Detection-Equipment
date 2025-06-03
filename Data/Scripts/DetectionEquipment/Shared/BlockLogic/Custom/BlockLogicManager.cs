using System.Collections.Generic;

namespace DetectionEquipment.Shared.BlockLogic.Custom
{
    internal class BlockLogicManager
    {
        private static BlockLogicManager _;
        private static Dictionary<long, List<IBlockLogic>> Logics = new Dictionary<long, List<IBlockLogic>>();

        public static void Load()
        {

        }

        public static void Unload()
        {

        }
    }
}
