using DetectionEquipment.Client.Networking;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.Helpers;
using DetectionEquipment.Shared.Networking;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using System;
using System.Collections.Generic;
using DetectionEquipment.Shared.Utils;
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
        where TLogicType : class, IControlBlockBase
        where TBlockType : IMyTerminalBlock, IMyFunctionalBlock
    {
        public readonly Dictionary<TLogicType, long[]> SelectedBlocks = new Dictionary<TLogicType, long[]>();
        public readonly IMyTerminalControlListbox ListBox;
        private readonly Func<TLogicType, IEnumerable<IMyCubeBlock>> _availableBlocks;
        private readonly Action<TLogicType, List<IMyCubeBlock>> _onListChanged;
        private readonly bool _useSubgrids;

        private readonly string _id;

        private readonly List<IMyCubeBlock> _selectedBuffer = new List<IMyCubeBlock>();
        private readonly List<IMyCubeGrid> _gridBuffer = new List<IMyCubeGrid>();

        private readonly Dictionary<TLogicType, HashSet<IMyCubeBlock>> _prevSelectedBlocks = new Dictionary<TLogicType, HashSet<IMyCubeBlock>>();
        private readonly Dictionary<TLogicType, LogicContainerHelper> _containers = new Dictionary<TLogicType, LogicContainerHelper>();

        public BlockSelectControl(TerminalControlAdder<TLogicType, TBlockType> controlAdder, string id, string tooltip, string description, bool multiSelect, bool useSubgrids, Func<TLogicType, IEnumerable<IMyCubeBlock>> availableBlocks, Action<TLogicType, List<IMyCubeBlock>> onListChanged = null)
        {
            ListBox = controlAdder.CreateListbox(
                id,
                tooltip,
                description,
                multiSelect,
                GetContent,
                OnSelect
                );
            _availableBlocks = availableBlocks;
            _onListChanged = onListChanged;
            _useSubgrids = useSubgrids;
            _id = controlAdder.IdPrefix + id;
            ControlBlockManager.BlockControls[_id] = this;
        }

        public void UpdateSelectedFromPersistent(IControlBlockBase logic, long[] selectedPersistent)
        {
            var thisLogic = logic as TLogicType;
            if (thisLogic == null)
                return;

            // sometimes stuff breaks and I don't know why
            if (selectedPersistent == null)
                selectedPersistent = Array.Empty<long>();

            var entIds = new List<long>(selectedPersistent.Length);
            if (_useSubgrids)
                logic.CubeBlock.CubeGrid.GetGridGroup(GridLinkTypeEnum.Physical).GetGrids(_gridBuffer);
            foreach (var id in selectedPersistent)
            {
                // skip empty item
                if (id == -1)
                    continue;

                var block = logic.CubeBlock.CubeGrid.GetBlockByPersistentId(id);
                if (block == null && _useSubgrids)
                {
                    foreach (var subgrid in _gridBuffer)
                    {
                        if (subgrid == logic.CubeBlock.CubeGrid)
                            continue;
                        block = subgrid.GetBlockByPersistentId(id);
                        if (block != null)
                            break;
                    }
                }

                if (block != null)
                    entIds.Add(block.EntityId);
            }

            UpdateSelected(logic, entIds.ToArray());
        }

        public void UpdateSelected(IControlBlockBase logic, long[] selected, bool fromNetwork = false)
        {
            var thisLogic = logic as TLogicType;
            if (thisLogic == null)
                return;

            // sometimes stuff breaks and I don't know why
            if (selected == null)
                selected = Array.Empty<long>();

            List<long> persistentIds = new List<long>(selected.Length);
            foreach (var id in selected)
            {
                // skip empty item
                if (id == -1)
                    continue;

                var block = MyAPIGateway.Entities.GetEntityById(id) as IMyCubeBlock;
                if (block != null)
                {
                    _selectedBuffer.Add(block);
                    persistentIds.Add(block.GetOrCreatePersistentId());
                }
            }
            SelectedBlocks[thisLogic] = persistentIds.ToArray();
            OnUpdateClearAction(thisLogic);

            if (_onListChanged != null)
            {
                _onListChanged?.Invoke(thisLogic, _selectedBuffer);
                _selectedBuffer.Clear();
                _gridBuffer.Clear();
            }

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

        public void UpdateSelectedPersistent(IControlBlockBase logic, long[] selectedPersistent)
        {
            var thisLogic = logic as TLogicType;
            if (thisLogic == null)
                return;

            SelectedBlocks[thisLogic] = selectedPersistent;
            OnUpdateClearAction(thisLogic);

            if (_onListChanged != null)
            {
                _onListChanged?.Invoke(thisLogic, _selectedBuffer);
                _selectedBuffer.Clear();
                _gridBuffer.Clear();
            }
        }

        private void GetContent(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> content, List<MyTerminalControlListBoxItem> selected)
        {
            var logic = ControlBlockManager.GetLogic<TLogicType>(block);
            if (logic == null)
                return;

            if (!SelectedBlocks.ContainsKey(logic))
            {
                SelectedBlocks[logic] = Array.Empty<long>();
                _onListChanged?.Invoke(logic, _selectedBuffer);
                logic.OnClose += () =>
                {
                    long[] closeSelected;
                    if (SelectedBlocks.TryGetValue(logic, out closeSelected))
                    {
                        foreach (var blockId in closeSelected)
                        {
                            IMyCubeBlock extBlock = MyAPIGateway.Entities.GetEntityById(blockId) as IMyCubeBlock;
                            if (extBlock == null)
                                continue;
                            extBlock.OnClosing += GetContainer(logic).OnBlockClosing;
                        }

                        SelectedBlocks.Remove(logic);
                    }
                };
            }

            var emptyItem = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute("[NONE]"), MyStringId.NullOrEmpty, -1L);
            content.Add(emptyItem);

            foreach (var available in _availableBlocks.Invoke(logic))
            {
                var item = new MyTerminalControlListBoxItem(MyStringId.GetOrCompute(available.DisplayNameText), MyStringId.NullOrEmpty, available.EntityId);
                content.Add(item);
                if (SelectedBlocks[logic].Contains(available.GetOrCreatePersistentId()))
                    selected.Add(item);
            }
        }

        private void OnSelect(IMyTerminalBlock block, List<MyTerminalControlListBoxItem> selected)
        {
            var logic = ControlBlockManager.GetLogic<TLogicType>(block);
            if (logic == null)
                return;
            var array = new long[selected.Count];
            for (int i = 0; i < array.Length; i++)
                array[i] = (long)selected[i].UserData;

            UpdateSelected(logic, array);
        }

        // absolute nonsense meant to help with very annoying oversight on my part, forgot to remove closed blocks from the blockselect terminal control's internal buffer
        // - 1am ari
        private void OnUpdateClearAction(TLogicType logic)
        {
            long[] newSelected = SelectedBlocks[logic];
            HashSet<long> checkedSelected = new HashSet<long>(newSelected);
            HashSet<IMyCubeBlock> oldSelected;
            if (!_prevSelectedBlocks.TryGetValue(logic, out oldSelected))
            {
                oldSelected = new HashSet<IMyCubeBlock>();
                _prevSelectedBlocks.Add(logic, oldSelected);
            }


            var toRemove = GlobalObjectPools.BlockPool.Pop();
            foreach (var block in oldSelected)
            {
                long pId = block.GetOrCreatePersistentId();

                // ignore blocks already with onclose
                if (checkedSelected.Contains(pId))
                {
                    checkedSelected.Remove(pId);
                    continue;
                }

                // remove onclose from deselected blocks
                block.OnClosing -= GetContainer(logic).OnBlockClosing;
                toRemove.Add(block);
            }

            foreach (var bTR in toRemove) // she modify on my collection till i exception???
                oldSelected.Remove(bTR);

            GlobalObjectPools.BlockPool.Push(toRemove);

            // add close action for new selections
            foreach (var blockId in checkedSelected)
            {
                IMyCubeBlock block = logic.CubeBlock.CubeGrid.GetBlockByPersistentId(blockId);
                if (block == null)
                    continue;
                block.OnClosing += GetContainer(logic).OnBlockClosing;
                oldSelected.Add(block);
            }
        }

        private LogicContainerHelper GetContainer(TLogicType logic)
        {
            LogicContainerHelper container;
            if (!_containers.TryGetValue(logic, out container))
            {
                container = new LogicContainerHelper(logic, this);
                _containers.Add(logic, container);
            }

            return container;
        }

        private struct LogicContainerHelper
        {
            public TLogicType Logic;
            public BlockSelectControl<TLogicType, TBlockType> Parent;

            public LogicContainerHelper(TLogicType logic, BlockSelectControl<TLogicType, TBlockType> parent)
            {
                Logic = logic;
                Parent = parent;
            }

            public void OnBlockClosing(IMyEntity entity)
            {
                long pId = ((IMyCubeBlock)entity).GetOrCreatePersistentId();

                long[] prevSelected;
                if (!Parent.SelectedBlocks.TryGetValue(Logic, out prevSelected) || !prevSelected.Contains(pId))
                    return;

                long[] newSelected = new long[prevSelected.Length - 1];
                int j = 0;
                for (int i = 0; i < prevSelected.Length; i++)
                {
                    if (pId == prevSelected[i])
                        continue;
                    newSelected[j++] = prevSelected[i];
                }

                Parent.UpdateSelectedPersistent(Logic, newSelected); // treated as network update to stop propagation
            }
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
            //Log.Info("BlockSelectControl", $"Packet received {BlockId} {Selected?.Length ?? -1}\n" +
            //                               $"{block != null}, {block != null && ControlBlockManager.I.Blocks.ContainsKey((MyCubeBlock)block)}, {ControlBlockManager.I.BlockControls.ContainsKey(ControlId)}");
            if (block == null || !ControlBlockManager.I.Blocks.TryGetValue((MyCubeBlock)block, out controller) || !ControlBlockManager.BlockControls.TryGetValue(ControlId, out control))
                return;
            control.UpdateSelected(controller, Selected, true);

            if (!fromServer)
                ServerNetwork.SendToEveryoneInSync(this, block.GetPosition());
        }

        public override PacketInfo GetInfo()
        {
            return PacketInfo.FromPacket(this,
                new PacketInfo
                {
                    PacketTypeName = nameof(BlockId),
                    PacketSize = sizeof(long)
                },
                new PacketInfo
                {
                    PacketTypeName = nameof(Selected),
                    PacketSize = Selected == null ? 0 : Selected.Length * sizeof(long)
                },
                new PacketInfo
                {
                    PacketTypeName = nameof(ControlId),
                    PacketSize = ControlId == null ? 0 : ControlId.Length * sizeof(char)
                }
            );
        }
    }
}
