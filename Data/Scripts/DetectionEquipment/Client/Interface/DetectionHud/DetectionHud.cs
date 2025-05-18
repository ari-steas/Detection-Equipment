using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.ExternalApis;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using RichHudFramework;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Client.Interface.DetectionHud
{
    internal static class DetectionHud
    {
        private static Dictionary<long, DetectionHudItem> _hudItems;

        public static void Init()
        {
            _hudItems = new Dictionary<long, DetectionHudItem>();
            ApiManager.RichHudOnLoadRegisterOrInvoke(OnRichHudReady);
            Log.Info("DetectionHud", "Initialized.");
        }

        public static void Close()
        {
            _hudItems = null;
            Log.Info("DetectionHud", "Closed.");
        }

        public static void Draw()
        {
            foreach (var item in _hudItems.Values)
                item.Update();
        }

        public static void UpdateDetections(ICollection<WorldDetectionInfo> detections)
        {
            // TODO cache these
            var deadItems = new HashSet<long>(_hudItems.Keys);
            foreach (var detection in detections)
            {
                if (_hudItems.ContainsKey(detection.EntityId))
                {
                    _hudItems[detection.EntityId].Update(detection);
                    deadItems.Remove(detection.EntityId);
                }
                else
                {
                    _hudItems[detection.EntityId] = new DetectionHudItem(HudMain.HighDpiRoot, detection);
                }
            }

            foreach (var deadItem in deadItems)
            {
                HudMain.HighDpiRoot.RemoveChild(_hudItems[deadItem]);
                _hudItems.Remove(deadItem);
            }
        }

        private static void OnRichHudReady()
        {
            Log.Info("DetectionHud", "RichHud notified ready!");
        }
    }
}
