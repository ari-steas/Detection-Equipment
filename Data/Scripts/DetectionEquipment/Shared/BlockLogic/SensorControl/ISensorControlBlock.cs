using DetectionEquipment.Shared.Networking;

namespace DetectionEquipment.Shared.BlockLogic.SensorControl
{
    internal interface ISensorControlBlock
    {
        SimpleSync<bool> InvertAllowControl { get; }
        SimpleSync<int> ControlPriority { get; }
    }
}
