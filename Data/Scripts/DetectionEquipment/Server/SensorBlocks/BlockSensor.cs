using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using Sandbox.ModAPI;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;
using static DetectionEquipment.Shared.Definitions.SensorDefinition;

namespace DetectionEquipment.Server.SensorBlocks
{
    internal class BlockSensor
    {
        public IMyCubeBlock Block;
        public ISensor Sensor;
        public SubpartManager SubpartManager;
        public readonly SensorDefinition Definition;

        public HashSet<DetectionInfo> Detections = new HashSet<DetectionInfo>();

        private float _azimuth = 0, _elevation = 0;
        protected Matrix _rotationMatrix = Matrix.Identity;
        public float Azimuth
        {
            get
            {
                return _azimuth;
            }
            set
            {
                _azimuth = value;
                UpdateRotationMatrix();
            }
        }

        public float Elevation
        {
            get
            {
                return _elevation;
            }
            set
            {
                _elevation = value;
                UpdateRotationMatrix();
            }
        }

        private MyEntitySubpart _aziPart = null, _elevPart = null;

        public BlockSensor(IMyCubeBlock block, SensorDefinition definition)
        {
            Block = block;
            Definition = definition;
            SubpartManager = new SubpartManager();

            if (Definition.Movement != null)
            {
                _aziPart = SubpartManager.RecursiveGetSubpart(block, Definition.Movement.Value.AzimuthPart);
                _elevPart = SubpartManager.RecursiveGetSubpart(block, Definition.Movement.Value.ElevationPart);
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
        }

        public virtual void Update(ICollection<VisibilitySet> cachedVisibility)
        {
            if (Sensor is RadarSensor)
                Azimuth += (float) (4 * Math.PI / 60 / 60);

            if (_aziPart != null)
                SubpartManager.LocalRotateSubpartAbs(_aziPart, GetAzimuthMatrix(1/60f));
            if (_elevPart != null)
            {
                SubpartManager.LocalRotateSubpartAbs(_elevPart, GetElevationMatrix(1/60f));
                Sensor.Position = _elevPart.WorldMatrix.Translation;
                Sensor.Direction = _elevPart.WorldMatrix.Forward;
            }
            else
            {
                Sensor.Position = Block.WorldAABB.Center;
                Sensor.Direction = (_rotationMatrix * Block.WorldMatrix).Forward;
            }

            if (!Block.IsWorking)
                return;

            {
                var color = new Color(0, 0, 255, 100);
                var matrix = _rotationMatrix * Block.WorldMatrix;

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

            foreach (var detection in Detections)
            {
                DebugDraw.AddLine(Sensor.Position, Sensor.Position + detection.Bearing * detection.Range, Color.Red, 0);
            }
        }

        public virtual void Close()
        {
            (Sensor as PassiveRadarSensor)?.Close();
        }

        private void UpdateRotationMatrix()
        {
            _rotationMatrix = Matrix.CreateFromYawPitchRoll(_azimuth, _elevation, 0);
        }

        private Matrix GetAzimuthMatrix(float delta)
        {
            var _limitedAzimuth = MathUtils.LimitRotationSpeed(_azimuth, _azimuth, 1 * delta);

            Azimuth = (float)MathUtils.NormalizeAngle(_limitedAzimuth); // Adjust rotation to (-180, 180), but don't have any limits
            return Matrix.CreateFromYawPitchRoll(_azimuth, 0, 0);
        }

        private Matrix GetElevationMatrix(float delta)
        {
            var _limitedElevation = MathUtils.LimitRotationSpeed(_elevation, _elevation, 1 * delta);

            Elevation = (float)MathUtils.NormalizeAngle(_limitedElevation);
            return Matrix.CreateFromYawPitchRoll(0, _elevation, 0);
        }
    }
}
