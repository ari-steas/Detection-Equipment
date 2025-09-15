using DetectionEquipment.Server.SensorBlocks;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Client.BlockLogic.Sensors;
using DetectionEquipment.Shared.ExternalApis;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.BlockLogic.Aggregator.Datalink;

namespace DetectionEquipment.Shared.BlockLogic.Aggregator
{
    internal class AggregatorControls : TerminalControlAdder<AggregatorBlock, IMyConveyorSorter>
    {
        public static BlockSelectControl<AggregatorBlock, IMyConveyorSorter> ActiveSensorSelect = null;
        public static BlockSelectControl<AggregatorBlock, IMyConveyorSorter> ActiveWeaponSelect = null;
        public static Dictionary<AggregatorBlock, HashSet<BlockSensor>> ActiveSensors = new Dictionary<AggregatorBlock, HashSet<BlockSensor>>();
        public static Dictionary<AggregatorBlock, HashSet<IMyCubeBlock>> ClientActiveSensors = new Dictionary<AggregatorBlock, HashSet<IMyCubeBlock>>();
        public static Dictionary<AggregatorBlock, HashSet<IMyTerminalBlock>> ActiveWeapons = new Dictionary<AggregatorBlock, HashSet<IMyTerminalBlock>>();

        public override void DoOnce(IControlBlockBase thisLogic)
        {
            if (!IsDone)
                ActiveSensors.Clear();
            base.DoOnce(thisLogic);
            ActiveSensors[(AggregatorBlock)thisLogic] = new HashSet<BlockSensor>();
            ActiveWeapons[(AggregatorBlock)thisLogic] = new HashSet<IMyTerminalBlock>();
            ClientActiveSensors[(AggregatorBlock)thisLogic] = new HashSet<IMyCubeBlock>();
        }

        protected override void CreateTerminalActions()
        {
            CreateSlider(
                "Time",
                "Interval",
                "Interval over which detections should be aggregated.",
                1/60f,
                15,
                b => ControlBlockManager.GetLogic<AggregatorBlock>(b)?.AggregationTime,
                (b, v) => ControlBlockManager.GetLogic<AggregatorBlock>(b).AggregationTime.Value = (float) Math.Round(v*60)/60,
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<AggregatorBlock>(b).AggregationTime.Value.ToString("F1") + "s")
                );
            CreateSlider(
                "VelocityError",
                "Velocity Threshold",
                "Maximum velocity variation at which to incorporate into position estimate.",
                0,
                100,
                b => ControlBlockManager.GetLogic<AggregatorBlock>(b)?.VelocityErrorThreshold,
                (b, v) => ControlBlockManager.GetLogic<AggregatorBlock>(b).VelocityErrorThreshold.Value = v,
                (b, sb) => sb.Append("R^2 = " + ControlBlockManager.GetLogic<AggregatorBlock>(b).VelocityErrorThreshold.Value.ToString("F1"))
                );
            CreateToggle(
                "UseAllSensors",
                "Use All Grid Sensors",
                "Whether the aggregator should use data from all sensors on the grid.",
                b => ControlBlockManager.GetLogic<AggregatorBlock>(b)?.UseAllSensors,
                (b, v) =>
                {
                    ControlBlockManager.GetLogic<AggregatorBlock>(b).UseAllSensors.Value = v;
                    ActiveSensorSelect.ListBox.UpdateVisual();
                });

            ActiveSensorSelect = new BlockSelectControl<AggregatorBlock, IMyConveyorSorter>(
                this,
                "ActiveSensors",
                "Active Sensors",
                "Sensors this aggregator should use data from. Ctrl+Click to select multiple.",
                true,
                false,
                logic => (MyAPIGateway.Session.IsServer ?
                        logic.GridSensors.BlockSensorMap.Keys :
                        (IEnumerable<IMyCubeBlock>)SensorBlockManager.SensorBlocks[logic.CubeBlock.CubeGrid]),
                (logic, selected) =>
                {
                    if (!MyAPIGateway.Utilities.IsDedicated)
                    {
                        ClientActiveSensors[logic].Clear();

                        HashSet<IMyCubeBlock> gridSensorBlocks;
                        if (SensorBlockManager.SensorBlocks.TryGetValue(logic.Block.CubeGrid, out gridSensorBlocks))
                        {
                            foreach (var block in selected)
                            {
                                foreach (var sensor in gridSensorBlocks)
                                {
                                    if (sensor != block)
                                        continue;
                                    ClientActiveSensors[logic].Add(sensor);
                                    break;
                                }
                            }
                        }
                    }

                    if (MyAPIGateway.Session.IsServer)
                    {
                        ActiveSensors[logic].Clear();

                        foreach (var block in selected)
                        {
                            foreach (var sensor in logic.GridSensors.Sensors)
                            {
                                if (sensor.Block != block)
                                    continue;
                                ActiveSensors[logic].Add(sensor);
                                break;
                            }
                        }
                    }
                }
                );
            ActiveSensorSelect.ListBox.Enabled = b => !(ControlBlockManager.GetLogic<AggregatorBlock>(b)?.UseAllSensors ?? true);

            CreateSeperator("DatalinkSeparator");
            CreateLabel(
                "DatalinkLabel",
                "Antenna DataLink"
                );
            CreateSlider(
                "DatalinkInShareType",
                "Datalink Source Type",
                "Relations to read DataLink input from.",
                0,
                3,
                b => ControlBlockManager.GetLogic<AggregatorBlock>(b)?.DatalinkInShareType,
                (b, v) => ControlBlockManager.GetLogic<AggregatorBlock>(b).DatalinkInShareType.Value = (int)Math.Round(v),
                (b, sb) => sb.Append((AggregatorBlock.ShareType)ControlBlockManager.GetLogic<AggregatorBlock>(b).DatalinkInShareType.Value)
                );
            CreateSlider(
                "DatalinkChannel",
                "Datalink Channel",
                "Datalink channel ID this aggregator should broadcast on. Set to -1 to disable.",
                -1,
                8,
                b => ControlBlockManager.GetLogic<AggregatorBlock>(b)?.DatalinkOutChannel,
                (b, v) => ControlBlockManager.GetLogic<AggregatorBlock>(b).DatalinkOutChannel.Value = (int)v,
                (b, sb) => sb.Append("ID: " + ControlBlockManager.GetLogic<AggregatorBlock>(b).DatalinkOutChannel.Value.ToString("N0"))
                );

            CreateListbox(
                "DatalinkSources",
                "Datalink Sources",
                "Datalink channel IDs this aggregator should receive from.",
                true,
                (block, content, selected) =>
                {
                    var logic = ControlBlockManager.GetLogic<AggregatorBlock>(block);
                    var activeChannels = DatalinkManager.GetActiveDatalinkChannels(block.CubeGrid, block.OwnerId);

                    var nullItem = new MyTerminalControlListBoxItem(MyStringId.NullOrEmpty, MyStringId.NullOrEmpty, null);
                    content.Add(nullItem);
                    if (logic.DatalinkInChannels.Length == 0)
                        selected.Add(nullItem);

                    // Display all channels because clients won't always have sources loaded.
                    for (int id = 0; id <= 8; id++)
                    {
                        HashSet<AggregatorBlock> channel;
                        int count = activeChannels.TryGetValue(id, out channel) ? channel.Count : 0;

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
                    var logic = ControlBlockManager.GetLogic<AggregatorBlock>(block);

                    logic.DatalinkInChannels = selected.Where(item => item.UserData != null).Select(item => (int)item.UserData).ToArray();
                }
                );

            if (ApiManager.WcApi.IsReady)
                CreateWcControls();
        }

        protected override void CreateTerminalProperties()
        {

        }

        private void CreateWcControls()
        {
            CreateSeperator("WcControlSeparator");
            CreateLabel(
                "WcControlLabel",
                "Weapon Integration"
                );
            CreateToggle(
                "WcDoTargetingToggle",
                "Contribute Targeting Data",
                "Whether this aggregator should give targeting data to weapons. Pulls from datalink.",
                block => ControlBlockManager.GetLogic<AggregatorBlock>(block).DoWcTargeting.Value,
                (block, value) => ControlBlockManager.GetLogic<AggregatorBlock>(block).DoWcTargeting.Value = value
                );
            CreateToggle(
                "WcUseAllWeapons",
                "Contribute to All Weapons",
                "Whether this aggregator should give targeting data to all grid weapons, or only selected.",
                block => ControlBlockManager.GetLogic<AggregatorBlock>(block).UseAllWeapons.Value,
                (block, value) =>
                {
                    ControlBlockManager.GetLogic<AggregatorBlock>(block).UseAllWeapons.Value = value;
                    ActiveWeaponSelect.ListBox.UpdateVisual();
                });
            ActiveWeaponSelect = new BlockSelectControl<AggregatorBlock, IMyConveyorSorter>(
                this,
                "WcActiveWeapons",
                "Controlled Weapons",
                "Weapons this aggregator should give targeting data to. Ctrl+Click to select multiple.",
                true,
                true,
                logic =>
                {
                    // awful and I hate it.
                    List<IMyCubeBlock> blocks = new List<IMyCubeBlock>();
                    List<IMyCubeGrid> grids = new List<IMyCubeGrid>();
                    logic.CubeBlock.CubeGrid.GetGridGroup(GridLinkTypeEnum.Logical).GetGrids(grids);
                    foreach (var grid in grids)
                        foreach (var block in grid.GetFatBlocks<IMyTerminalBlock>())
                            if (ApiManager.WcApi.HasCoreWeapon((MyEntity)block))
                                blocks.Add(block);
                    return blocks;
                },
                (logic, selected) =>
                {
                    if (!MyAPIGateway.Session.IsServer)
                        return;

                    ActiveWeapons[logic].Clear();
                    foreach (var block in selected)
                        ActiveWeapons[logic].Add((IMyTerminalBlock) block);
                }
            );
            ActiveWeaponSelect.ListBox.Enabled = b => !(ControlBlockManager.GetLogic<AggregatorBlock>(b)?.UseAllWeapons.Value ?? true);
        }
    }
}
