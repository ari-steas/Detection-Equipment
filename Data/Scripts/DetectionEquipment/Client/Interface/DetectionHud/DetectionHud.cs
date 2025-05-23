using System.Collections.Generic;
using DetectionEquipment.Shared.ExternalApis;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using VRage.Input;

namespace DetectionEquipment.Client.Interface.DetectionHud
{
    internal static class DetectionHud
    {
        private static Dictionary<long, DetectionHudItem> _hudItems;
        private static HashSet<long> _deadItems;

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
            if (MyAPIGateway.Input.IsNewKeyPressed(MyKeys.Tab))
            {
                bool hudVisible = MyAPIGateway.Session?.Config?.HudState != 0;
                foreach (var item in _hudItems)
                {
                    item.Value.SetVisible(hudVisible);
                }
            }

            foreach (var item in _hudItems)
            {
                item.Value.Update();
                _deadItems.Add(item.Key);
            }
        }

        public static void UpdateDetections(ICollection<WorldDetectionInfo> detections)
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
                    _hudItems[detection.EntityId] = new DetectionHudItem(HudMain.HighDpiRoot, detection);
                }
            }
        }

        private static void OnRichHudReady()
        {
            Log.Info("DetectionHud", "RichHud notified ready!");
        }
    }
}
