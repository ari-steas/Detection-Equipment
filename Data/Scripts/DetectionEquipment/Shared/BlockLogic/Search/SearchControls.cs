using DetectionEquipment.Client.Sensors;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;

namespace DetectionEquipment.Shared.BlockLogic.Search
{
    internal class SearchControls : TerminalControlAdder<SearchBlock, IMyConveyorSorter>
    {
        public static BlockSelectControl<SearchBlock, IMyConveyorSorter> ActiveSensorSelect;
        public static Dictionary<SearchBlock, HashSet<BlockSensor>> ActiveSensors = new Dictionary<SearchBlock, HashSet<BlockSensor>>();

        public override void DoOnce(IControlBlockBase thisLogic)
        {
            base.DoOnce(thisLogic);
            ActiveSensors[(SearchBlock) thisLogic] = new HashSet<BlockSensor>();
        }

        protected override void CreateTerminalActions()
        {
            ActiveSensorSelect = new BlockSelectControl<SearchBlock, IMyConveyorSorter>(
                "ActiveSensors",
                "Active Sensors",
                "Sensors this block should direct. Ctrl+Click to select multiple.",
                true,
                logic => MyAPIGateway.Session.IsServer ?
                         logic.GridSensors.BlockSensorIdMap.Keys :
                         (IEnumerable<IMyCubeBlock>) SensorBlockManager.GridBlockSensorsMap[logic.CubeBlock.CubeGrid],
                (logic, selected) =>
                {
                    if (!MyAPIGateway.Session.IsServer)
                        return;

                    ActiveSensors[logic].Clear();
                    logic.DirectionSigns.Clear();
                    foreach (var sensor in logic.GridSensors.Sensors)
                    {
                        for (int i = 0; i < selected.Length; i++)
                        {
                            if (sensor.Block.EntityId != selected[i])
                                continue;
                            ActiveSensors[logic].Add(sensor);
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
