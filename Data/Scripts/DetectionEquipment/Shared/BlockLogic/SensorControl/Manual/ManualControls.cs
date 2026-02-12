using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.SensorControl.Manual
{
    internal class ManualControls : SensorControlBlockControlsBase<ManualBlock>
    {
        protected override void CreateTerminalActions()
        {
            base.CreateTerminalActions();

            
        }

        protected override void CreateTerminalProperties()
        {
            base.CreateTerminalProperties();

            
        }

        protected override void OnSensorsSelected(ManualBlock logic, List<IMyCubeBlock> selected)
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            ActiveSensors[logic].Clear();

            foreach (var sensor in logic.GridSensors.Sensors)
            {
                if (sensor.Definition.Movement == null || !selected.Contains(sensor.Block))
                    continue;
                ActiveSensors[logic].Add(sensor);
            }
        }
    }
}
