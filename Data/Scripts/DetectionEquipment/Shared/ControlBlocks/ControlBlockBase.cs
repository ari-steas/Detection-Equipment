using DetectionEquipment.Server;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.ControlBlocks.Controls;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace DetectionEquipment.Shared.ControlBlocks
{
    internal abstract class ControlBlockBase : MyGameLogicComponent
    {
        protected IMyConveyorSorter Block;
        protected GridSensorManager GridSensors;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            Block = (IMyConveyorSorter) Entity;

            NeedsUpdate |= MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            if(Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            HideSorterControls.DoOnce();

            if (!MyAPIGateway.Session.IsServer)
                return;

            ControlBlockManager.I.Blocks.Add((MyCubeBlock) Block, this);
            GridSensors = ServerMain.I.GridSensorMangers[Block.CubeGrid];
            NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void MarkForClose()
        {
            base.MarkForClose();
            ControlBlockManager.I.Blocks.Remove((MyCubeBlock) Block);
        }
    }
}
