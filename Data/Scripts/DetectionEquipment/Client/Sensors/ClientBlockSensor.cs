using DetectionEquipment.Client.Networking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.BlockLogic.ControlBlocks;
using DetectionEquipment.Shared.BlockLogic.ControlBlocks.GenericControls;
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

        private Queue<Color> _colorSet = new Queue<Color>();

        public ClientBlockSensor(IMyCameraBlock block)
        {
            block.GameLogic.Container.Add(this);
            Block = block;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            if (Sensors.Count == 0)
                ClientNetwork.SendToServer(new SensorInitPacket(Block.EntityId));

            OnCustomDataChanged(Block);
            Block.CustomDataChanged += OnCustomDataChanged;
        }

        public override void UpdateAfterSimulation()
        {
            if (!Block.IsWorking)
                return;
            foreach (var sensor in Sensors.Values)
            {
                sensor.Update(sensor.Id == CurrentSensorId);
            }
        }

        public void RegisterSensor(SensorInitPacket packet)
        {
            Sensors[packet.Id] = new ClientSensorData(
                packet.Id,
                DefinitionManager.GetSensorDefinition(packet.DefinitionId),
                Block,
                _colorSet.Count > 0 ? (Color?)_colorSet.Dequeue() : null
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

        private void OnCustomDataChanged(IMyTerminalBlock obj)
        {
            // TODO custom data change isn't ever invoked lol lmao
            _colorSet.Clear();
            foreach (var line in obj.CustomData.Split('\n'))
            {
                if (!line.StartsWith("<"))
                    continue;
                var split = line.RemoveChars('<', '>').Split(',');

                if (split.Length < 3)
                    continue;
                int r, g, b, a;
                if (!int.TryParse(split[0], out r) || !int.TryParse(split[1], out g) || !int.TryParse(split[2], out b))
                    continue;
                if (split.Length >= 4 && int.TryParse(split[3], out a))
                    _colorSet.Enqueue(new Color(r, g, b, a));
                else
                    _colorSet.Enqueue(new Color(r, g, b, 26));
            }

            if (Sensors.Count > 0)
            {
                foreach (var sensor in Sensors.Values)
                {
                    if (_colorSet.Count == 0)
                        break;
                    sensor.Color = _colorSet.Dequeue();
                }
            }
        }

        public class ClientSensorData
        {
            public readonly uint Id;
            public readonly SensorDefinition Definition;
            public float Aperture = 0;
            public float DesiredAzimuth = 0;
            public float DesiredElevation = 0;

            public float Azimuth  { get; private set; } = 0;
            public float Elevation  { get; private set; } = 0;
            public Vector3D Position { get; private set; } = Vector3D.Zero;
            public Vector3D Direction { get; private set; } = Vector3D.Forward;

            private readonly MyEntitySubpart _aziPart, _elevPart;
            private readonly Matrix _baseLocalMatrix;
            private readonly SubpartManager _subpartManager = new SubpartManager();

            private readonly IMyModelDummy _sensorDummy = null;
            private readonly MyEntity _dummyParent = null;
            private readonly IMyCameraBlock _block;
            public Color Color;

            private MatrixD SensorMatrix => _sensorDummy == null
                ? (_elevPart == null ? (MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * _block.WorldMatrix) : _elevPart.WorldMatrix)
                : SensorDummyMatrix;

            private MatrixD SensorDummyMatrix => (_elevPart == null && Definition.Movement != null)
                ? MatrixD.CreateFromYawPitchRoll(Azimuth, Elevation, 0) * _sensorDummy.Matrix * _dummyParent.WorldMatrix
                : _sensorDummy.Matrix * _dummyParent.WorldMatrix;

            public ClientSensorData(uint id, SensorDefinition definition, IMyCameraBlock block, Color? color)
            {
                Id = id;
                Definition = definition;
                _block = block;

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

                Color = color ?? new Color((uint) ((50 + Id) * block.EntityId)).Alpha(0.1f);
            }

            public void Update(bool isPrimarySensor)
            {
                UpdateSensorMatrix();
                if (isPrimarySensor)
                    UpdateCameraView();

                // HUD
                if (_block.ShowOnHUD)
                {
                    var matrix = SensorMatrix;
                    if (Aperture < Math.PI)
                        MySimpleObjectDraw.DrawTransparentCone(ref matrix, (float) Math.Tan(Aperture) * MyAPIGateway.Session.SessionSettings.SyncDistance, MyAPIGateway.Session.SessionSettings.SyncDistance, ref Color, 8, DebugDraw.MaterialSquare);
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
                if (MyAPIGateway.Session.IsServer)
                    return;

                // Hide/show & rotate block based on whether a player is in the camera. TODO: This doesn't quite work.
                if (_block.IsActive)
                {
                    _block.Visible = false;

                    _block.LocalMatrix = SensorMatrix * MatrixD.Invert(_block.CubeGrid.WorldMatrix);
                }
                else if (!_block.Visible)
                {
                    _block.Visible = true;
                    _block.LocalMatrix = _baseLocalMatrix;
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
