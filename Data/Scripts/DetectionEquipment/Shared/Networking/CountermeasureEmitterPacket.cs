using System;
using System.Collections.Generic;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Shared.Networking
{
    /// <summary>
    /// Spawn particle on client when an emitter fires.
    /// </summary>
    [ProtoContract]
    internal class CountermeasureEmitterPacket : PacketBase // TODO: Cache this, waste of cpu time
    {
        [ProtoMember(1)] private long _emitterEntityId;
        [ProtoMember(2)] private int _muzzleIdx;
        [ProtoMember(3)] private int _definitionId;

        public CountermeasureEmitterPacket(Server.Countermeasures.CountermeasureEmitterBlock emitter)
        {
            _emitterEntityId = emitter.Block.EntityId;
            _muzzleIdx = emitter.CurrentMuzzleIdx;
            _definitionId = emitter.Definition.Id;
        }

        public override void Received(ulong senderSteamId, bool fromServer)
        {
            if (!fromServer)
                return;

            var block = MyAPIGateway.Entities.GetEntityById(_emitterEntityId) as IMyCubeBlock;
            if (block == null)
                return;

            var bufferDict = new Dictionary<string, IMyModelDummy>();
            var definition = DefinitionManager.GetCountermeasureEmitterDefinition(_definitionId);
            var muzzleId = definition.Muzzles[_muzzleIdx];
            if (TrySpawnParticle(block, muzzleId, definition, ref bufferDict))
                return;
            foreach (var subpart in SubpartManager.GetAllSubparts(block))
                if (TrySpawnParticle(subpart, muzzleId, definition, ref bufferDict))
                    break;
        }

        /// <summary>
        /// Spawns an emitter fire particle on a given entity's dummy with id <see cref="muzzleId"/> if present.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="muzzleId"></param>
        /// <param name="definition"></param>
        /// <param name="bufferDict"></param>
        /// <returns>True if a particle was attempted to spawn</returns>
        private static bool TrySpawnParticle(IMyEntity entity, string muzzleId, CountermeasureEmitterDefinition definition, ref Dictionary<string, IMyModelDummy> bufferDict)
        {
            entity.Model.GetDummies(bufferDict);
            IMyModelDummy muzzleDummy;
            if (bufferDict.TryGetValue(muzzleId, out muzzleDummy))
            {
                MyParticleEffect discard;

                uint renderId = entity.Render?.GetRenderObjectID() ?? uint.MaxValue;
                var matrix = (MatrixD) muzzleDummy.Matrix;
                var pos = (matrix * entity.WorldMatrix).Translation;
                if (!MyParticlesManager.TryCreateParticleEffect(definition.FireParticle, ref matrix, ref pos, renderId, out discard))
                    Log.Exception("CountermeasureEmitterPacket", new Exception($"Failed to create new projectile particle \"{definition.FireParticle}\"!"));
                return true;
            }

            bufferDict.Clear();
            return false;
        }
    }
}
