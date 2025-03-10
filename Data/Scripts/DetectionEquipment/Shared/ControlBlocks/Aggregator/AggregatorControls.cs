using DetectionEquipment.Shared.ControlBlocks.GenericControls;
using Sandbox.ModAPI;
using System;
using System.Linq;
using VRage.Utils;

namespace DetectionEquipment.Shared.ControlBlocks.Aggregator
{
    internal class AggregatorControls : TerminalControlAdder<AggregatorBlock, IMyConveyorSorter>
    {
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
                (b, sb) => sb.Append((100*b.GameLogic.GetAs<AggregatorBlock>().DistanceThreshold.Value).ToString("F1") + "%")
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
                (b, sb) => sb.Append((100*b.GameLogic.GetAs<AggregatorBlock>().RCSThreshold.Value).ToString("F1") + "%")
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
            CreateListbox(
                "ActiveSensors",
                "Active Sensors",
                "Sensors this aggregator should use data from. Ctrl+Click to select multiple.",
                true,
                (block, content, selected) =>
                {
                    var logic = block.GameLogic.GetAs<AggregatorBlock>();
                    if (logic == null)
                        return;

                    foreach (var sensor in logic.GridSensors.Sensors)
                    {
                        var item = new VRage.ModAPI.MyTerminalControlListBoxItem(MyStringId.GetOrCompute(sensor.Block.DisplayNameText), MyStringId.GetOrCompute(sensor.Definition.Type.ToString()), sensor.Block.EntityId);
                        content.Add(item);
                        if (logic.ActiveSensors.Value.Contains(sensor.Block.EntityId))
                            selected.Add(item);
                    }
                },
                (block, selected) =>
                {
                    var logic = block.GameLogic.GetAs<AggregatorBlock>();
                    if (logic == null)
                        return;
                    var array = new long[selected.Count];
                    for (int i = 0; i < array.Length; i++)
                        array[i] = (long) selected[i].UserData;
                    logic.ActiveSensors.Value = array;
                }
                ).Enabled = b => !(b.GameLogic.GetAs<AggregatorBlock>()?.UseAllSensors ?? true);
        }

        protected override void CreateTerminalProperties()
        {
            
        }
    }
}
