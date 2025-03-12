using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Structs;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;
using static DetectionEquipment.Shared.Definitions.SensorDefinition;

namespace DetectionEquipment.Server.SensorBlocks
{
    internal class BlockSensor
    {
        public IMyCameraBlock Block;
        public ISensor Sensor;
        public SubpartManager SubpartManager;
        public readonly SensorDefinition Definition;

        public HashSet<DetectionInfo> Detections = new HashSet<DetectionInfo>();

        
        MyDefinitionId ElectricityId = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/Electricity");

        public double Azimuth { get; private set;} = 0;
        public double Elevation { get; private set; } = 0;

        private double _desiredAzimuth = 0, _desiredElevation = 0;
        public double DesiredAzimuth
        {
            get
            {
                return _desiredAzimuth;
            }
            set
            {
                _desiredAzimuth = MathUtils.NormalizeAngle(value);
            }
        }
        public double DesiredElevation
        {
            get
            {
                return _desiredElevation;
            }
            set
            {
                _desiredElevation = MathUtils.NormalizeAngle(value);
            }
        }

        private MyEntitySubpart _aziPart = null, _elevPart = null;
        private Matrix _baseLocalMatrix;

        public BlockSensor(IMyFunctionalBlock block, SensorDefinition definition)
        {
            Block = (IMyCameraBlock) block;
            Definition = definition;
            SubpartManager = new SubpartManager();

            if (Definition.Movement != null)
            {
                _aziPart = SubpartManager.RecursiveGetSubpart(block, Definition.Movement.AzimuthPart);
                _elevPart = SubpartManager.RecursiveGetSubpart(block, Definition.Movement.ElevationPart);
                _baseLocalMatrix = Block.LocalMatrix;
            }

            switch (definition.Type)
            {
                case SensorType.Radar:
                    Sensor = new RadarSensor(block, definition)
                    {
                        Aperture = definition.MaxAperture,
                        MinStableSignal = definition.DetectionThreshold,
                        Power = definition.MaxPowerDraw,
                    };
                    break;
                case SensorType.PassiveRadar:
                    Sensor = new PassiveRadarSensor(block, definition)
                    {
                        Aperture = definition.MaxAperture,
                        MinStableSignal = definition.DetectionThreshold,
                    };
                    break;
                case SensorType.Optical:
                case SensorType.Infrared:
                    Sensor = new VisualSensor(definition)
                    {
                        Aperture = definition.MaxAperture,
                        MinVisibility = definition.DetectionThreshold,
                    };
                    break;
            }

            ServerMain.I.BlockSensorIdMap[Sensor.Id] = this;

            if (Definition.MaxPowerDraw > 0)
            {
                Block.ResourceSink.SetMaxRequiredInputByType(ElectricityId, (float) Definition.MaxPowerDraw);
            }
        }

        public virtual void Update(ICollection<VisibilitySet> cachedVisibility)
        {
            if (_aziPart != null && Azimuth != DesiredAzimuth)
                SubpartManager.LocalRotateSubpartAbs(_aziPart, GetAzimuthMatrix(1/60f));
            if (_elevPart != null)
            {
                if (Elevation != DesiredElevation)
                    SubpartManager.LocalRotateSubpartAbs(_elevPart, GetElevationMatrix(1/60f));
                Sensor.Position = _elevPart.WorldMatrix.Translation;
                Sensor.Direction = _elevPart.WorldMatrix.Forward;

                if (Block.IsActive)
                {
                    Block.Visible = false;
                    Block.LocalMatrix = MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * _baseLocalMatrix;

                    Sensor.Direction = Block.WorldMatrix.Forward;
                }
                else if (!Block.Visible)
                {
                    Block.Visible = true;
                    Block.LocalMatrix = _baseLocalMatrix;
                }
            }
            else
            {
                Sensor.Position = Block.WorldAABB.Center;
                Sensor.Direction = (MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * Block.WorldMatrix).Forward;
            }

            if (!Block.IsWorking)
                return;

            if (Block.ShowOnHUD)
            {
                var color = new Color(0, 0, 255, 100);
                var matrix = MatrixD.CreateWorld(Sensor.Position, Sensor.Direction, Vector3D.CalculatePerpendicularVector(Sensor.Direction));

                if (Sensor.Aperture < Math.PI)
                    MySimpleObjectDraw.DrawTransparentCone(ref matrix, (float) Math.Tan(Sensor.Aperture) * MyAPIGateway.Session.SessionSettings.SyncDistance, MyAPIGateway.Session.SessionSettings.SyncDistance, ref color, 8, DebugDraw.MaterialSquare);
            }

            Detections.Clear();

            foreach (var track in cachedVisibility)
            {
                var detection = Sensor.GetDetectionInfo(track);
                if (detection != null)
                    Detections.Add(detection.Value);
            }

            if (Block.ShowOnHUD)
            {
                foreach (var detection in Detections)
                {
                    DebugDraw.AddLine(Sensor.Position, Sensor.Position + detection.Bearing * detection.Range, Color.Red, 0);
                }
            }
        }

        public virtual void Close()
        {
            ServerMain.I.BlockSensorIdMap.Remove(Sensor.Id);
            Sensor.Close();
        }

        public bool CanAimAt(Vector3D position)
        {
            if (Definition.Movement == null)
                return false;

            var angle = GetAngleToTarget(position);
            return angle.X <= Definition.Movement.MaxAzimuth && angle.X >= Definition.Movement.MinAzimuth && angle.Y <= Definition.Movement.MaxElevation && angle.Y >= Definition.Movement.MinElevation;
        }

        public void AimAt(Vector3D position)
        {
            if (Definition.Movement == null)
                return;

            var angle = GetAngleToTarget(position);

            DesiredAzimuth = MathHelper.Clamp(angle.X, Definition.Movement.MinAzimuth, Definition.Movement.MaxAzimuth);
            DesiredElevation = MathHelper.Clamp(angle.Y, Definition.Movement.MinElevation, Definition.Movement.MaxElevation);
        }

        private Matrix GetAzimuthMatrix(float delta)
        {
            var _limitedAzimuth = MathUtils.LimitRotationSpeed(Azimuth, DesiredAzimuth, Definition.Movement.AzimuthRate * delta);

            if (!Definition.Movement.CanElevateFull)
                Azimuth = MathUtils.Clamp(_limitedAzimuth, Definition.Movement.MinAzimuth, Definition.Movement.MaxAzimuth);
            else
                Azimuth = MathUtils.NormalizeAngle(_limitedAzimuth);

            return Matrix.CreateFromYawPitchRoll((float) Azimuth, 0, 0);
        }

        private Matrix GetElevationMatrix(float delta)
        {
            var _limitedElevation = MathUtils.LimitRotationSpeed(Elevation, DesiredElevation, Definition.Movement.ElevationRate * delta);

            if (!Definition.Movement.CanElevateFull)
                Elevation = MathUtils.Clamp(_limitedElevation, Definition.Movement.MinElevation, Definition.Movement.MaxElevation);
            else
                Elevation = MathUtils.NormalizeAngle(_limitedElevation);

            return Matrix.CreateFromYawPitchRoll(0, (float) Elevation, 0);
        }

        private Vector2D GetAngleToTarget(Vector3D? targetPos)
        {
            if (targetPos == null)
                return Vector2D.Zero;
        
            Vector3D vecFromTarget = Block.WorldMatrix.Translation - targetPos.Value;
        
            vecFromTarget = Vector3D.Rotate(vecFromTarget.Normalized(), MatrixD.Invert(Block.WorldMatrix));
        
            double desiredAzimuth = Math.Atan2(vecFromTarget.X, vecFromTarget.Z);
            if (double.IsNaN(desiredAzimuth))
                desiredAzimuth = Math.PI;
        
            double desiredElevation = Math.Asin(-vecFromTarget.Y);
            if (double.IsNaN(desiredElevation))
                desiredElevation = Math.PI;
        
            return new Vector2D(desiredAzimuth, desiredElevation);
        }
    }
}
