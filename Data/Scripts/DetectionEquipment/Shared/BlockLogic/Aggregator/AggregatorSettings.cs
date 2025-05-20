using ProtoBuf;
using Sandbox.ModAPI;
using System;

namespace DetectionEquipment.Shared.BlockLogic.Aggregator
{
    [ProtoContract]
    internal class AggregatorSettings : ControlBlockSettingsBase
    {
        [ProtoMember(1)] private float _aggregationTime = 1f;
        [ProtoMember(3)] private float _velocityErrorThreshold = 32f;
        [ProtoMember(6)] private bool _useAllSensors = true;
        [ProtoMember(7)] private long[] _selectedSensors = Array.Empty<long>();
        [ProtoMember(8)] private int _datalinkOutChannel = 0;
        [ProtoMember(9)] private int[] _datalinkInChannels = { 0 };
        [ProtoMember(10)] private int _datalinkInShareType = 1;
        [ProtoMember(11)] private bool _doWcTargeting = true; 
        [ProtoMember(12)] private bool _useAllWeapons = true;
        [ProtoMember(13)] private long[] _selectedWeapons = Array.Empty<long>();

        [ProtoIgnore] private new AggregatorBlock AttachedLogic => (AggregatorBlock) base.AttachedLogic;

        public AggregatorSettings(AggregatorBlock logic) : base(logic) { }

        protected AggregatorSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<AggregatorSettings>(rawData);

        protected override void AssignData()
        {
            AttachedLogic.AggregationTime.Value = _aggregationTime;
            AttachedLogic.VelocityErrorThreshold.Value = _velocityErrorThreshold;
            AttachedLogic.UseAllSensors.Value = _useAllSensors;

            AggregatorControls.ActiveSensorSelect.UpdateSelected(AttachedLogic, _selectedSensors ?? Array.Empty<long>());
            AggregatorControls.ActiveWeaponSelect?.UpdateSelected(AttachedLogic, _selectedWeapons ?? Array.Empty<long>()); // this might be null if WC isn't loaded

            AttachedLogic.DatalinkOutChannel.Value = _datalinkOutChannel;
            AttachedLogic.DatalinkInChannels = _datalinkInChannels;
            AttachedLogic.DatalinkInShareType.Value = _datalinkInShareType;
            AttachedLogic.DoWcTargeting.Value = _doWcTargeting;
            AttachedLogic.UseAllWeapons.Value = _useAllWeapons;
        }

        protected override void RetrieveData()
        {
            _aggregationTime = AttachedLogic.AggregationTime.Value;
            _velocityErrorThreshold = AttachedLogic.VelocityErrorThreshold.Value;
            _useAllSensors = AttachedLogic.UseAllSensors.Value;

            if (!AggregatorControls.ActiveSensorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out _selectedSensors))
                _selectedSensors = Array.Empty<long>();
            if (!AggregatorControls.ActiveWeaponSelect?.SelectedBlocks.TryGetValue(AttachedLogic, out _selectedWeapons) ?? true)
                _selectedWeapons = Array.Empty<long>();

            _datalinkOutChannel = AttachedLogic.DatalinkOutChannel.Value;
            _datalinkInChannels = AttachedLogic.DatalinkInChannels;
            _datalinkInShareType = AttachedLogic.DatalinkInShareType.Value;
            _doWcTargeting = AttachedLogic.DoWcTargeting.Value;
            _useAllWeapons = AttachedLogic.UseAllWeapons.Value;
        }
    }
}
