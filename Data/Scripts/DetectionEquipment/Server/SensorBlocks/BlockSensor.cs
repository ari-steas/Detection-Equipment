using System;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Serialization;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System.Collections.Generic;
using DetectionEquipment.Server.Countermeasures;
using Sandbox.Game.EntityComponents;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
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

        private IMyModelDummy _sensorDummy = null;
        private MyEntity _dummyParent = null;

        private MatrixD SensorMatrix => _sensorDummy == null
            ? (_elevPart == null ? (MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * Block.WorldMatrix) : _elevPart.WorldMatrix)
            : SensorDummyMatrix;

        private MatrixD SensorDummyMatrix => (_elevPart == null && Definition.Movement != null)
            ? MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * _sensorDummy.Matrix * _dummyParent.WorldMatrix
            : _sensorDummy.Matrix * _dummyParent.WorldMatrix;
        
        private bool _settingsUpdated = false;
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
                var normalized = MathUtils.NormalizeAngle(value);
                if (_desiredAzimuth == normalized)
                    return;
                _desiredAzimuth = normalized;
                ServerNetwork.SendToEveryoneInSync(new Client.BlockLogic.Sensors.SensorUpdatePacket(this), Block.GetPosition());
                _settingsUpdated = true;
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
                var normalized = MathUtils.NormalizeAngle(value);
                if (_desiredElevation == normalized)
                    return;
                _desiredElevation = normalized;
                ServerNetwork.SendToEveryoneInSync(new Client.BlockLogic.Sensors.SensorUpdatePacket(this), Block.GetPosition());
                _settingsUpdated = true;
            }
        }
        public double Aperture
        {
            get
            {
                return Sensor.Aperture;
            }
            set
            {
                var normalized = MathHelper.Clamp(value, Definition.MinAperture, Definition.MaxAperture);
                if (Sensor.Aperture == normalized)
                    return;
                Sensor.Aperture = normalized;
                ServerNetwork.SendToEveryoneInSync(new Client.BlockLogic.Sensors.SensorUpdatePacket(this), Block.GetPosition());
                _settingsUpdated = true;
            }
        }

        public void UpdateFromPacket(Client.BlockLogic.Sensors.SensorUpdatePacket packet)
        {
            _desiredAzimuth = packet.Azimuth;
            _desiredElevation = packet.Elevation;
            Sensor.Aperture = packet.Aperture;
            _settingsUpdated = true;
        }

        private MyEntitySubpart _aziPart, _elevPart;

        public BlockSensor(IMyFunctionalBlock block, SensorDefinition definition)
        {
            Block = (IMyCameraBlock) block;
            Definition = definition;
            SubpartManager = new SubpartManager();

            if (Definition.Movement != null)
            {
                _aziPart = SubpartManager.RecursiveGetSubpart(block, Definition.Movement.AzimuthPart);
                _elevPart = SubpartManager.RecursiveGetSubpart(block, Definition.Movement.ElevationPart);
                _desiredAzimuth = Definition.Movement.HomeAzimuth;
                _desiredElevation = Definition.Movement.HomeElevation;
            }

            if (!string.IsNullOrEmpty(Definition.SensorEmpty))
                _sensorDummy = SubpartManager.RecursiveGetDummy(Block, Definition.SensorEmpty, out _dummyParent);

            switch (definition.Type)
            {
                case SensorType.Radar:
                    Sensor = new RadarSensor(block, definition);
                    break;
                case SensorType.PassiveRadar:
                    Sensor = new PassiveRadarSensor(block, definition);
                    break;
                case SensorType.Optical:
                case SensorType.Infrared:
                    Sensor = new VisualSensor(definition);
                    break;
                default:
                    throw new Exception($"Invalid SensorType {definition.Type}");
            }

            var resourceSink = (MyResourceSinkComponent)Block.ResourceSink;
            resourceSink.SetRequiredInputFuncByType(GlobalData.ElectricityId,
                () => Block.Enabled ? (float)Definition.MaxPowerDraw / 1000000 : 0);
            resourceSink.SetMaxRequiredInputByType(GlobalData.ElectricityId, (float)Definition.MaxPowerDraw / 1000000);
            resourceSink.Update();
            Block.EnabledChanged += b => resourceSink.Update();

            ServerMain.I.BlockSensorIdMap[Sensor.Id] = this;
        }

        public virtual void Update(ICollection<VisibilitySet> cachedVisibility)
        {
            Sensor.Enabled = Block.IsWorking;
            Detections.Clear();

            UpdateSensorMatrix();

            if (_settingsUpdated && MyAPIGateway.Session.GameplayFrameCounter % 9 == 0)
            {
                BlockSensorSettings.SaveBlockSettings(Block);
                _settingsUpdated = false;
            }

            if (!Block.IsWorking)
                return;

            Sensor.CountermeasureNoise = CountermeasureManager.GetNoise(Sensor);

            foreach (var track in cachedVisibility)
            {
                var detection = Sensor.GetDetectionInfo(track);
                if (detection != null)
                    Detections.Add(detection.Value);
            }

            if (Block.ShowOnHUD && !MyAPIGateway.Utilities.IsDedicated)
                foreach (var detection in Detections)
                    DebugDraw.AddLine(Sensor.Position, detection.Position, Color.Red, 0);
        }

        private void UpdateSensorMatrix()
        {
            if (Block.IsWorking && Definition.Movement != null)
            {
                if (Azimuth != DesiredAzimuth)
                {
                    var matrix = GetAzimuthMatrix(1 / 60f);
                    if (_aziPart != null)
                        SubpartManager.LocalRotateSubpartAbs(_aziPart, matrix);
                }

                if (Elevation != DesiredElevation)
                {
                    var matrix = GetElevationMatrix(1 / 60f);
                    if (_elevPart != null)
                        SubpartManager.LocalRotateSubpartAbs(_elevPart, matrix);
                }
            }

            var sensorMatrix = SensorMatrix;
            Sensor.Position = sensorMatrix.Translation;
            Sensor.Direction = sensorMatrix.Forward;
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

            var angle = MathUtils.GetAngleTo(Block.WorldMatrix, position);
            return angle.X <= Definition.Movement.MaxAzimuth && angle.X >= Definition.Movement.MinAzimuth && angle.Y <= Definition.Movement.MaxElevation && angle.Y >= Definition.Movement.MinElevation;
        }

        public void AimAt(Vector3D position)
        {
            if (Definition.Movement == null)
                return;

            var angle = MathUtils.GetAngleTo(Block.WorldMatrix, position);

            DesiredAzimuth = MathHelper.Clamp(angle.X, Definition.Movement.MinAzimuth, Definition.Movement.MaxAzimuth);
            DesiredElevation = MathHelper.Clamp(angle.Y, Definition.Movement.MinElevation, Definition.Movement.MaxElevation);
        }

        private Matrix GetAzimuthMatrix(float delta)
        {
            var limitedAzimuth = MathUtils.LimitRotationSpeed(Azimuth, DesiredAzimuth, Definition.Movement.AzimuthRate * delta);

            if (!Definition.Movement.CanElevateFull)
                Azimuth = MathUtils.Clamp(limitedAzimuth, Definition.Movement.MinAzimuth, Definition.Movement.MaxAzimuth);
            else
                Azimuth = MathUtils.NormalizeAngle(limitedAzimuth);

            return Matrix.CreateFromYawPitchRoll((float) Azimuth, 0, 0);
        }

        private Matrix GetElevationMatrix(float delta)
        {
            var limitedElevation = MathUtils.LimitRotationSpeed(Elevation, DesiredElevation, Definition.Movement.ElevationRate * delta);

            if (!Definition.Movement.CanElevateFull)
                Elevation = MathUtils.Clamp(limitedElevation, Definition.Movement.MinElevation, Definition.Movement.MaxElevation);
            else
                Elevation = MathUtils.NormalizeAngle(limitedElevation);

            return Matrix.CreateFromYawPitchRoll(0, (float) Elevation, 0);
        }
    }
}
