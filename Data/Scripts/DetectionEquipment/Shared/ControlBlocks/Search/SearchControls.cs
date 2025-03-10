using DetectionEquipment.Shared.ControlBlocks.GenericControls;
using Sandbox.ModAPI;
using System;
using System.Linq;
using VRage.Utils;

namespace DetectionEquipment.Shared.ControlBlocks.Search
{
    internal class SearchControls : TerminalControlAdder<SearchBlock, IMyConveyorSorter>
    {
        protected override void CreateTerminalActions()
        {
            CreateListbox(
                "ActiveSensors",
                "Active Sensors",
                "Sensors this block should direct. Ctrl+Click to select multiple.",
                true,
                (block, content, selected) =>
                {
                    var logic = block.GameLogic.GetAs<SearchBlock>();
                    if (logic == null)
                        return;

                    foreach (var sensor in logic.GridSensors.Sensors)
                    {
                        if (sensor.Definition.Movement == null)
                            continue;
                        var item = new VRage.ModAPI.MyTerminalControlListBoxItem(MyStringId.GetOrCompute(sensor.Block.DisplayNameText), MyStringId.GetOrCompute(sensor.Definition.Type.ToString()), sensor.Block.EntityId);
                        content.Add(item);
                        if (logic.ActiveSensors.Value.Contains(sensor.Block.EntityId))
                            selected.Add(item);
                    }
                },
                (block, selected) =>
                {
                    var logic = block.GameLogic.GetAs<SearchBlock>();
                    if (logic == null)
                        return;
                    var array = new long[selected.Count];
                    for (int i = 0; i < array.Length; i++)
                        array[i] = (long) selected[i].UserData;
                    logic.ActiveSensors.Value = array;
                }
                );
        }

        protected override void CreateTerminalProperties()
        {
            
        }
    }
}
