using DetectionEquipment.Shared.BlockLogic.GenericControls;
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
                "IFF code returned when a sensor pings this grid",
                b => new StringBuilder(ControlBlockManager.GetLogic<IffReflectorBlock>(b)?.IffCode.Value),
                (b, v) => ControlBlockManager.GetLogic<IffReflectorBlock>(b).IffCode.Value = v?.ToString()
                );
            CreateToggle(
                "ReturnHash",
                "Return Hashed IFF Code",
                "Whether the IFF code should be returned as a hash or a plain string.",
                b => ControlBlockManager.GetLogic<IffReflectorBlock>(b)?.ReturnHash.Value ?? false,
                (b, v) => ControlBlockManager.GetLogic<IffReflectorBlock>(b).ReturnHash.Value = v
                );
        }

        protected override void CreateTerminalProperties()
        {

        }
    }
}
