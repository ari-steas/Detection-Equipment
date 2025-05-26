using DetectionEquipment.Client.Networking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.BlockLogic;
using DetectionEquipment.Shared.BlockLogic.GenericControls;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace DetectionEquipment.Client.Sensors
{
    internal class ClientBlockSensor : ControlBlockBase<IMyCameraBlock>
    {
        public readonly Dictionary<uint, ClientSensorData> Sensors = new Dictionary<uint, ClientSensorData>();
        protected override ControlBlockSettingsBase GetSettings => null; // A seperate system is used for syncing sensor data.
        protected override ITerminalControlAdder GetControls => new SensorControls();

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
            Sensors[packet.Id] = new ClientSensorData(
                packet.Id,
                DefinitionManager.GetSensorDefinition(packet.DefinitionId),
                Block
            );
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

            private MyEntitySubpart _aziPart, _elevPart;
            private Matrix _baseLocalMatrix;
            private Matrix _baseMuzzleLocalMatrix = Matrix.Identity;
            private SubpartManager _subpartManager = new SubpartManager();

            private IMyModelDummy _sensorDummy = null;
            private MyEntity _dummyParent = null;

            public ClientSensorData(uint id, SensorDefinition definition, IMyCubeBlock block)
            {
                Id = id;
                Definition = definition;

                if (Definition.Movement != null)
                {
                    _aziPart = _subpartManager.RecursiveGetSubpart(block, Definition.Movement.AzimuthPart);
                    _elevPart = _subpartManager.RecursiveGetSubpart(block, Definition.Movement.ElevationPart);
                    _baseLocalMatrix = block.LocalMatrix;

                    if (_aziPart == null)
                        Log.Info("ClientSensorData", $"Failed to get sensor w/ DefId {Definition.Id} azimuth part {Definition.Movement.AzimuthPart}!\n" +
                                                     $"Valid subparts:\n\t{string.Join("\n\t", SubpartManager.GetAllSubpartsDict(block).Keys)}");
                    if (_elevPart == null)
                        Log.Info("ClientSensorData", $"Failed to get sensor w/ DefId {Definition.Id} elevation part {Definition.Movement.AzimuthPart}!\n" +
                                                     $"Valid subparts:\n\t{string.Join("\n\t", SubpartManager.GetAllSubpartsDict(block).Keys)}");
                    //Log.Info("ClientBlockSensor", "Inited subparts for " + block.BlockDefinition.SubtypeName);
                }

                _dummyParent = (MyEntity) block;
                if (!string.IsNullOrEmpty(Definition.SensorEmpty))
                    _sensorDummy = SubpartManager.RecursiveGetDummy(block, Definition.SensorEmpty, out _dummyParent);

                if (_sensorDummy != null)
                {
                    _baseMuzzleLocalMatrix = _sensorDummy.Matrix;
                    var next = _dummyParent;
                    while (next != block)
                    {
                        _baseMuzzleLocalMatrix *= next.PositionComp.LocalMatrixRef;
                        next = next.Parent;
                    }
                }
            }

            public void Update(IMyCameraBlock block, bool isPrimarySensor)
            {
                // Sensor Movement
                if (!MyAPIGateway.Session.IsServer) // Server already rotates parts, don't interfere with that
                {
                    if (_aziPart != null && Azimuth != DesiredAzimuth)
                        _subpartManager.LocalRotateSubpartAbs(_aziPart, GetAzimuthMatrix(1/60f));
                    if (_elevPart != null && Elevation != DesiredElevation)
                        _subpartManager.LocalRotateSubpartAbs(_elevPart, GetElevationMatrix(1/60f));
                }

                UpdateSensorMatrix(block);
                if (isPrimarySensor)
                    UpdateCameraView(block);

                // HUD
                if (block.ShowOnHUD)
                {
                    var color = new Color((uint) ((50 + Id) * block.EntityId)).Alpha(0.1f);
                    var matrix = MatrixD.CreateWorld(Position, Direction, Vector3D.CalculatePerpendicularVector(Direction));

                    if (Aperture < Math.PI)
                        MySimpleObjectDraw.DrawTransparentCone(ref matrix, (float) Math.Tan(Aperture) * MyAPIGateway.Session.SessionSettings.SyncDistance, MyAPIGateway.Session.SessionSettings.SyncDistance, ref color, 8, DebugDraw.MaterialSquare);
                }
            }

            private void UpdateSensorMatrix(IMyCameraBlock block)
            {
                if (_sensorDummy != null)
                {
                    var sensorMatrix = _sensorDummy.Matrix * _dummyParent.WorldMatrix;
                    Position = sensorMatrix.Translation;
                    Direction = sensorMatrix.Forward;
                }
                else if (_elevPart != null)
                {
                    Position = _elevPart.WorldMatrix.Translation;
                    Direction = _elevPart.WorldMatrix.Forward;
                }
                else
                {
                    Position = block.WorldAABB.Center;
                    Direction = (MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * block.WorldMatrix).Forward;
                }
            }

            private void UpdateCameraView(IMyCameraBlock block)
            {
                // Hide/show & rotate block based on whether a player is in the camera. TODO: This doesn't quite work.
                if (block.IsActive)
                {
                    block.Visible = false;

                    if (_sensorDummy != null)
                    {
                        var muzzleLocalMatrix = _sensorDummy.Matrix;
                        var next = _dummyParent;
                        while (next != block)
                        {
                            muzzleLocalMatrix *= next.PositionComp.LocalMatrixRef;
                            next = next.Parent;
                        }

                        block.LocalMatrix = muzzleLocalMatrix * _baseLocalMatrix;
                    }
                    else
                    {
                        block.LocalMatrix = Matrix.CreateFromYawPitchRoll((float) Math.PI - Azimuth, Elevation, 0) * _baseLocalMatrix;
                    }
                }
                else if (!block.Visible)
                {
                    block.Visible = true;
                    block.LocalMatrix = _baseLocalMatrix;
                }
            }

            private Matrix GetAzimuthMatrix(float delta)
            {
                var limitedAzimuth = MathUtils.LimitRotationSpeed(Azimuth, DesiredAzimuth, Definition.Movement.AzimuthRate * delta);

                if (!Definition.Movement.CanElevateFull)
                    Azimuth = (float) MathUtils.Clamp(limitedAzimuth, Definition.Movement.MinAzimuth, Definition.Movement.MaxAzimuth);
                else
                    Azimuth = (float) MathUtils.NormalizeAngle(limitedAzimuth);

                return Matrix.CreateFromYawPitchRoll(Azimuth, 0, 0);
            }

            private Matrix GetElevationMatrix(float delta)
            {
                var limitedElevation = MathUtils.LimitRotationSpeed(Elevation, DesiredElevation, Definition.Movement.ElevationRate * delta);

                if (!Definition.Movement.CanElevateFull)
                    Elevation = (float) MathUtils.Clamp(limitedElevation, Definition.Movement.MinElevation, Definition.Movement.MaxElevation);
                else
                    Elevation = (float) MathUtils.NormalizeAngle(limitedElevation);

                return Matrix.CreateFromYawPitchRoll(0, Elevation, 0);
            }
        }
    }
}
