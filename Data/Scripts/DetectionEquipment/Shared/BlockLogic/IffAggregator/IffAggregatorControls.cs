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
                b => b.GameLogic.GetAs<IffAggregatorBlock>()?.AutoSelfIff.Value ?? false,
                (b, v) => b.GameLogic.GetAs<IffAggregatorBlock>().AutoSelfIff.Value = v
            );
            CreateTextbox(
                "FriendlyIffCodes",
                "Friendly IFF Codes",
                "Known friendly IFF codes, comma-separated.",
                b => new StringBuilder(string.Join(",", b.GameLogic.GetAs<IffAggregatorBlock>()?.FriendlyIffCodes.Value ?? Array.Empty<string>())),
                (b, v) => b.GameLogic.GetAs<IffAggregatorBlock>().FriendlyIffCodes.Value = v.ToString().Split(',').Select(code => code.Trim()).ToArray()
            );
        }

        protected override void CreateTerminalProperties()
        {

        }
    }
}
