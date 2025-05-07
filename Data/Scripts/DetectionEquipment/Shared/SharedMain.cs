using System;
using DetectionEquipment.Shared.BlockLogic;
using DetectionEquipment.Shared.BlockLogic.Aggregator.Datalink;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.ExternalApis;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

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

        private int _ticks = 0;

        public override void UpdateAfterSimulation()
        {
            try
            {
                if (_ticks % 10 == 0)
                {
                    DefinitionManager.Update();
                    GlobalData.UpdatePlayers();
                }

                //if (_ticks % 600 == 0)
                //{
                //    Log.Info("", "");
                //    Log.Info("Sync Distance", MyAPIGateway.Session.SessionSettings.SyncDistance.ToString());
                //    Log.Info("View Distance", MyAPIGateway.Session.SessionSettings.ViewDistance.ToString());
                //    MyAPIGateway.Entities.GetEntities(null, e =>
                //    {
                //        var grid = e as IMyCubeGrid;
                //        if (grid == null)
                //            return false;
                //        //if (e is IMyCubeGrid)
                //        //    e.Flags |= EntityFlags.DrawOutsideViewDistance;
                //        Log.Info(grid.DisplayName, $"{(MyAPIGateway.Utilities.IsDedicated ? "" : $"Distance: {Vector3D.Distance(MyAPIGateway.Session.Player.Character.GetPosition(), e.GetPosition()):N0} | ")}Flags: {e.Flags}");
                //        return false;
                //    });
                //}

                _ticks++;
            }
            catch (Exception ex)
            {
                Log.Exception("SharedMain", ex);
            }
        }

        protected override void UnloadData()
        {
            try
            {
                Log.Info("SharedMain", "Start unload...");
                Log.IncreaseIndent();
        
                GlobalData.Unload();
                ApiManager.Unload();
                DatalinkManager.Unload();
                ControlBlockManager.Unload();
                DefinitionManager.Unload();

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
