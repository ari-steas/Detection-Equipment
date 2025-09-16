using Sandbox.Definitions;
using Sandbox.Game.Components;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using System;
using DetectionEquipment.Shared.Utils;
using Sandbox.Common.ObjectBuilders;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace DetectionEquipment.Shared.BlockLogic
{
    /// <summary>
    /// Only purpose is to notify on IsSerialized and fix LCD screens.
    /// </summary>
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false)]
    internal class SerializationNotifier : MyGameLogicComponent
    {
        public static Action<IMyEntity> OnSerialize;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);

            var block = Entity as IMyCubeBlock;
            if (block == null) return;

            // LCD screen fix from Digi's "Fix LCD support" mod (https://steamcommunity.com/sharedfiles/filedetails/?id=2989537125)
            {
                var def = (block as MyCubeBlock)?.BlockDefinition as MyFunctionalBlockDefinition;
                if (def == null || block.Render is MyRenderComponentScreenAreas)
                    return;

                if(def.ScreenAreas == null || def.ScreenAreas.Count <= 0)
                    return; // doesn't need LCDs

                var oldRender = block.Render;

                var newRender = new MyRenderComponentScreenAreas(block as MyCubeBlock);
                block.Render = newRender;

                // preserve color, skin, etc
                block.Render.ColorMaskHsv = oldRender.ColorMaskHsv;
                block.Render.EnableColorMaskHsv = oldRender.EnableColorMaskHsv;
                block.Render.TextureChanges = oldRender.TextureChanges;
                block.Render.MetalnessColorable = oldRender.MetalnessColorable;
                block.Render.PersistentFlags = oldRender.PersistentFlags;

                // fix for LCDs not working when block spawns instead of placed
                block.Components.Get<MyMultiTextPanelComponent>()?.SetRender(newRender);
            }
        }

        public override bool IsSerialized()
        {
            OnSerialize?.Invoke(Entity);
            return base.IsSerialized();
        }
    }
}
