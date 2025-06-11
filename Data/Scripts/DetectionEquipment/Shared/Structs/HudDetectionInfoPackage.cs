using System;
using DetectionEquipment.Shared.Definitions;
using ProtoBuf;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRageMath;

namespace DetectionEquipment.Shared.Structs
{
    [ProtoContract(UseProtoMembersOnly = true)]
    internal struct HudDetectionInfoPackage
    {
        [ProtoMember(1)] private long _entityId;
        [ProtoMember(2)] private float _crossSection;
        [ProtoMember(3)] private float _error;

        private Vector3D Position => new Vector3D(_posX, _posY, _posZ);
        [ProtoMember(4)] private float _posX;
        [ProtoMember(5)] private float _posY;
        [ProtoMember(6)] private float _posZ;

        private Vector3D? Velocity => _velX == short.MaxValue ? (Vector3D?) null : new Vector3D(_velX, _velY, _velZ);
        [ProtoMember(7)] private short _velX;
        [ProtoMember(8)] private short _velY;
        [ProtoMember(9)] private short _velZ;

        private double? VelocityVariance => float.IsPositiveInfinity(_velocityVariance) ? (double?)null : _velocityVariance;
        [ProtoMember(10)] private float _velocityVariance;
        private WorldDetectionInfo.DetectionFlags SensorType => (WorldDetectionInfo.DetectionFlags) _sensorType;
        [ProtoMember(11)] private byte _sensorType;
        [ProtoMember(12)] private string[] _iffCodes; // TODO: Cache this between server and client.

        private MyRelationsBetweenPlayers? Relations => _relations == byte.MaxValue
            ? null
            : (MyRelationsBetweenPlayers?) _relations;
        [ProtoMember(13)] private byte _relations;

        public static explicit operator HudDetectionInfo(HudDetectionInfoPackage infoPackage) => new HudDetectionInfo
        {
            EntityId = infoPackage._entityId,
            Entity = (MyEntity) MyAPIGateway.Entities.GetEntityById(infoPackage._entityId),
            CrossSection = infoPackage._crossSection,
            Error = infoPackage._error,
            Position = infoPackage.Position,
            Velocity = infoPackage.Velocity,
            VelocityVariance = infoPackage.VelocityVariance,
            DetectionType = infoPackage.SensorType,
            IffCodes = infoPackage._iffCodes ?? Array.Empty<string>(),
            Relations = infoPackage.Relations,
        };

        public static explicit operator HudDetectionInfoPackage(HudDetectionInfo info) => new HudDetectionInfoPackage
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

    internal struct HudDetectionInfo
    {
        public long EntityId;
        public MyEntity Entity;
        public double CrossSection;
        public double Error;
        public Vector3D Position;
        public Vector3D? Velocity;
        public double? VelocityVariance;
        public WorldDetectionInfo.DetectionFlags DetectionType;
        public string[] IffCodes;
        public MyRelationsBetweenPlayers? Relations;

        public HudDetectionInfo(WorldDetectionInfo info)
        {
            EntityId = info.EntityId;
            Entity = info.Entity;
            CrossSection = info.CrossSection;
            Error = info.SumError;
            Position = info.Position;
            Velocity = info.Velocity;
            VelocityVariance = info.VelocityVariance;
            DetectionType = info.DetectionType;
            IffCodes = info.IffCodes;
            Relations = info.Relations;
        }
    }
}
