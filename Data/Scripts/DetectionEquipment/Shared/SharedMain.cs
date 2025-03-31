using DetectionEquipment.Shared.BlockLogic;
using DetectionEquipment.Shared.BlockLogic.Aggregator.Datalink;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using VRage.Game.Components;

namespace DetectionEquipment.Shared
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation, Priority = int.MinValue)]
    internal class SharedMain : MySessionComponentBase
    {
        public static SharedMain I;

        public override void LoadData()
        {
            Log.Init();
            Log.Info("SharedMain", "Start initialize...");
            Log.IncreaseIndent();

            I = this;
        
            GlobalData.Init();
            ControlBlockManager.Load();
            DefinitionManager.Load();
            DatalinkManager.Load();

            Log.DecreaseIndent();
            Log.Info("SharedMain", "Initialized.");
        }

        private int _ticks = 0;
        public override void UpdateAfterSimulation()
        {
            if (_ticks % 10 == 0)
            {
                DefinitionManager.Update();
                GlobalData.UpdatePlayers();
            }
            _ticks++;
        }

        protected override void UnloadData()
        {
            Log.Info("SharedMain", "Start unload...");
            Log.IncreaseIndent();
        
            GlobalData.Unload();
            DatalinkManager.Unload();
            ControlBlockManager.Unload();
            DefinitionManager.Unload();

            I = null;

            Log.DecreaseIndent();
            Log.Info("SharedMain", "Unloaded.");
        }
    }
}
