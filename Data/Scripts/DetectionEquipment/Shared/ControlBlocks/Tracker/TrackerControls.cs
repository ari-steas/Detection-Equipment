using DetectionEquipment.Shared.ControlBlocks.Aggregator;
using DetectionEquipment.Shared.ControlBlocks.GenericControls;
using Sandbox.ModAPI;
using System;
using System.Linq;
using VRage.Utils;

namespace DetectionEquipment.Shared.ControlBlocks.Tracker
{
    internal class TrackerControls : TerminalControlAdder<TrackerBlock, IMyConveyorSorter>
    {
        protected override void CreateTerminalActions()
        {
            CreateSlider(
                "ResetAngleTime",
                "Tracking Reset Time",
                "How long sensors should attempt to track a lost target.",
                0,
                10,
                b => b.GameLogic.GetAs<TrackerBlock>()?.ResetAngleTime,
                (b, v) => b.GameLogic.GetAs<TrackerBlock>().ResetAngleTime.Value = v,
                (b, sb) => sb.Append(b.GameLogic.GetAs<TrackerBlock>().ResetAngleTime.Value.ToString("F1") + "s")
                );
            CreateListbox(
                "SourceAggregator",
                "Source Aggregator",
                "Aggregator this block should use to direct sensors.",
                false,
                (block, content, selected) =>
                {
                    var logic = block.GameLogic.GetAs<TrackerBlock>();
                    if (logic == null)
                        return;

                    foreach (var control in ControlBlockManager.I.Blocks.Values)
                    {
                        if (!(control is AggregatorBlock) || control.Block.CubeGrid != block.CubeGrid)
                            continue;

                        var item = new VRage.ModAPI.MyTerminalControlListBoxItem(MyStringId.GetOrCompute(control.Block.DisplayNameText), MyStringId.GetOrCompute(""), control.Block.EntityId);
                        content.Add(item);
                        if (logic.ActiveAggregator.Value == control.Block.EntityId)
                            selected.Add(item);
                    }
                },
                (block, selected) =>
                {
                    var logic = block.GameLogic.GetAs<TrackerBlock>();
                    if (logic == null)
                        return;
                    logic.ActiveAggregator.Value = selected.Count == 0 ? -1 : (long) selected[0].UserData;
                }
                ).VisibleRowsCount = 5;
            CreateListbox(
                "ActiveSensors",
                "Active Sensors",
                "Sensors this tracker should direct. Ctrl+Click to select multiple.",
                true,
                (block, content, selected) =>
                {
                    var logic = block.GameLogic.GetAs<TrackerBlock>();
                    if (logic == null)
                        return;

                    foreach (var sensor in logic.GridSensors.Sensors)
                    {
                        if (sensor.Definition.Movement == null)
                            continue;
                        var item = new VRage.ModAPI.MyTerminalControlListBoxItem(MyStringId.GetOrCompute(sensor.Block.DisplayNameText), MyStringId.GetOrCompute(sensor.Definition.Type.ToString()), sensor.Block.EntityId);
                        content.Add(item);
                        if (logic.ActiveSensors.Value.Contains(sensor.Block.EntityId))
                            selected.Add(item);
                    }
                },
                (block, selected) =>
                {
                    var logic = block.GameLogic.GetAs<TrackerBlock>();
                    if (logic == null)
                        return;
                    var array = new long[selected.Count];
                    for (int i = 0; i < array.Length; i++)
                        array[i] = (long) selected[i].UserData;
                    logic.ActiveSensors.Value = array;
                }
                );
        }

        protected override void CreateTerminalProperties()
        {
            
        }
    }
}
