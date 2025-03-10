using DetectionEquipment.Shared.ControlBlocks.GenericControls;
using DetectionEquipment.Shared.ControlBlocks.Tracker;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DetectionEquipment.Shared.ControlBlocks.IffReflector
{
    internal class IffControls : TerminalControlAdder<IffReflectorBlock, IMyConveyorSorter>
    {
        protected override void CreateTerminalActions()
        {
            CreateTextbox(
                "IffCode",
                "IFF Code",
                "IFF code returned when a radar pings this grid",
                b => new StringBuilder(b.GameLogic.GetAs<IffReflectorBlock>()?.IffCode),
                (b, v) => b.GameLogic.GetAs<IffReflectorBlock>().IffCode.Value = v.ToString()
                );
            CreateToggle(
                "ReturnHash",
                "Return Hashed IFF Code",
                "Whether the IFF code should be returned as a hash or a plain string.",
                b => b.GameLogic.GetAs<IffReflectorBlock>()?.ReturnHash,
                (b, v) => b.GameLogic.GetAs<IffReflectorBlock>().ReturnHash.Value = v
                );
        }

        protected override void CreateTerminalProperties()
        {
            
        }
    }
}
