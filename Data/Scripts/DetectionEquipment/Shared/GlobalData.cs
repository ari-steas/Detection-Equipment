using DetectionEquipment.Shared.Utils;
using Sandbox.Definitions;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRage.ModAPI;
using VRage.Utils;
using static DetectionEquipment.Shared.Utils.IniConfig;

namespace DetectionEquipment.Shared
{
    public static class GlobalData
    {
        private static IniConfig _iniConfig = new IniConfig(
            FileLocation.WorldStorage,
            "config.ini",
            "General Config",
            " Detection Equipment World Settings\n\n Set config values below,\n   then restart the world.\n Delete a line to reset it to default.\n ");

        /// <summary>
        /// Kill switch for the entire mod
        /// </summary>
        public static bool Killswitch = true;
        public const ushort ServerNetworkId = 15289;
        public const ushort DataNetworkId = 15288;
        public const ushort ClientNetworkId = 15287;
        public static int MainThreadId = -1;
        public static double SyncRange => MyAPIGateway.Session.SessionSettings.SyncDistance;
        public static double SyncRangeSq => (double) MyAPIGateway.Session.SessionSettings.SyncDistance * MyAPIGateway.Session.SessionSettings.SyncDistance;
        public static readonly Guid SettingsGuid = new Guid("b4e33a2c-0406-4aea-bf0a-d1ad04266a14");
        public static readonly Guid PersistentBlockIdGuid = new Guid("385ace88-f770-4241-a02c-af63e0851c06");
        public static List<IMyPlayer> Players = new List<IMyPlayer>();
        public static IMyModContext ModContext;
        public static HashSet<string> LowRcsSubtypes;
        public static int DebugLevel = 0;
        public static List<MyPlanet> Planets = new List<MyPlanet>();

        public static string[] IgnoredEntityTypes = {
            "MyVoxelPhysics",
            "MyDebrisTree"
        };
        public static readonly MyDefinitionId ElectricityId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Electricity");
        public static readonly MyDefinitionId HydrogenId = new MyDefinitionId(typeof(MyObjectBuilder_GasProperties), "Hydrogen");

        /// <summary>
        /// Furthest distance (in meters) a radar can lock onto a target. Don't increase this too high or syncing will break.
        /// </summary>
        public static IniSetting<double> MaxSensorRange = new IniSetting<double>(
            _iniConfig,
            "MaxSensorRange",
            "Furthest distance (in meters) a radar can lock onto a target. Don't increase this too high or syncing will break.",
            150000);

        /// <summary>
        /// Furthest distance (in meters) a camera can lock onto a target. Cannot be further than MaxSensorRange.
        /// </summary>
        public static IniSetting<double> MaxVisualSensorRange = new IniSetting<double>(
            _iniConfig,
            "MaxVisualSensorRange",
            "Furthest distance (in meters) a camera can lock onto a target. Cannot be further than MaxSensorRange.",
            Math.Max(MaxSensorRange, 50000));

        /// <summary>
        /// Required for extended-range WeaponCore integration. Increases sync distance; set to 0 or a negative number to disable.
        /// </summary>
        public static IniSetting<int> OverrideSyncDistance = new IniSetting<int>(
            _iniConfig,
            "OverrideSyncDistance",
            "Required for extended-range WeaponCore integration. Increases sync distance; set to 0 or a negative number to disable.",
            (int) MaxSensorRange,
            SyncDistanceUpdated);

        /// <summary>
        /// Should aggregators be able to provide targeting to WeaponCore guns?
        /// </summary>
        public static IniSetting<bool> ContributeWcTargeting = new IniSetting<bool>(
            _iniConfig,
            "ContributeWcTargeting",
            "Should aggregators be able to provide targeting to WeaponCore guns?",
            true);

        /// <summary>
        /// Should vanilla WeaponCore magic targeting be disabled? If true, forces <see cref="ContributeWcTargeting"/> enabled.
        /// </summary>
        public static IniSetting<bool> OverrideWcTargeting = new IniSetting<bool>(
            _iniConfig,
            "OverrideWcTargeting",
            "Should vanilla WeaponCore magic targeting be disabled? If true, forces ContributeWcTargeting enabled.",
            true,
            WcTargetingUpdated);

        /// <summary>
        /// Maximum range for WeaponCore magic targeting, in meters. Only applies if OverrideWcTargeting is false. Set to zero or less to disable.
        /// </summary>
        public static IniSetting<float> MaxWcMagicTargetingRange = new IniSetting<float>(
            _iniConfig,
            "MaxWcMagicTargetingRange",
            "Maximum range for WeaponCore magic targeting, in meters. Only applies if OverrideWcTargeting is false. Set to zero or less to disable.",
            -1f);

        /// <summary>
        /// Maximum relative error at which aggregator locks should be added to WeaponCore targeting. E_r = error / distance
        /// </summary>
        public static IniSetting<float> MinLockForWcTarget = new IniSetting<float>(
            _iniConfig,
            "MinLockForWcTarget",
            "Maximum relative error at which aggregator locks should be added to WeaponCore targeting. E_r = error / distance",
            0.25f);

        /// <summary>
        /// Multiplier on all radar cross-sections for all entities.
        /// </summary>
        public static IniSetting<float> RcsModifier = new IniSetting<float>(
            _iniConfig,
            "RcsModifier",
            "Multiplier on all radar cross-sections for all entities.",
            1f);

        /// <summary>
        /// Multiplier on all visual cross-sections for all entities.
        /// </summary>
        public static IniSetting<float> VcsModifier = new IniSetting<float>(
            _iniConfig,
            "VcsModifier",
            "Multiplier on all visual cross-sections for all entities.",
            1f);

        /// <summary>
        /// Multiplier on light armor RCS for all grids.
        /// </summary>
        public static IniSetting<float> LightRcsModifier = new IniSetting<float>(
            _iniConfig,
            "LightRcsModifier",
            "Multiplier on light armor RCS for all grids.",
            0.5f);

        /// <summary>
        /// Multiplier on heavy armor RCS for all grids.
        /// </summary>
        public static IniSetting<float> HeavyRcsModifier = new IniSetting<float>(
            _iniConfig,
            "HeavyRcsModifier",
            "Multiplier on heavy armor RCS for all grids.",
            1f);

        /// <summary>
        /// Multiplier on fatblock ("functional" block) RCS for all grids.
        /// </summary>
        public static IniSetting<float> FatblockRcsModifier = new IniSetting<float>(
            _iniConfig,
            "FatblockRcsModifier",
            "Multiplier on fatblock (\"functional\" block) RCS for all grids.",
            2f);

        /// <summary>
        /// If true, IFF reflectors will always return a code regardless of their functional state.
        /// </summary>
        public static IniSetting<bool> ForceEnableIff = new IniSetting<bool>(
            _iniConfig,
            "ForceEnableIff",
            "If true, IFF reflectors will always return a code regardless of their functional state.",
            false);


        /// <summary>
        /// Multiplier on fatblock ("functional" block) RCS for all grids.
        /// </summary>
        public static IniSetting<float> WcHeatToWattConversionRatio = new IniSetting<float>(
            _iniConfig,
            "WcHeatToWattConversionRatio",
            "Conversion ratio from WC Heat to Watts for IRS, if WC is present.",
            500);

        private static void SyncDistanceUpdated(int distance)
        {
            if (OverrideSyncDistance > 0)
            {
                double prevDist = SyncRange;
                MyAPIGateway.Session.SessionSettings.SyncDistance = OverrideSyncDistance;
                Log.Info("GlobalData", $"Sync distance overriden from {prevDist / 1000d:N1}km to {OverrideSyncDistance / 1000d:N1}km.\n" + "If you're using this for WeaponCore interaction, make sure to increase MaxHudFocusDistance in the world settings!");
            }
            else
            {
                Log.Info("GlobalData", $"Sync distance: {SyncRange / 1000d:N1}km. If WeaponCore interaction is enabled, a low sync distance may cause problems!");
            }
        }

        private static void WcTargetingUpdated(bool value)
        {
            ContributeWcTargeting.Value |= OverrideWcTargeting.Value;
            if (OverrideWcTargeting.Value)
                Log.Info("GlobalData", "Overriding WC targeting, if present.");
            else if (ContributeWcTargeting.Value)
                Log.Info("GlobalData", "Contributing to WC targeting, if present.");
            else
                Log.Info("GlobalData", "Not contributing to WC targeting, if present.");
        }

        internal static bool CheckShouldLoad()
        {
            foreach (var mod in MyAPIGateway.Session.Mods)
            {
                if (mod.GetModContext().ModPath == SharedMain.I.ModContext.ModPath)
                    continue;

                if (mod.GetModContext().ModId.RemoveChars(' ').ToLower().Contains("detectionequipment"))
                {
                    Killswitch = true;
                    MyLog.Default.WriteLineAndConsole($"[Detection Equipment] Found local DetEq version \"{mod.GetPath()}\" - cancelling init and disabling mod. My ModId: {SharedMain.I.ModContext.ModId}");
                    return false;
                }
            }

            Killswitch = false;
            return true;
        }

        public static bool IsReady = false;
        internal static void Init()
        {
            Log.Info("GlobalData", "Start initialize...");
            Log.IncreaseIndent();

            if (MyAPIGateway.Session.IsServer)
            {
                _iniConfig.ReadSettings();
                _iniConfig.WriteSettings();
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(DataNetworkId, ServerMessageHandler);
                IsReady = true;
            }
            else if (!IsReady)
            {
                Log.Info("GlobalData", "Reading config data from network. Default configs will temporarily be used.");
                MyAPIGateway.Multiplayer.RegisterSecureMessageHandler(DataNetworkId, ClientMessageHandler);
                MyAPIGateway.Multiplayer.SendMessageToServer(DataNetworkId, Array.Empty<byte>());
            }

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
                MainThreadId = Environment.CurrentManagedThreadId;
                Log.Info("GlobalData", $"Main thread ID: {MainThreadId}");
            }
            
            {
                LowRcsSubtypes = new HashSet<string>();
                foreach (var definition in MyDefinitionManager.Static.GetAllDefinitions())
                {
                    MyCubeBlockDefinition block = definition as MyCubeBlockDefinition;
                    if (block == null || !block.DisplayNameText.Contains("Light Armor"))
                        continue;
                    LowRcsSubtypes.Add(block.Id.SubtypeName);
                }
                Log.Info("GlobalData", $"{LowRcsSubtypes.Count} low-RCS block definitions found.");
            }

            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                OnEntityAdd(e);
                return false;
            });

            Log.DecreaseIndent();
            Log.Info("GlobalData", "Initial values set.");
            IsReady = true;
        }

        private static void ServerMessageHandler(ushort channelId, byte[] serialized, ulong senderSteamId, bool isSenderServer)
        {
            try
            {
                Log.Info("GlobalData", $"Received data request from {senderSteamId}.");
                if (isSenderServer)
                    return;

                var file = _iniConfig.ReadFile();

                MyAPIGateway.Multiplayer.SendMessageTo(DataNetworkId, MyAPIGateway.Utilities.SerializeToBinary(file), senderSteamId);
            }
            catch (Exception ex)
            {
                Log.Exception("GlobalData", ex, true);
            }
        }

        private static void ClientMessageHandler(ushort channelId, byte[] serialized, ulong senderSteamId, bool isSenderServer)
        {
            if (!isSenderServer)
                return;

            try
            {
                var data = MyAPIGateway.Utilities.SerializeFromBinary<string>(serialized);
                if (data == null)
                {
                    Log.Info("GlobalData", "Null message!");
                    return;
                }

                Log.Info("GlobalData",
                    $"Reading settings data from network:\n===========================================\n\n{data}\n===========================================\n");

                var ini = new MyIni();
                if (!ini.TryParse(data))
                {
                    Log.Info("GlobalData", "Failed to read settings data!");
                    return;
                }

                foreach (var setting in _iniConfig.AllSettings)
                    setting.Read(ini, "General Config");

                // Can't unregister network handlers inside a network handler call
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(DataNetworkId, ClientMessageHandler);
                });
            }
            catch (Exception ex)
            {
                Log.Exception("GlobalData", ex, true);
            }
        }

        internal static void UpdatePlayers()
        {
            Players.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(Players);
        }

        internal static void Unload()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            Players = null;
            Planets = null;
            LowRcsSubtypes = null;
            if (MyAPIGateway.Session.IsServer)
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(DataNetworkId, ServerMessageHandler);
            else
                MyAPIGateway.Multiplayer.UnregisterSecureMessageHandler(DataNetworkId, ClientMessageHandler);
            _iniConfig = null;
            Log.Info("GlobalData", "Data cleared.");
        }

        private static void OnEntityAdd(IMyEntity entity)
        {
            var planet = entity as MyPlanet;
            if (planet != null)
                Planets.Add(planet);
        }
    }
}
