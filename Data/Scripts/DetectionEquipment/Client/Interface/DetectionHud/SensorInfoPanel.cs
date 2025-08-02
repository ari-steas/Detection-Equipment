using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DetectionEquipment.Client.BlockLogic.Sensors;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.BlockLogic.HudController;
using DetectionEquipment.Shared.Definitions;
using RichHudFramework;
using RichHudFramework.UI;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRageMath;

namespace DetectionEquipment.Client.Interface.DetectionHud
{
    internal class SensorInfoPanel : HudElementBase
    {
        private Label _mainLabel;
        private List<HudControllerBlock> _controllers = new List<HudControllerBlock>();
        private bool _needsTextUpdate = false;

        public SensorInfoPanel(HudParentBase parent) : base(parent)
        {
            Size = new Vector2(1920, 1080);
            _mainLabel = new Label(this)
            {
                Format = UserData.StandardFont.WithAlignment(TextAlignment.Right),
                BuilderMode = TextBuilderModes.Lined,
                ParentAlignment = ParentAlignments.Right | ParentAlignments.Top | ParentAlignments.Inner,
                Padding = Vector2.One * 50,
            };
        }

        public void UpdateDraw()
        {
            if (_needsTextUpdate || MyAPIGateway.Session.GameplayFrameCounter % 10 == 0)
            {
                UpdateText();
                _needsTextUpdate = false;
            }
        }

        private void UpdateText()
        {
            if (_controllers.Count == 0)
            {
                _mainLabel.Text = "";
                return;
            }

            StringBuilder sb = new StringBuilder();

            float totalPowerDraw = 0;

            // sensors
            foreach (var controller in _controllers)
            {
                if (controller.SourceAggregator == null)
                {
                    sb.Append("[NOSRC] ").AppendLine(controller.Block.CustomName);
                    continue;
                }
                sb.AppendLine(controller.SourceAggregator.Block.CustomName);

                var aggregatorSensors = new Dictionary<SensorDefinition.SensorType, List<ClientSensorData>>();
                var damagedSensors = new Dictionary<SensorDefinition.SensorType, List<ClientSensorData>>();
                foreach (var sensorLogic in controller.SourceAggregator.ClientActiveSensors)
                {
                    foreach (var sensor in sensorLogic.Sensors.Values)
                    {
                        if (!aggregatorSensors.ContainsKey(sensor.Definition.Type))
                        {
                            aggregatorSensors.Add(sensor.Definition.Type, new List<ClientSensorData> { sensor });
                            damagedSensors.Add(sensor.Definition.Type, new List<ClientSensorData>());
                        }
                        else
                            aggregatorSensors[sensor.Definition.Type].Add(sensor);

                        if (!sensorLogic.Block.IsWorking)
                            damagedSensors[sensor.Definition.Type].Add(sensor);
                    }

                    if (((IMyCameraBlock)sensorLogic.Block).Enabled && sensorLogic.Block.IsFunctional)
                        totalPowerDraw += sensorLogic.Block.ResourceSink.MaxRequiredInputByType(GlobalData.ElectricityId);
                }

                int typeCount = aggregatorSensors.Count;
                foreach (var sensorType in aggregatorSensors)
                {
                    typeCount--;
                    sb.Append("x").Append(sensorType.Value.Count.ToString()).Append(' ').Append(SensorTypeName(sensorType.Key));
                    sb.AppendLine(typeCount == 0 ? @"─┘" : @"─┤");

                    for (var i = 0; i < damagedSensors[sensorType.Key].Count; i++)
                    {
                        var dmgedSensor = damagedSensors[sensorType.Key][i];
                        // sensor name, trimmed to 10 characters
                        string sName = dmgedSensor.Block.CustomName;
                        sb.Append(sName.Length > 10 ? sName.Substring(0, 4) + ".." + sName.Substring(sName.Length-4, 4) : sName);

                        // alert symbol; delta for not enabled, x-in-circle for damaged, i-in-triangle otherwise (i.e. no power)
                        sb.Append($" {GetAlertSymbol(dmgedSensor.Block)} ──");

                        // right-aligned text tree
                        sb.Append(i == damagedSensors[sensorType.Key].Count - 1 ? @"┘" : @"┤");
                        sb.AppendLine(typeCount == 0 ? @"  " : @" │");
                    }
                }

                sb.AppendLine();
            }

            var distributor = (MyResourceDistributorComponent) _controllers.First().Block.CubeGrid.ResourceDistributor;
            
            float availablePower = distributor.MaxAvailableResourceByType(GlobalData.ElectricityId);
            if (totalPowerDraw > availablePower)
            {
                int a = 5 * 500;
                sb.AppendLine($"\uE056 POWER OVERDRAW \uE056\n{totalPowerDraw:N1}/{availablePower:N1}MW ");
            }

            _mainLabel.Text = sb;
        }

        public void UpdateColor(Color newColor)
        {
            _mainLabel.Format = _mainLabel.Format.WithColor(newColor);
        }

        public void AddController(HudControllerBlock controller)
        {
            _controllers.Add(controller);
            _needsTextUpdate = true;
        }

        public void RemoveController(HudControllerBlock controller)
        {
            _controllers.Remove(controller);
            _needsTextUpdate = true;
        }

        private static string SensorTypeName(SensorDefinition.SensorType type)
        {
            switch (type)
            {
                case SensorDefinition.SensorType.Radar:
                    return "ACT-RDR";
                case SensorDefinition.SensorType.PassiveRadar:
                    return "WRN-RDR";
                case SensorDefinition.SensorType.Optical:
                    return "VIS-OPT";
                case SensorDefinition.SensorType.Infrared:
                    return "IRS-OPT";
                case SensorDefinition.SensorType.Antenna:
                    return "WRN-COM";
                case SensorDefinition.SensorType.None:
                default:
                    return "N/A";
            }
        }

        private static char GetAlertSymbol(IMyCameraBlock block, int blinkInterval = 60)
        {
            // blinking alert symbol; delta for not enabled, hashed block for damaged, i-in-triangle otherwise (i.e. no power)
            float frame = (float) (MyAPIGateway.Session.GameplayFrameCounter % blinkInterval) / blinkInterval; // blink cycle interval 1 second

            char[] activeAlerts = new char[2];
            int idx = 0;
            if (!block.Enabled)
                activeAlerts[idx++] = '\u2206';
            if (!block.IsFunctional)
                activeAlerts[idx++] = '\u2591';
            if (idx == 0)
                activeAlerts[idx++] = '\uE056';
            if (idx == 1)
                activeAlerts[1] = ' ';
            
            return activeAlerts[(int)Math.Round((activeAlerts.Length-1) * frame)];
        }
    }
}
