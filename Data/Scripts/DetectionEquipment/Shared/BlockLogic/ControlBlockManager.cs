using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game.Entities;
using System.Collections.Generic;
using VRage.Game.Components;

namespace DetectionEquipment.Shared.BlockLogic
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    internal class ControlBlockManager : MySessionComponentBase
    {
        public static ControlBlockManager I;
        public Dictionary<MyCubeBlock, IControlBlockBase> Blocks = new Dictionary<MyCubeBlock, IControlBlockBase>();
        public Dictionary<string, IBlockSelectControl> BlockControls = new Dictionary<string, IBlockSelectControl>();

        public override void LoadData()
        {
            I = this;
            Log.Info("ControlBlockManager", "Ready.");
        }

        protected override void UnloadData()
        {
            Blocks = null;
            I = null;
            Log.Info("ControlBlockManager", "Unloaded.");
        }
    }
}
