using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using VRageMath;

namespace DetectionEquipment.Shared.BlockLogic.HudController
{
    internal class HudControllerControls : TerminalControlAdder<HudControllerBlock, IMyConveyorSorter>
    {
        public static BlockSelectControl<HudControllerBlock, IMyConveyorSorter> ActiveAggregatorSelect;
        public static Dictionary<HudControllerBlock, AggregatorBlock> ActiveAggregators = new Dictionary<HudControllerBlock, AggregatorBlock>();

        public override void DoOnce(IControlBlockBase thisLogic)
        {
            base.DoOnce(thisLogic);
            ActiveAggregators[(HudControllerBlock) thisLogic] = null;
        }

        protected override void CreateTerminalActions()
        {
            CreateToggle(
                "AlwaysDisplay",
                "Always Display",
                "Should targets always display, regardless of HUD state?",
                b => b.GameLogic.GetAs<HudControllerBlock>().AlwaysDisplay.Value,
                (b, v) => b.GameLogic.GetAs<HudControllerBlock>().AlwaysDisplay.Value = v
            );
            CreateSlider(
                "CombineAngle",
                "Combine Angle",
                "Angle at which to combine close targets.",
                0,
                10,
                b => MathHelper.ToDegrees(b.GameLogic.GetAs<HudControllerBlock>().CombineAngle.Value),
                (b, v) => b.GameLogic.GetAs<HudControllerBlock>().CombineAngle.Value = MathHelper.ToRadians(v),
                (b, sb) => sb.Append($"{MathHelper.ToDegrees(b.GameLogic.GetAs<HudControllerBlock>().CombineAngle.Value):F}°")
            );
            ActiveAggregatorSelect = new BlockSelectControl<HudControllerBlock, IMyConveyorSorter>(
                "SourceAggregator",
                "Source Aggregator",
                "Aggregator this block should use to display HUD info.",
                true,
                // TODO convert this into a single yield action
                logic => ControlBlockManager.I.Blocks.Values.Where(control => control is AggregatorBlock && control.CubeBlock.CubeGrid == logic.Block.CubeGrid).Select(c => c.CubeBlock),
                (logic, selected) =>
                {
                    if (!MyAPIGateway.Session.IsServer)
                        return;

                    foreach (var control in ControlBlockManager.I.Blocks.Values)
                    {
                        if (!(control is AggregatorBlock) || control.CubeBlock.CubeGrid != logic.Block.CubeGrid || !selected.Contains(control.CubeBlock.EntityId))
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
