using System.Collections.Generic;
using DetectionEquipment.Client.External;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.Networking
{
    [ProtoContract]
    internal class WcTargetingPacket : PacketBase
    {
        [ProtoMember(1)] private long _gridId;
        [ProtoMember(2)] private long[] _visibleTargets;

        public WcTargetingPacket(MyCubeGrid grid, List<MyEntity> entities)
        {
            _gridId = grid.EntityId;
            _visibleTargets = new long[entities.Count];
            for (int i = 0; i < entities.Count; i++)
                _visibleTargets[i] = entities[i].EntityId;
        }

        private WcTargetingPacket() { }

        public override void Received(ulong senderSteamId, bool fromServer)
        {
            if (fromServer && MyAPIGateway.Session.IsServer)
                return;
            var grid = MyAPIGateway.Entities.GetEntityById(_gridId) as MyCubeGrid;
            if (grid == null)
            {
                Log.Info("WcTargetingPacket", $"Failed to update grid targeting for {_gridId}!");
                return;
            }

            var validEnts = new List<MyEntity>();
            if (_visibleTargets != null)
            {
                foreach (var id in _visibleTargets)
                {
                    var possible = MyAPIGateway.Entities.GetEntityById(id);
                    if (possible != null)
                        validEnts.Add((MyEntity) possible);
                }
            }
            Log.Info("WcTargetingPacket", $"Received targeting packet for {((IMyCubeGrid)grid).CustomName} - {validEnts.Count} of {_visibleTargets?.Length ?? 0} targets valid.");
            WcInteractionManager.VisibleTargets[grid] = validEnts;
        }
    }
}
