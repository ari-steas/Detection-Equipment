using DetectionEquipment.Shared.Definitions;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;

namespace DetectionEquipment.Shared.Structs
{
    [ProtoContract(UseProtoMembersOnly = true)]
    internal struct HudDetectionInfo
    {
        [ProtoMember(0)] private long _entityId;
        [ProtoMember(1)] private float _crossSection;
        [ProtoMember(2)] private float _error;

        private Vector3D Position => new Vector3D(_posX, _posY, _posZ);
        [ProtoMember(3)] private float _posX;
        [ProtoMember(4)] private float _posY;
        [ProtoMember(5)] private float _posZ;

        private Vector3D? Velocity => _velX == short.MaxValue ? (Vector3D?) null : new Vector3D(_velX, _velY, _velZ);
        [ProtoMember(6)] private short _velX;
        [ProtoMember(7)] private short _velY;
        [ProtoMember(8)] private short _velZ;

        private double? VelocityVariance => float.IsPositiveInfinity(_velocityVariance) ? (double?)null : _velocityVariance;
        [ProtoMember(9)] private float _velocityVariance;
        private SensorDefinition.SensorType SensorType => (SensorDefinition.SensorType) _sensorType;
        [ProtoMember(10)] private byte _sensorType;
        [ProtoMember(11)] private string[] _iffCodes; // TODO: Cache this between server and client.

        private MyRelationsBetweenPlayers? Relations => _relations == byte.MaxValue
            ? null
            : (MyRelationsBetweenPlayers?) _relations;
        [ProtoMember(12)] private byte _relations;

        public static explicit operator WorldDetectionInfo(HudDetectionInfo info) => new WorldDetectionInfo
        {
            EntityId = info._entityId,
            Entity = (MyEntity) MyAPIGateway.Entities.GetEntityById(info._entityId),
            CrossSection = info._crossSection,
            Error = info._error,
            Position = info.Position,
            Velocity = info.Velocity,
            VelocityVariance = info.VelocityVariance,
            DetectionType = info.SensorType,
            IffCodes = info._iffCodes,
            Relations = info.Relations,
        };

        public static explicit operator HudDetectionInfo(WorldDetectionInfo info) => new HudDetectionInfo
        {
            _entityId = info.EntityId,
            _crossSection = (float) info.CrossSection,
            _error = (float) info.Error,

            _posX = (float) info.Position.X,
            _posY = (float) info.Position.Y,
            _posZ = (float) info.Position.Z,

            _velX = info.Velocity == null ? short.MaxValue : (short) info.Velocity.Value.X,
            _velY = info.Velocity == null ? short.MaxValue : (short) info.Velocity.Value.Y,
            _velZ = info.Velocity == null ? short.MaxValue : (short) info.Velocity.Value.Z,

            _velocityVariance = info.VelocityVariance == null ? float.PositiveInfinity : (float) info.VelocityVariance,
            _sensorType = (byte) info.DetectionType,
            _iffCodes = info.IffCodes,
            _relations = info.Relations == null ? byte.MaxValue : (byte) info.Relations,
        };
    }
}
