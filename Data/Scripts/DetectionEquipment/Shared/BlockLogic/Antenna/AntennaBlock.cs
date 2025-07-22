using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.BlockLogic.IffReflector;
using DetectionEquipment.Shared.Definitions;
using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;

namespace DetectionEquipment.Shared.BlockLogic.Antenna
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_RadioAntenna), false)]
    internal class AntennaBlock : IffReflectorBlock
    {
        public override SensorDefinition.SensorType SensorType => SensorDefinition.SensorType.Antenna;
        protected override ITerminalControlAdder GetControls => new AntennaControls();
    }
}
