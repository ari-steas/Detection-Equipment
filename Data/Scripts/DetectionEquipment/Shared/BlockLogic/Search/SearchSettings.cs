using ProtoBuf;
using Sandbox.ModAPI;
using System;

namespace DetectionEquipment.Shared.BlockLogic.Search
{
    [ProtoContract]
    internal class SearchSettings : ControlBlockSettingsBase
    {
        [ProtoMember(1)] private long[] _selectedSensors = Array.Empty<long>();
        [ProtoMember(2)] private SearchBlock.SearchModes _searchMode = SearchBlock.SearchModes.Auto;
        [ProtoMember(3)] private bool _invertAllowControl = false;

        [ProtoIgnore] private new SearchBlock AttachedLogic => (SearchBlock)base.AttachedLogic;

        public SearchSettings(SearchBlock logic) : base(logic) { }

        protected SearchSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<SearchSettings>(rawData);

        protected override void AssignData()
        {
            SearchControls.ActiveSensorSelect.UpdateSelectedFromPersistent(AttachedLogic, _selectedSensors ?? Array.Empty<long>());
            AttachedLogic.SearchMode.Value = _searchMode;
            AttachedLogic.InvertAllowControl.Value = _invertAllowControl;
        }

        protected override void RetrieveData()
        {
            if (!SearchControls.ActiveSensorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out _selectedSensors))
                _selectedSensors = Array.Empty<long>();
            _searchMode = AttachedLogic.SearchMode.Value;
            _invertAllowControl = AttachedLogic.InvertAllowControl.Value;
        }
    }
}
