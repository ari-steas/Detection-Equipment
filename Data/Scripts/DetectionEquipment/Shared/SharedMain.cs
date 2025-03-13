using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;

namespace DetectionEquipment.Shared
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation, Priority = int.MinValue)]
    internal class SharedMain : MySessionComponentBase
    {
        public override void LoadData()
        {
            Log.Init();
            Log.Info("SharedMain", "Initialized.");
        }

        protected override void UnloadData()
        {
            Log.Info("SharedMain", "Unloaded.");
            Log.Close();
        }
    }
}
