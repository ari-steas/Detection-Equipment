using System;
using ProtoBuf;
using Sandbox.ModAPI;
using VRageMath;

namespace DetectionEquipment.Shared.BlockLogic.HudController
{
    [ProtoContract]
    internal class HudControllerSettings : ControlBlockSettingsBase
    {
        [ProtoMember(1)] public long[] SelectedAggregator = Array.Empty<long>();
        [ProtoMember(2)] public bool AlwaysDisplay = false;
        [ProtoMember(3)] public float CombineAngle = (float) MathHelper.ToRadians(2.5);
        [ProtoMember(4)] public bool ShowSelf = false;
        [ProtoIgnore] private new HudControllerBlock AttachedLogic => (HudControllerBlock)base.AttachedLogic;

        public HudControllerSettings(HudControllerBlock logic) : base(logic) { }

        protected HudControllerSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<HudControllerSettings>(rawData);

        protected override void AssignData()
        {
            HudControllerControls.ActiveAggregatorSelect.UpdateSelectedFromPersistent(AttachedLogic, SelectedAggregator);
            AttachedLogic.AlwaysDisplay.Value = AlwaysDisplay;
            AttachedLogic.CombineAngle.Value = CombineAngle;
            AttachedLogic.ShowSelf.Value = ShowSelf;
        }

        protected override void RetrieveData()
        {
            if (!HudControllerControls.ActiveAggregatorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out SelectedAggregator))
                SelectedAggregator = Array.Empty<long>();
            AlwaysDisplay = AttachedLogic.AlwaysDisplay.Value;
            CombineAngle = AttachedLogic.CombineAngle.Value;
            ShowSelf = AttachedLogic.ShowSelf.Value;
        }
    }
}
