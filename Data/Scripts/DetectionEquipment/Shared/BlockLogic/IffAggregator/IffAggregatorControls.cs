using System;
using System.Linq;
using System.Text;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.IffAggregator
{
    internal class IffAggregatorControls : TerminalControlAdder<IffAggregatorBlock, IMyConveyorSorter>
    {
        public override void DoOnce(IControlBlockBase thisLogic)
        {
            new AggregatorControls().DoOnce(thisLogic); // can't inherit directly because _isDone is shared.
            base.DoOnce(thisLogic);
        }

        protected override void CreateTerminalActions()
        {
            CreateSeperator("IffSeparator");
            CreateToggle(
                "AutoSelfIff",
                "Auto-add Grid IFF",
                "Should IFF codes in reflectors on this grid be marked as friendly?",
                b => ControlBlockManager.GetLogic<IffAggregatorBlock>(b)?.AutoSelfIff.Value ?? false,
                (b, v) => ControlBlockManager.GetLogic<IffAggregatorBlock>(b).AutoSelfIff.Value = v
            );
            CreateTextbox(
                "FriendlyIffCodes",
                "Friendly IFF Codes",
                "Known friendly IFF codes, comma-separated.",
                b => new StringBuilder(string.Join(",", ControlBlockManager.GetLogic<IffAggregatorBlock>(b)?.FriendlyIffCodes.Value ?? Array.Empty<string>())),
                (b, v) => ControlBlockManager.GetLogic<IffAggregatorBlock>(b).FriendlyIffCodes.Value = v.ToString().Split(',').Select(code => code.Trim()).ToArray()
            );
        }

        protected override void CreateTerminalProperties()
        {

        }
    }
}
