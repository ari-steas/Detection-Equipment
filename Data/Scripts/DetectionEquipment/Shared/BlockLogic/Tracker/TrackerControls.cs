using DetectionEquipment.Client.BlockLogic;
using DetectionEquipment.Client.BlockLogic.Sensors;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.BlockLogic.Search;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.Tracker
{
    internal class TrackerControls : TerminalControlAdder<TrackerBlock, IMyConveyorSorter>
    {
        public static BlockSelectControl<TrackerBlock, IMyConveyorSorter> ActiveSensorSelect;
        public static Dictionary<TrackerBlock, HashSet<BlockSensor>> ActiveSensors = new Dictionary<TrackerBlock, HashSet<BlockSensor>>();

        public static BlockSelectControl<TrackerBlock, IMyConveyorSorter> ActiveAggregatorSelect;
        public static Dictionary<TrackerBlock, AggregatorBlock> ActiveAggregators = new Dictionary<TrackerBlock, AggregatorBlock>();

        public override void DoOnce(IControlBlockBase thisLogic)
        {
            base.DoOnce(thisLogic);

            ActiveAggregators[(TrackerBlock)thisLogic] = (AggregatorBlock)ControlBlockManager.I.Blocks.Values.FirstOrDefault(b => b is AggregatorBlock && b.CubeBlock.CubeGrid == thisLogic.CubeBlock.CubeGrid);
            ActiveSensors[(TrackerBlock)thisLogic] = new HashSet<BlockSensor>();
        }

        protected override void CreateTerminalActions()
        {
            CreateSlider(
                "ResetAngleTime",
                "Tracking Reset Time",
                "How long sensors should attempt to track a lost target.",
                0,
                10,
                b => ControlBlockManager.GetLogic<TrackerBlock>(b)?.ResetAngleTime,
                (b, v) => ControlBlockManager.GetLogic<TrackerBlock>(b).ResetAngleTime.Value = v,
                (b, sb) => sb?.Append(ControlBlockManager.GetLogic<TrackerBlock>(b)?.ResetAngleTime?.Value.ToString("F1") + "s")
                );

            ActiveSensorSelect = new BlockSelectControl<TrackerBlock, IMyConveyorSorter>(
                this,
                "ActiveSensors",
                "Active Sensors",
                "Sensors this block should direct. Ctrl+Click to select multiple.",
                true,
                false,
                AvailableSensors,
                (logic, selected) =>
                {
                    if (!MyAPIGateway.Session.IsServer)
                        return;

                    ActiveSensors[logic].Clear();
                    logic.LockDecay.Clear();

                    foreach (var sensor in logic.GridSensors.Sensors)
                    {
                        if (sensor.Definition.Movement == null || !selected.Contains(sensor.Block))
                            continue;
                        ActiveSensors[logic].Add(sensor);
                        break;
                    }
                }
                );
            ActiveAggregatorSelect = new BlockSelectControl<TrackerBlock, IMyConveyorSorter>(
                this,
                "SourceAggregator",
                "Source Aggregator",
                "Aggregator this block should use to direct sensors.",
                false,
                false,
                // TODO convert this into a single yield action
                logic => ControlBlockManager.I.Blocks.Values.Where(control => control is AggregatorBlock && control.CubeBlock.CubeGrid == logic.Block.CubeGrid).Select(c => c.CubeBlock),
                (logic, selected) =>
                {
                    if (!MyAPIGateway.Session.IsServer)
                        return;

                    foreach (var control in ControlBlockManager.I.Blocks.Values)
                    {
                        if (!(control is AggregatorBlock) || control.CubeBlock.CubeGrid != logic.Block.CubeGrid || !selected.Contains(control.CubeBlock))
                            continue;
                        ActiveAggregators[logic] = (AggregatorBlock)control;
                    }
                }
                );
            ActiveAggregatorSelect.ListBox.VisibleRowsCount = 5;

            CreateToggle(
                "InvertAllowControl",
                "Invert Allow Control",
                "If enabled, this block inverts \"Allow Mechanical Control\" on sensors.",
                b => ControlBlockManager.GetLogic<TrackerBlock>(b).InvertAllowControl.Value,
                (b, selected) => ControlBlockManager.GetLogic<TrackerBlock>(b).InvertAllowControl.Value = selected
            );
        }

        protected override void CreateTerminalProperties()
        {

        }

        private IEnumerable<IMyCubeBlock> AvailableSensors(TrackerBlock logic)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                return logic.GridSensors.BlockSensorMap.Keys;
            }

            return SensorBlockManager.SensorBlocks[logic.CubeBlock.CubeGrid].Where(sb =>
                sb.GetLogic<ClientSensorLogic>()?.Sensors.Values.Any(s => s.Definition.Movement != null) ?? false);
        }
    }
}
