using DetectionEquipment.Client.Countermeasures;
using VRage.Game.Components;
using DetectionEquipment.Client.Sensors;
using DetectionEquipment.Shared.Utils;
using DetectionEquipment.Client.Networking;
using DetectionEquipment.Client.Interface;
using System;

namespace DetectionEquipment.Client
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    // ReSharper disable once UnusedType.Global
    internal class ClientMain : MySessionComponentBase
    {
        public override void LoadData()
        {
            try
            {
                Log.Info("ClientMain", "Start initialize...");
                Log.IncreaseIndent();

                SensorBlockManager.Init();
                new ClientNetwork().LoadData();
                BlockCategoryManager.Init();
                CountermeasureManager.Init();
                RcsTool.Init();

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
            try
            {
                ClientNetwork.I.Update();
                SensorBlockManager.Update();
                CountermeasureManager.Update();
                RcsTool.Update();
            }
            catch (Exception ex)
            {
                Log.Exception("ClientMain", ex);
            }
        }

        protected override void UnloadData()
        {
            try
            {
                Log.Info("ClientMain", "Start unload...");
                Log.IncreaseIndent();

                RcsTool.Close();
                CountermeasureManager.Close();
                BlockCategoryManager.Close();
                ClientNetwork.I.UnloadData();
                SensorBlockManager.Unload();

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
