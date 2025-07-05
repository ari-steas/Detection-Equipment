using DetectionEquipment.Client.Interface.DetectionHud;
using DetectionEquipment.Shared.Utils;
using VRageMath;

namespace DetectionEquipment.Shared
{
    internal class UserData
    {
        private static IniConfig _config = new IniConfig(
            IniConfig.FileLocation.LocalStorage,
            "userconfig.ini",
            "User Config",
            " Detection Equipment User Settings\n\n Set config values below,\n   then restart the world.\n Delete a line to reset it to default.\n "
            );

        public static IniConfig.IniSetting<Color> HudFriendlyColor = new IniConfig.IniSetting<Color>(
            _config,
            "HudFriendlyColor",
            "Outline color for friendly tracks.",
            Color.Green,
            DetectionHud.UpdateColors
            );

        public static IniConfig.IniSetting<Color> HudNeutralColor = new IniConfig.IniSetting<Color>(
            _config,
            "HudNeutralColor",
            "Outline color for neutral tracks.",
            Color.White,
            DetectionHud.UpdateColors
            );

        public static IniConfig.IniSetting<Color> HudEnemyColor = new IniConfig.IniSetting<Color>(
            _config,
            "HudEnemyColor",
            "Outline color for enemy tracks.",
            Color.Red,
            DetectionHud.UpdateColors
        );

        public static IniConfig.IniSetting<Color> HudTextColor = new IniConfig.IniSetting<Color>(
            _config,
            "HudTextColor",
            "Track info text color.",
            Color.Lime,
            DetectionHud.UpdateColors
        );

        public static void Init()
        {
            _config.ReadSettings();
            _config.WriteSettings();
        }

        public static void Unload()
        {
            _config.WriteSettings();
            _config = null;
        }
    }
}
