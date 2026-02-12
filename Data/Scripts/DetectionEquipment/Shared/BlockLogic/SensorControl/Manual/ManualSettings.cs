using ProtoBuf;
using Sandbox.ModAPI;
using System;

namespace DetectionEquipment.Shared.BlockLogic.SensorControl.Manual
{
    [ProtoContract]
    internal class ManualSettings : ControlBlockSettingsBase
    {
        [ProtoMember(1)] private long[] _selectedSensors = Array.Empty<long>();
        [ProtoMember(3)] private bool _invertAllowControl = false;
        [ProtoMember(4)] private int _controlPriority = 0;
        [ProtoMember(5)] private long[] _selectedControllers = Array.Empty<long>();

        [ProtoIgnore] private new ManualBlock AttachedLogic => (ManualBlock)base.AttachedLogic;

        public ManualSettings(ManualBlock logic) : base(logic) { }

        protected ManualSettings() : base() { }

        protected override ControlBlockSettingsBase Deserialize(byte[] rawData) => MyAPIGateway.Utilities.SerializeFromBinary<ManualSettings>(rawData);

        protected override void AssignData()
        {
            ManualControls.ActiveSensorSelect.UpdateSelectedFromPersistent(AttachedLogic, _selectedSensors ?? Array.Empty<long>());
            AttachedLogic.InvertAllowControl.Value = _invertAllowControl;
            AttachedLogic.ControlPriority.Value = _controlPriority;
            ManualControls.ShipControllersSelect.UpdateSelectedFromPersistent(AttachedLogic, _selectedControllers ?? Array.Empty<long>());
        }

        protected override void RetrieveData()
        {
            if (!ManualControls.ActiveSensorSelect.SelectedBlocks.TryGetValue(AttachedLogic, out _selectedSensors))
                _selectedSensors = Array.Empty<long>();
            _invertAllowControl = AttachedLogic.InvertAllowControl.Value;
            _controlPriority = AttachedLogic.ControlPriority.Value;
            if (!ManualControls.ShipControllersSelect.SelectedBlocks.TryGetValue(AttachedLogic, out _selectedControllers))
                _selectedControllers = Array.Empty<long>();
        }
    }
}
