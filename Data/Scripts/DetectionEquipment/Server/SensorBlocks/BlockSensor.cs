using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
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
        public IMyTerminalBlock Block;
        public ISensor Sensor;
        public SubpartManager SubpartManager;
        public readonly SensorDefinition Definition;

        public HashSet<DetectionInfo> Detections = new HashSet<DetectionInfo>();

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

        public BlockSensor(IMyTerminalBlock block, SensorDefinition definition)
        {
            Block = block;
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

            if (Block.ShowOnHUD)
            {
                var color = new Color(0, 0, 255, 100);
                var matrix = MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * Block.WorldMatrix;

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
    }
}
