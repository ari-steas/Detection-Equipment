using ProtoBuf;
using Sandbox.ModAPI;
using System;

namespace DetectionEquipment.Shared.BlockLogic.Tracker
{
    [ProtoContract]
    internal class TrackerSettings : ControlBlockSettingsBase
    {
        [ProtoMember(1)] public long[] SelectedAggregators = Array.Empty<long>();
        [ProtoMember(2)] public long[] SelectedSensors = Array.Empty<long>();
        [ProtoMember(3)] public float ResetAngleTime = 4;

        [ProtoIgnore] public new TrackerBlock AttachedLogic => (TrackerBlock) base.AttachedLogic;

        public TrackerSettings(TrackerBlock logic) : base(logic) { }

        protected TrackerSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<TrackerSettings>(rawData);

        protected override void AssignData()
        {
            TrackerControls.ActiveAggregatorSelect.UpdateSelected(AttachedLogic, SelectedAggregators ?? Array.Empty<long>(), false);
            TrackerControls.ActiveSensorSelect.UpdateSelected(AttachedLogic, SelectedSensors ?? Array.Empty<long>(), false);
            AttachedLogic.ResetAngleTime.Value = ResetAngleTime;
        }

        protected override void RetrieveData()
        {
            if (!TrackerControls.ActiveAggregatorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out SelectedAggregators))
                SelectedAggregators = Array.Empty<long>();
            if (!TrackerControls.ActiveSensorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out SelectedSensors))
                SelectedSensors = Array.Empty<long>();
            ResetAngleTime = AttachedLogic.ResetAngleTime.Value;
        }
    }
}
