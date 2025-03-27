using ProtoBuf;
using Sandbox.ModAPI;
using System;

namespace DetectionEquipment.Shared.BlockLogic.Aggregator
{
    [ProtoContract]
    internal class AggregatorSettings : ControlBlockSettingsBase
    {
        [ProtoMember(1)] public float AggregationTime = 1f;
        [ProtoMember(2)] public float DistanceThreshold = 2f;
        [ProtoMember(3)] public float VelocityErrorThreshold = 32f;
        [ProtoMember(4)] public float RCSThreshold = 1f;
        [ProtoMember(5)] public bool AggregateTypes = true;
        [ProtoMember(6)] public bool UseAllSensors = true;
        [ProtoMember(7)] public long[] SelectedSensors = Array.Empty<long>();

        [ProtoIgnore] public new AggregatorBlock AttachedLogic => (AggregatorBlock) base.AttachedLogic;

        public AggregatorSettings(AggregatorBlock logic) : base(logic) { }

        protected AggregatorSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<AggregatorSettings>(rawData);

        protected override void AssignData()
        {
            AttachedLogic.AggregationTime.Value = AggregationTime;
            AttachedLogic.DistanceThreshold.Value = DistanceThreshold;
            AttachedLogic.VelocityErrorThreshold.Value = VelocityErrorThreshold;
            AttachedLogic.RCSThreshold.Value = RCSThreshold;
            AttachedLogic.AggregateTypes.Value = AggregateTypes;
            AttachedLogic.UseAllSensors.Value = UseAllSensors;

            AggregatorControls.ActiveSensorSelect.UpdateSelected(AttachedLogic, SelectedSensors ?? Array.Empty<long>(), false);
        }

        protected override void RetrieveData()
        {
            AggregationTime = AttachedLogic.AggregationTime.Value;
            DistanceThreshold = AttachedLogic.DistanceThreshold.Value;
            VelocityErrorThreshold = AttachedLogic.VelocityErrorThreshold.Value;
            RCSThreshold = AttachedLogic.RCSThreshold.Value;
            AggregateTypes = AttachedLogic.AggregateTypes.Value;
            UseAllSensors = AttachedLogic.UseAllSensors.Value;

            if (!AggregatorControls.ActiveSensorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out SelectedSensors))
                SelectedSensors = Array.Empty<long>();
        }
    }
}
