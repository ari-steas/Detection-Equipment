using System;
using System.Collections.Generic;
using DetectionEquipment.Client.Networking;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.Components;

namespace DetectionEquipment.Shared.Networking
{
    /// <summary>
    /// Simple automatic sync class. Similar to MySync, but works with any serializable type.
    /// </summary>
    /// <typeparam name="TValue"></typeparam>
    public class SimpleSync<TValue> : ISimpleSync
    {
        /// <summary>
        /// Unique SyncId for this SimpleSync
        /// </summary>
        public long SyncId { get; set; }
        /// <summary>
        /// Invoked whenever <see cref="Value"/> is modified. Argument 1 is the new value, argument 2 is true if the change was from network.
        /// </summary>
        public Action<TValue, bool> OnValueChanged = null;

        public MyGameLogicComponent Component { get; }
        private TValue _value;

        public TValue Value
        {
            get
            {
                return _value;
            }
            set
            {
                if (value.Equals(_value))
                    return;
                _value = value;

                if (!MyAPIGateway.Multiplayer.MultiplayerActive)
                    return;

                var packet = new SimpleSyncManager.InternalSimpleSyncBothWays
                {
                    SyncId = SyncId,
                    Contents = MyAPIGateway.Utilities.SerializeToBinary(_value)
                };
                if (MyAPIGateway.Session.IsServer)
                    ServerNetwork.SendToEveryoneInSync(packet, Component.Entity.GetPosition());
                else
                    ClientNetwork.SendToServer(packet);
                OnValueChanged?.Invoke(value, false);
            }
        }

        public SimpleSync(MyGameLogicComponent component, TValue value, Action<TValue, bool> onValueChanged) : this(component, value)
        {
            OnValueChanged = onValueChanged;
        }

        public SimpleSync(MyGameLogicComponent component, TValue value)
        {
            Component = component;
            _value = value;
            SimpleSyncManager.RegisterSync(this, component.Entity.EntityId);
            component.BeforeRemovedFromContainer += comp => SimpleSyncManager.UnregisterSync(this);
        }

        /// <summary>
        /// Updates the SimpleSync from network.
        /// </summary>
        /// <param name="contents"></param>
        public void Update(byte[] contents)
        {
            _value = MyAPIGateway.Utilities.SerializeFromBinary<TValue>(contents);
            OnValueChanged?.Invoke(_value, true);
        }

        public static implicit operator TValue(SimpleSync<TValue> sync) => sync._value;
    }

    internal static class SimpleSyncManager
    {
        private static readonly Dictionary<long, ISimpleSync> SyncIdMap = new Dictionary<long, ISimpleSync>();
        private static bool _didInit = false;

        public static void Init()
        {
            if (_didInit)
                return;
            _didInit = true;
            Log.Info("SimpleSyncManager", "Ready.");
        }

        public static void RegisterSync(ISimpleSync sync, long entityId)
        {
            long id = entityId;
            while (SyncIdMap.ContainsKey(id))
                id++;
            sync.SyncId = id;

            SyncIdMap.Add(sync.SyncId, sync);
            Log.Info("SimpleSyncManager", $"Registered SimpleSync {sync.SyncId} on {sync.Component.GetType().Name}");
        }

        public static void UnregisterSync(ISimpleSync sync)
        {
            SyncIdMap.Remove(sync.SyncId);
            Log.Info("SimpleSyncManager", $"Unregistered SimpleSync {sync.SyncId}");
        }
        
        public static void Close()
        {
            if (!_didInit)
                return;
            Log.Info("SimpleSyncManager", "Closed.");
            SyncIdMap.Clear();
            _didInit = false;
        }

        [ProtoContract]
        public class InternalSimpleSyncBothWays : PacketBase
        {
            [ProtoMember(1)] public long SyncId;
            [ProtoMember(2)] public byte[] Contents;

            public override void Received(ulong senderSteamId, bool fromServer)
            {
                if (fromServer && MyAPIGateway.Session.IsServer)
                    return;

                ISimpleSync theSync;
                if (!SyncIdMap.TryGetValue(SyncId, out theSync))
                    return;
                theSync.Update(Contents);
            }
        }
    }

    internal interface ISimpleSync
    {
        long SyncId { get; set; }
        MyGameLogicComponent Component { get; }
        void Update(byte[] data);
    }
}
