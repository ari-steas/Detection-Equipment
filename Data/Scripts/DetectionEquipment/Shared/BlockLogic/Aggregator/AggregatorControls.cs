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
using DetectionEquipment.Shared.Helpers;

namespace DetectionEquipment.Shared.BlockLogic.Aggregator
{
    internal class AggregatorControls : TerminalControlAdder<AggregatorBlock, IMyConveyorSorter>
    {
        public static BlockSelectControl<AggregatorBlock, IMyConveyorSorter> ActiveSensorSelect = null;
        public static BlockSelectControl<AggregatorBlock, IMyConveyorSorter> ActiveWeaponSelect = null;
        public static Dictionary<AggregatorBlock, HashSet<BlockSensor>> ActiveSensors = new Dictionary<AggregatorBlock, HashSet<BlockSensor>>();
        public static Dictionary<AggregatorBlock, HashSet<IMyTerminalBlock>> ActiveWeapons = new Dictionary<AggregatorBlock, HashSet<IMyTerminalBlock>>();

        public override void DoOnce(IControlBlockBase thisLogic)
        {
            if (!IsDone)
                ActiveSensors.Clear();
            base.DoOnce(thisLogic);
            ActiveSensors[(AggregatorBlock)thisLogic] = new HashSet<BlockSensor>();
            ActiveWeapons[(AggregatorBlock)thisLogic] = new HashSet<IMyTerminalBlock>();
        }

        protected override void CreateTerminalActions()
        {
            CreateSlider(
                "Time",
                "Interval",
                "Interval over which detections should be aggregated.",
                1/60f,
                15,
                b => b.GameLogic.GetAs<AggregatorBlock>()?.AggregationTime,
                (b, v) => b.GameLogic.GetAs<AggregatorBlock>().AggregationTime.Value = (float) Math.Round(v*60)/60,
                (b, sb) => sb.Append(b.GameLogic.GetAs<AggregatorBlock>().AggregationTime.Value.ToString("F1") + "s")
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
            CreateToggle(
                "UseAllSensors",
                "Use All Grid Sensors",
                "Whether the aggregator should use data from all sensors on the grid.",
                b => b.GameLogic.GetAs<AggregatorBlock>()?.UseAllSensors,
                (b, v) =>
                {
                    b.GameLogic.GetAs<AggregatorBlock>().UseAllSensors.Value = v;
                    ActiveSensorSelect.ListBox.UpdateVisual();
                });

            ActiveSensorSelect = new BlockSelectControl<AggregatorBlock, IMyConveyorSorter>(
                this,
                "ActiveSensors",
                "Active Sensors",
                "Sensors this aggregator should use data from. Ctrl+Click to select multiple.",
                true,
                false,
                logic => MyAPIGateway.Session.IsServer ?
                         logic.GridSensors.BlockSensorMap.Keys :
                         (IEnumerable<IMyCubeBlock>)SensorBlockManager.SensorBlocks.GetValueOrDefault(logic.CubeBlock.CubeGrid, new HashSet<IMyCubeBlock>()),
                (logic, selected) =>
                {
                    if (!MyAPIGateway.Session.IsServer)
                        return;

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
                );
            ActiveSensorSelect.ListBox.Enabled = b => !(b.GameLogic.GetAs<AggregatorBlock>()?.UseAllSensors ?? true);

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
                b => b.GameLogic.GetAs<AggregatorBlock>()?.DatalinkInShareType,
                (b, v) => b.GameLogic.GetAs<AggregatorBlock>().DatalinkInShareType.Value = (int)Math.Round(v),
                (b, sb) => sb.Append((AggregatorBlock.ShareType)b.GameLogic.GetAs<AggregatorBlock>().DatalinkInShareType.Value)
                );
            CreateSlider(
                "DatalinkChannel",
                "Datalink Channel",
                "Datalink channel ID this aggregator should broadcast on. Set to -1 to disable.",
                -1,
                8,
                b => b.GameLogic.GetAs<AggregatorBlock>()?.DatalinkOutChannel,
                (b, v) => b.GameLogic.GetAs<AggregatorBlock>().DatalinkOutChannel.Value = (int)v,
                (b, sb) => sb.Append("ID: " + b.GameLogic.GetAs<AggregatorBlock>().DatalinkOutChannel.Value.ToString("N0"))
                );

            CreateListbox(
                "DatalinkSources",
                "Datalink Sources",
                "Datalink channel IDs this aggregator should receive from.",
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
                    var logic = block.GameLogic.GetAs<AggregatorBlock>();

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
                block => block.GameLogic.GetAs<AggregatorBlock>().DoWcTargeting.Value,
                (block, value) => block.GameLogic.GetAs<AggregatorBlock>().DoWcTargeting.Value = value
                );
            CreateToggle(
                "WcUseAllWeapons",
                "Contribute to All Weapons",
                "Whether this aggregator should give targeting data to all grid weapons, or only selected.",
                block => block.GameLogic.GetAs<AggregatorBlock>().UseAllWeapons.Value,
                (block, value) =>
                {
                    block.GameLogic.GetAs<AggregatorBlock>().UseAllWeapons.Value = value;
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
            ActiveWeaponSelect.ListBox.Enabled = b => !(b.GameLogic.GetAs<AggregatorBlock>()?.UseAllWeapons.Value ?? true);
        }
    }
}
