using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;

namespace DetectionEquipment.Shared.ControlBlocks
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    internal class ControlBlockManager : MySessionComponentBase
    {
        public static ControlBlockManager I;
        public Dictionary<MyCubeBlock, ControlBlockBase> Blocks = new Dictionary<MyCubeBlock, ControlBlockBase>();


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
