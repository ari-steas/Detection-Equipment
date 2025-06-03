using System;
using DetectionEquipment.Shared.BlockLogic.ControlBlocks;
using ProtoBuf;
using Sandbox.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.ControlBlocks.HudController
{
    [ProtoContract]
    internal class HudControllerSettings : ControlBlockSettingsBase
    {
        [ProtoMember(1)] public long[] SelectedAggregator = Array.Empty<long>();
        [ProtoIgnore] private new HudControllerBlock AttachedLogic => (HudControllerBlock)base.AttachedLogic;

        public HudControllerSettings(HudControllerBlock logic) : base(logic) { }

        protected HudControllerSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<HudControllerSettings>(rawData);

        protected override void AssignData()
        {
            HudControllerControls.ActiveAggregatorSelect.UpdateSelected(AttachedLogic, SelectedAggregator);
        }

        protected override void RetrieveData()
        {
            if (!HudControllerControls.ActiveAggregatorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out SelectedAggregator))
                SelectedAggregator = Array.Empty<long>();
        }
    }
}
