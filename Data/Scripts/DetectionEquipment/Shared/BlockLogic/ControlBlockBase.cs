﻿using DetectionEquipment.Server;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using DetectionEquipment.Shared.Utils;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Network;
using VRage.ObjectBuilders;

namespace DetectionEquipment.Shared.BlockLogic
{
    internal abstract class ControlBlockBase<TBlock> : MyGameLogicComponent, IMyEventProxy, IControlBlockBase
        where TBlock : IMyTerminalBlock, IMyFunctionalBlock
    {
        public TBlock Block;
        public IMyCubeBlock CubeBlock => Block;
        public GridSensorManager GridSensors { get; private set; }
        public Action OnClose { get; set; }
        protected abstract ControlBlockSettingsBase GetSettings { get; }
        protected abstract ITerminalControlAdder GetControls { get; }

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

            try
            {
                HideSorterControls.DoOnce();
                ControlBlockManager.I.Blocks.Add(Block as MyCubeBlock, this);

                if (MyAPIGateway.Session.IsServer)
                {
                    GridSensors = ServerMain.I.GridSensorMangers[Block.CubeGrid];
                }

                GetControls.DoOnce(this);
                GetSettings?.LoadSettings();
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            }
            catch (Exception ex)
            {
                Log.Exception($"ControlBlockBase::{GetType().Name}", ex, true);
            }
        }

        public override bool IsSerialized()
        {
            GetSettings?.SaveBlockSettings();
            return base.IsSerialized();
        }

        public override void MarkForClose()
        {
            base.MarkForClose();
            ControlBlockManager.I.Blocks.Remove(Block as MyCubeBlock);
            OnClose?.Invoke();
        }
    }
}
