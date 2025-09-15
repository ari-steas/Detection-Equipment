using Sandbox.ModAPI;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Helpers;
using DetectionEquipment.Shared.Utils;
using VRage.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.IffReflector
{
    internal class IffReflectorBlock : ControlBlockBase<IMyFunctionalBlock>
    {
        public readonly SimpleSync<string> IffCode = new SimpleSync<string>("NOCODE");
        public readonly SimpleSync<bool> ReturnHash = new SimpleSync<bool>(true);

        public string IffCodeCache { get; private set; } = "NOCODE";
        public virtual SensorDefinition.SensorType SensorType => SensorDefinition.SensorType.Radar;
        public bool Enabled => Block.IsWorking || GlobalData.ForceEnableIff.Value;
        public string DefaultCode => Block.CubeGrid.CustomName;

        protected override ControlBlockSettingsBase GetSettings => new IffReflectorSettings(this);
        protected override ITerminalControlAdder GetControls => new IffControls();

        public IffReflectorBlock(IMyFunctionalBlock block) : base(block)
        {
        }

        public override void Init()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            IffCode.Value = DefaultCode;
            IffCode.Component = this;
            IffCode.Validate = value => value.RemoveChars(',', '#', '&').Trim();
            IffCode.OnValueChanged = (value, fromNetwork) =>
            {
                IffCodeCache =
                    ReturnHash.Value
                        ? "#" + IffHelper.GetIffHashCode(IffCode.Value)
                        : "&" + IffCode.Value; // note that commas aren't allowed.
            };

            ReturnHash.Component = this;
            ReturnHash.OnValueChanged = (value, fromNetwork) =>
            {
                IffCodeCache = ReturnHash.Value ? "#" + IffCode.Value.GetHashCode() : "&" + IffCode.Value;
            };

            IffCode.OnValueChanged.Invoke(IffCode.Value, false);
            base.Init();

            IffHelper.RegisterComponent(Block.CubeGrid, this);
        }

        public override void MarkForClose(IMyEntity entity)
        {
            base.MarkForClose(entity);

            IffHelper.RemoveComponent(Block.CubeGrid, this);
        }

        public void ForceUpdateHash()
        {
            if (!ReturnHash.Value)
                return;
            IffCode.OnValueChanged.Invoke(IffCode.Value, false);
        }
    }
}
