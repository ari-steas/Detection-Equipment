using DetectionEquipment.Server.SensorBlocks;
using System;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;

namespace DetectionEquipment.Shared.BlockLogic.ControlBlocks
{
    internal interface IControlBlockBase
    {
        IMyCubeBlock CubeBlock { get; }
        GridSensorManager GridSensors { get; }
        Action OnClose { get; set; }

        void Init(MyObjectBuilder_EntityBase objectBuilder);
        void MarkForClose();
        void UpdateOnceBeforeFrame();
    }
}