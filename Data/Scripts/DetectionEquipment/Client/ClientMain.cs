using DetectionEquipment.Client.Countermeasures;
using VRage.Game.Components;
using DetectionEquipment.Client.Sensors;
using DetectionEquipment.Shared.Utils;
using DetectionEquipment.Client.Networking;
using DetectionEquipment.Client.Interface;

namespace DetectionEquipment.Client
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    // ReSharper disable once UnusedType.Global
    internal class ClientMain : MySessionComponentBase
    {
        public override void LoadData()
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

        public override void UpdateAfterSimulation()
        {
            ClientNetwork.I.Update();
            SensorBlockManager.Update();
            CountermeasureManager.Update();
            RcsTool.Update();
        }

        protected override void UnloadData()
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
    }
}
