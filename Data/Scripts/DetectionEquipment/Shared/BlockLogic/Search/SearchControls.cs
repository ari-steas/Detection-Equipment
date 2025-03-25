using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.BlockLogic.Aggregator;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Utils;

namespace DetectionEquipment.Shared.BlockLogic.Search
{
    internal class SearchControls : TerminalControlAdder<SearchBlock, IMyConveyorSorter>
    {
        protected static BlockSelectControl<SearchBlock, IMyConveyorSorter> ActiveSensorSelect;
        public static Dictionary<SearchBlock, HashSet<BlockSensor>> ActiveSensors = new Dictionary<SearchBlock, HashSet<BlockSensor>>();

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
                    if (!ActiveSensors.ContainsKey(logic))
                        ActiveSensors[logic] = new HashSet<BlockSensor>();
                    else
                        ActiveSensors[logic].Clear();
                    foreach (var sensor in logic.GridSensors.Sensors)
                    {
                        for (int i = 0; i < selected.Length; i++)
                        {
                            if (sensor.Block.EntityId != selected[i])
                                continue;
                            ActiveSensors[logic].Add(sensor);
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
