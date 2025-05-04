using DetectionEquipment.Client.Networking;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.Networking;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace DetectionEquipment.Shared.BlockLogic.GenericControls
{
    internal interface IBlockSelectControl
    {
        void UpdateSelected(IControlBlockBase logic, long[] selected, bool fromNetwork = false);
    }

    /// <summary>
    /// Network-synced block selector listbox terminal control.
    /// </summary>
    /// <typeparam name="TLogicType"></typeparam>
    /// <typeparam name="TBlockType"></typeparam>
    internal class BlockSelectControl<TLogicType, TBlockType> : IBlockSelectControl
        where TLogicType : MyGameLogicComponent, IControlBlockBase
        where TBlockType : IMyTerminalBlock, IMyFunctionalBlock
    {
        public readonly Dictionary<TLogicType, long[]> SelectedBlocks = new Dictionary<TLogicType, long[]>();
        public readonly IMyTerminalControlListbox ListBox;
        private readonly Func<TLogicType, IEnumerable<IMyCubeBlock>> _availableBlocks;
        private readonly Action<TLogicType, long[]> _onListChanged;

        private readonly string _id;

        public BlockSelectControl(string id, string tooltip, string description, bool multiSelect, Func<TLogicType, IEnumerable<IMyCubeBlock>> availableBlocks, Action<TLogicType, long[]> onListChanged = null)
        {
            ListBox = TerminalControlAdder<TLogicType, TBlockType>.CreateListbox(
                id,
                tooltip,
                description,
                multiSelect,
                GetContent,
                OnSelect
                );
            _availableBlocks = availableBlocks;
            _onListChanged = onListChanged;

            _id = TerminalControlAdder<TLogicType, TBlockType>.IdPrefix + id;
            ControlBlockManager.I.BlockControls[_id] = this;
        }

        public void UpdateSelected(IControlBlockBase logic, long[] selected, bool fromNetwork = false)
        {
            var thisLogic = logic as TLogicType;
            if (thisLogic == null)
                return;

            // sometimes stuff breaks and I don't know why
            if (selected == null)
                selected = Array.Empty<long>();

            SelectedBlocks[thisLogic] = selected;
            _onListChanged?.Invoke(thisLogic, selected);

            if (!fromNetwork)
            {
                if (MyAPIGateway.Session.IsServer)
                {
                    ServerNetwork.SendToEveryoneInSync(new BlockSelectControlPacket
                    {
                        BlockId = logic.CubeBlock.EntityId,
                        ControlId = _id,
                        Selected = selected
                    }, logic.CubeBlock.GetPosition());
                }
                else
                {
                    ClientNetwork.SendToServer(new BlockSelectControlPacket
                    {
                        BlockId = logic.CubeBlock.EntityId,
                        ControlId = _id,
                        Selected = selected
                    });
                }
            }
        }

        private void GetContent(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> content, List<MyTerminalControlListBoxItem> selected)
        {
            var logic = block.GameLogic.GetAs<TLogicType>();
            if (logic == null)
                return;

            if (!SelectedBlocks.ContainsKey(logic))
            {
                SelectedBlocks[logic] = Array.Empty<long>();
                _onListChanged?.Invoke(logic, SelectedBlocks[logic]);
                logic.OnClose += () => SelectedBlocks.Remove(logic);
            }

            foreach (var available in _availableBlocks.Invoke(logic))
            {
                var item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(available.DisplayNameText), MyStringId.NullOrEmpty, available.EntityId);
                content.Add(item);
                if (SelectedBlocks[logic].Contains(available.EntityId))
                    selected.Add(item);
            }
        }

        private void OnSelect(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
        {
            var logic = block.GameLogic.GetAs<TLogicType>();
            if (logic == null)
                return;
            var array = new long[selected.Count];
            for (int i = 0; i < array.Length; i++)
                array[i] = (long)selected[i].UserData;
            
            UpdateSelected(logic, array);
        }
    }

    [ProtoContract]
    public class BlockSelectControlPacket : PacketBase
    {
        [ProtoMember(1)] public long BlockId;
        [ProtoMember(2)] public long[] Selected;
        [ProtoMember(3)] public string ControlId;

        public override void Received(ulong senderSteamId, bool fromServer)
        {
            var block = MyAPIGateway.Entities.GetEntityById(BlockId);
            IControlBlockBase controller;
            IBlockSelectControl control;
            if (block == null || !ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock)block, out controller) || !ControlBlockManager.I.BlockControls.TryGetValue(ControlId, out control)) return;
            control.UpdateSelected(controller, Selected, true);

            if (!fromServer)
                ServerNetwork.SendToEveryoneInSync(this, block.GetPosition());
        }
    }
}
