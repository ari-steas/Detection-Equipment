﻿using System;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using ProtoBuf;
using Sandbox.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.IffAggregator
{
    [ProtoContract]
    internal class IffAggregatorSettings : AggregatorSettings
    {
        [ProtoMember(14)] private bool _autoSelfIff = true;
        [ProtoMember(15)] private string[] _friendlyIffCodes = Array.Empty<string>();

        [ProtoIgnore] private new IffAggregatorBlock AttachedLogic => (IffAggregatorBlock)base.AttachedLogic;

        public IffAggregatorSettings(IffAggregatorBlock logic) : base(logic) { }

        protected IffAggregatorSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<IffAggregatorSettings>(rawData);

        protected override void AssignData()
        {
            base.AssignData();
            AttachedLogic.AutoSelfIff.Value = _autoSelfIff;
            AttachedLogic.FriendlyIffCodes.Value = _friendlyIffCodes;
        }

        protected override void RetrieveData()
        {
            base.RetrieveData();
            _autoSelfIff = AttachedLogic.AutoSelfIff.Value;
            _friendlyIffCodes = AttachedLogic.FriendlyIffCodes.Value;
        }
    }
}
