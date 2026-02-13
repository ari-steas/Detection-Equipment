using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.SensorControl.Manual
{
    internal class ManualControls : SensorControlBlockControlsBase<ManualBlock>
    {
        public static BlockSelectControl<ManualBlock, IMyConveyorSorter> ShipControllersSelect;
        public static readonly Dictionary<ManualBlock, HashSet<IMyShipController>> ShipControllers = new Dictionary<ManualBlock, HashSet<IMyShipController>>();

        public static BlockSelectControl<ManualBlock, IMyConveyorSorter> ActiveAggregatorSelect;
        public static Dictionary<ManualBlock, AggregatorBlock> ActiveAggregators = new Dictionary<ManualBlock, AggregatorBlock>();

        public override void DoOnce(IControlBlockBase thisLogic)
        {
            base.DoOnce(thisLogic);
            ShipControllers[(ManualBlock)thisLogic] = new HashSet<IMyShipController>();
            ActiveAggregators[(ManualBlock)thisLogic] = (AggregatorBlock)ControlBlockManager.I.Blocks.Values.FirstOrDefault(b => b is AggregatorBlock && b.CubeBlock.CubeGrid == thisLogic.CubeBlock.CubeGrid);
        }

        protected override void CreateTerminalActions()
        {
            base.CreateTerminalActions();

            CreateAction(
                "ParallaxAccount",
                "Parallax Account",
                b =>
                {
                    var sync = ControlBlockManager.GetLogic<ManualBlock>(b).ParallaxAccount;
                    sync.Value = !sync.Value;
                },
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<ManualBlock>(b).ParallaxAccount.Value.ToString()),
                @"Textures\GUI\Icons\Actions\MovingObjectToggle.dds"
            );

            CreateAction(
                "LockTarget",
                "Lock Target",
                b =>
                {
                    ControlBlockManager.GetLogic<ManualBlock>(b).TryLockTarget();
                },
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<ManualBlock>(b).LockedTarget.Value == long.MinValue ? "NO LOCK" : "LOCKED"),
                @"Textures\GUI\Icons\Actions\SubsystemTargeting_Cycle.dds"
            );

            CreateAction(
                "UnlockTarget",
                "Unlock Target",
                b =>
                {
                    ControlBlockManager.GetLogic<ManualBlock>(b).UnlockTarget();
                },
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<ManualBlock>(b).LockedTarget.Value == long.MinValue ? "NO LOCK" : "LOCKED"),
                @"Textures\GUI\Icons\Actions\SubsystemTargeting_None.dds"
            );

            CreateAction(
                "CycleMode",
                "Cycle Mode",
                b =>
                {
                    var logic = ControlBlockManager.GetLogic<ManualBlock>(b);
                    if (!logic.Block.Enabled)
                    { // disabled -> enabled boresight
                        logic.Block.Enabled = true;
                        logic.UnlockTarget();
                    }
                    else if (logic.LockedTarget.Value == long.MinValue)
                    { // boresight -> lock
                        logic.TryLockTarget();
                    }
                    else
                    { // lock -> disabled
                        logic.Block.Enabled = false;
                    }
                },
                (b, sb) =>
                {
                    var logic = ControlBlockManager.GetLogic<ManualBlock>(b);

                    if (!logic.Block.Enabled)
                    {
                        sb.Append("DISABLED");
                    }
                    else if (logic.LockedTarget.Value == long.MinValue)
                    {
                        sb.Append("BORESIGHT");
                    }
                    else
                    {
                        sb.Append("LOCKED");
                    }
                },
                @"Textures\GUI\Icons\Actions\Reset.dds"
            );
        }

        protected override void CreateTerminalProperties()
        {
            base.CreateTerminalProperties();

            ShipControllersSelect = new BlockSelectControl<ManualBlock, IMyConveyorSorter>(
                this,
                "ShipControllers",
                "Ship Controllers",
                "Cockpits that can direct this block's sensors. Ctrl+Click to select multiple.",
                true,
                false,
                AvailableControllers,
                OnControllersSelected
            );
            ShipControllersSelect.ListBox.VisibleRowsCount = 5;

            ActiveAggregatorSelect = new BlockSelectControl<ManualBlock, IMyConveyorSorter>(
                this,
                "SourceAggregator",
                "Source Aggregator",
                "(Optional) Aggregator this block should use for holding locks..",
                false,
                false,
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
                "ParallaxAccount",
                "Parallax Account",
                "If enabled, sensors will aim for the closest entity along the player's line of sight.",
                b => ControlBlockManager.GetLogic<ManualBlock>(b).ParallaxAccount.Value,
                (b, selected) => ControlBlockManager.GetLogic<ManualBlock>(b).ParallaxAccount.Value = selected
            );
        }

        protected virtual IEnumerable<IMyCubeBlock> AvailableControllers(ManualBlock logic) => logic.Block.CubeGrid.GetFatBlocks<IMyShipController>();

        protected virtual void OnControllersSelected(ManualBlock logic, List<IMyCubeBlock> selected)
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            ShipControllers[logic].Clear();

            foreach (var myCubeBlock in selected)
            {
                var controller = myCubeBlock as IMyShipController;
                if (controller != null)
                    ShipControllers[logic].Add(controller);
            }
        }

        protected override void OnSensorsSelected(ManualBlock logic, List<IMyCubeBlock> selected)
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            ActiveSensors[logic].Clear();

            foreach (var sensor in logic.GridSensors.Sensors)
            {
                if (sensor.Definition.Movement == null || !selected.Contains(sensor.Block))
                    continue;
                ActiveSensors[logic].Add(sensor);
            }
        }
    }
}
