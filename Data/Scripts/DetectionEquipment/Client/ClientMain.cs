using DetectionEquipment.Client.Countermeasures;
using VRage.Game.Components;
using DetectionEquipment.Shared.Utils;
using DetectionEquipment.Client.Networking;
using DetectionEquipment.Client.Interface;
using System;
using DetectionEquipment.Client.BlockLogic;
using Sandbox.ModAPI;
using DetectionEquipment.Client.External;
using DetectionEquipment.Client.Interface.Commands;
using DetectionEquipment.Client.Interface.DetectionHud;
using DetectionEquipment.Shared;

namespace DetectionEquipment.Client
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    // ReSharper disable once UnusedType.Global
    internal class ClientMain : MySessionComponentBase
    {
        public override void LoadData()
        {
            if (MyAPIGateway.Utilities.IsDedicated || GlobalData.Killswitch)
                return;

            try
            {
                Log.Info("ClientMain", "Start initialize...");
                Log.IncreaseIndent();

                BlockLogicManager.Load();
                new ClientNetwork().LoadData();
                BlockCategoryManager.Init();
                CountermeasureManager.Init();
                RcsTool.Init();
                WcInteractionManager.Init();
                ModderNotification.Init();
                DetectionHud.Init();
                CommandHandler.Init();

                Log.DecreaseIndent();
                Log.Info("ClientMain", "Initialized.");
            }
            catch (Exception ex)
            {
                Log.Exception("ClientMain", ex, true);
            }
        }

        public override void UpdateAfterSimulation()
        {
            if (MyAPIGateway.Utilities.IsDedicated || GlobalData.Killswitch)
                return;

            try
            {
                ClientNetwork.I.Update();
                BlockLogicManager.UpdateAfterSimulation();
                CountermeasureManager.Update();
                RcsTool.Update();
                ModderNotification.Update();
                DetectionHud.UpdateAfterSimulation();
            }
            catch (Exception ex)
            {
                Log.Exception("ClientMain", ex);
            }
        }

        public override void Draw()
        {
            if (MyAPIGateway.Utilities.IsDedicated)
                return;

            try
            {
                DetectionHud.Draw();
            }
            catch (Exception ex)
            {
                Log.Exception("ClientMain", ex);
            }
        }

        protected override void UnloadData()
        {
            if (MyAPIGateway.Utilities.IsDedicated || GlobalData.Killswitch)
                return;

            try
            {
                Log.Info("ClientMain", "Start unload...");
                Log.IncreaseIndent();

                CommandHandler.Close();
                DetectionHud.Close();
                WcInteractionManager.Close();
                RcsTool.Close();
                CountermeasureManager.Close();
                BlockCategoryManager.Close();
                ClientNetwork.I.UnloadData();
                BlockLogicManager.Unload();

                Log.DecreaseIndent();
                Log.Info("ClientMain", "Unloaded.");
                Log.Close();
            }
            catch (Exception ex)
            {
                Log.Exception("ClientMain", ex, true);
            }
        }
    }
}
