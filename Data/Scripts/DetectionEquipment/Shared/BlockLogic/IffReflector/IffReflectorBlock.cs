using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using DetectionEquipment.Shared.Networking;
using VRage.Game.Components;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Helpers;

namespace DetectionEquipment.Shared.BlockLogic.IffReflector
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "IffReflector", "IffReflector_Small", "TorpIFF")]
    internal class IffReflectorBlock : ControlBlockBase<IMyFunctionalBlock>, IIffComponent
    {
        public readonly SimpleSync<string> IffCode = new SimpleSync<string>("");
        public readonly SimpleSync<bool> ReturnHash = new SimpleSync<bool>(true);

        public string IffCodeCache { get; private set; } = "";
        public virtual SensorDefinition.SensorType SensorType => SensorDefinition.SensorType.Radar;
        public bool Enabled => Block.Enabled;

        protected override ControlBlockSettingsBase GetSettings => new IffReflectorSettings(this);
        protected override ITerminalControlAdder GetControls => new IffControls();

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null || GlobalData.Killswitch) // ignore projected and other non-physical grids
                return;

            IffCode.Value = Block.CubeGrid.CustomName;
            IffCode.Component = this;
            IffCode.OnValueChanged = (value, fromNetwork) =>
            {
                IffCodeCache =
                    ReturnHash.Value
                        ? "#" + IffCode.Value.GetHashCode()
                        : "&" + IffCode.Value; // note that commas aren't allowed.
            };
            ReturnHash.Component = this;
            ReturnHash.OnValueChanged = (value, fromNetwork) =>
            {
                IffCodeCache = ReturnHash.Value ? "#" + IffCode.Value.GetHashCode() : "&" + IffCode.Value;
            };

            base.UpdateOnceBeforeFrame();

            IffHelper.RegisterComponent(Block.CubeGrid, this);
        }

        public override void MarkForClose()
        {
            base.MarkForClose();
            if (GlobalData.Killswitch)
                return;

            IffHelper.RemoveComponent(Block.CubeGrid, this);
        }
    }
}
