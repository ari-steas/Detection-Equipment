using System;
using DetectionEquipment.Server.SensorBlocks;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Client.BlockLogic;
using DetectionEquipment.Client.BlockLogic.Sensors;
using VRage.Game.ModAPI;
using VRageMath;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using VRage.ModAPI;
using VRage.Utils;

namespace DetectionEquipment.Shared.BlockLogic.Search
{
    internal class SearchControls : TerminalControlAdder<SearchBlock, IMyConveyorSorter>
    {
        public static BlockSelectControl<SearchBlock, IMyConveyorSorter> ActiveSensorSelect;
        public static Dictionary<SearchBlock, HashSet<BlockSensor>> ActiveSensors = new Dictionary<SearchBlock, HashSet<BlockSensor>>();

        public override void DoOnce(IControlBlockBase thisLogic)
        {
            base.DoOnce(thisLogic);
            ActiveSensors[(SearchBlock)thisLogic] = new HashSet<BlockSensor>();
        }

        protected override void CreateTerminalActions()
        {
            CreateAction(
                "IncrementMode",
                "Search Mode",
                b =>
                {
                    var mode = b.GameLogic.GetAs<SearchBlock>().SearchMode;
                    mode.Value = (int)mode.Value == Enum.GetNames(typeof(SearchBlock.SearchModes)).Length - 1
                        ? 0
                        : mode.Value + 1;
                },
                (b, sb) => sb.Append(b.GameLogic.GetAs<SearchBlock>().SearchMode.Value.ToString()),
                @"Textures\GUI\Icons\Actions\SubsystemTargeting_Cycle.dds"
            );
        }

        protected override void CreateTerminalProperties()
        {
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
                b => (long) b.GameLogic.GetAs<SearchBlock>().SearchMode.Value,
                (b, selected) => b.GameLogic.GetAs<SearchBlock>().SearchMode.Value = (SearchBlock.SearchModes) selected
            );

            ActiveSensorSelect = new BlockSelectControl<SearchBlock, IMyConveyorSorter>(
                this,
                "ActiveSensors",
                "Active Sensors",
                "Sensors this block should direct. Ctrl+Click to select multiple.",
                true,
                false,
                logic => (MyAPIGateway.Session.IsServer ?
                        logic.GridSensors.BlockSensorMap.Keys :
                        (IEnumerable<IMyCubeBlock>)SensorBlockManager.SensorBlocks[logic.CubeBlock.CubeGrid])
                    // BRIMSTONE LINQ HELL
                    .Where(sb => sb.GetLogic<ClientSensorLogic>()?.Sensors.Values.Any(s => s.Definition.Movement != null) ?? false),
                (logic, selected) =>
                {
                    if (!MyAPIGateway.Session.IsServer)
                        return;

                    ActiveSensors[logic].Clear();
                    logic.DirectionSigns.Clear();
                    foreach (var item in selected)
                    {
                        foreach (var sensor in logic.GridSensors.Sensors)
                        {
                            if (sensor.Block != item || sensor.Definition.Movement == null)
                                continue;
                            ActiveSensors[logic].Add(sensor);
                            sensor.DesiredAzimuth = 0;
                            sensor.DesiredElevation = 0;
                            logic.DirectionSigns[sensor] = Vector2I.One;
                            break;
                        }
                    }
                }
            );
        }
    }
}
