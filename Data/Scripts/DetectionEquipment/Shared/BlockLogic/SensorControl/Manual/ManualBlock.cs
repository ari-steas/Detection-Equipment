using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using System.Collections.Generic;

namespace DetectionEquipment.Shared.BlockLogic.SensorControl.Manual
{
    internal class ManualBlock : SensorControlBlockBase<IMyConveyorSorter>
    {
        internal override HashSet<BlockSensor> ControlledSensors => ManualControls.ActiveSensors[this];

        protected override ControlBlockSettingsBase GetSettings => new ManualSettings(this);
        protected override ITerminalControlAdder GetControls => new ManualControls();

        public ManualBlock(IMyFunctionalBlock block) : base(block)
        {
        }

        public override void Init()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;

            

            base.Init();
        }

        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer || !Block.IsWorking)
                return;

            foreach (var sensor in ControlledSensors)
            {
                if (!(sensor.AllowMechanicalControl ^ InvertAllowControl.Value) || !sensor.TryTakeControl(this))
                    continue;


            }
        }
    }
}
