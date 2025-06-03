using DetectionEquipment.Shared.BlockLogic.ControlBlocks;
using ProtoBuf;
using Sandbox.ModAPI;
using System;

namespace DetectionEquipment.Shared.BlockLogic.ControlBlocks.Search
{
    [ProtoContract]
    internal class SearchSettings : ControlBlockSettingsBase
    {
        [ProtoMember(1)] private long[] _selectedSensors = Array.Empty<long>();

        [ProtoIgnore] private new SearchBlock AttachedLogic => (SearchBlock)base.AttachedLogic;

        public SearchSettings(SearchBlock logic) : base(logic) { }

        protected SearchSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<SearchSettings>(rawData);

        protected override void AssignData()
        {
            SearchControls.ActiveSensorSelect.UpdateSelected(AttachedLogic, _selectedSensors ?? Array.Empty<long>());
        }

        protected override void RetrieveData()
        {
            if (!SearchControls.ActiveSensorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out _selectedSensors))
                _selectedSensors = Array.Empty<long>();
        }
    }
}
