using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.BlockLogic.HudController;
using DetectionEquipment.Shared.Definitions;
using RichHudFramework.Client;
using RichHudFramework.UI;
using RichHudFramework.UI.Rendering;
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
            ParentAlignment = ParentAlignments.Right | ParentAlignments.Top;
            _mainLabel = new Label(this)
            {
                Format = UserData.StandardFont,
                BuilderMode = TextBuilderModes.Lined
            };
        }

        public void UpdateDraw()
        {
//            _mainLabel.Text = @"Weapon
//├── Projectile
//│   ├── GridSystem
//│   │   └── Blocks
//│   └── VehicleController
//├── Player
//└── Environment";

            if (_needsTextUpdate)
            {
                UpdateText();
                _needsTextUpdate = false;
            }
        }

        private void UpdateText()
        {
            StringBuilder sb = new StringBuilder();
            foreach (var controller in _controllers)
            {
                if (controller.SourceAggregator == null)
                {
                    sb.Append("[NOSRC] ").AppendLine(controller.Block.CustomName);
                    continue;
                }
                sb.AppendLine(controller.SourceAggregator.Block.CustomName);

                var aggregatorSensors = new Dictionary<SensorDefinition.SensorType, List<BlockSensor>>();
                var damagedSensors = new Dictionary<SensorDefinition.SensorType, List<BlockSensor>>();
                foreach (var sensor in controller.SourceAggregator.ActiveSensors)
                {
                    if (!aggregatorSensors.ContainsKey(sensor.Definition.Type))
                    {
                        aggregatorSensors.Add(sensor.Definition.Type, new List<BlockSensor> { sensor });
                        damagedSensors.Add(sensor.Definition.Type, new List<BlockSensor>());
                    }
                    else
                        aggregatorSensors[sensor.Definition.Type].Add(sensor);

                    if (!sensor.Block.IsWorking)
                        damagedSensors[sensor.Definition.Type].Add(sensor);
                }

                int typeCount = aggregatorSensors.Count;
                foreach (var sensorType in aggregatorSensors)
                {
                    typeCount--;
                    sb.Append(typeCount == 0 ? @"└─ " : @"├─ ");

                    sb.Append(SensorTypeName(sensorType.Key)).Append(" x").AppendLine(sensorType.Value.Count.ToString());

                    for (var i = 0; i < damagedSensors[sensorType.Key].Count; i++)
                    {
                        var dmgedSensor = damagedSensors[sensorType.Key][i];

                        sb.Append(typeCount == 0 ? @"    " : @"│   ");
                        sb.Append(i == damagedSensors[sensorType.Key].Count - 1 ? @"└" : @"├");
                        sb.Append(@"── ! ").Append(dmgedSensor.Block.CustomName).Append(" !");
                    }
                }
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
                    return "RDR";
                case SensorDefinition.SensorType.PassiveRadar:
                    return "RWR";
                case SensorDefinition.SensorType.Optical:
                    return "VIS";
                case SensorDefinition.SensorType.Infrared:
                    return "IRS";
                case SensorDefinition.SensorType.Antenna:
                    return "COM";
                case SensorDefinition.SensorType.None:
                default:
                    return "N/A";
            }
        }
    }
}
