using System;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.BlockLogic.IffReflector;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Structs;
using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.Game.Entity;

namespace DetectionEquipment.Shared.BlockLogic.IffAggregator
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "DetectionIffAggregatorBlock")]
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class IffAggregatorBlock : AggregatorBlock
    {
        protected override ControlBlockSettingsBase GetSettings => new IffAggregatorSettings(this);
        protected override ITerminalControlAdder GetControls => new IffAggregatorControls();

        public SimpleSync<bool> AutoSelfIff;
        public SimpleSync<string[]> FriendlyIffCodes;

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            AutoSelfIff = new SimpleSync<bool>(this, true);
            FriendlyIffCodes = new SimpleSync<string[]>(this, Array.Empty<string>());
            base.UpdateOnceBeforeFrame();
        }

        public override MyRelationsBetweenPlayers GetInfoRelations(WorldDetectionInfo info)
        {
            if (info.IffCodes.Length == 0)
            {
                if (info.DetectionType == SensorDefinition.SensorType.PassiveRadar) // Radar locks are probably enemies
                    return MyRelationsBetweenPlayers.Enemies;
                return MyRelationsBetweenPlayers.Neutral;
            }

            string[] ownIffCodes = null;
            if (AutoSelfIff.Value)
                ownIffCodes = IffReflectorBlock.GetIffCodes(Block.CubeGrid);

            foreach (var code in info.IffCodes)
            {
                if (FriendlyIffCodes.Value.Contains(code))
                    return MyRelationsBetweenPlayers.Allies;
                if (AutoSelfIff.Value && ownIffCodes.Contains(code))
                    return MyRelationsBetweenPlayers.Allies;
            }

            return MyRelationsBetweenPlayers.Enemies;
        }
    }
}
