using Sandbox.Common.ObjectBuilders;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using DetectionEquipment.Client.Networking;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Network;
using VRage.Sync;
using Sandbox.Game.Multiplayer;

namespace DetectionEquipment.Shared.BlockLogic.IffReflector
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorSorter), false, "IffReflector")]
    internal class IffReflectorBlock : ControlBlockBase<IMyConveyorSorter>
    {
        private string _iffCode = "";
        public string IffCode
        {
            get
            {
                return _iffCode;
            }
            set
            {
                _iffCode = value;
                IffCodeCache = _returnHash ? "H" + _iffCode.GetHashCode() : "S" + _iffCode;
                if (MyAPIGateway.Session.IsServer)
                    ServerNetwork.SendToEveryoneInSync(new IffReflectorPacket(this), Block.WorldMatrix.Translation);
                else
                    ClientNetwork.SendToServer(new IffReflectorPacket(this));
            }
        }

        private bool _returnHash = false;
        public bool ReturnHash
        {
            get
            {
                return _returnHash;
            }
            set
            {
                _returnHash = value;
                IffCodeCache = _returnHash ? "H" + _iffCode.GetHashCode() : "S" + _iffCode;
                if (MyAPIGateway.Session.IsServer)
                    ServerNetwork.SendToEveryoneInSync(new IffReflectorPacket(this), Block.WorldMatrix.Translation);
                else
                    ClientNetwork.SendToServer(new IffReflectorPacket(this));
            }
        }

        public string IffCodeCache { get; private set; } = "";
        protected override ControlBlockSettingsBase GetSettings => new IffReflectorSettings(this);

        public override void UpdateOnceBeforeFrame()
        {
            if (Block?.CubeGrid?.Physics == null) // ignore projected and other non-physical grids
                return;
            
            new IffControls().DoOnce(this);
            base.UpdateOnceBeforeFrame();

            if (!_iffMap.ContainsKey(Block.CubeGrid))
                _iffMap.Add(Block.CubeGrid, new HashSet<IffReflectorBlock>());
            _iffMap[Block.CubeGrid].Add(this);

            if (MyAPIGateway.Session.IsServer)
                ServerNetwork.SendToEveryoneInSync(new IffReflectorPacket(this), Block.WorldMatrix.Translation);
        }

        public override void MarkForClose()
        {
            base.MarkForClose();
            if (!_iffMap.ContainsKey(Block.CubeGrid))
                return;

            _iffMap[Block.CubeGrid].Remove(this);
            if (_iffMap[Block.CubeGrid].Count == 0)
                _iffMap.Remove(Block.CubeGrid);
        }

        private static Dictionary<IMyCubeGrid, HashSet<IffReflectorBlock>> _iffMap = new Dictionary<IMyCubeGrid, HashSet<IffReflectorBlock>>();
        public static string[] GetIffCodes(IMyCubeGrid grid)
        {
            HashSet<IffReflectorBlock> map;
            if (!_iffMap.TryGetValue(grid, out map))
                return Array.Empty<string>();
            var codes = new HashSet<string>();
            foreach (var reflector in map)
                if (reflector.Block.Enabled)
                    codes.Add(reflector.IffCodeCache);

            var array = new string[codes.Count];
            int i = 0;
            foreach (var code in codes)
            {
                array[i] = code;
                i++;
            }

            return array;
        }

        [ProtoContract]
        internal class IffReflectorPacket : PacketBase
        {
            [ProtoMember(1)] private string _iffCode;
            [ProtoMember(2)] private bool _returnHash;
            [ProtoMember(3)] private long _blockId;
            [ProtoIgnore] private bool _hasWaited = false;

            private IffReflectorPacket() { }

            public IffReflectorPacket(IffReflectorBlock block)
            {
                _iffCode = block.IffCode; // TODO custom mysync setup
                _returnHash = block.ReturnHash;
                _blockId = block.Block.EntityId;
            }

            public override void Received(ulong senderSteamId, bool fromServer)
            {
                if (MyAPIGateway.Session.IsServer && fromServer)
                    return; // assume it was already updated

                var logic = (MyAPIGateway.Entities.GetEntityById(_blockId) as IMyCubeBlock)?.GameLogic
                    ?.GetAs<IffReflectorBlock>();
                if (logic == null)
                {
                    // Slight init delay in case this packet arrives before the client is ready.
                    if (!_hasWaited)
                        MyAPIGateway.Utilities.InvokeOnGameThread(() => Received(senderSteamId, fromServer), StartAt: MyAPIGateway.Session.GameplayFrameCounter + 10);
                    else
                        Log.Exception("IffReflectorPacket", new Exception($"Invalid EntityId \"{_blockId}\" for IFF reflector!")); // TODO this isn't working. try a cache method like with sensors. FUCK
                    _hasWaited = true;
                    return;
                }

                logic._iffCode = _iffCode;
                logic._returnHash = _returnHash;
            
                if (MyAPIGateway.Session.IsServer && !fromServer)
                    ServerNetwork.SendToEveryoneInSync(this, logic.Block.WorldMatrix.Translation);
            }
        }
    }
}
