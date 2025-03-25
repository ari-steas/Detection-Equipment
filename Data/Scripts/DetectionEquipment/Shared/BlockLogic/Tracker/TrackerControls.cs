using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.BlockLogic.Search;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Utils;

namespace DetectionEquipment.Shared.BlockLogic.Tracker
{
    internal class TrackerControls : TerminalControlAdder<TrackerBlock, IMyConveyorSorter>
    {
        protected static BlockSelectControl<TrackerBlock, IMyConveyorSorter> ActiveSensorSelect;
        public static Dictionary<TrackerBlock, HashSet<BlockSensor>> ActiveSensors = new Dictionary<TrackerBlock, HashSet<BlockSensor>>();

        protected static BlockSelectControl<TrackerBlock, IMyConveyorSorter> ActiveAggregatorSelect;
        public static Dictionary<TrackerBlock, AggregatorBlock> ActiveAggregators = new Dictionary<TrackerBlock, AggregatorBlock>();

        public override void DoOnce(TrackerBlock thisLogic)
        {
            base.DoOnce(thisLogic);

            ActiveAggregators[thisLogic] = (AggregatorBlock)ControlBlockManager.I.Blocks.Values.FirstOrDefault(b => b is AggregatorBlock && b.CubeBlock.CubeGrid == thisLogic.Block.CubeGrid);
            ActiveSensors[thisLogic] = new HashSet<BlockSensor>();
        }

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

            ActiveSensorSelect = new BlockSelectControl<TrackerBlock, IMyConveyorSorter>(
                "ActiveSensors",
                "Active Sensors",
                "Sensors this block should direct. Ctrl+Click to select multiple.",
                true,
                logic => logic.GridSensors.BlockSensorIdMap.Keys,
                (logic, selected) =>
                {
                    ActiveSensors[logic].Clear();
                    logic.LockDecay.Clear();
                    foreach (var sensor in logic.GridSensors.Sensors)
                    {
                        for (int i = 0; i < selected.Length; i++)
                        {
                            if (sensor.Block.EntityId != selected[i])
                                continue;
                            ActiveSensors[logic].Add(sensor);
                            logic.LockDecay[sensor] = 0;
                            break;
                        }
                    };
                }
                );
            ActiveAggregatorSelect = new BlockSelectControl<TrackerBlock, IMyConveyorSorter>(
                "SourceAggregator",
                "Source Aggregator",
                "Aggregator this block should use to direct sensors.",
                false,
                // TODO convert this into a yield action
                logic => ControlBlockManager.I.Blocks.Values.Where(control => control is AggregatorBlock && control.CubeBlock.CubeGrid == logic.Block.CubeGrid).Select(c => c.CubeBlock),
                (logic, selected) =>
                {
                    foreach (var control in ControlBlockManager.I.Blocks.Values)
                    {
                        if (!(control is AggregatorBlock) || control.CubeBlock.CubeGrid != logic.Block.CubeGrid)
                            continue;
                        ActiveAggregators[logic] = (AggregatorBlock) control;
                    }
                }
                );
            ActiveAggregatorSelect.ListBox.VisibleRowsCount = 5;
        }

        protected override void CreateTerminalProperties()
        {

        }
    }
}
