using DetectionEquipment.Shared.BlockLogic.HudController;
using DetectionEquipment.Shared.ExternalApis;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using System.Collections.Generic;
using DetectionEquipment.Shared;
using VRage.Input;
using VRageMath;

namespace DetectionEquipment.Client.Interface.DetectionHud
{
    internal static class DetectionHud
    {
        private static Dictionary<long, DetectionHudItem> _hudItems;
        private static HashSet<HudControllerBlock> _hudControllers;
        private static HashSet<HudControllerBlock> _deadHudControllers;
        private static HashSet<long> _deadItems;

        private static SensorInfoPanel _sensorPanel = null;

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
            _hudControllers = new HashSet<HudControllerBlock>();
            _deadHudControllers = new HashSet<HudControllerBlock>();
            ApiManager.RichHudOnLoadRegisterOrInvoke(OnRichHudReady);
            Log.Info("DetectionHud", "Initialized.");
        }

        public static void Close()
        {
            _hudItems = null;
            _deadItems = null;
            _hudControllers = null;
            _deadHudControllers = null;
            _sensorPanel = null;
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

            foreach (var deadController in _deadHudControllers)
            {
                _hudControllers.Remove(deadController);
                _sensorPanel.RemoveController(deadController);
            }
            _deadHudControllers.Clear();
        }

        public static void Draw()
        {
            // Pulling the current HudState is SLOOOOWWWW, so we only pull it when tab is just pressed.
            if (!AlwaysShow && MyAPIGateway.Input.IsNewKeyPressed(MyKeys.Tab))
                UpdateVisible(MyAPIGateway.Session?.Config?.HudState ?? 1);

            _sensorPanel?.UpdateDraw();

            foreach (var item in _hudItems)
            {
                if (_visible != 0)
                    item.Value.Update();
                _deadItems.Add(item.Key);
            }

            foreach (var controller in _hudControllers)
                _deadHudControllers.Add(controller);
        }

        private static int _visible = MyAPIGateway.Session?.Config?.HudState ?? 1;
        private static void UpdateVisible(int visible)
        {
            _visible = visible;
            foreach (var item in _hudItems)
            {
                item.Value.SetVisible(_visible);
            }

            if (_sensorPanel != null)
                _sensorPanel.Visible = _visible == 1;
        }

        public static void UpdateDetections(HudControllerBlock controller, ICollection<HudDetectionInfo> detections)
        {
            if (_hudControllers.Add(controller))
                _sensorPanel?.AddController(controller);
            _deadHudControllers.Remove(controller);
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

        public static void UpdateColors(Color color)
        {
            if (_hudItems == null)
                return;
            foreach (var hudItem in _hudItems)
                hudItem.Value.Update(hudItem.Value.Detection);
            _sensorPanel?.UpdateColor(UserData.HudTextColor);
        }

        private static void OnRichHudReady()
        {
            _sensorPanel = new SensorInfoPanel(HudMain.HighDpiRoot);
            foreach (var controller in _hudControllers)
                _sensorPanel.AddController(controller);

            Log.Info("DetectionHud", "RichHud notified ready!");
        }
    }
}
