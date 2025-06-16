using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using VRage.Game.ModAPI;
using VRage.Game;
using VRage.Utils;

namespace DetectionEquipment.Shared.Utils
{
    public static class Log
    {
        // non-fatal exception checker
        private const int MaxExceptionsOverInterval = 10;
        private const int MaxExceptionIntervalTicks = 60;
        private static Queue<int> _exceptions;
        private static int _lastTickExceptions = 0;

        private const int MaxFileSize = 1048576;
        private static int _currentFileSize = 0;

        private static IMyModContext _modContext;
        private const string ModName = "Detection Equipment";
        private static TextWriter _writer;
        private static string _indent = "";

        public static void Init(IMyModContext context)
        {
            _exceptions = new Queue<int>(MaxExceptionIntervalTicks + 1);
            _modContext = context;
            try
            {
                string logName = ModName.Replace(" ", "");
                bool didRotateLogs = MyAPIGateway.Utilities.FileExistsInGlobalStorage(logName + ".log");
                if (didRotateLogs)
                {
                    var oldLogReader = MyAPIGateway.Utilities.ReadFileInGlobalStorage(logName + ".log");
                    string oldLogContents = oldLogReader.ReadToEnd();
                    oldLogReader.Close();
                    var oldLogWriter = MyAPIGateway.Utilities.WriteFileInGlobalStorage(logName + "_PREV.log");
                    oldLogWriter.Write(oldLogContents);
                    oldLogWriter.Flush();
                    oldLogWriter.Close();
                }

                _writer = MyAPIGateway.Utilities.WriteFileInGlobalStorage(logName + ".log");

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

                if (didRotateLogs)
                    Log.Info("Log.Init", $"Rotated previous log file to .\\{logName}_PREV.log");
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
            _exceptions = null;
            _writer?.Close();
            _writer = null;
        }

        public static void Info(string source, string text)
        {
            if (_currentFileSize > MaxFileSize)
                return;

            var toWrite = $"{DateTime.UtcNow:HH:mm:ss}\t{_indent}[INFO]\t{source}\t{text.Replace("\n", $"\n{DateTime.UtcNow:HH:mm:ss}\t{_indent}\t\t")}";
            _writer?.WriteLine(toWrite);
            _currentFileSize += toWrite.Length;

            if (_currentFileSize > MaxFileSize)
            {
                _writer?.WriteLine($"Log exceeded file size limit {MaxFileSize/1024:N0}kB - truncating all further info messages.");
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    MyAPIGateway.Utilities.ShowNotification(
                        "[Detection Equipment] Log exceeded file size limit - something has gone horribly wrong.",
                        20000, "Red");
                    MyLog.Default.WriteLineAndConsole($"[Detection Equipment] Log exceeded file size limit {MaxFileSize/1024:N0}kB - truncating all further info messages.");
                });
            }
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
            // WHY DON'T YOU CHECK CONTROL.VISIBLE???
            if (fatal && source.StartsWith("TerminalControlAdder"))
            {
                //Log.Info("Log.Exception", $"Intercepted possible Build Vision exception in {source}...");
                throw CustomCrashModContext.GenerateException(source, exception);
            }

            var toWrite = $"{DateTime.UtcNow:HH:mm:ss}\t{_indent}[{(fatal ? "FATAL " : "")}EXCEPTION]\t{source}\n{exception}";
            _writer?.WriteLine(toWrite);
            _writer?.Flush();
            MyLog.Default.WriteLineAndConsole($"[DetectionEquipment] [{(fatal ? "FATAL " : "")}EXCEPTION]\t{source}\n{exception}");
            _currentFileSize += toWrite.Length;

            if (fatal)
            {
                CustomCrashModContext.Throw(source, exception);
                return;
            }
            _lastTickExceptions += 1;
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

        public static void Update()
        {
            if (_exceptions == null)
                return;

            _exceptions.Enqueue(_lastTickExceptions);
            while (_exceptions.Count > MaxExceptionIntervalTicks)
                _exceptions.Dequeue();
            _lastTickExceptions = 0;

            int sumExceptions = 0;
            foreach (var exceptionCount in _exceptions)
                sumExceptions += exceptionCount;
            if (sumExceptions >= MaxExceptionsOverInterval)
                Log.Exception("Log.Update", new Exception($"Too many non-fatal exceptions ({sumExceptions}/{MaxExceptionsOverInterval}) in {MaxExceptionIntervalTicks/60f:N}s!"), true);
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
                Log.Info("CustomCrashModContext", "Generating custom exception message...");
                // Invoking on main thread to guarantee the hard-crash message
                MyAPIGateway.Utilities.InvokeOnGameThread(() =>
                {
                    Log.Close();
                    throw GenerateException(source, ex);
                });
            }

            public static ModCrashedException GenerateException(string source, Exception ex)
            {
                var context = new CustomCrashModContext(Log._modContext,
                    "Please reach out to @aristeas. on discord with logs for help.\n\n" +
                    "Mod: %AppData%\\SpaceEngineers\\Storage\\DetectionEquipment.log \n" +
                    "Game: %AppData%\\SpaceEngineers\\SpaceEngineers_*_*.log\n\n" +
                    $"{source}: {ex.Message}\n{ex.InnerException?.Message}\n"
                );
                return new ModCrashedException(ex, context); 
            }
        }
    }
}
