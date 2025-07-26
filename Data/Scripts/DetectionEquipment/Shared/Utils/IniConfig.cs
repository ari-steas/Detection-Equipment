using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRageMath;

namespace DetectionEquipment.Shared.Utils
{
    /// <summary>
    /// This must be declared before all IniSettings.
    /// </summary>
    public class IniConfig
    {
        public readonly List<IIniSetting> AllSettings = new List<IIniSetting>();

        public readonly FileLocation Location;
        public readonly string FileName, SectionName, SectionComment;

        public IniConfig(FileLocation location, string fileName, string sectionName, string sectionComment)
        {
            Location = location;
            FileName = fileName;
            SectionName = sectionName;
            SectionComment = sectionComment;
        }

        public void ReadSettings()
        {
            if (!FileExists())
            {
                Log.Info("IniConfig", "Skipped read settings data.");
                return;
            }

            Log.Info("IniConfig", $"Reading {FileName}.{SectionName} settings data...");
            Log.IncreaseIndent();
            var file = ReadFile();
            var ini = new MyIni();
            ini.TryParse(file);

            foreach (var setting in AllSettings)
            {
                try
                {
                    setting.Read(ini, SectionName);
                }
                catch (Exception ex)
                {
                    Log.Info("IniConfig", $"Failed to read {FileName}.{SectionName}.{setting.Name} - {ex}");
                }
            }
            Log.DecreaseIndent();
            Log.Info("IniConfig", $"Successfully read {FileName}.{SectionName} settings data.");
        }

        public void WriteSettings()
        {
            var ini = new MyIni();
            ini.AddSection(SectionName);
            ini.SetSectionComment(SectionName, SectionComment);
        
            foreach (var setting in AllSettings)
                setting.Write(ini, SectionName);

            WriteFile(ini.ToString());
            Log.Info("GlobalData", $"Successfully wrote {FileName}.{SectionName} settings data.");
        }

        public bool FileExists()
        {
            switch (Location)
            {
                case FileLocation.WorldStorage:
                    return MyAPIGateway.Utilities.FileExistsInWorldStorage(FileName, typeof(IniConfig));
                case FileLocation.LocalStorage:
                    return MyAPIGateway.Utilities.FileExistsInLocalStorage(FileName, typeof(IniConfig));
                case FileLocation.GlobalStorage:
                default:
                    return MyAPIGateway.Utilities.FileExistsInGlobalStorage(FileName);
            }
        }

        public string ReadFile()
        {
            TextReader reader;
            switch (Location)
            {
                case FileLocation.WorldStorage:
                    reader = MyAPIGateway.Utilities.ReadFileInWorldStorage(FileName, typeof(IniConfig));
                    break;
                case FileLocation.LocalStorage:
                    reader = MyAPIGateway.Utilities.ReadFileInLocalStorage(FileName, typeof(IniConfig));
                    break;
                case FileLocation.GlobalStorage:
                default:
                    reader = MyAPIGateway.Utilities.ReadFileInGlobalStorage(FileName);
                    break;
            }

            var result = reader.ReadToEnd();
            reader.Close();
            return result;
        }

        public void WriteFile(string data)
        {
            TextWriter writer;
            switch (Location)
            {
                case FileLocation.WorldStorage:
                    writer = MyAPIGateway.Utilities.WriteFileInWorldStorage(FileName, typeof(IniConfig));
                    break;
                case FileLocation.LocalStorage:
                    writer = MyAPIGateway.Utilities.WriteFileInLocalStorage(FileName, typeof(IniConfig));
                    break;
                case FileLocation.GlobalStorage:
                default:
                    writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage(FileName);
                    break;
            }

            writer.Write(data);
            writer.Flush();
            writer.Close();
        }

        public enum FileLocation
        {
            WorldStorage,
            LocalStorage,
            GlobalStorage
        }

        public class IniSetting<TValue> : IIniSetting
        {
            public readonly IniConfig Config;
            public string Name { get; }
            public readonly string Description;
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

            public IniSetting(IniConfig config, string name, string description, TValue value, Action<TValue> onChanged = null)
            {
                Config = config;
                Name = name;

                Description = description.TrimEnd();
                if (!Description.EndsWith("."))
                    Description += ".";
                Description += $" Default {value}";

                _value = value;
                Config.AllSettings.Add(this);
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
                if (_value is Color)
                    ini.Set(section, Name, "#" + ((Color)(object)_value).PackedValue.ToString("X8"));
                else
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
                else if (_value is Color)
                    _value = (TValue) (object) new Color(uint.Parse(ini.Get(section, Name).ToString().Substring(1), NumberStyles.HexNumber));
                else
                    throw new Exception("Invalid setting TValue " + typeof(TValue).FullName);
                _onChanged?.Invoke(_value);
            }

            public static implicit operator TValue(IniSetting<TValue> setting) => setting.Value;
        }

        public interface IIniSetting
        {
            string Name { get; }
            void Write(MyIni ini, string section);
            void Read(MyIni ini, string section);
        }
    }
}
