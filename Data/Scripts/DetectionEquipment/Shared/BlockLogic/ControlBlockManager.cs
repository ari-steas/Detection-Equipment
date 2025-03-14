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


        public override void LoadData()
        {
            I = this;
        }

        protected override void UnloadData()
        {
            Blocks = null;
            I = null;
        }
    }
}
