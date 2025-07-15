using DetectionEquipment.Shared.Definitions;

namespace DetectionEquipment.Shared.Helpers
{
    internal interface IIffComponent
    {
        bool Enabled { get; }
        string IffCodeCache { get; }
        SensorDefinition.SensorType SensorType { get; }
    }
}
