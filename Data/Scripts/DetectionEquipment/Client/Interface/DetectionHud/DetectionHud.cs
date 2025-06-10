using System.Collections.Generic;
using DetectionEquipment.Shared.ExternalApis;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using VRage.Input;
using VRageMath;

namespace DetectionEquipment.Client.Interface.DetectionHud
{
    internal static class DetectionHud
    {
        private static Dictionary<long, DetectionHudItem> _hudItems;
        private static HashSet<long> _deadItems;

        private static bool _alwaysShow = false;

        public static bool AlwaysShow
        {
            get
            {
                return _alwaysShow;
            }
            set
            {
                if (value == _alwaysShow)
                    return;
                _alwaysShow = value;
                UpdateVisible(_alwaysShow ? 1 : MyAPIGateway.Session?.Config?.HudState ?? 1);
            }
        }
        public static float CombineAngle = (float) MathHelper.ToRadians(2.5); // TODO make this do anything

        public static void Init()
        {
            _hudItems = new Dictionary<long, DetectionHudItem>();
            _deadItems = new HashSet<long>();
            ApiManager.RichHudOnLoadRegisterOrInvoke(OnRichHudReady);
            Log.Info("DetectionHud", "Initialized.");
        }

        public static void Close()
        {
            _hudItems = null;
            _deadItems = null;
            Log.Info("DetectionHud", "Closed.");
        }

        public static void UpdateAfterSimulation()
        {
            // Remove hud items not updated in the last tick.
            foreach (var deadItem in _deadItems)
            {
                HudMain.HighDpiRoot.RemoveChild(_hudItems[deadItem]);
                _hudItems.Remove(deadItem);
            }
            _deadItems.Clear();
        }

        public static void Draw()
        {
            // Pulling the current HudState is SLOOOOWWWW, so we only pull it when tab is just pressed.
            if (!AlwaysShow && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.Tab))
                UpdateVisible(MyAPIGateway.Session?.Config?.HudState ?? 1);

            foreach (var item in _hudItems)
            {
                if (_visible != 0)
                    item.Value.Update();
                _deadItems.Add(item.Key);
            }
        }

        private static int _visible = MyAPIGateway.Session?.Config?.HudState ?? 1;
        private static void UpdateVisible(int visible)
        {
            _visible = visible;
            foreach (var item in _hudItems)
            {
                item.Value.SetVisible(_visible);
            }
        }

        public static void UpdateDetections(ICollection<HudDetectionInfo> detections)
        {
            foreach (var detection in detections)
            {
                if (_hudItems.ContainsKey(detection.EntityId))
                {
                    _hudItems[detection.EntityId].Update(detection);
                    _deadItems.Remove(detection.EntityId);
                }
                else
                {
                    _hudItems[detection.EntityId] = new DetectionHudItem(HudMain.HighDpiRoot, detection, _visible);
                }
            }
        }

        private static void OnRichHudReady()
        {
            Log.Info("DetectionHud", "RichHud notified ready!");
        }
    }
}
