﻿using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using DetectionEquipment.Shared;
using Sandbox.ModAPI;
using System;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game;
using VRageMath;

namespace DetectionEquipment.Client.BlockLogic.Sensors
{
    public class ClientSensorData
    {
        public readonly uint Id;
        public readonly SensorDefinition Definition;
        public float Aperture = 0;
        public float _desiredAzimuth = 0, _minAzimuth, _maxAzimuth;
        public float _desiredElevation = 0, _minElevation, _maxElevation;

        public float DesiredAzimuth
        {
            get { return _desiredAzimuth; }
            set { _desiredAzimuth = MathHelper.Clamp(value, MinAzimuth, MaxAzimuth); }
        }

        public float MinAzimuth
        {
            get { return _minAzimuth; }
            set
            {
                _minAzimuth = value; 
                if (MaxAzimuth < MinAzimuth)
                    MaxAzimuth = MinAzimuth;
                if (DesiredAzimuth < MinAzimuth)
                    DesiredAzimuth = MinAzimuth;
            }
        }

        public float MaxAzimuth
        {
            get { return _maxAzimuth; }
            set
            {
                _maxAzimuth = value;
                if (MinAzimuth > MaxAzimuth)
                    MinAzimuth = MaxAzimuth;
                if (DesiredAzimuth > MaxAzimuth)
                    DesiredAzimuth = MaxAzimuth;
            }
        }

        public float DesiredElevation
        {
            get { return _desiredElevation; }
            set { _desiredElevation = MathHelper.Clamp(value, MinElevation, MaxElevation); }
        }

        public float MinElevation
        {
            get { return _minElevation; }
            set
            {
                _minElevation = value; 
                if (MaxElevation < MinElevation)
                    MaxElevation = MinElevation;
                if (DesiredElevation < MinElevation)
                    DesiredElevation = MinElevation;
            }
        }

        public float MaxElevation
        {
            get { return _maxElevation; }
            set
            {
                _maxElevation = value;
                if (MinElevation > MaxElevation)
                    MinElevation = MaxElevation;
                if (DesiredElevation > MaxElevation)
                    DesiredElevation = MaxElevation;
            }
        }

        public bool AllowMechanicalControl = true;

        public float Azimuth  { get; private set; } = 0;
        public float Elevation  { get; private set; } = 0;
        public Vector3D Position { get; private set; } = Vector3D.Zero;
        public Vector3D Direction { get; private set; } = Vector3D.Forward;

        private readonly MyEntitySubpart _aziPart, _elevPart;
        private readonly Matrix _baseLocalMatrix;
        private readonly SubpartManager _subpartManager = new SubpartManager();

        private readonly IMyModelDummy _sensorDummy = null;
        private readonly MyEntity _dummyParent = null;

        public readonly IMyFunctionalBlock Block;
        public readonly IMyCameraBlock CameraBlock;

        public Color Color;

        private MatrixD SensorMatrix => _sensorDummy == null
            ? (_elevPart == null ? (MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * Block.WorldMatrix) : _elevPart.WorldMatrix)
            : SensorDummyMatrix;

        private MatrixD SensorDummyMatrix => (_elevPart == null && Definition.Movement != null)
            ? MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * _sensorDummy.Matrix * _dummyParent.WorldMatrix
            : _sensorDummy.Matrix * _dummyParent.WorldMatrix;

        public ClientSensorData(uint id, SensorDefinition definition, IMyFunctionalBlock block, Color? color)
        {
            Id = id;
            Definition = definition;
            Block = block;
            CameraBlock = block as IMyCameraBlock;

            if (Definition.Movement != null)
            {
                _aziPart = _subpartManager.RecursiveGetSubpart(block, Definition.Movement.AzimuthPart);
                _elevPart = _subpartManager.RecursiveGetSubpart(block, Definition.Movement.ElevationPart);
                _baseLocalMatrix = block.LocalMatrix;

                if (_aziPart == null && !string.IsNullOrEmpty(Definition.Movement.AzimuthPart))
                    Log.Info("ClientSensorData", $"Failed to get sensor w/ DefId {Definition.Id} azimuth part {Definition.Movement.AzimuthPart}!\n" +
                                                 $"Valid subparts:\n\t{string.Join("\n\t", SubpartManager.GetAllSubpartsDict(block).Keys)}");
                if (_elevPart == null && !string.IsNullOrEmpty(Definition.Movement.ElevationPart))
                    Log.Info("ClientSensorData", $"Failed to get sensor w/ DefId {Definition.Id} elevation part {Definition.Movement.AzimuthPart}!\n" +
                                                 $"Valid subparts:\n\t{string.Join("\n\t", SubpartManager.GetAllSubpartsDict(block).Keys)}");
                //Log.Info("ClientBlockSensor", "Inited subparts for " + block.BlockDefinition.SubtypeName);

                MinAzimuth = (float) Definition.Movement.MinAzimuth;
                MaxAzimuth = (float) Definition.Movement.MaxAzimuth;
                MinElevation = (float) Definition.Movement.MinElevation;
                MaxElevation = (float) Definition.Movement.MaxElevation;
            }

            _dummyParent = (MyEntity) block;
            if (!string.IsNullOrEmpty(Definition.SensorEmpty))
                _sensorDummy = SubpartManager.RecursiveGetDummy(block, Definition.SensorEmpty, out _dummyParent);

            Color = color ?? new Color((uint) ((50 + Id) * block.EntityId)).Alpha(0.1f);
        }

        public void Update(bool isPrimarySensor)
        {
            UpdateSensorMatrix();
            if (isPrimarySensor)
                UpdateCameraView();

            // HUD
            if (Block.ShowOnHUD && Block.HasLocalPlayerAccess())
            {
                var matrix = SensorMatrix;
                if (Aperture < Math.PI)
                    MySimpleObjectDraw.DrawTransparentCone(ref matrix, (float) Math.Tan(Aperture) * MyAPIGateway.Session.SessionSettings.SyncDistance, MyAPIGateway.Session.SessionSettings.SyncDistance, ref Color, 8, DebugDraw.MaterialSquare);
                else
                {
                    DebugDraw.AddLine(Position, Position + Direction * GlobalData.MaxSensorRange.Value, Color, 0);
                    MySimpleObjectDraw.DrawTransparentSphere(ref matrix, (float) GlobalData.MaxSensorRange.Value, ref Color, MySimpleObjectRasterizer.Wireframe, 20);
                }
            }
        }

        private void UpdateSensorMatrix()
        {
            if (Definition.Movement != null)
            {
                if (Azimuth != DesiredAzimuth)
                {
                    var matrix = GetAzimuthMatrix(1 / 60f);
                    if (_aziPart != null && !MyAPIGateway.Session.IsServer) // Server already rotates parts, don't interfere with that
                        _subpartManager.LocalRotateSubpartAbs(_aziPart, matrix);
                }

                if (Elevation != DesiredElevation)
                {
                    var matrix = GetElevationMatrix(1 / 60f);
                    if (_elevPart != null && !MyAPIGateway.Session.IsServer)
                        _subpartManager.LocalRotateSubpartAbs(_elevPart, matrix);
                }
            }

            var sensorMatrix = SensorMatrix;
            Position = sensorMatrix.Translation;
            Direction = sensorMatrix.Forward;
        }

        private void UpdateCameraView()
        {
            return; // TODO bring this back

            if (MyAPIGateway.Session.IsServer || CameraBlock == null)
                return;

            // Hide/show & rotate block based on whether a player is in the camera. TODO: This doesn't quite work.
            if (CameraBlock.IsActive)
            {
                CameraBlock.Visible = false;

                CameraBlock.LocalMatrix = SensorMatrix * MatrixD.Invert(CameraBlock.CubeGrid.WorldMatrix);
            }
            else if (!CameraBlock.Visible)
            {
                CameraBlock.Visible = true;
                CameraBlock.LocalMatrix = _baseLocalMatrix;
            }
        }

        private Matrix GetAzimuthMatrix(float delta)
        {
            var limitedAzimuth = MathUtils.LimitRotationSpeed(Azimuth, DesiredAzimuth, Definition.Movement.AzimuthRate * delta);

            if (!Definition.Movement.CanElevateFull)
                Azimuth = (float) MathUtils.Clamp(limitedAzimuth, Math.Max(Definition.Movement.MinAzimuth, MinAzimuth), Math.Min(Definition.Movement.MaxAzimuth, MaxAzimuth));
            else
                Azimuth = (float) MathUtils.NormalizeAngle(limitedAzimuth);

            return Matrix.CreateFromYawPitchRoll(Azimuth, 0, 0);
        }

        private Matrix GetElevationMatrix(float delta)
        {
            var limitedElevation = MathUtils.LimitRotationSpeed(Elevation, DesiredElevation, Definition.Movement.ElevationRate * delta);

            if (!Definition.Movement.CanElevateFull)
                Elevation = (float) MathUtils.Clamp(limitedElevation, Math.Max(Definition.Movement.MinElevation, MinElevation), Math.Min(Definition.Movement.MaxElevation, MaxElevation));
            else
                Elevation = (float) MathUtils.NormalizeAngle(limitedElevation);

            return Matrix.CreateFromYawPitchRoll(0, Elevation, 0);
        }
    }
}
