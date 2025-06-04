using ProtoBuf;
using Sandbox.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.IffReflector
{
    [ProtoContract]
    internal class IffReflectorSettings : ControlBlockSettingsBase
    {
        [ProtoMember(1)] private string _iffCode = "";
        [ProtoMember(2)] private bool _returnHash = true;

        [ProtoIgnore] private new IffReflectorBlock AttachedLogic => (IffReflectorBlock)base.AttachedLogic;

        public IffReflectorSettings(IffReflectorBlock logic) : base(logic)
        {
            _iffCode = logic.CubeBlock.CubeGrid.CustomName;
        }

        protected IffReflectorSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<IffReflectorSettings>(rawData);

        protected override void AssignData()
        {
            AttachedLogic.IffCode.Value = _iffCode;
            AttachedLogic.ReturnHash.Value = _returnHash;
        }

        protected override void RetrieveData()
        {
            _iffCode = AttachedLogic.IffCode.Value;
            _returnHash = AttachedLogic.ReturnHash.Value;
        }
    }
}
