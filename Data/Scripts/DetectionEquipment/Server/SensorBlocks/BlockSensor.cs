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

        
        MyDefinitionId _electricityId = MyDefinitionId.Parse("MyObjectBuilder_GasProperties/Electricity");

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
                ServerNetwork.SendToEveryoneInSync(new SensorUpdatePacket(this), Block.GetPosition());
                BlockSensorSettings.SaveBlockSettings(Block);
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
                ServerNetwork.SendToEveryoneInSync(new SensorUpdatePacket(this), Block.GetPosition());
                BlockSensorSettings.SaveBlockSettings(Block);
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
                Sensor.Aperture = MathHelper.Clamp(value, Definition.MinAperture, Definition.MaxAperture);
                ServerNetwork.SendToEveryoneInSync(new SensorUpdatePacket(this), Block.GetPosition());
                BlockSensorSettings.SaveBlockSettings(Block);
            }
        }

        public void UpdateFromPacket(SensorUpdatePacket packet)
        {
            _desiredAzimuth = packet.Azimuth;
            _desiredElevation = packet.Elevation;
            Sensor.Aperture = packet.Aperture;
            BlockSensorSettings.SaveBlockSettings(Block);
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
            }

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

            ServerMain.I.BlockSensorIdMap[Sensor.Id] = this;

            if (Definition.MaxPowerDraw > 0)
            {
                Block.ResourceSink.SetMaxRequiredInputByType(_electricityId, (float) Definition.MaxPowerDraw);
            }

            ServerNetwork.SendToEveryoneInSync(new SensorInitPacket(this), Block.GetPosition());
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
            }
            else
            {
                Sensor.Position = Block.WorldAABB.Center;
                Sensor.Direction = (MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * Block.WorldMatrix).Forward;
            }

            if (!Block.IsWorking)
                return;

            Detections.Clear();
            Sensor.CountermeasureNoise = CountermeasureManager.GetNoise(Sensor);

            foreach (var track in cachedVisibility)
            {
                var detection = Sensor.GetDetectionInfo(track);
                if (detection != null)
                    Detections.Add(detection.Value);
            }

            if (Block.ShowOnHUD && !MyAPIGateway.Utilities.IsDedicated)
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
