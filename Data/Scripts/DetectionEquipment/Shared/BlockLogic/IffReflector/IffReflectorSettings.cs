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
            IffCode = logic.CubeBlock.GetOwnerFactionTag();
        }

        protected IffReflectorSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<IffReflectorSettings>(rawData);

        protected override void AssignData()
        {
            AttachedLogic.IffCode.Value = IffCode;
            AttachedLogic.ReturnHash.Value = ReturnHash;
        }

        protected override void RetrieveData()
        {
            IffCode = AttachedLogic.IffCode.Value;
            ReturnHash = AttachedLogic.ReturnHash.Value;
        }
    }
}
