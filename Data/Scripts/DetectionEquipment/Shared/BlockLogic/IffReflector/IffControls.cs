﻿using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System.Text;

namespace DetectionEquipment.Shared.BlockLogic.IffReflector
{
    internal class IffControls : TerminalControlAdder<IffReflectorBlock, IMyConveyorSorter>
    {
        protected override void CreateTerminalActions()
        {
            CreateTextbox(
                "IffCode",
                "IFF Code",
                "IFF code returned when a radar pings this grid",
                b => new StringBuilder(b.GameLogic.GetAs<IffReflectorBlock>()?.IffCode.Value),
                (b, v) => b.GameLogic.GetAs<IffReflectorBlock>().IffCode.Value = v?.ToString().RemoveChars(',', '#', '&').Trim()
                );
            CreateToggle(
                "ReturnHash",
                "Return Hashed IFF Code",
                "Whether the IFF code should be returned as a hash or a plain string.",
                b => b.GameLogic.GetAs<IffReflectorBlock>()?.ReturnHash.Value ?? false,
                (b, v) => b.GameLogic.GetAs<IffReflectorBlock>().ReturnHash.Value = v
                );
        }

        protected override void CreateTerminalProperties()
        {

        }
    }
}
