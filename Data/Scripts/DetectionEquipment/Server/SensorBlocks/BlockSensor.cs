using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using VRageMath;

namespace DetectionEquipment.Server.SensorBlocks
{
    internal abstract class BlockSensor
    {
        public IMyCubeBlock Block;
        public ISensor Sensor;

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

        public virtual void Update()
        {
            Sensor.Position = Block.WorldAABB.Center;
            Sensor.Direction = Vector3D.Transform(Block.WorldMatrix.Forward, _rotationMatrix);

            DebugDraw.AddLine(Sensor.Position, Sensor.Position + Vector3D.Rotate(Sensor.Direction * 100000, MatrixD.CreateFromAxisAngle(Block.WorldMatrix.Right, Sensor.Aperture)), Color.Blue, 0);
            DebugDraw.AddLine(Sensor.Position, Sensor.Position + Vector3D.Rotate(Sensor.Direction * 100000, MatrixD.CreateFromAxisAngle(Block.WorldMatrix.Left, Sensor.Aperture)), Color.Blue, 0);
            DebugDraw.AddLine(Sensor.Position, Sensor.Position + Vector3D.Rotate(Sensor.Direction * 100000, MatrixD.CreateFromAxisAngle(Block.WorldMatrix.Up, Sensor.Aperture)), Color.Blue, 0);
            DebugDraw.AddLine(Sensor.Position, Sensor.Position + Vector3D.Rotate(Sensor.Direction * 100000, MatrixD.CreateFromAxisAngle(Block.WorldMatrix.Down, Sensor.Aperture)), Color.Blue, 0);

            foreach (var track in ServerMain.I.Tracks.Values)
            {
                GridTrack gT = track as GridTrack;
                if (gT?.Grid?.GetTopMostParent() == Block.CubeGrid.GetTopMostParent())
                    continue;

                if (gT != null)
                {
                    double rcs, vcs;
                    gT.CalculateRcs(Vector3D.Normalize(gT.Grid.WorldAABB.Center - MyAPIGateway.Session.Camera.Position), out rcs, out vcs);

                    var a = Sensor.GetDetectionInfo(gT, Sensor is RadarSensor ? rcs : vcs);
                    if (a == null)
                        continue;
                    var info = a.Value;

                    var gps = MyAPIGateway.Session.GPS.Create("", "", info.Bearing * info.Range + Sensor.Position, true, true);
                    gps.GPSColor = Color.Ivory;
                    gps.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(1);

                    MyAPIGateway.Session.GPS.AddLocalGps(gps);
                    //MyAPIGateway.Utilities.ShowMessage("", info.ToString());
                }
                else
                {
                    var a = Sensor.GetDetectionInfo(track);
                    if (a != null)
                    {
                        var info = a.Value;

                        var gps = MyAPIGateway.Session.GPS.Create("", "", info.Bearing * info.Range + Sensor.Position, true, true);
                        gps.GPSColor = Color.Ivory;
                        gps.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(1);

                        MyAPIGateway.Session.GPS.AddLocalGps(gps);
                        //MyAPIGateway.Utilities.ShowMessage("", info.ToString());
                    }
                }
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

        public override void Update()
        {
            base.Update();
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
