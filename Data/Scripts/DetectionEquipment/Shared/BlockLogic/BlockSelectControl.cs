using DetectionEquipment.Shared.BlockLogic.GenericControls;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace DetectionEquipment.Shared.BlockLogic
{
    internal class BlockSelectControl<LogicType, BlockType>
        where LogicType : MyGameLogicComponent, IControlBlockBase
        where BlockType : IMyTerminalBlock, IMyFunctionalBlock
    {
        public Dictionary<LogicType, long[]> SelectedBlocks = new Dictionary<LogicType, long[]>();
        public IMyTerminalControlListbox ListBox;
        public Func<LogicType, IEnumerable<IMyCubeBlock>> AvailableBlocks;
        public Action<LogicType, long[]> OnListChanged;

        public BlockSelectControl(string id, string tooltip, string description, bool multiSelect, Func<LogicType, IEnumerable<IMyCubeBlock>> availableBlocks, Action<LogicType, long[]> onListChanged = null)
        {
            ListBox = TerminalControlAdder<LogicType, BlockType>.CreateListbox(
                id,
                tooltip,
                description,
                multiSelect,
                Content,
                OnSelect
                );
            AvailableBlocks = availableBlocks;
            OnListChanged = onListChanged;
        }

        private void Content(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> content, List<MyTerminalControlListBoxItem> selected)
        {
            var logic = block.GameLogic.GetAs<LogicType>();
            if (logic == null)
                return;

            if (!SelectedBlocks.ContainsKey(logic))
            {
                SelectedBlocks[logic] = Array.Empty<long>();
                OnListChanged?.Invoke(logic, SelectedBlocks[logic]);
                logic.OnClose += () => SelectedBlocks.Remove(logic);
            }

            foreach (var available in AvailableBlocks.Invoke(logic))
            {
                var item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(available.DisplayNameText), MyStringId.NullOrEmpty, available.EntityId);
                content.Add(item);
                if (SelectedBlocks[logic].Contains(available.EntityId))
                    selected.Add(item);
            }
        }

        private void OnSelect(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
        {
            var logic = block.GameLogic.GetAs<LogicType>();
            if (logic == null)
                return;
            var array = new long[selected.Count];
            for (int i = 0; i < array.Length; i++)
                array[i] = (long)selected[i].UserData;
            SelectedBlocks[logic] = array;
            OnListChanged?.Invoke(logic, array); // TODO networking
        }
    }
}
