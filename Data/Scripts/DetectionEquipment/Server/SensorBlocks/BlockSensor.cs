using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using static DetectionEquipment.Server.SensorBlocks.GridSensorManager;

namespace DetectionEquipment.Server.SensorBlocks
{
    internal abstract class BlockSensor
    {
        public IMyCubeBlock Block;
        public ISensor Sensor;

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

        public BlockSensor(ISensor sensor, IMyCubeBlock block)
        {
            Sensor = sensor;
            Block = block;

            Sensor.Position = block.WorldAABB.Center;
            Sensor.Direction = block.WorldMatrix.Forward;
        }

        public virtual void Update(ICollection<VisibilitySet> cachedVisibility)
        {
            Sensor.Position = Block.WorldAABB.Center;
            Sensor.Direction = Vector3D.Transform(Block.WorldMatrix.Forward, _rotationMatrix);

            if (!Block.IsWorking)
                return;

            DebugDraw.AddLine(Sensor.Position, Sensor.Position + Vector3D.Rotate(Sensor.Direction * 100000, MatrixD.CreateFromAxisAngle(Block.WorldMatrix.Right, Sensor.Aperture)), Color.Blue, 0);
            DebugDraw.AddLine(Sensor.Position, Sensor.Position + Vector3D.Rotate(Sensor.Direction * 100000, MatrixD.CreateFromAxisAngle(Block.WorldMatrix.Left, Sensor.Aperture)), Color.Blue, 0);
            DebugDraw.AddLine(Sensor.Position, Sensor.Position + Vector3D.Rotate(Sensor.Direction * 100000, MatrixD.CreateFromAxisAngle(Block.WorldMatrix.Up, Sensor.Aperture)), Color.Blue, 0);
            DebugDraw.AddLine(Sensor.Position, Sensor.Position + Vector3D.Rotate(Sensor.Direction * 100000, MatrixD.CreateFromAxisAngle(Block.WorldMatrix.Down, Sensor.Aperture)), Color.Blue, 0);
            //MyAPIGateway.Utilities.ShowNotification($"Detections: {Detections.Count}", 1000/60);

            Detections.Clear();

            foreach (var track in cachedVisibility)
            {
                var detection = Sensor.GetDetectionInfo(track);
                if (detection != null)
                    Detections.Add(detection.Value);
            }
        }

        public virtual void Close()
        {
            
        }

        private void UpdateRotationMatrix()
        {
            _rotationMatrix = Matrix.CreateRotationZ(_azimuth) * Matrix.CreateRotationX(_elevation);
        }
    }

    internal class BlockSensor<T> : BlockSensor where T : class, ISensor
    {
        public new readonly T Sensor;

        public BlockSensor(T sensor, IMyCubeBlock block) : base(sensor, block)
        {
            
        }

        public override void Close()
        {
            base.Close();

            if (typeof(T) == typeof(PassiveRadarSensor))
                (Sensor as PassiveRadarSensor)?.Close();
        }

        public static explicit operator T(BlockSensor<T> sensor) => sensor.Sensor;
    }
}
