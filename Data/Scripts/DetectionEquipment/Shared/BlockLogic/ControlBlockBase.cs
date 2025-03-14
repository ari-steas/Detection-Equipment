using DetectionEquipment.Server;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;

namespace DetectionEquipment.Shared.BlockLogic
{
    internal abstract class ControlBlockBase<TBlock> : MyGameLogicComponent, IMyEventProxy, IControlBlockBase where TBlock : IMyTerminalBlock, IMyFunctionalBlock
    {
        public TBlock Block;
        public IMyCubeBlock CubeBlock => Block;
        public GridSensorManager GridSensors { get; private set; }

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Block = (TBlock)Entity;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            HideSorterControls.DoOnce();

            if (!MyAPIGateway.Session.IsServer)
                return;

            ControlBlockManager.I.Blocks.Add(Block as MyCubeBlock, this);
            GridSensors = ServerMain.I.GridSensorMangers[Block.CubeGrid];
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void MarkForClose()
        {
            base.MarkForClose();
            ControlBlockManager.I.Blocks.Remove(Block as MyCubeBlock);
        }
    }
}
