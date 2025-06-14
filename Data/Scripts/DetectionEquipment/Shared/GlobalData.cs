﻿using DetectionEquipment.Shared.Utils;
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

namespace DetectionEquipment.Shared
{
    public static class GlobalData
    {
        // this has to be above all IniSettings.
        private static List<IIniSetting> _allSettings = new List<IIniSetting>();

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
        public static bool Debug = false;
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
            "MaxSensorRange",
            "Furthest distance (in meters) a radar can lock onto a target. Don't increase this too high or syncing will break.",
            150000);

        /// <summary>
        /// Furthest distance (in meters) a camera can lock onto a target. Cannot be further than MaxSensorRange.
        /// </summary>
        public static IniSetting<double> MaxVisualSensorRange = new IniSetting<double>(
            "MaxVisualSensorRange",
            "Furthest distance (in meters) a camera can lock onto a target. Cannot be further than MaxSensorRange.",
            Math.Max(MaxSensorRange, 50000));

        /// <summary>
        /// Required for extended-range WeaponCore integration. Increases sync distance; set to 0 or a negative number to disable.
        /// </summary>
        public static IniSetting<int> OverrideSyncDistance = new IniSetting<int>(
            "OverrideSyncDistance",
            "Required for extended-range WeaponCore integration. Increases sync distance; set to 0 or a negative number to disable.",
            (int) MaxSensorRange,
            SyncDistanceUpdated);

        /// <summary>
        /// Should aggregators be able to provide targeting to WeaponCore guns?
        /// </summary>
        public static IniSetting<bool> ContributeWcTargeting = new IniSetting<bool>(
            "ContributeWcTargeting",
            "Should aggregators be able to provide targeting to WeaponCore guns?",
            true);

        /// <summary>
        /// Should vanilla WeaponCore magic targeting be disabled? If true, forces <see cref="ContributeWcTargeting"/> enabled.
        /// </summary>
        public static IniSetting<bool> OverrideWcTargeting = new IniSetting<bool>(
            "OverrideWcTargeting",
            "Should vanilla WeaponCore magic targeting be disabled? If true, forces ContributeWcTargeting enabled.",
            true,
            WcTargetingUpdated);

        /// <summary>
        /// Maximum range for WeaponCore magic targeting, in meters. Only applies if OverrideWcTargeting is false. Set to zero or less to disable.
        /// </summary>
        public static IniSetting<float> MaxWcMagicTargetingRange = new IniSetting<float>(
            "MaxWcMagicTargetingRange",
            "Maximum range for WeaponCore magic targeting, in meters. Only applies if OverrideWcTargeting is false. Set to zero or less to disable.",
            -1f);

        /// <summary>
        /// Maximum relative error at which aggregator locks should be added to WeaponCore targeting. E_r = error / distance
        /// </summary>
        public static IniSetting<float> MinLockForWcTarget = new IniSetting<float>(
            "MinLockForWcTarget",
            "Maximum relative error at which aggregator locks should be added to WeaponCore targeting. E_r = error / distance",
            0.25f);



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
                ReadSettings();
                WriteSettings();
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

                var file = MyAPIGateway.Utilities.ReadFileInWorldStorage("config.ini", typeof(GlobalData));
                var data = file.ReadToEnd();
                file.Close();

                MyAPIGateway.Multiplayer.SendMessageTo(DataNetworkId, MyAPIGateway.Utilities.SerializeToBinary(data), senderSteamId);
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

                var ini = new MyIni();
                ini.TryParse(data);

                Log.Info("GlobalData",
                    $"Read settings data from network:\n===========================================\n\n{data}\n===========================================\n");

                foreach (var setting in _allSettings)
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
            Log.Info("GlobalData", "Data cleared.");
        }

        private static void OnEntityAdd(IMyEntity entity)
        {
            var planet = entity as MyPlanet;
            if (planet != null)
                Planets.Add(planet);
        }

        #region Settings Load/Save

        private static void ReadSettings()
        {
            if (!MyAPIGateway.Utilities.FileExistsInWorldStorage("config.ini", typeof(GlobalData)))
            {
                Log.Info("GlobalData", "Skipped read settings data.");
                return;
            }

            Log.Info("GlobalData", "Reading settings data...");
            Log.IncreaseIndent();
            var file = MyAPIGateway.Utilities.ReadFileInWorldStorage("config.ini", typeof(GlobalData));
            var ini = new MyIni();
            ini.TryParse(file.ReadToEnd());
            file.Close();

            foreach (var setting in _allSettings)
                setting.Read(ini, "General Config");
            Log.DecreaseIndent();
            Log.Info("GlobalData", "Successfully read settings data.");
        }

        private static void WriteSettings()
        {
            var ini = new MyIni();
            ini.AddSection("General Config");
            ini.SetSectionComment("General Config", " Detection Equipment World Settings\n\n Set config values below,\n   then restart the world.\n Delete a line to reset it to default.\n ");
        
            foreach (var setting in _allSettings)
                setting.Write(ini, "General Config");

            var file = MyAPIGateway.Utilities.WriteFileInWorldStorage("config.ini", typeof(GlobalData));
            file.Write(ini.ToString());
            file.Flush();
            file.Close();
            Log.Info("GlobalData", "Successfully wrote settings data.");
        }

        public class IniSetting<TValue> : IIniSetting
        {
            public string Name;
            public string Description;
            private TValue _value;
            public TValue Value
            {
                get
                {
                    return _value;
                }
                set
                {
                    if (_value.Equals(value))
                        return;
                    _value = value;
                    _onChanged?.Invoke(_value);
                }
            }

            private Action<TValue> _onChanged = null;

            public IniSetting(string name, string description, TValue value, Action<TValue> onChanged = null)
            {
                Name = name;
                Description = description;
                _value = value;
                GlobalData._allSettings.Add(this);
                _onChanged = onChanged;
            }

            /// <summary>
            /// Adds and invokes an action invoked on value change.
            /// </summary>
            /// <param name="onChanged"></param>
            public void AddOnChanged(Action<TValue> onChanged)
            {
                onChanged?.Invoke(_value);
                _onChanged += onChanged;
            }

            public void Write(MyIni ini, string section)
            {
                ini.Set(section, Name, _value.ToString());
                ini.SetComment(section, Name, Description);
            }

            public void Read(MyIni ini, string section)
            {
                if (_value is string)
                    _value = (TValue) (object) ini.Get(section, Name).ToString((string) (object) _value);
                else if (_value is bool)
                    _value = (TValue) (object) ini.Get(section, Name).ToBoolean((bool) (object) _value); // the devil has a name and it is keen software house
                else if (_value is byte)
                    _value = (TValue) (object) ini.Get(section, Name).ToByte((byte) (object) _value);
                else if (_value is char)
                    _value = (TValue) (object) ini.Get(section, Name).ToChar((char) (object) _value);
                else if (_value is decimal)
                    _value = (TValue) (object) ini.Get(section, Name).ToDecimal((decimal) (object) _value);
                else if (_value is double)
                    _value = (TValue) (object) ini.Get(section, Name).ToDouble((double) (object) _value);
                else if (_value is short)
                    _value = (TValue) (object) ini.Get(section, Name).ToInt16((short) (object) _value);
                else if (_value is int)
                    _value = (TValue) (object) ini.Get(section, Name).ToInt32((int) (object) _value);
                else if (_value is long)
                    _value = (TValue) (object) ini.Get(section, Name).ToInt64((long) (object) _value);
                else if (_value is sbyte)
                    _value = (TValue) (object) ini.Get(section, Name).ToSByte((sbyte) (object) _value);
                else if (_value is float)
                    _value = (TValue) (object) ini.Get(section, Name).ToSingle((float) (object) _value);
                else if (_value is ushort)
                    _value = (TValue) (object) ini.Get(section, Name).ToUInt16((ushort) (object) _value);
                else if (_value is uint)
                    _value = (TValue) (object) ini.Get(section, Name).ToUInt32((uint) (object) _value);
                else if (_value is ulong)
                    _value = (TValue) (object) ini.Get(section, Name).ToUInt64((ulong) (object) _value);
                else
                    throw new Exception("Invalid setting TValue " + typeof(TValue).FullName);
                _onChanged?.Invoke(_value);
            }

            public static implicit operator TValue(IniSetting<TValue> setting) => setting.Value;
        }

        private interface IIniSetting
        {
            void Write(MyIni ini, string section);
            void Read(MyIni ini, string section);
        }

        #endregion
    }
}
