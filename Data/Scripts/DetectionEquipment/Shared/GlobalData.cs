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
        // TODO: Make some of these configurable.
        public const ushort ServerNetworkId = 15289;
        public const ushort ClientNetworkId = 15287;
        public static double SyncRange => MyAPIGateway.Session.SessionSettings.SyncDistance;
        public static double SyncRangeSq => MyAPIGateway.Session.SessionSettings.SyncDistance * MyAPIGateway.Session.SessionSettings.SyncDistance;
        public static readonly Guid SettingsGuid = new Guid("b4e33a2c-0406-4aea-bf0a-d1ad04266a14");
        public static readonly List<IMyPlayer> Players = new List<IMyPlayer>();
        public static IMyModContext ModContext;
        public static string[] LowRcsSubtypes;

        /// <summary>
        /// Furthest distance (in meters) a radar can lock onto a target. Don't increase this too high or syncing will break.
        /// </summary>
        public static double MaxSensorRange = 150000;
        /// <summary>
        /// Furthest distance (in meters) a camera can lock onto a target. Cannot be further than MaxSensorRange.
        /// </summary>
        public static double MaxVisualSensorRange = Math.Max(MaxSensorRange, 50000);
        /// <summary>
        /// Required for extended-range WeaponCore integration. Increases sync distance to <see cref="MaxSensorRange"/>.
        /// </summary>
        public static bool OverrideSyncDistance = true;
        /// <summary>
        /// Should aggregators be able to provide targeting to WeaponCore guns?
        /// </summary>
        public static bool ContributeWcTargeting = true;
        /// <summary>
        /// Should vanilla WeaponCore magic targeting be disabled? If true, forces <see cref="ContributeWcTargeting"/> enabled.
        /// </summary>
        public static bool OverrideWcTargeting = true;
        /// <summary>
        /// Maximum relative error at which aggregator locks should be added to WeaponCore targeting. E_r = error / distance
        /// </summary>
        public static float MinLockForWcTarget = 1.0f;

        internal static void Init()
        {
            Log.Info("GlobalData", "Start initialize...");
            Log.IncreaseIndent();

            {
                ModContext = SharedMain.I.ModContext;
                string modId = ModContext.ModId.Replace(".sbm", "");
                long discard;

                Log.Info("GlobalData", "ModContext:\n" +
                                       $"\tName: {ModContext.ModName}\n" +
                                       $"\tItem: {(long.TryParse(modId, out discard) ? "https://steamcommunity.com/workshop/filedetails/?id=" : "LocalMod ")}{modId}\n" +
                                       $"\tService: {ModContext.ModServiceName} (if this isn't steam, please report the mod)");
            }
            

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

            if (OverrideSyncDistance)
            {
                double prevDist = SyncRange;
                MyAPIGateway.Session.SessionSettings.SyncDistance = (int)Math.Abs(MaxSensorRange);
                Log.Info("GlobalData", $"Sync distance overriden from {prevDist/1000d:N1}km to {Math.Abs(MaxSensorRange)/1000d:N1}km.\n" +
                                       "If you're using this for WeaponCore interaction, make sure to increase MaxHudFocusDistance in the world settings!");
            }
            else
            {
                Log.Info("GlobalData", $"Sync distance: {SyncRange/1000d:N1}km. If WeaponCore interaction is enabled, a low sync distance may cause problems!");
            }

            {
                ContributeWcTargeting |= OverrideWcTargeting;
                if (OverrideWcTargeting)
                    Log.Info("GlobalData", "Overriding WC targeting, if present.");
                else if (ContributeWcTargeting)
                    Log.Info("GlobalData", "Contributing to WC targeting, if present.");
                else
                    Log.Info("GlobalData", "Not contributing to WC targeting, if present.");
            }

            Log.DecreaseIndent();
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
