using DetectionEquipment.Client.Networking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.BlockLogic;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Entity;
using VRageMath;

namespace DetectionEquipment.Client.Sensors
{
    internal class ClientBlockSensor : ControlBlockBase<IMyCameraBlock>
    {
        public readonly Dictionary<uint, ClientSensorData> Sensors = new Dictionary<uint, ClientSensorData>();

        public uint CurrentSensorId = uint.MaxValue;
        public float CurrentAperture
        {
            get
            {
                return Sensors[CurrentSensorId].Aperture;
            }
            set
            {
                Sensors[CurrentSensorId].Aperture = value;
                ClientNetwork.SendToServer(new SensorUpdatePacket(Sensors[CurrentSensorId]));
            }
        }
        public float CurrentDesiredAzimuth
        {
            get
            {
                return Sensors[CurrentSensorId].DesiredAzimuth;
            }
            set
            {
                Sensors[CurrentSensorId].DesiredAzimuth = value;
                ClientNetwork.SendToServer(new SensorUpdatePacket(Sensors[CurrentSensorId]));
            }
        }
        public float CurrentDesiredElevation
        {
            get
            {
                return Sensors[CurrentSensorId].DesiredElevation;
            }
            set
            {
                Sensors[CurrentSensorId].DesiredElevation = value;
                ClientNetwork.SendToServer(new SensorUpdatePacket(Sensors[CurrentSensorId]));
            }
        }
        public SensorDefinition CurrentDefinition
        {
            get
            {
                if (CurrentSensorId == uint.MaxValue)
                    throw new Exception("CurrentSensorId is invalid - have any sensors been inited?");
                return Sensors[CurrentSensorId].Definition;
            }
        }

        public ClientBlockSensor(IMyCameraBlock block)
        {
            block.GameLogic.Container.Add(this);
            Block = block;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            if (Sensors.Count == 0)
            {
                ClientNetwork.SendToServer(new SensorInitPacket(Block.EntityId));
            }

            new SensorControls().DoOnce(this);
        }

        public override void UpdateAfterSimulation()
        {
            if (!Block.IsWorking)
                return;
            foreach (var sensor in Sensors.Values)
            {
                sensor.Update(Block, sensor.Id == CurrentSensorId);
            }
        }

        public void RegisterSensor(SensorInitPacket packet)
        {
            Sensors[packet.Id] = new ClientSensorData()
            {
                Id = packet.Id,
                Definition = DefinitionManager.GetDefinition(packet.DefinitionId),
            };
            if (CurrentSensorId == uint.MaxValue)
                CurrentSensorId = packet.Id;
            SensorBlockManager.BlockSensorIdMap[packet.Id] = this;
        }

        public void UpdateFromPacket(SensorUpdatePacket packet)
        {
            var data = Sensors[packet.Id];
            data.Aperture = packet.Aperture;
            data.DesiredAzimuth = packet.Azimuth;
            data.DesiredElevation = packet.Elevation;
        }

        public class ClientSensorData
        {
            public uint Id;
            public SensorDefinition Definition;
            public float Aperture = 0;
            public float DesiredAzimuth = 0;
            public float DesiredElevation = 0;

            public float Azimuth  { get; private set; } = 0;
            public float Elevation  { get; private set; } = 0;
            public Vector3D Position { get; private set; } = Vector3D.Zero;
            public Vector3D Direction { get; private set; } = Vector3D.Forward;

            private MyEntitySubpart _aziPart = null, _elevPart = null;
            private Matrix _baseLocalMatrix;
            private SubpartManager SubpartManager = new SubpartManager();

            public void Update(IMyCameraBlock block, bool isPrimarySensor)
            {
                if (_aziPart == null && _elevPart == null && Definition.Movement != null)
                {
                    _aziPart = SubpartManager.RecursiveGetSubpart(block, Definition.Movement.AzimuthPart);
                    _elevPart = SubpartManager.RecursiveGetSubpart(block, Definition.Movement.ElevationPart);
                    _baseLocalMatrix = block.LocalMatrix;
                    Log.Info("ClientBlockSensor", "Inited subparts for " + block.BlockDefinition.SubtypeName);
                }

                // Sensor Movement
                if (_aziPart != null && Azimuth != DesiredAzimuth)
                    if (!MyAPIGateway.Session.IsServer) // Server rotates parts too
                        SubpartManager.LocalRotateSubpartAbs(_aziPart, GetAzimuthMatrix(1/60f));
                if (_elevPart != null)
                {
                    if (Elevation != DesiredElevation)
                        if (!MyAPIGateway.Session.IsServer) // Server rotates parts too
                            SubpartManager.LocalRotateSubpartAbs(_elevPart, GetElevationMatrix(1/60f));
                    Position = _elevPart.WorldMatrix.Translation;
                    Direction = _elevPart.WorldMatrix.Forward;

                    // Hide/show & rotate block based on whether a player is in the camera. TODO: This doesn't quite work.
                    if (isPrimarySensor)
                    {
                        if (block.IsActive)
                        {
                            block.Visible = false;
                            block.LocalMatrix = MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * _baseLocalMatrix;

                            Direction = block.WorldMatrix.Forward;
                        }
                        else if (!block.Visible)
                        {
                            block.Visible = true;
                            block.LocalMatrix = _baseLocalMatrix;
                        }
                    }
                }
                else
                {
                    Position = block.WorldAABB.Center;
                    Direction = (MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * block.WorldMatrix).Forward;
                }

                // HUD
                if (block.ShowOnHUD)
                {
                    var color = new Color(0, 0, 255, 100);
                    var matrix = MatrixD.CreateWorld(Position, Direction, Vector3D.CalculatePerpendicularVector(Direction));

                    if (Aperture < Math.PI)
                        MySimpleObjectDraw.DrawTransparentCone(ref matrix, (float) Math.Tan(Aperture) * MyAPIGateway.Session.SessionSettings.SyncDistance, MyAPIGateway.Session.SessionSettings.SyncDistance, ref color, 8, DebugDraw.MaterialSquare);
                }
            }

            private Matrix GetAzimuthMatrix(float delta)
            {
                var _limitedAzimuth = MathUtils.LimitRotationSpeed(Azimuth, DesiredAzimuth, Definition.Movement.AzimuthRate * delta);

                if (!Definition.Movement.CanElevateFull)
                    Azimuth = (float) MathUtils.Clamp(_limitedAzimuth, Definition.Movement.MinAzimuth, Definition.Movement.MaxAzimuth);
                else
                    Azimuth = (float) MathUtils.NormalizeAngle(_limitedAzimuth);

                return Matrix.CreateFromYawPitchRoll(Azimuth, 0, 0);
            }

            private Matrix GetElevationMatrix(float delta)
            {
                var _limitedElevation = MathUtils.LimitRotationSpeed(Elevation, DesiredElevation, Definition.Movement.ElevationRate * delta);

                if (!Definition.Movement.CanElevateFull)
                    Elevation = (float) MathUtils.Clamp(_limitedElevation, Definition.Movement.MinElevation, Definition.Movement.MaxElevation);
                else
                    Elevation = (float) MathUtils.NormalizeAngle(_limitedElevation);

                return Matrix.CreateFromYawPitchRoll(0, Elevation, 0);
            }
        }
    }
}
