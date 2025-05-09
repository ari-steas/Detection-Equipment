using ProtoBuf;
using Sandbox.ModAPI;
using System;

namespace DetectionEquipment.Shared.BlockLogic.Tracker
{
    [ProtoContract]
    internal class TrackerSettings : ControlBlockSettingsBase
    {
        [ProtoMember(1)] private long[] _selectedAggregators = Array.Empty<long>();
        [ProtoMember(2)] private long[] _selectedSensors = Array.Empty<long>();
        [ProtoMember(3)] private float _resetAngleTime = 4;

        [ProtoIgnore] private new TrackerBlock AttachedLogic => (TrackerBlock) base.AttachedLogic;

        public TrackerSettings(TrackerBlock logic) : base(logic) { }

        protected TrackerSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<TrackerSettings>(rawData);

        protected override void AssignData()
        {
            TrackerControls.ActiveAggregatorSelect.UpdateSelected(AttachedLogic, _selectedAggregators ?? Array.Empty<long>());
            TrackerControls.ActiveSensorSelect.UpdateSelected(AttachedLogic, _selectedSensors ?? Array.Empty<long>());
            AttachedLogic.ResetAngleTime.Value = _resetAngleTime;
        }

        protected override void RetrieveData()
        {
            if (!TrackerControls.ActiveAggregatorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out _selectedAggregators))
                _selectedAggregators = Array.Empty<long>();
            if (!TrackerControls.ActiveSensorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out _selectedSensors))
                _selectedSensors = Array.Empty<long>();
            _resetAngleTime = AttachedLogic.ResetAngleTime.Value;
        }
    }
}
