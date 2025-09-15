using DetectionEquipment.Server.SensorBlocks;
using System;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic
{
    internal interface IControlBlockBase
    {
        IMyCubeBlock CubeBlock { get; }
        GridSensorManager GridSensors { get; }
        Action OnClose { get; set; }

        void UpdateAfterSimulation();
        void UpdateAfterSimulation10();

        void Init();
        void Serialize();
        void MarkForClose(IMyEntity entity);
    }
}