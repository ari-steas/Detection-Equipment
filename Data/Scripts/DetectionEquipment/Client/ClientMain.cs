using VRage.Game.Components;
using DetectionEquipment.Client.Sensors;
using DetectionEquipment.Shared.Utils;
using DetectionEquipment.Client.Networking;
using Sandbox.Definitions;
using System.Collections.Generic;

namespace DetectionEquipment.Client
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class ClientMain : MySessionComponentBase
    {
        public override void LoadData()
        {
            Log.Info("ClientMain", "Start initialize...");
            Log.IncreaseIndent();

            SensorBlockManager.Init();
            new ClientNetwork().LoadData();

            Log.DecreaseIndent();
            Log.Info("ClientMain", "Initialized.");
        }

        public override void UpdateAfterSimulation()
        {
            ClientNetwork.I.Update();
            SensorBlockManager.Update();
        }

        protected override void UnloadData()
        {
            Log.Info("ClientMain", "Start unload...");
            Log.IncreaseIndent();

            ClientNetwork.I.UnloadData();
            SensorBlockManager.Unload();

            Log.DecreaseIndent();
            Log.Info("ClientMain", "Unloaded.");
            Log.Close();
        }
    }
}
