using System;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Serialization;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System.Collections.Generic;
using DetectionEquipment.Client.BlockLogic.Sensors;
using DetectionEquipment.Server.Countermeasures;
using Sandbox.Game.EntityComponents;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;
using static DetectionEquipment.Shared.Definitions.SensorDefinition;
using static DetectionEquipment.Client.BlockLogic.Sensors.SensorUpdatePacket;

namespace DetectionEquipment.Server.SensorBlocks
{
    internal class BlockSensor
    {
        public IMyFunctionalBlock Block;
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
        
        private FieldId _settingsUpdated = FieldId.None;
        public double Azimuth { get; private set;} = 0;
        public double Elevation { get; private set; } = 0;

        private double _desiredAzimuth = 0, _minAzimuth, _maxAzimuth, _desiredElevation = 0, _minElevation, _maxElevation;
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
                _settingsUpdated |= FieldId.Azimuth;
            }
        }
        public double MinAzimuth
        {
            get
            {
                return _minAzimuth;
            }
            set
            {
                if (_minAzimuth == value)
                    return;
                _minAzimuth = value;
                if (MaxAzimuth < MinAzimuth)
                    MaxAzimuth = MinAzimuth;
                if (DesiredAzimuth < MinAzimuth)
                    DesiredAzimuth = MinAzimuth;
                _settingsUpdated |= FieldId.MinAzimuth;
            }
        }
        public double MaxAzimuth
        {
            get
            {
                return _maxAzimuth;
            }
            set
            {
                if (_maxAzimuth == value)
                    return;
                _maxAzimuth = value;
                if (MinAzimuth > MaxAzimuth)
                    MinAzimuth = MaxAzimuth;
                if (DesiredAzimuth > MaxAzimuth)
                    DesiredAzimuth = MaxAzimuth;
                _settingsUpdated |= FieldId.MaxAzimuth;
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
                _settingsUpdated |= FieldId.Elevation;
            }
        }
        public double MinElevation
        {
            get
            {
                return _minElevation;
            }
            set
            {
                if (_minElevation == value)
                    return;
                _minElevation = value;
                if (MaxElevation < MinElevation)
                    MaxElevation = MinElevation;
                if (DesiredElevation < MinElevation)
                    DesiredElevation = MinElevation;
                _settingsUpdated |= FieldId.MinElevation;
            }
        }
        public double MaxElevation
        {
            get
            {
                return _maxElevation;
            }
            set
            {
                if (_maxElevation == value)
                    return;
                _maxElevation = value;
                if (MinElevation > MaxElevation)
                    MinElevation = MaxElevation;
                if (DesiredElevation > MaxElevation)
                    DesiredElevation = MaxElevation;
                _settingsUpdated |= FieldId.MaxElevation;
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
                _settingsUpdated |= FieldId.Aperture;
            }
        }

        public bool AllowMechanicalControl;

        public void UpdateFromPacket(SensorUpdatePacket packet)
        {
            if ((_settingsUpdated & FieldId.Azimuth) == 0)
                packet.SetField(FieldId.Azimuth, ref _desiredAzimuth);
            if ((_settingsUpdated & FieldId.Elevation) == 0)
                packet.SetField(FieldId.Elevation, ref _desiredElevation);
            if ((_settingsUpdated & FieldId.Aperture) == 0)
            {
                double sensorAperture = Sensor.Aperture;
                packet.SetField(FieldId.Aperture, ref sensorAperture);
                Sensor.Aperture = sensorAperture;
            }
            if ((_settingsUpdated & FieldId.MinAzimuth) == 0)
                packet.SetField(FieldId.MinAzimuth, ref _minAzimuth);
            if ((_settingsUpdated & FieldId.MaxAzimuth) == 0)
                packet.SetField(FieldId.MaxAzimuth, ref _maxAzimuth);
            if ((_settingsUpdated & FieldId.MinElevation) == 0)
                packet.SetField(FieldId.MinElevation, ref _minElevation);
            if ((_settingsUpdated & FieldId.MaxElevation) == 0)
                packet.SetField(FieldId.MaxElevation, ref _maxElevation);
            if ((_settingsUpdated & FieldId.AllowMechanicalControl) == 0)
                packet.SetField(FieldId.AllowMechanicalControl, ref AllowMechanicalControl);

            _settingsUpdated |= packet.Fields;
        }

        internal void LoadDefaultSettings()
        {
            AllowMechanicalControl = true;
            _minAzimuth = (float) (Definition.Movement?.MinAzimuth ?? 0);
            _maxAzimuth = (float) (Definition.Movement?.MaxAzimuth ?? 0);
            _minElevation = (float) (Definition.Movement?.MinElevation ?? 0);
            _maxElevation = (float) (Definition.Movement?.MaxElevation ?? 0);
            _settingsUpdated = FieldId.All;
        }

        private MyEntitySubpart _aziPart, _elevPart;

        public BlockSensor(IMyFunctionalBlock block, SensorDefinition definition)
        {
            Block = block;
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
                case SensorType.Antenna:
                    Sensor = new AntennaSensor(definition);
                    break;
                default:
                    throw new Exception($"Invalid SensorType {definition.Type}");
            }

            ServerMain.I.BlockSensorIdMap[Sensor.Id] = this;
            MyAPIGateway.Utilities.InvokeOnGameThread(((MyResourceSinkComponent)block.ResourceSink).Update);
        }

        public virtual void Update(ICollection<VisibilitySet> cachedVisibility)
        {
            Sensor.Enabled = Block.IsWorking || (Block.IsFunctional && Block.Enabled && Definition.MaxPowerDraw <= 0);
            Detections.Clear();

            UpdateSensorMatrix();

            if (_settingsUpdated != FieldId.None && MyAPIGateway.Session.GameplayFrameCounter % 7 == 0)
            {
                ServerNetwork.SendToEveryoneInSync(new SensorUpdatePacket(this, _settingsUpdated), Block.GetPosition());
                BlockSensorSettings.SaveBlockSettings(Block);
                _settingsUpdated = FieldId.None;
            }

            if (!Sensor.Enabled)
                return;

            Sensor.CountermeasureNoise = CountermeasureManager.GetNoise(Sensor);

            foreach (var track in cachedVisibility)
            {
                var detection = Sensor.GetDetectionInfo(track);
                if (detection != null)
                    Detections.Add(detection.Value);
            }

            foreach (var drfmTrack in CountermeasureManager.GenerateDrfmTracks(Sensor, Block))
                Detections.Add(drfmTrack);

            if (Block.ShowOnHUD && !MyAPIGateway.Utilities.IsDedicated && Block.HasLocalPlayerAccess())
                foreach (var detection in Detections)
                    DebugDraw.AddLine(Sensor.Position, detection.Position, Color.Red, 0);
        }

        private void UpdateSensorMatrix()
        {
            if (Sensor.Enabled && Definition.Movement != null)
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

        public static float GetPowerDraw(IMyFunctionalBlock block)
        {
            GridSensorManager manager;
            List<BlockSensor> sensors;
            if (!block.Enabled || !block.IsFunctional || !ServerMain.I.GridSensorMangers.TryGetValue(block.CubeGrid, out manager) || !manager.BlockSensorMap.TryGetValue(block, out sensors))
                return 0;

            float totalDraw = 0;
            foreach (var sensor in sensors)
                if (sensor.Sensor.Enabled)
                    totalDraw += (float) sensor.Definition.MaxPowerDraw;
            block.ResourceSink.SetMaxRequiredInputByType(GlobalData.ElectricityId, totalDraw / 1000000);
            return totalDraw / 1000000;
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

            DesiredAzimuth = MathHelper.Clamp(angle.X, MinAzimuth, MaxAzimuth);
            DesiredElevation = MathHelper.Clamp(angle.Y, MinElevation, MaxElevation);
        }

        private Matrix GetAzimuthMatrix(float delta)
        {
            var limitedAzimuth = MathUtils.LimitRotationSpeed(Azimuth, DesiredAzimuth, Definition.Movement.AzimuthRate * delta);

            if (!Definition.Movement.CanElevateFull)
                Azimuth = MathUtils.Clamp(limitedAzimuth, Math.Max(Definition.Movement.MinAzimuth, MinAzimuth), Math.Min(Definition.Movement.MaxAzimuth, MaxAzimuth));
            else
                Azimuth = MathUtils.NormalizeAngle(limitedAzimuth);

            return Matrix.CreateFromYawPitchRoll((float) Azimuth, 0, 0);
        }

        private Matrix GetElevationMatrix(float delta)
        {
            var limitedElevation = MathUtils.LimitRotationSpeed(Elevation, DesiredElevation, Definition.Movement.ElevationRate * delta);

            if (!Definition.Movement.CanElevateFull)
                Elevation = MathUtils.Clamp(limitedElevation, Math.Max(Definition.Movement.MinElevation, MinElevation), Math.Min(Definition.Movement.MaxElevation, MaxElevation));
            else
                Elevation = MathUtils.NormalizeAngle(limitedElevation);

            return Matrix.CreateFromYawPitchRoll(0, (float) Elevation, 0);
        }
    }
}
