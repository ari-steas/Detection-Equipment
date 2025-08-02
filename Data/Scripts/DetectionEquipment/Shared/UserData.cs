using System.Linq;
using DetectionEquipment.Client.Interface.DetectionHud;
using DetectionEquipment.Shared.ExternalApis;
using DetectionEquipment.Shared.Utils;
using RichHudFramework.Client;
using RichHudFramework.UI;
using RichHudFramework.UI.Rendering;
using RichHudFramework.UI.Rendering.Client;
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

        public static IniConfig.IniSetting<float> HudSensorInfoX = new IniConfig.IniSetting<float>(
            _config,
            "HudSensorInfoX",
            "X-offset for HUD sensor info panel. Scaled by 1920x1080.",
            0
        );

        public static IniConfig.IniSetting<float> HudSensorInfoY = new IniConfig.IniSetting<float>(
            _config,
            "HudSensorInfoY",
            "Y-offset for HUD sensor info panel. Scaled by 1920x1080.",
            0
        );

        public static GlyphFormat StandardFont => new GlyphFormat(HudTextColor, TextAlignment.Left, 0.75f, new Vector2I(1 /*mono*/, (int) FontStyles.Regular));

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
