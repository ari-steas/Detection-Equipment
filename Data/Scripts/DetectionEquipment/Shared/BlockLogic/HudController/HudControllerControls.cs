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
            ActiveAggregators[(HudControllerBlock)thisLogic] = null;
        }

        protected override void CreateTerminalActions()
        {
            CreateToggle(
                "AlwaysDisplay",
                "Always Display",
                "Should targets always display, regardless of HUD state?",
                b => ControlBlockManager.GetLogic<HudControllerBlock>(b).AlwaysDisplay.Value,
                (b, v) => ControlBlockManager.GetLogic<HudControllerBlock>(b).AlwaysDisplay.Value = v
            );
            //CreateSlider(
            //    "CombineAngle",
            //    "Combine Angle",
            //    "Angle at which to combine close targets.",
            //    0,
            //    10,
            //    b => MathHelper.ToDegrees(b.GameLogic.GetAs<HudControllerBlock>().CombineAngle.Value),
            //    (b, v) => b.GameLogic.GetAs<HudControllerBlock>().CombineAngle.Value = MathHelper.ToRadians(v),
            //    (b, sb) => sb.Append($"{MathHelper.ToDegrees(b.GameLogic.GetAs<HudControllerBlock>().CombineAngle.Value):F}°")
            //);
            CreateToggle(
                "DisplaySelf",
                "Display Self",
                "Should this grid's track be visible if detected over datalink?",
                b => ControlBlockManager.GetLogic<HudControllerBlock>(b).ShowSelf.Value,
                (b, v) => ControlBlockManager.GetLogic<HudControllerBlock>(b).ShowSelf.Value = v
            );
            ActiveAggregatorSelect = new BlockSelectControl<HudControllerBlock, IMyConveyorSorter>(
                this,
                "SourceAggregator",
                "Source Aggregator",
                "Aggregator this block should use to display HUD info.",
                true,
                false,
                // TODO convert this into a single yield action
                logic => ControlBlockManager.I.Blocks.Values.Where(control => control is AggregatorBlock && control.CubeBlock.CubeGrid == logic.Block.CubeGrid).Select(c => c.CubeBlock),
                (logic, selected) =>
                {
                    // if (!MyAPIGateway.Session.IsServer) // clients need access to this for HUD sensor info
                    //     return;

                    foreach (var control in ControlBlockManager.I.Blocks.Values)
                    {
                        if (!(control is AggregatorBlock) || control.CubeBlock.CubeGrid != logic.Block.CubeGrid || !selected.Contains(control.CubeBlock))
                            continue;
                        ActiveAggregators[logic] = (AggregatorBlock)control;
                    }
                }
            );
            ActiveAggregatorSelect.ListBox.VisibleRowsCount = 5;

            CreateColor(
                "FriendlyColor",
                "Friendly Color",
                "Outline color for friendly tracks. Global per-player.",
                b => UserData.HudFriendlyColor.Value,
                (b, v) => UserData.HudFriendlyColor.Value = v
            );
            CreateColor(
                "NeutralColor",
                "Neutral Color",
                "Outline color for neutral tracks. Global per-player.",
                b => UserData.HudNeutralColor.Value,
                (b, v) => UserData.HudNeutralColor.Value = v
            );
            CreateColor(
                "EnemyColor",
                "Enemy Color",
                "Outline color for enemy tracks. Global per-player.",
                b => UserData.HudEnemyColor.Value,
                (b, v) => UserData.HudEnemyColor.Value = v
            );
            CreateColor(
                "TextColor",
                "Text Color",
                "Color for track text info. Global per-player.",
                b => UserData.HudTextColor.Value,
                (b, v) => UserData.HudTextColor.Value = v
            );
        }

        protected override void CreateTerminalProperties()
        {

        }
    }
}
