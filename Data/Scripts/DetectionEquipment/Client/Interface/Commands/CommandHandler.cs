using System;
using System.Collections.Generic;
using System.Text;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Utils;
using RichHudFramework.Internal;
using Sandbox.ModAPI;

namespace DetectionEquipment.Client.Interface.Commands
{
    /// <summary>
    ///     Parses commands from chat and triggers relevant methods.
    /// </summary>
    public class CommandHandler
    {
        public static CommandHandler I;

        private readonly Dictionary<string, Command> _commands = new Dictionary<string, Command>
        {
            ["help"] = new Command(
                "DetEq",
                "Displays command help.",
                message => I.ShowHelp()),

            #region Util Commands

            ["debug"] = new Command(
                "DetEq",
                "Sets debug mode level. Most useful on localhost. Takes integer level as an optional argument, where a higher level provides more debug info.",
                CommandMethods.ToggleDebug),

            #endregion
        };

        private CommandHandler()
        {
        }

        private void ShowHelp()
        {
            var helpBuilder = new StringBuilder();
            var modNames = new List<string>();
            foreach (var command in _commands.Values)
                if (!modNames.Contains(command.ModName))
                    modNames.Add(command.ModName);

            MyAPIGateway.Utilities.ShowMessage("Detection Equipment Help", "");

            foreach (var modName in modNames)
            {
                foreach (var command in _commands)
                    if (command.Value.ModName == modName)
                        helpBuilder.Append($"\n{{/de {command.Key}}}: " + command.Value.HelpText);

                MyAPIGateway.Utilities.ShowMessage($"[{modName}]", helpBuilder + "\n");
                helpBuilder.Clear();
            }
        }

        public static void Init()
        {
            Log.Info("CommandHandler", "Initializing...");
            Close(); // Close existing command handlers.
            I = new CommandHandler();
            MyAPIGateway.Utilities.MessageEnteredSender += I.Command_MessageEnteredSender;
            MyAPIGateway.Utilities.ShowMessage("DetEq",
                "Run \"/de help\" for commands.");

            Log.IncreaseIndent();
            foreach (var command in I._commands.Keys)
                Log.Info("CommandHandler", $"Registered internal chat command \"/de {command}\".");
            Log.DecreaseIndent();
            Log.Info("CommandHandler", "Ready.");
        }

        public static void Close()
        {
            if (I != null)
            {
                MyAPIGateway.Utilities.MessageEnteredSender -= I.Command_MessageEnteredSender;
                I._commands.Clear();
            }

            I = null;
            Log.Info("CommandHandler", "Closed.");
        }

        private void Command_MessageEnteredSender(ulong sender, string messageText, ref bool sendToOthers)
        {
            try
            {
                // Only register for commands
                if (messageText.Length == 0 || !messageText.ToLower().StartsWith("/de "))
                    return;

                sendToOthers = false;

                var parts = messageText.Substring(4).Trim(' ').Split(' '); // Convert commands to be more parseable

                if (parts[0] == "")
                {
                    ShowHelp();
                    return;
                }

                var command = parts[0].ToLower();

                // Really basic command handler
                if (_commands.ContainsKey(command))
                    _commands[command].Action.Invoke(parts);
                else
                    MyAPIGateway.Utilities.ShowMessage("Detection Equipment",
                        $"Unrecognized command \"{command}\".");
            }
            catch (Exception ex)
            {
                Log.Exception("CommandHandler", ex, true);
            }
        }

        /// <summary>
        ///     Registers a command for Universal Gamemodes' command handler.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="action"></param>
        /// <param name="modName"></param>
        public static void AddCommand(string command, string helpText, Action<string[]> action,
            string modName = "Detection Equipment")
        {
            if (I == null)
                return;

            command = command.ToLower();
            if (I._commands.ContainsKey(command))
            {
                Log.Exception("CommandHandler", new Exception("Attempted to add duplicate command " + command + " from [" + modName + "]"));
                return;
            }

            I._commands.Add(command, new Command(modName, helpText, action));
            Log.Info("CommandHandler", $"Registered new chat command \"!{command}\" from [{modName}]");
        }

        /// <summary>
        ///     Removes a command from Universal Gamemodes' command handler.
        /// </summary>
        /// <param name="command"></param>
        public static void RemoveCommand(string command)
        {
            command = command.ToLower();
            if (I == null || command == "help" || command == "debug") // Debug and Help should never be removed.
                return;
            if (I._commands.Remove(command))
                Log.Info("CommandHandler", $"De-registered chat command \"!{command}\".");
        }

        private class Command
        {
            public readonly Action<string[]> Action;
            public readonly string HelpText;
            public readonly string ModName;

            public Command(string modName, string helpText, Action<string[]> action)
            {
                ModName = modName;
                HelpText = helpText;
                Action = action;
            }
        }
    }
}
