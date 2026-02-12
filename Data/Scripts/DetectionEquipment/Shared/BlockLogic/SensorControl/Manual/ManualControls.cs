using DetectionEquipment.Client.BlockLogic.Sensors;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Client.BlockLogic;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.SensorControl.Manual
{
    internal class ManualControls : SensorControlBlockControlsBase<ManualBlock>
    {
        public static BlockSelectControl<ManualBlock, IMyConveyorSorter> ShipControllersSelect;
        public static readonly Dictionary<ManualBlock, HashSet<IMyShipController>> ShipControllers = new Dictionary<ManualBlock, HashSet<IMyShipController>>();

        public override void DoOnce(IControlBlockBase thisLogic)
        {
            base.DoOnce(thisLogic);
            ShipControllers[(ManualBlock)thisLogic] = new HashSet<IMyShipController>();
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
