using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;

namespace DetectionEquipment.Client.Interface.Commands
{
    internal static class CommandMethods
    {
        #region Utils

        public static void ToggleDebug(string[] args)
        {
            int level;
            if (args.Length < 2 || !int.TryParse(args[1], out level))
            {
                GlobalData.DebugLevel = GlobalData.DebugLevel > 0 ? 0 : 1;
            }
            else
            {
                GlobalData.DebugLevel = level;
            }

            var infoStr = $"Set debug mode to LEVEL {GlobalData.DebugLevel}.";
            Log.Info("CommandHandler", infoStr);
            MyAPIGateway.Utilities.ShowMessage("DetEq", infoStr);
        }

        #endregion
    }
}