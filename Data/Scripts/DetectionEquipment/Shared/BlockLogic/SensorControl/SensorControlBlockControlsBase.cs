using DetectionEquipment.Client.BlockLogic;
using DetectionEquipment.Client.BlockLogic.Sensors;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.SensorControl
{
    internal abstract class SensorControlBlockControlsBase<TLogic> : TerminalControlAdder<TLogic, IMyConveyorSorter>
        where TLogic : class, IControlBlockBase, ISensorControlBlock
    {
        public static BlockSelectControl<TLogic, IMyConveyorSorter> ActiveSensorSelect;
        public static readonly Dictionary<TLogic, HashSet<BlockSensor>> ActiveSensors = new Dictionary<TLogic, HashSet<BlockSensor>>();

        public override void DoOnce(IControlBlockBase thisLogic)
        {
            base.DoOnce(thisLogic);
            ActiveSensors[(TLogic)thisLogic] = new HashSet<BlockSensor>();
        }

        protected override void CreateTerminalActions()
        {
            CreateAction(
                "IncrementPriority",
                "Increase Priority",
                b =>
                {
                    ControlBlockManager.GetLogic<TLogic>(b).ControlPriority.Value++;
                },
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<TLogic>(b).ControlPriority.Value.ToString()),
                @"Textures\GUI\Icons\Actions\Increase.dds"
            );

            CreateAction(
                "DecrementPriority",
                "Decrease Priority",
                b =>
                {
                    ControlBlockManager.GetLogic<TLogic>(b).ControlPriority.Value--;
                },
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<TLogic>(b).ControlPriority.Value.ToString()),
                @"Textures\GUI\Icons\Actions\Decrease.dds"
            );

            CreateAction(
                "ToggleInvertControl",
                "Invert Allowed Control",
                b =>
                {
                    var sync = ControlBlockManager.GetLogic<TLogic>(b).InvertAllowControl;
                    sync.Value = !sync.Value;
                },
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<TLogic>(b).InvertAllowControl.Value.ToString()),
                @"Textures\GUI\Icons\Actions\Reverse.dds"
            );
        }

        protected override void CreateTerminalProperties()
        {
            ActiveSensorSelect = new BlockSelectControl<TLogic, IMyConveyorSorter>(
                this,
                "ActiveSensors",
                "Active Sensors",
                "Sensors this block should direct. Ctrl+Click to select multiple.",
                true,
                false,
                AvailableSensors,
                OnSensorsSelected
            );

            CreateToggle(
                "InvertAllowControl",
                "Invert Allow Control",
                "If enabled, this block inverts \"Allow Mechanical Control\" on sensors.",
                b => ControlBlockManager.GetLogic<TLogic>(b).InvertAllowControl.Value,
                (b, selected) => ControlBlockManager.GetLogic<TLogic>(b).InvertAllowControl.Value = selected
            );

            CreateSlider(
                "Priority",
                "Control Priority",
                "Higher priority control blocks will take precedence over lower priority.",
                -10,
                10,
                b => ControlBlockManager.GetLogic<TLogic>(b).ControlPriority.Value,
                (b, v) => ControlBlockManager.GetLogic<TLogic>(b).ControlPriority.Value = (int) Math.Round(v),
                (b, sb) => sb.Append(ControlBlockManager.GetLogic<TLogic>(b).ControlPriority.Value)
            );

            CreateSeperator("SelectionSeparator");
        }

        protected virtual IEnumerable<IMyCubeBlock> AvailableSensors(TLogic logic)
        {
            if (MyAPIGateway.Session.IsServer)
            {
                return logic.GridSensors.BlockSensorMap.Keys;
            }

            return SensorBlockManager.SensorBlocks[logic.CubeBlock.CubeGrid].Where(sb =>
                sb.GetLogic<ClientSensorLogic>()?.Sensors.Values.Any(s => s.Definition.Movement != null) ?? false);
        }

        protected abstract void OnSensorsSelected(TLogic logic, List<IMyCubeBlock> selected);
    }
}
