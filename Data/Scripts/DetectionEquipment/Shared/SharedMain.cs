using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;

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

    public static class GlobalData
    {
        public const ushort ServerNetworkId = 15289;
        public const ushort ClientNetworkId = 15287;
        public static readonly double SyncRange = MyAPIGateway.Session.SessionSettings.SyncDistance;
        public static readonly double SyncRangeSq = MyAPIGateway.Session.SessionSettings.SyncDistance * MyAPIGateway.Session.SessionSettings.SyncDistance;
        public static readonly Guid SettingsGuid = new Guid("b4e33d2c-0406-4aea-bf0a-d1ad04266a14");
        public static readonly List<IMyPlayer> Players = new List<IMyPlayer>();

        internal static void Init()
        {
            Log.Info("GlobalData", "Initial values set.");
        }

        internal static void UpdatePlayers()
        {
            Players.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(Players);
        }

        internal static void Unload()
        {
            Players.Clear();
            Log.Info("GlobalData", "Data cleared.");
        }
    }
}
