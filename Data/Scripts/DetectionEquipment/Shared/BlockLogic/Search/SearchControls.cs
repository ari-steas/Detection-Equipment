using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRageMath;

namespace DetectionEquipment.Shared.BlockLogic.Search
{
    internal class SearchControls : TerminalControlAdder<SearchBlock, IMyConveyorSorter>
    {
        protected static BlockSelectControl<SearchBlock, IMyConveyorSorter> ActiveSensorSelect;
        public static Dictionary<SearchBlock, HashSet<BlockSensor>> ActiveSensors = new Dictionary<SearchBlock, HashSet<BlockSensor>>();

        public override void DoOnce(SearchBlock thisLogic)
        {
            base.DoOnce(thisLogic);
            ActiveSensors[thisLogic] = new HashSet<BlockSensor>();
        }

        protected override void CreateTerminalActions()
        {
            ActiveSensorSelect = new BlockSelectControl<SearchBlock, IMyConveyorSorter>(
                "ActiveSensors",
                "Active Sensors",
                "Sensors this block should direct. Ctrl+Click to select multiple.",
                true,
                logic => logic.GridSensors.BlockSensorIdMap.Keys,
                (logic, selected) =>
                {
                    ActiveSensors[logic].Clear();
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
                    };
                }
                );
        }

        protected override void CreateTerminalProperties()
        {

        }
    }
}
