using System;
using DetectionEquipment.Shared.BlockLogic;
using DetectionEquipment.Shared.BlockLogic.Aggregator.Datalink;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.ExternalApis;
using DetectionEquipment.Shared.Helpers;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using VRage.Game.Components;

namespace DetectionEquipment.Shared
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation, Priority = int.MinValue)]
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class SharedMain : MySessionComponentBase
    {
        public static SharedMain I;

        public override void LoadData()
        {
            try
            {
                I = this;

                Log.Init(ModContext);
                Log.Info("SharedMain", "Start initialize...");
                Log.IncreaseIndent();

                GlobalData.Init();
                PersistentBlockIdHelper.Load();
                ObjectPackager.Load();
                ApiManager.Init();
                ControlBlockManager.Load();
                DefinitionManager.Load();
                DatalinkManager.Load();

                Log.DecreaseIndent();
                Log.Info("SharedMain", "Initialized.");
            }
            catch (Exception ex)
            {
                Log.Exception("SharedMain", ex, true);
            }
        }

        public int Ticks = 0;

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (Ticks % 10 == 0)
                {
                    DefinitionManager.Update();
                    GlobalData.UpdatePlayers();
                }

                Log.Update();
                Ticks++;
            }
            catch (Exception ex)
            {
                Log.Exception("SharedMain", ex);
            }
        }

        protected override void UnloadData()
        {
            Log.GameUnloading = true;

            try
            {
                Log.Info("SharedMain", "Start unload...");
                Log.IncreaseIndent();
        
                ApiManager.Unload();
                DatalinkManager.Unload();
                ControlBlockManager.Unload();
                DefinitionManager.Unload();
                PersistentBlockIdHelper.Unload();
                ObjectPackager.Unload();
                GlobalData.Unload();

                I = null;

                Log.DecreaseIndent();
                Log.Info("SharedMain", "Unloaded.");
            }
            catch (Exception ex)
            {
                Log.Exception("SharedMain", ex, true);
            }
        }
    }
}
