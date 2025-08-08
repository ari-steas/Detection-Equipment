using System;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Helpers;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game;
using Sandbox.ModAPI;

namespace DetectionEquipment.Client.Interface.Commands
{
    internal static class CommandMethods
    {
        #region Utils

        public static void ToggleDebug(string[] args)
        {
            /* ** SUBJECT TO CHANGE - LAST UPDATED 7/11/2025 **
             * - 1+ is extra logging
             * - 2+ is some visual info from aggregators and sensors, LOS check info lines & hit pos
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

        public static void TestHashing(string[] args)
        {
            int numSaltPairs;
            if (args.Length < 1 || !int.TryParse(args[0], out numSaltPairs))
                numSaltPairs = 4;

            IffHelper.TestHashing(numSaltPairs);
            MyAPIGateway.Utilities.ShowMessage("DetEq", "Finished hash testing - results sent to log file.");
        }

        #endregion

        #region Info

        public static void ShowGit(string[] args)
        {
            MyVisualScriptLogicProvider.OpenSteamOverlayLocal(@"https://steamcommunity.com/linkfilter/?url=https://github.com/ari-steas/Detection-Equipment"); //hey dumbass, use this before the url. fucking keen https://steamcommunity.com/linkfilter/?url={url}
        }

        public static void ShowReport(string[] args)
        {
            MyVisualScriptLogicProvider.OpenSteamOverlayLocal(@"https://steamcommunity.com/linkfilter/?url=https://github.com/ari-steas/Detection-Equipment/issues/new/choose"); //hey dumbass, use this before the url. fucking keen https://steamcommunity.com/linkfilter/?url={url}
        }

        public static void ShowWorkshop(string[] args)
        {
            MyVisualScriptLogicProvider.OpenSteamOverlayLocal(@"https://steamcommunity.com/workshop/filedetails/?id=3476173167");
        }

        #endregion
    }
}