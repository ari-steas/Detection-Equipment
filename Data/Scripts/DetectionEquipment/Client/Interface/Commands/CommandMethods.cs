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
            GlobalData.Debug = !GlobalData.Debug;
            var infoStr = $"{(GlobalData.Debug ? "Enabled" : "Disabled")} debug mode.";
            Log.Info("CommandHandler", infoStr);
            MyAPIGateway.Utilities.ShowMessage("DetEq", infoStr);
        }

        #endregion
    }
}