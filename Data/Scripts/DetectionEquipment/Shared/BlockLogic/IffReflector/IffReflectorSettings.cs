using ProtoBuf;
using Sandbox.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.IffReflector
{
    [ProtoContract]
    internal class IffReflectorSettings : ControlBlockSettingsBase
    {
        [ProtoMember(1)] public string IffCode = "";
        [ProtoMember(2)] public bool ReturnHash = true;

        [ProtoIgnore] public new IffReflectorBlock AttachedLogic => (IffReflectorBlock) base.AttachedLogic;

        public IffReflectorSettings(IffReflectorBlock logic) : base(logic)
        {
            IffCode = logic.CubeBlock.CubeGrid.CustomName;
        }

        protected IffReflectorSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<IffReflectorSettings>(rawData);

        protected override void AssignData()
        {
            AttachedLogic.IffCode = IffCode;
            AttachedLogic.ReturnHash = ReturnHash;
        }

        protected override void RetrieveData()
        {
            IffCode = AttachedLogic.IffCode;
            ReturnHash = AttachedLogic.ReturnHash;
        }
    }
}
