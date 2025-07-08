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
using VRage.Game;
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

            // LCD screen fix from Digi's "Fix LCD support" mod (https://steamcommunity.com/sharedfiles/filedetails/?id=2989537125)
            {
                var def = (Block as MyCubeBlock)?.BlockDefinition as MyFunctionalBlockDefinition;
                if (def == null || Block.Render is MyRenderComponentScreenAreas)
                    return;

                if(def.ScreenAreas == null || def.ScreenAreas.Count <= 0)
                    return; // doesn't need LCDs

                var oldRender = Block.Render;

                var newRender = new MyRenderComponentScreenAreas(Block as MyCubeBlock);
                Block.Render = newRender;

                // preserve color, skin, etc
                Block.Render.ColorMaskHsv = oldRender.ColorMaskHsv;
                Block.Render.EnableColorMaskHsv = oldRender.EnableColorMaskHsv;
                Block.Render.TextureChanges = oldRender.TextureChanges;
                Block.Render.MetalnessColorable = oldRender.MetalnessColorable;
                Block.Render.PersistentFlags = oldRender.PersistentFlags;

                // fix for LCDs not working when block spawns instead of placed
                Block.Components.Get<MyMultiTextPanelComponent>()?.SetRender(newRender);
            }
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            if (Block?.CubeGrid?.Physics == null || GlobalData.Killswitch) // ignore projected and other non-physical grids
                return;

            try
            {
                HideSorterControls.DoOnce(Block);
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
            if (GlobalData.Killswitch)
                return base.IsSerialized();

            GetSettings?.SaveBlockSettings();
            return base.IsSerialized();
        }

        public override void MarkForClose()
        {
            base.MarkForClose();
            if (GlobalData.Killswitch)
                return;

            ControlBlockManager.I.Blocks.Remove(Block as MyCubeBlock);
            OnClose?.Invoke();
        }
    }
}
