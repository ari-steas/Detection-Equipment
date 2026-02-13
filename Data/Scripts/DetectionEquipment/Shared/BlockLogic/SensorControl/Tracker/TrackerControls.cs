using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.SensorControl.Tracker
{
    internal class TrackerControls : SensorControlBlockControlsBase<TrackerBlock>
    {
        public static BlockSelectControl<TrackerBlock, IMyConveyorSorter> ActiveAggregatorSelect;
        public static Dictionary<TrackerBlock, AggregatorBlock> ActiveAggregators = new Dictionary<TrackerBlock, AggregatorBlock>();

        public override void DoOnce(IControlBlockBase thisLogic)
        {
            base.DoOnce(thisLogic);

            ActiveAggregators[(TrackerBlock)thisLogic] = (AggregatorBlock)ControlBlockManager.I.Blocks.Values.FirstOrDefault(b => b is AggregatorBlock && b.CubeBlock.CubeGrid == thisLogic.CubeBlock.CubeGrid);
        }

        protected override void CreateTerminalActions()
        {
            base.CreateTerminalActions();

            CreateAction(
                "TargetAllies",
                "Target Allies",
                b =>
                {
                    var sync = ControlBlockManager.GetLogic<TrackerBlock>(b).TrackAllies;
                    sync.Value = !sync.Value;
                },
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<TrackerBlock>(b).TrackAllies.Value.ToString()),
                @"Textures\GUI\Icons\Actions\CharacterToggle.dds"
            );
            CreateAction(
                "TargetNeutrals",
                "Target Neutrals",
                b =>
                {
                    var sync = ControlBlockManager.GetLogic<TrackerBlock>(b).TrackNeutrals;
                    sync.Value = !sync.Value;
                },
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<TrackerBlock>(b).TrackNeutrals.Value.ToString()),
                @"Textures\GUI\Icons\Actions\NeutralToggle.dds"
            );
            CreateAction(
                "TargetEnemies",
                "Target Enemies",
                b =>
                {
                    var sync = ControlBlockManager.GetLogic<TrackerBlock>(b).TrackEnemies;
                    sync.Value = !sync.Value;
                },
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<TrackerBlock>(b).TrackEnemies.Value.ToString()),
                @"Textures\GUI\Icons\Actions\MovingObjectToggle.dds"
            );
        }

        protected override void CreateTerminalProperties()
        {
            base.CreateTerminalProperties();

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

            CreateSlider(
                "MaxSensorsPerLock",
                "Maximum Sensors Per Target",
                "Maximum number of sensors that can track a single target. Any extra sensors will go idle. 0 to disable.",
                0,
                10,
                b => ControlBlockManager.GetLogic<TrackerBlock>(b).MaxSensorsPerLock.Value,
                (b, v) => ControlBlockManager.GetLogic<TrackerBlock>(b).MaxSensorsPerLock.Value = (int)Math.Round(v),
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<TrackerBlock>(b).MaxSensorsPerLock.Value)
            );
            CreateToggle(
                "TrackAllies",
                "Track Allies",
                "If enabled, this block can direct sensors to track allies.",
                b => ControlBlockManager.GetLogic<TrackerBlock>(b).TrackAllies.Value,
                (b, selected) => ControlBlockManager.GetLogic<TrackerBlock>(b).TrackAllies.Value = selected
            );
            CreateToggle(
                "TrackEnemies",
                "Track Enemies",
                "If enabled, this block can direct sensors to track enemies.",
                b => ControlBlockManager.GetLogic<TrackerBlock>(b).TrackEnemies.Value,
                (b, selected) => ControlBlockManager.GetLogic<TrackerBlock>(b).TrackEnemies.Value = selected
            );
            CreateToggle(
                "TrackNeutrals",
                "Track Neutrals",
                "If enabled, this block can direct sensors to track neutrals.",
                b => ControlBlockManager.GetLogic<TrackerBlock>(b).TrackNeutrals.Value,
                (b, selected) => ControlBlockManager.GetLogic<TrackerBlock>(b).TrackNeutrals.Value = selected
            );
        }

        protected override void OnSensorsSelected(TrackerBlock logic, List<IMyCubeBlock> selected)
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            ActiveSensors[logic].Clear();
            logic.LockDecay.Clear();

            foreach (var block in selected)
            {
                List<BlockSensor> sensors;
                if (logic.GridSensors.BlockSensorMap.TryGetValue(block, out sensors))
                {
                    foreach (var sensor in sensors)
                    {
                        if (sensor.Definition.Movement == null)
                            continue;
                        ActiveSensors[logic].Add(sensor);
                    }
                }
            }
        }
    }
}
