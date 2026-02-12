using System;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;
using VRage.ModAPI;
using VRage.Utils;

namespace DetectionEquipment.Shared.BlockLogic.SensorControl.Search
{
    internal class SearchControls : SensorControlBlockControlsBase<SearchBlock>
    {
        protected override void CreateTerminalActions()
        {
            base.CreateTerminalActions();

            CreateAction(
                "IncrementMode",
                "Search Mode",
                b =>
                {
                    var mode = ControlBlockManager.GetLogic<SearchBlock>(b).SearchMode;
                    mode.Value = (int)mode.Value == Enum.GetNames(typeof(SearchBlock.SearchModes)).Length - 1
                        ? 0
                        : mode.Value + 1;
                },
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<SearchBlock>(b).SearchMode.Value.ToString()),
                @"Textures\GUI\Icons\Actions\SubsystemTargeting_Cycle.dds"
            );
        }

        protected override void CreateTerminalProperties()
        {
            base.CreateTerminalProperties();

            CreateCombobox(
                "ModeSelect",
                "Search Mode",
                "Search mode for all sensors controlled by this block.",
                (content) =>
                {
                    var enumNames = Enum.GetNames(typeof(SearchBlock.SearchModes));
                    for (var i = 0; i < enumNames.Length; i++)
                    {
                        content.Add(new MyTerminalControlComboBoxItem
                        {
                            Key = i,
                            Value = MyStringId.GetOrCompute(enumNames[i])
                        });
                    }
                },
                b => (long) ControlBlockManager.GetLogic<SearchBlock>(b).SearchMode.Value,
                (b, selected) => ControlBlockManager.GetLogic<SearchBlock>(b).SearchMode.Value = (SearchBlock.SearchModes) selected
            );
        }

        protected override void OnSensorsSelected(SearchBlock logic, List<IMyCubeBlock> selected)
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            ActiveSensors[logic].Clear();
            logic.DirectionSigns.Clear();

            foreach (var sensor in logic.GridSensors.Sensors)
            {
                if (sensor.Definition.Movement == null || !selected.Contains(sensor.Block))
                    continue;
                ActiveSensors[logic].Add(sensor);
                if (sensor.AllowMechanicalControl ^ logic.InvertAllowControl.Value)
                {
                    sensor.DesiredAzimuth = sensor.Definition.Movement.HomeAzimuth;
                    sensor.DesiredElevation = sensor.Definition.Movement.HomeElevation;
                }
                logic.DirectionSigns[sensor] = Vector2I.One;
            }
        }
    }
}
