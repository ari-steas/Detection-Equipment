using DetectionEquipment.Shared.Utils;
using Sandbox.Definitions;
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
        public static string[] LowRcsSubtypes;
        public static float MinLockForWcTarget = 1.0f;

        internal static void Init()
        {
            ModContext = SharedMain.I.ModContext;

            {
                var lowRcsBlocksBuffer = new List<string>();
                foreach (var definition in MyDefinitionManager.Static.GetAllDefinitions())
                {
                    MyCubeBlockDefinition block = definition as MyCubeBlockDefinition;
                    if (block == null || !block.DisplayNameText.Contains("Light Armor"))
                        continue;
                    lowRcsBlocksBuffer.Add(block.Id.SubtypeName);
                }
                LowRcsSubtypes = lowRcsBlocksBuffer.ToArray();
                Log.Info("GlobalData", $"{LowRcsSubtypes.Length} low-RCS block definitions found.");
            }

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
