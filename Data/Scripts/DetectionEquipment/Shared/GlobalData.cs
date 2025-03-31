using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared
{
    public static class GlobalData
    {
        public const ushort ServerNetworkId = 15289;
        public const ushort ClientNetworkId = 15287;
        public static readonly double SyncRange = MyAPIGateway.Session.SessionSettings.SyncDistance;
        public static readonly double SyncRangeSq = MyAPIGateway.Session.SessionSettings.SyncDistance * MyAPIGateway.Session.SessionSettings.SyncDistance;
        public static readonly Guid SettingsGuid = new Guid("b4e33a2c-0406-4aea-bf0a-d1ad04266a14");
        public static readonly List<IMyPlayer> Players = new List<IMyPlayer>();
        public static IMyModContext ModContext;

        internal static void Init()
        {
            ModContext = SharedMain.I.ModContext;
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
