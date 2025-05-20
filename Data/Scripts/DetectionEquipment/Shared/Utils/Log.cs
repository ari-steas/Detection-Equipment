using Sandbox.ModAPI;
using System;
using System.IO;
using VRage.Game.ModAPI;
using VRage.Game;
using VRage.Utils;

namespace DetectionEquipment.Shared.Utils
{
    public static class Log
    {
        private static IMyModContext _modContext;
        private const string ModName = "Detection Equipment";
        private static TextWriter _writer;
        private static string _indent = "";

        public static void Init(IMyModContext context)
        {
            _modContext = context;
            try
            {
                _writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage(ModName.Replace(" ", "") + ".log");

                int utcOffset = (DateTime.Now - DateTime.UtcNow).Hours;

                _writer.WriteLine($"{ModName} Debug Log");
                _writer.WriteLine( "  by Aristeas");
                _writer.WriteLine($"Local DateTime: {DateTime.Now:G} (UTC {(utcOffset > 0 ? "+" : "")}{utcOffset:00}:{(DateTime.Now - DateTime.UtcNow).Minutes:00})");
                _writer.WriteLine( "");
                _writer.WriteLine($"Space Engineers v{MyAPIGateway.Session?.Version}");
                _writer.WriteLine($"Server: {MyAPIGateway.Session?.IsServer} | Client: {!MyAPIGateway.Utilities.IsDedicated}");
                _writer.WriteLine($"Session: {MyAPIGateway.Session?.Name ?? "MultiplayerSession"} | Client Info: {(string.IsNullOrEmpty(MyAPIGateway.Multiplayer?.MyName) ? null : MyAPIGateway.Multiplayer?.MyName) ?? "DedicatedHost"}::{MyAPIGateway.Multiplayer?.MyId}");
                _writer.WriteLine("=================================================");
                _writer.Flush();
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to open log writer - did the previous session unload properly?", ex);
            }

            MyLog.Default.WriteLineAndConsole($@"[{ModName}] - Debug log can be found in %AppData%\Roaming\SpaceEngineers\Storage\DetectionEquipment.log");
        }

        public static void Close()
        {
            Info("Log", "Unloaded.");
            _writer?.Close();
            _writer = null;
        }

        public static void Info(string source, string text)
        {
            _writer?.WriteLine($"{DateTime.UtcNow:HH:mm:ss}\t{_indent}[INFO]\t{source}\t{text.Replace("\n", $"\n{DateTime.UtcNow:HH:mm:ss}\t{_indent}\t\t")}");
            _writer?.Flush();
            //if (MyAPIGateway.Utilities.IsDedicated)
            //    MyLog.Default.WriteLineToConsole($"{_indent}[INFO]\t{source}\t{text}");
        }

        /// <summary>
        /// Logs an exception to the debug log.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="exception"></param>
        /// <param name="fatal"></param>
        public static void Exception(string source, Exception exception, bool fatal = false)
        {
            _writer?.WriteLine($"{DateTime.UtcNow:HH:mm:ss}\t{_indent}[{(fatal ? "FATAL " : "")}EXCEPTION]\t{source}\n{exception}");
            _writer?.Flush();
            if (MyAPIGateway.Utilities.IsDedicated)
                MyLog.Default.WriteLineToConsole($"{source}\n{exception.Message}\n{exception.StackTrace}");
            if (fatal)
                CustomCrashModContext.Throw(source, exception);
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

        private class CustomCrashModContext : IMyModContext
        {
            public string ModName { get; }
            public string ModId { get; }
            public string ModServiceName { get; }
            public string ModPath { get; }
            public string ModPathData { get; }
            public bool IsBaseGame { get; }
            public MyObjectBuilder_Checkpoint.ModItem ModItem { get; }

            private CustomCrashModContext(IMyModContext context, string customInfo)
            {
                // we really don't want to throw any exceptions early
                if (context == null)
                {
                    ModName = Log.ModName;
                    ModId = "HANDLER FAIL";
                    ModServiceName = "HANDLER FAIL)\n" + customInfo + "\n\n\n\n\n\n\n\n\n\n\n\n\n\n";
                    ModPath = "HANDLER FAIL";
                    ModPathData = "HANDLER FAIL";
                    IsBaseGame = false;
                    ModItem = new MyObjectBuilder_Checkpoint.ModItem();

                    return;
                }

                ModName = context.ModName;
                ModId = context.ModId;
                ModServiceName = $"{context.ModId})\n" + customInfo + "\n\n\n\n\n\n\n\n\n\n\n\n\n\n";
                ModPath = context.ModPath;
                ModPathData = context.ModPathData;
                IsBaseGame = context.IsBaseGame;
                ModItem = context.ModItem;
            }

            public static void Throw(string source, Exception ex)
            {
                Log.Info("CustomCrashModContext", "Generating custom exception message and closing log...");
                Log.Close();
                var context = new CustomCrashModContext(Log._modContext,
                    "Please reach out to @aristeas. on discord with logs for help.\n\n" +
                    "Mod: %AppData%\\SpaceEngineers\\Storage\\DetectionEquipment.log \n" +
                    "Game: %AppData%\\SpaceEngineers\\SpaceEngineers_*_*.log\n\n" +
                    $"{source}: {ex.Message}\n{ex.InnerException?.Message}\n"
                    );
                // Invoking on main thread to guarantee the hard-crash message
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    throw new ModCrashedException(ex, context);
                });
            }
        }
    }
}
