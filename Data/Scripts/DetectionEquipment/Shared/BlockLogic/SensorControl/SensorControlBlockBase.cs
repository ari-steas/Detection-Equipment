using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.Networking;
using Sandbox.ModAPI;
using System.Collections.Generic;

namespace DetectionEquipment.Shared.BlockLogic.SensorControl
{
    internal abstract class SensorControlBlockBase<TBlock> : ControlBlockBase<TBlock>, ISensorControlBlock
        where TBlock : IMyTerminalBlock, IMyFunctionalBlock
    {
        public SimpleSync<bool> InvertAllowControl { get; } = new SimpleSync<bool>(false);
        public SimpleSync<int> ControlPriority { get; } = new SimpleSync<int>(0);

        internal abstract HashSet<BlockSensor> ControlledSensors { get; }

        protected SensorControlBlockBase(IMyFunctionalBlock block) : base(block)
        {
            InvertAllowControl.Component = this;
            ControlPriority.Component = this;
        }
    }
}
