using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.ModAPI;
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

        public MyParticleEffect Particle;

        private readonly CountermeasureEmitterBlock ParentEmitter = null;
        private readonly int AttachedMuzzleIdx = -1;

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
        }

        public Countermeasure(CountermeasureDefinition definition, CountermeasureEmitterBlock emitter) : this(
            definition)
        {
            if (emitter.Definition.IsCountermeasureAttached)
            {
                ParentEmitter = emitter;
                AttachedMuzzleIdx = emitter.CurrentMuzzleIdx;
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
        }

        public void Update()
        {
            if (ParentEmitter != null)
            {
                if (ParentEmitter.Block.Closed)
                {
                    Close();
                    return;
                }

                var muzzleMatrix = ParentEmitter.Muzzles[AttachedMuzzleIdx].GetMatrix();
                Position = muzzleMatrix.Translation;
                Direction = muzzleMatrix.Forward;
            }
            else 
            {
                //if (Definition.HasPhysics)
                //{
                //    float ignored;
                //    Velocity += MyAPIGateway.Physics.CalculateNaturalGravityAt(Position, out ignored) / 60;
                //    if (Definition.DragMultiplier != 0 && Velocity != Vector3D.Zero)
                //        Velocity = Vector3D.Lerp(Velocity, Vector3D.Zero, Velocity.LengthSquared() * Definition.DragMultiplier / 60);
                //}
                Position += Velocity / 60;
            }

            if (Particle == null && !string.IsNullOrEmpty(Definition.ParticleEffect))
            {
                var matrix = MatrixD.CreateWorld(Position, Direction, Vector3D.CalculatePerpendicularVector(Direction));
                if (!MyParticlesManager.TryCreateParticleEffect(Definition.ParticleEffect, ref matrix, ref Position, uint.MaxValue, out Particle))
                {
                    Log.Exception("Countermeasure", new Exception($"Failed to create new projectile particle \"{Definition.ParticleEffect}\"!"));
                }
            }

            if (Particle != null)
                Particle.WorldMatrix = MatrixD.CreateWorld(Position, Direction, Vector3D.Up);

            if (--RemainingLifetime == 0)
                Close();
        }

        public float GetSensorNoise(ISensor sensor)
        {
            if (Definition.CountermeasureType == CountermeasureDefinition.CountermeasureTypeEnum.None)
                return 0;

            bool sensorIsVisual = sensor is VisualSensor;
            bool sensorIsInfrared = sensorIsVisual && ((VisualSensor)sensor).IsInfrared;
            sensorIsVisual &= !sensorIsInfrared;

            if (sensor is RadarSensor && (Definition.CountermeasureType & CountermeasureDefinition.CountermeasureTypeEnum.Radar) == 0 ||
                sensorIsVisual && (Definition.CountermeasureType & CountermeasureDefinition.CountermeasureTypeEnum.Optical) == 0 ||
                sensorIsInfrared && (Definition.CountermeasureType & CountermeasureDefinition.CountermeasureTypeEnum.Infrared) == 0
                )
                return 0;

            var distance = (float) Vector3D.Distance(sensor.Position, Position);

            if (distance > Definition.MaxRange || Vector3D.Angle(sensor.Direction, Position - sensor.Position) > sensor.Aperture || Vector3D.Angle(Direction, sensor.Position - Position) > EffectAperture)
                return 0;

            switch (Definition.FalloffType)
            {
                case CountermeasureDefinition.FalloffTypeEnum.Quadratic:
                    return Definition.FalloffScalar / ((distance + Definition.MaxRange)*(distance + Definition.MaxRange)) + Definition.MinNoise;
                case CountermeasureDefinition.FalloffTypeEnum.Linear:
                    return Definition.FalloffScalar * (Definition.MaxRange - distance) + Definition.MinNoise;
                case CountermeasureDefinition.FalloffTypeEnum.None:
                default:
                    return Definition.MinNoise;
            }
        }

        public void Close()
        {
            IsActive = false;
            Particle?.Close();
            //ServerNetwork.SendToEveryoneInSync((SerializedCloseProjectile) deadCountermeasure, deadCountermeasure.Position);
        }
    }
}
