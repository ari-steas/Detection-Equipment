using Sandbox.ModAPI;
using System;
using System.IO;
using VRage.Utils;

namespace DetectionEquipment.Shared.Utils
{
    public class Log
    {
        private static TextWriter _writer;
        private static string _indent = "";

        public static void Init()
        {
            _writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage("DetectionEquipment.log");
            int utcOffset = (DateTime.Now - DateTime.UtcNow).Hours;

            _writer.WriteLine($"DetectionEquipment Debug Log");
            _writer.WriteLine($"Local DateTime: {DateTime.Now:G} (UTC {(utcOffset > 0 ? "+" : "")}{utcOffset:00}:{(DateTime.Now - DateTime.UtcNow).Minutes:00})");
            _writer.WriteLine($"");
            _writer.WriteLine($"Space Engineers v{MyAPIGateway.Session?.Version}");
            _writer.WriteLine($"Server: {MyAPIGateway.Session?.IsServer} | Client: {!MyAPIGateway.Utilities.IsDedicated}");
            _writer.WriteLine($"Session: {MyAPIGateway.Session?.Name ?? "MultiplayerSession"} | Client Info: {(MyAPIGateway.Multiplayer?.MyName == "" ? null : MyAPIGateway.Multiplayer.MyName) ?? "DedicatedHost"}::{MyAPIGateway.Multiplayer.MyId}");
            _writer.WriteLine("=================================================");
            _writer.Flush();

            MyLog.Default.WriteLineAndConsole("[DetectionEquipment] - Debug log can be found in %AppData%\\Roaming\\SpaceEngineers\\Storage\\DetectionEquipment.log");
        }

        public static void Close()
        {
            Info("Log", "Unloaded.");
            _writer?.Close();
            _writer = null;
        }

        public static void Info(string source, string text)
        {
            _writer?.WriteLine($"{DateTime.UtcNow:HH:mm:ss}\t{_indent}[INFO]\t{source}\t{text}");
            _writer?.Flush();
            //if (MyAPIGateway.Utilities.IsDedicated)
            //    MyLog.Default.WriteLineToConsole($"{_indent}[INFO]\t{source}\t{text}");
        }

        public static void Exception(string source, Exception exception)
        {
            _writer?.WriteLine($"{DateTime.UtcNow:HH:mm:ss}\t{_indent}[EXCEPTION]\t{source}\n{exception}");
            _writer?.Flush();
            if (MyAPIGateway.Utilities.IsDedicated)
                MyLog.Default.WriteLineToConsole($"{source}\n{exception}");
        }

        public static void IncreaseIndent()
        {
            _indent += '\t';
        }

        public static void DecreaseIndent()
        {
            if (_indent.Length > 0)
                _indent = _indent.Remove(_indent.Length - 1);
        }
    }
}
