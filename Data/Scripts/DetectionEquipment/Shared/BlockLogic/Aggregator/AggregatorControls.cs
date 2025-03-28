using DetectionEquipment.Client.Sensors;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.Aggregator.Datalink;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace DetectionEquipment.Shared.BlockLogic.Aggregator
{
    internal class AggregatorControls : TerminalControlAdder<AggregatorBlock, IMyConveyorSorter>
    {
        public static BlockSelectControl<AggregatorBlock, IMyConveyorSorter> ActiveSensorSelect;
        public static Dictionary<AggregatorBlock, HashSet<BlockSensor>> ActiveSensors = new Dictionary<AggregatorBlock, HashSet<BlockSensor>>();

        public override void DoOnce(AggregatorBlock thisLogic)
        {
            base.DoOnce(thisLogic);
            ActiveSensors[thisLogic] = new HashSet<BlockSensor>();
        }

        protected override void CreateTerminalActions()
        {
            CreateSlider(
                "Time",
                "Interval",
                "Interval over which detections should be aggregated.",
                0,
                15,
                b => b.GameLogic.GetAs<AggregatorBlock>()?.AggregationTime,
                (b, v) => b.GameLogic.GetAs<AggregatorBlock>().AggregationTime.Value = v,
                (b, sb) => sb.Append(b.GameLogic.GetAs<AggregatorBlock>().AggregationTime.Value.ToString("F1") + "s")
                );
            CreateSlider(
                "Distance",
                "Distance Threshold",
                "Scalar for position error over which to combine detections.",
                0,
                10,
                b => b.GameLogic.GetAs<AggregatorBlock>()?.DistanceThreshold,
                (b, v) => b.GameLogic.GetAs<AggregatorBlock>().DistanceThreshold.Value = v,
                (b, sb) => sb.Append((100 * b.GameLogic.GetAs<AggregatorBlock>().DistanceThreshold.Value).ToString("F1") + "%")
                );
            CreateSlider(
                "VelocityError",
                "Velocity Threshold",
                "Maximum velocity variation at which to incorporate into position estimate.",
                0,
                100,
                b => b.GameLogic.GetAs<AggregatorBlock>()?.VelocityErrorThreshold,
                (b, v) => b.GameLogic.GetAs<AggregatorBlock>().VelocityErrorThreshold.Value = v,
                (b, sb) => sb.Append("R^2 = " + b.GameLogic.GetAs<AggregatorBlock>().VelocityErrorThreshold.Value.ToString("F1"))
                );
            CreateSlider(
                "RcsThreshold",
                "Cross-Section Threshold",
                "Scalar for V/RCS difference at which to combine detections.",
                0,
                10,
                b => b.GameLogic.GetAs<AggregatorBlock>()?.RCSThreshold,
                (b, v) => b.GameLogic.GetAs<AggregatorBlock>().RCSThreshold.Value = v,
                (b, sb) => sb.Append((100 * b.GameLogic.GetAs<AggregatorBlock>().RCSThreshold.Value).ToString("F1") + "%")
                );
            CreateToggle(
                "AggregateTypes",
                "Aggregate Sensor Types",
                "Whether unique sensor types should be aggregated together.",
                b => b.GameLogic.GetAs<AggregatorBlock>()?.AggregateTypes,
                (b, v) => b.GameLogic.GetAs<AggregatorBlock>().AggregateTypes.Value = v
                );
            CreateToggle(
                "UseAllSensors",
                "Use All Grid Sensors",
                "Whether the aggregator should use data from all sensors on the grid.",
                b => b.GameLogic.GetAs<AggregatorBlock>()?.UseAllSensors,
                (b, v) => b.GameLogic.GetAs<AggregatorBlock>().UseAllSensors.Value = v
                );

            ActiveSensorSelect = new BlockSelectControl<AggregatorBlock, IMyConveyorSorter>(
                "ActiveSensors",
                "Active Sensors",
                "Sensors this aggregator should use data from. Ctrl+Click to select multiple.",
                true,
                logic => MyAPIGateway.Session.IsServer ?
                         logic.GridSensors.BlockSensorIdMap.Keys :
                         (IEnumerable<IMyCubeBlock>) SensorBlockManager.GridBlockSensorsMap[logic.CubeBlock.CubeGrid],
                (logic, selected) =>
                {
                    if (!MyAPIGateway.Session.IsServer)
                        return;

                    ActiveSensors[logic].Clear();
                    foreach (var sensor in logic.GridSensors.Sensors)
                    {
                        for (int i = 0; i < selected.Length; i++)
                        {
                            if (sensor.Block.EntityId != selected[i])
                                continue;
                            ActiveSensors[logic].Add(sensor);
                            break;
                        }
                    };
                }
                );
            ActiveSensorSelect.ListBox.Enabled = b => !(b.GameLogic.GetAs<AggregatorBlock>()?.UseAllSensors ?? true);

            CreateSeperator("DatalinkSeperator");
            CreateLabel(
                "DatalinkLabel",
                "Antenna DataLink"
                );
            CreateSlider(
                "DatalinkChannel",
                "Datalink Channel",
                "Datalink channel ID this aggregator should broadcast on. Set to -1 to disable.",
                -1,
                8,
                b => b.GameLogic.GetAs<AggregatorBlock>()?.DatalinkOutChannel,
                (b, v) => b.GameLogic.GetAs<AggregatorBlock>().DatalinkOutChannel.Value = (int) v,
                (b, sb) => sb.Append("ID: " + b.GameLogic.GetAs<AggregatorBlock>().DatalinkOutChannel.Value.ToString("N0"))
                );

            CreateListbox(
                "DatalinkSources",
                "Datalink Sources",
                "Datalink channel IDs this aggregator should recieve from.",
                true,
                (block, content, selected) =>
                {
                    var logic = block.GameLogic.GetAs<AggregatorBlock>();
                    var activeChannels = DatalinkManager.GetActiveDatalinkChannels(block.CubeGrid, block.OwnerId);

                    var nullItem = new MyTerminalControlListBoxItem(MyStringId.NullOrEmpty, MyStringId.NullOrEmpty, null);
                    content.Add(nullItem);
                    if (logic.DatalinkInChannels.Length == 0)
                        selected.Add(nullItem);

                    // Display all channels because clients won't always have sources loaded.
                    for (int id = 0; id <= 8; id++)
                    {
                        int count = activeChannels.ContainsKey(id) ? activeChannels[id].Count : 0;

                        var item = new MyTerminalControlListBoxItem(
                            MyStringId.GetOrCompute($"ID {id}: {count} known source" + (count == 1 ? "" : "s")),
                            MyStringId.NullOrEmpty,
                            id);
                        content.Add(item);
                        if (logic.DatalinkInChannels.Contains(id))
                            selected.Add(item);
                    }
                },
                (block, selected) =>
                {
                    var logic = block.GameLogic.GetAs<AggregatorBlock>();

                    logic.DatalinkInChannels = selected.Where(item => item.UserData != null).Select(item => (int) item.UserData).ToArray();
                }
                );
        }

        protected override void CreateTerminalProperties()
        {

        }
    }
}
