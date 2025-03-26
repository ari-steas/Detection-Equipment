using DetectionEquipment.Shared.Utils;
using VRage.Game.Components;

namespace DetectionEquipment.Shared
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation, Priority = int.MinValue)]
    internal class SharedMain : MySessionComponentBase
    {
        public override void LoadData()
        {
            Log.Init();
            Log.Info("SharedMain", "Start initialize...");
            Log.IncreaseIndent();
        
            GlobalData.Init();

            Log.DecreaseIndent();
            Log.Info("SharedMain", "Initialized.");
        }

        private int _ticks = 0;
        public override void UpdateAfterSimulation()
        {
            if (_ticks % 10 == 0)
                GlobalData.UpdatePlayers();
            _ticks++;
        }

        protected override void UnloadData()
        {
            Log.Info("SharedMain", "Start unload...");
            Log.IncreaseIndent();
        
            GlobalData.Unload();

            Log.DecreaseIndent();
            Log.Info("SharedMain", "Unloaded.");
        }
    }
}
