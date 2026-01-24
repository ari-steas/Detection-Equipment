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
        [ProtoMember(4)] private bool _invertAllowControl = false;
        [ProtoMember(5)] private bool _trackAllies = false;
        [ProtoMember(6)] private bool _trackEnemies = true;
        [ProtoMember(7)] private bool _trackNeutrals = true;

        [ProtoIgnore] private new TrackerBlock AttachedLogic => (TrackerBlock)base.AttachedLogic;

        public TrackerSettings(TrackerBlock logic) : base(logic) { }

        protected TrackerSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<TrackerSettings>(rawData);

        protected override void AssignData()
        {
            TrackerControls.ActiveAggregatorSelect.UpdateSelectedFromPersistent(AttachedLogic, _selectedAggregators ?? Array.Empty<long>());
            TrackerControls.ActiveSensorSelect.UpdateSelectedFromPersistent(AttachedLogic, _selectedSensors ?? Array.Empty<long>());
            AttachedLogic.ResetAngleTime.Value = _resetAngleTime;
            AttachedLogic.InvertAllowControl.Value = _invertAllowControl;
            AttachedLogic.TrackAllies.Value = _trackAllies;
            AttachedLogic.TrackEnemies.Value = _trackEnemies;
            AttachedLogic.TrackNeutrals.Value = _trackNeutrals;
        }

        protected override void RetrieveData()
        {
            if (!TrackerControls.ActiveAggregatorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out _selectedAggregators))
                _selectedAggregators = Array.Empty<long>();
            if (!TrackerControls.ActiveSensorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out _selectedSensors))
                _selectedSensors = Array.Empty<long>();
            _resetAngleTime = AttachedLogic.ResetAngleTime.Value;
            _invertAllowControl = AttachedLogic.InvertAllowControl.Value;
            _trackAllies = AttachedLogic.TrackAllies.Value;
            _trackEnemies = AttachedLogic.TrackEnemies.Value;
            _trackNeutrals = AttachedLogic.TrackNeutrals.Value;
        }
    }
}
