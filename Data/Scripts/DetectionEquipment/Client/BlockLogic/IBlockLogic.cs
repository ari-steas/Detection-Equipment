using VRage.Game.ModAPI;

namespace DetectionEquipment.Client.BlockLogic
{
    internal interface IBlockLogic
    {
        IMyCubeBlock Block { get; set; }
        bool IsClosed { get; set; }
        void Register(IMyCubeBlock block);
        void Close();


        void UpdateAfterSimulation();
        void UpdateFromNetwork(BlockLogicUpdatePacket updateData);
    }
}
