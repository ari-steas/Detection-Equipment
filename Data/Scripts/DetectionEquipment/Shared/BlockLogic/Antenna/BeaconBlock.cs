using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.BlockLogic.IffReflector;
using DetectionEquipment.Shared.Definitions;
using Sandbox.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.Antenna
{
    internal class BeaconBlock : IffReflectorBlock
    {
        public override SensorDefinition.SensorType SensorType => SensorDefinition.SensorType.Antenna;
        protected override ITerminalControlAdder GetControls => new BeaconControls();

        public BeaconBlock(IMyFunctionalBlock block) : base(block)
        {
        }
    }
}
