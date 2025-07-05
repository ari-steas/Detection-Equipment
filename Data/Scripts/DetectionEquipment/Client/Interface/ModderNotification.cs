using DetectionEquipment.Shared.Utils;
using Sandbox.Game;
using Sandbox.ModAPI;

namespace DetectionEquipment.Client.Interface
{
    internal static class ModderNotification
    {
        private static int _displayTime = 1500;
        private static bool _isDisplaying = false;

        public static void Init()
        {
            if (MyAPIGateway.Utilities.FileExistsInWorldStorage("showedmodnotif", typeof(ModderNotification)))
                return;

            foreach (var mod in MyAPIGateway.Session.Mods)
            {
                if (mod.PublishedServiceName == "Steam" && !mod.Name.EndsWith(".sbm"))
                {
                    Log.Info("ModderNotification", $"Detected local mod \"{mod.Name}\" - displaying modder info.");
                    _isDisplaying = true;
                    MyAPIGateway.Utilities.WriteFileInWorldStorage("showedmodnotif", typeof(ModderNotification)).Close();
                    break;
                }
            }
        }

        public static void Hide()
        {
            _displayTime = 0;
        }

        public static void Update()
        {
            if (!_isDisplaying) return;
            if (_displayTime-- <= 0)
            {
                MyVisualScriptLogicProvider.SetQuestlogLocal(false, "Detection Equipment - Modder Info");
                _isDisplaying = false;
                return;
            }

            MyVisualScriptLogicProvider.SetQuestlogLocal(true,
                $"Detection Equipment - Modder Info");

            MyVisualScriptLogicProvider.AddQuestlogDetailLocal(
                "Hey, modder! In case this is your first time modding for DetEq:\n" +
                "Guide: https://github.com/ari-steas/Detection-Equipment\n" +
                "Logs: %AppData%\\Space Engineers\\Storage\\DetectionEquipment.log\n\n" +
                "Best of luck, and feel free to reach out if you have any questions!\n" +
                "- Aristeas",
                false, false);
        }
    }
}
