using DetectionEquipment.Server.SensorBlocks;
using Sandbox.ModAPI;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Client.BlockLogic;
using DetectionEquipment.Client.BlockLogic.Sensors;
using VRage.Game.ModAPI;
using VRageMath;
using DetectionEquipment.Shared.BlockLogic.GenericControls;

namespace DetectionEquipment.Shared.BlockLogic.Search
{
    internal class SearchControls : TerminalControlAdder<SearchBlock, IMyConveyorSorter>
    {
        public static BlockSelectControl<SearchBlock, IMyConveyorSorter> ActiveSensorSelect;
        public static Dictionary<SearchBlock, HashSet<BlockSensor>> ActiveSensors = new Dictionary<SearchBlock, HashSet<BlockSensor>>();

        public override void DoOnce(IControlBlockBase thisLogic)
        {
            base.DoOnce(thisLogic);
            ActiveSensors[(SearchBlock)thisLogic] = new HashSet<BlockSensor>();
        }

        protected override void CreateTerminalActions()
        {
            ActiveSensorSelect = new BlockSelectControl<SearchBlock, IMyConveyorSorter>(
                this,
                "ActiveSensors",
                "Active Sensors",
                "Sensors this block should direct. Ctrl+Click to select multiple.",
                true,
                false,
                logic => (MyAPIGateway.Session.IsServer ?
                        logic.GridSensors.BlockSensorMap.Keys :
                        (IEnumerable<IMyCubeBlock>)SensorBlockManager.SensorBlocks[logic.CubeBlock.CubeGrid])
                        // BRIMSTONE LINQ HELL
                        .Where(sb => sb.GetLogic<ClientSensorLogic>()?.Sensors.Values.Any(s => s.Definition.Movement != null) ?? false),
                (logic, selected) =>
                {
                    if (!MyAPIGateway.Session.IsServer)
                        return;

                    ActiveSensors[logic].Clear();
                    logic.DirectionSigns.Clear();
                    foreach (var item in selected)
                    {
                        foreach (var sensor in logic.GridSensors.Sensors)
                        {
                            if (sensor.Block != item || sensor.Definition.Movement == null)
                                continue;
                            ActiveSensors[logic].Add(sensor);
                            sensor.DesiredAzimuth = 0;
                            sensor.DesiredElevation = 0;
                            logic.DirectionSigns[sensor] = Vector2I.One;
                            break;
                        }
                    }
                }
                );
        }

        protected override void CreateTerminalProperties()
        {

        }
    }
}
