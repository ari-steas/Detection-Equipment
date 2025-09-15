using DetectionEquipment.Server;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Utils;
using Sandbox.Definitions;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic
{
    internal abstract class ControlBlockBase<TBlock> : IControlBlockBase
        where TBlock : IMyTerminalBlock, IMyFunctionalBlock
    {
        public TBlock Block;
        public IMyCubeBlock CubeBlock => Block;
        public GridSensorManager GridSensors { get; private set; }
        public Action OnClose { get; set; }
        protected abstract ControlBlockSettingsBase GetSettings { get; }
        protected abstract ITerminalControlAdder GetControls { get; }

        protected ControlBlockBase(IMyFunctionalBlock block)
        {
            Block = (TBlock) block;
        }

        public virtual void Init()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            try
            {
                HideSorterControls.DoOnce(Block);

                if (MyAPIGateway.Session.IsServer)
                {
                    GridSensors = ServerMain.I.GridSensorMangers[Block.CubeGrid];
                }

                GetControls.DoOnce(this);
                GetSettings?.LoadSettings();
            }
            catch (Exception ex)
            {
                Log.Exception($"ControlBlockBase::{GetType().Name}", ex, true);
            }
        }

        public virtual void UpdateAfterSimulation() { }

        public virtual void UpdateAfterSimulation10() { } // TODO remove try/catch spam?

        public virtual void Serialize()
        {
            GetSettings?.SaveBlockSettings();
        }

        public virtual void MarkForClose(IMyEntity entity)
        {
            ControlBlockManager.I.Blocks.Remove(Block as MyCubeBlock);
            OnClose?.Invoke();
        }
    }
}
