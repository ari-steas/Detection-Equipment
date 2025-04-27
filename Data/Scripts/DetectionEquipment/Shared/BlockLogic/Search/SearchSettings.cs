using ProtoBuf;
using Sandbox.ModAPI;
using System;

namespace DetectionEquipment.Shared.BlockLogic.Search
{
    [ProtoContract]
    internal class SearchSettings : ControlBlockSettingsBase
    {
        [ProtoMember(1)] public long[] SelectedSensors = Array.Empty<long>();

        [ProtoIgnore] public new SearchBlock AttachedLogic => (SearchBlock) base.AttachedLogic;

        public SearchSettings(SearchBlock logic) : base(logic) { }

        protected SearchSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<SearchSettings>(rawData);

        protected override void AssignData()
        {
            SearchControls.ActiveSensorSelect.UpdateSelected(AttachedLogic, SelectedSensors ?? Array.Empty<long>(), false);
        }

        protected override void RetrieveData()
        {
            if (!SearchControls.ActiveSensorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out SelectedSensors))
                SelectedSensors = Array.Empty<long>();
        }
    }
}
