using System;
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
            /* ** SUBJECT TO CHANGE - LAST UPDATED 7/10/2025 **
             * - 1+ is extra logging
             * - 2+ is some visual info from aggregators and sensors
             * - 3+ is light info from V/RCS calcs (hit normals, projected grid bounds)
             * - 4+ is full V/RCS calc info (cellcast lines)
             */

            int level;
            if (args.Length < 2 || !int.TryParse(args[1], out level))
            {
                GlobalData.DebugLevel = GlobalData.DebugLevel > 0 ? 0 : 1;
            }
            else
            {
                GlobalData.DebugLevel = Math.Abs(level);
            }

            var infoStr = GlobalData.DebugLevel == 0 ? "Disabled debug mode." : $"Set debug mode to LEVEL {GlobalData.DebugLevel}.";
            Log.Info("CommandHandler", infoStr);
            MyAPIGateway.Utilities.ShowMessage("DetEq", infoStr);
        }

        #endregion
    }
}