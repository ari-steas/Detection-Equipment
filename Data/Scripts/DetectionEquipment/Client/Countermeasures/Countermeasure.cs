using System;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using VRage.Game;
using VRageMath;

namespace DetectionEquipment.Client.Countermeasures
{
    internal class Countermeasure
    {
        private readonly CountermeasureDefinition _definition;
        private Vector3D _position, _direction, _velocity;
        private MyParticleEffect _particle;
        private uint _remainingLifetime;
        public bool IsActive = true;

        public Countermeasure(CountermeasurePacket packet)
        {
            _definition = DefinitionManager.GetCountermeasureDefinition(packet.DefinitionId);
            _position = packet.Position;
            _direction = packet.Direction;
            _velocity = packet.Velocity;

            _remainingLifetime = _definition.MaxLifetime;
        }

        public void Update()
        {
            if (_definition.HasPhysics)
            {
                float ignored;
                _velocity += MyAPIGateway.Physics.CalculateNaturalGravityAt(_position, out ignored) / 60;
                if (_definition.DragMultiplier != 0 && _velocity != Vector3D.Zero)
                    _velocity = Vector3D.Lerp(_velocity, Vector3D.Zero, _velocity.LengthSquared() * _definition.DragMultiplier / 60);
            }
            _position += _velocity / 60;

            if (_particle == null && !string.IsNullOrEmpty(_definition.ParticleEffect))
            {
                var matrix = MatrixD.CreateWorld(_position, _direction, Vector3D.CalculatePerpendicularVector(_direction));
                if (!MyParticlesManager.TryCreateParticleEffect(_definition.ParticleEffect, ref matrix, ref _position, uint.MaxValue, out _particle))
                {
                    Log.Exception("Countermeasure", new Exception($"Failed to create new projectile particle \"{_definition.ParticleEffect}\"!"));
                }
            }

            if (_particle != null)
            {
                _particle.WorldMatrix = MatrixD.CreateWorld(_position, _direction, Vector3D.Up);
                //_particle.Velocity = _velocity;
            }

            if (--_remainingLifetime == 0)
                Close();
        }

        public void Close()
        {
            IsActive = false;
            _particle?.Close();
        }
    }
}
