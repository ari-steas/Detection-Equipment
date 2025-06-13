using DetectionEquipment.Shared.BlockLogic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Client.BlockLogic
{
    internal interface IBlockLogic
    {
        IMyTerminalBlock Block { get; set; }
        bool IsClosed { get; set; }
        void Register(IMyTerminalBlock block);
        void Close();


        void UpdateAfterSimulation();
        void UpdateFromNetwork(BlockLogicUpdatePacket updateData);
    }
}
