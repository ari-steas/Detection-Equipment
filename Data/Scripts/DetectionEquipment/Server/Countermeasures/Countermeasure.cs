using DetectionEquipment.Server.Networking;
using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System.Reflection;
using VRageMath;

namespace DetectionEquipment.Server.Countermeasures
{
    internal class Countermeasure
    {
        public readonly uint Id;

        public bool IsActive = true;
        public uint RemainingLifetime;

        public Vector3D Position;
        public Vector3D Direction;
        public Vector3D Velocity;
        public float Intensity = 1;

        private readonly CountermeasureEmitterBlock _parentEmitter;
        private readonly int _attachedMuzzleIdx = -1;

        public float EffectAperture;

        public readonly CountermeasureDefinition Definition;

        private Countermeasure(CountermeasureDefinition definition)
        {
            Id = CountermeasureManager.HighestCountermeasureId++;
            CountermeasureManager.CountermeasureIdMap[Id] = this;

            Definition = definition;
            RemainingLifetime = definition.MaxLifetime;
            EffectAperture = definition.MaxEffectAperture;
        }

        public Countermeasure(CountermeasureDefinition definition, Vector3D position, Vector3D direction, Vector3D initialVelocity) : this(definition)
        {
            Position = position;
            Direction = direction;
            Velocity = initialVelocity;

            // We only want to send countermeasures with VFX
            if (!string.IsNullOrEmpty(Definition.ParticleEffect))
                ServerNetwork.SendToEveryoneInSync(new CountermeasurePacket(this), Position);
        }

        public Countermeasure(CountermeasureDefinition definition, CountermeasureEmitterBlock emitter) : this(
            definition)
        {
            if (emitter.Definition.IsCountermeasureAttached)
            {
                _parentEmitter = emitter;
                _attachedMuzzleIdx = emitter.CurrentMuzzleIdx;
            }
            
            var muzzleMatrix = emitter.MuzzleMatrix;
            Position = muzzleMatrix.Translation;
            Direction = muzzleMatrix.Forward;

            // Ejection velocity
            Velocity = Direction * emitter.Definition.EjectionVelocity;
            if (emitter.Block?.CubeGrid?.Physics != null)
            {
                var ownerGrid = emitter.Block.CubeGrid;
                Vector3D ownerCenter = ownerGrid.Physics.CenterOfMassWorld;

                // Add linear velocity at point; this accounts for angular velocity and linear velocity
                Velocity += ownerGrid.Physics.LinearVelocity + ownerGrid.Physics.AngularVelocity.Cross(Position - ownerCenter);
            }

            // We only want to send countermeasures with VFX
            if (!string.IsNullOrEmpty(Definition.ParticleEffect))
                ServerNetwork.SendToEveryoneInSync(new CountermeasurePacket(this), Position);
        }

        public void Update()
        {
            if (_parentEmitter != null)
            {
                if (_parentEmitter.Block.Closed)
                {
                    Close();
                    return;
                }

                var muzzleMatrix = _parentEmitter.Muzzles[_attachedMuzzleIdx].GetMatrix();
                Position = muzzleMatrix.Translation;
                Direction = muzzleMatrix.Forward;
            }
            else 
            {
                if (Definition.HasPhysics)
                {
                    float ignored;
                    Velocity += MyAPIGateway.Physics.CalculateNaturalGravityAt(Position, out ignored) / 60;
                    if (Definition.DragMultiplier != 0 && Velocity != Vector3D.Zero)
                    {
                        var airDensity = MiscUtils.GetAtmosphereDensity(Position);
                        if (airDensity > 0)
                            Velocity = Vector3D.Lerp(Velocity, Vector3D.Zero, Velocity.LengthSquared() * Definition.DragMultiplier * airDensity / 60);
                    }
                }
                Position += Velocity / 60;
            }

            if (--RemainingLifetime == 0)
                Close();
        }

        public double GetSensorNoise(ISensor sensor)
        {
            if (Definition.CountermeasureType == CountermeasureDefinition.CountermeasureTypeEnum.None)
                return 0;

            bool sensorIsVisual = sensor is VisualSensor;
            bool sensorIsInfrared = sensorIsVisual && ((VisualSensor)sensor).IsInfrared;
            sensorIsVisual &= !sensorIsInfrared;

            if (sensor is RadarSensor && (Definition.CountermeasureType & CountermeasureDefinition.CountermeasureTypeEnum.Radar) == 0 ||
                sensor is AntennaSensor && (Definition.CountermeasureType & CountermeasureDefinition.CountermeasureTypeEnum.Antenna) == 0 ||
                sensorIsVisual && (Definition.CountermeasureType & CountermeasureDefinition.CountermeasureTypeEnum.Optical) == 0 ||
                sensorIsInfrared && (Definition.CountermeasureType & CountermeasureDefinition.CountermeasureTypeEnum.Infrared) == 0
                )
                return 0;

            var distance = Vector3D.Distance(sensor.Position, Position);

            if (distance > Definition.MaxRange || Vector3D.Angle(sensor.Direction, Position - sensor.Position) > sensor.Aperture || Vector3D.Angle(Direction, sensor.Position - Position) > EffectAperture)
                return 0;

            switch (Definition.FalloffType)
            {
                case CountermeasureDefinition.FalloffTypeEnum.Quadratic:
                    return Intensity * (Definition.FalloffScalar / (distance*distance) + Definition.MinNoise);
                case CountermeasureDefinition.FalloffTypeEnum.Linear:
                    return Intensity * (Definition.FalloffScalar * (Definition.MaxRange - distance) + Definition.MinNoise);
                case CountermeasureDefinition.FalloffTypeEnum.None:
                default:
                    return Intensity * Definition.MinNoise;
            }
        }

        public void Close()
        {
            IsActive = false;
        }
    }
}
