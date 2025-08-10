using DetectionEquipment.Client.Networking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using DetectionEquipment.Shared.BlockLogic;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI.Interfaces.Terminal;
using VRage.Game.ModAPI;
using VRageMath;
using static DetectionEquipment.Client.BlockLogic.Sensors.SensorUpdatePacket;

namespace DetectionEquipment.Client.BlockLogic.Sensors
{
    internal class ClientSensorLogic : IBlockLogic
    {
        public readonly Dictionary<uint, ClientSensorData> Sensors = new Dictionary<uint, ClientSensorData>();
        public IMyTerminalBlock Block { get; set; }
        public bool IsClosed { get; set; }

        public uint CurrentSensorId = uint.MaxValue;
        public float CurrentAperture
        {
            get
            {
                return Sensors[CurrentSensorId].Aperture;
            }
            set
            {
                if (Sensors[CurrentSensorId].Aperture == value)
                    return;
                Sensors[CurrentSensorId].Aperture = value;
                ClientNetwork.SendToServer(new SensorUpdatePacket(Block.EntityId, Sensors[CurrentSensorId], FieldId.Aperture));
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
                if (Sensors[CurrentSensorId].DesiredAzimuth == value)
                    return;
                Sensors[CurrentSensorId].DesiredAzimuth = value;
                ClientNetwork.SendToServer(new SensorUpdatePacket(Block.EntityId, Sensors[CurrentSensorId], FieldId.Azimuth));
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
                if (Sensors[CurrentSensorId].DesiredElevation == value)
                    return;
                Sensors[CurrentSensorId].DesiredElevation = value;
                ClientNetwork.SendToServer(new SensorUpdatePacket(Block.EntityId, Sensors[CurrentSensorId], FieldId.Elevation));
            }
        }
        public float CurrentMinAzimuth
        {
            get
            {
                return Sensors[CurrentSensorId].MinAzimuth;
            }
            set
            {
                if (Sensors[CurrentSensorId].MinAzimuth == value)
                    return;
                Sensors[CurrentSensorId].MinAzimuth = value;
                ClientNetwork.SendToServer(new SensorUpdatePacket(Block.EntityId, Sensors[CurrentSensorId], FieldId.MinAzimuth));
            }
        }
        public float CurrentMaxAzimuth
        {
            get
            {
                return Sensors[CurrentSensorId].MaxAzimuth;
            }
            set
            {
                if (Sensors[CurrentSensorId].MaxAzimuth == value)
                    return;
                Sensors[CurrentSensorId].MaxAzimuth = value;
                ClientNetwork.SendToServer(new SensorUpdatePacket(Block.EntityId, Sensors[CurrentSensorId], FieldId.MaxAzimuth));
            }
        }
        public float CurrentMinElevation
        {
            get
            {
                return Sensors[CurrentSensorId].MinElevation;
            }
            set
            {
                if (Sensors[CurrentSensorId].MinElevation == value)
                    return;
                Sensors[CurrentSensorId].MinElevation = value;
                ClientNetwork.SendToServer(new SensorUpdatePacket(Block.EntityId, Sensors[CurrentSensorId], FieldId.MinElevation));
            }
        }
        public float CurrentMaxElevation
        {
            get
            {
                return Sensors[CurrentSensorId].MaxElevation;
            }
            set
            {
                if (Sensors[CurrentSensorId].MaxElevation == value)
                    return;
                Sensors[CurrentSensorId].MaxElevation = value;
                ClientNetwork.SendToServer(new SensorUpdatePacket(Block.EntityId, Sensors[CurrentSensorId], FieldId.MaxElevation));
            }
        }

        public bool CurrentAllowMechanicalControl
        {
            get
            {
                return Sensors[CurrentSensorId].AllowMechanicalControl;
            }
            set
            {
                if (Sensors[CurrentSensorId].AllowMechanicalControl == value)
                    return;
                Sensors[CurrentSensorId].AllowMechanicalControl = value;
                ClientNetwork.SendToServer(new SensorUpdatePacket(Block.EntityId, Sensors[CurrentSensorId], FieldId.AllowMechanicalControl));
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

        private List<uint> _sensorIds = null;
        private List<int> _definitionIds = null;
        public ClientSensorLogic(List<uint> sensorIds, List<int> definitionIds)
        {
            _sensorIds = sensorIds;
            _definitionIds = definitionIds;
        }

        public void Register(IMyTerminalBlock block)
        {
            Block = block;
            if (SensorBlockManager.SensorBlocks.ContainsKey(Block.CubeGrid)) 
                SensorBlockManager.SensorBlocks[Block.CubeGrid].Add(Block);
            else
                SensorBlockManager.SensorBlocks[Block.CubeGrid] = new HashSet<IMyCubeBlock> { Block };

            OnCustomDataChanged(Block);
            Block.CustomDataChanged += OnCustomDataChanged;

            // can't add terminal controls to imyfunctional/imyterminalblock so this is the least messy workaround
            if (block is IMyCameraBlock)
                new SensorControls<IMyCameraBlock>().DoOnce();
            if (block is IMyRadioAntenna)
                new SensorControls<IMyRadioAntenna>().DoOnce();

            for (int i = 0; i < _sensorIds.Count; i++)
                RegisterSensor(_sensorIds[i], _definitionIds[i]);
            _sensorIds = null;
            _definitionIds = null;

            if (!MyAPIGateway.Session.IsServer && Block is IMyCameraBlock)
            {
                var resourceSink = (MyResourceSinkComponent)Block.ResourceSink;
                resourceSink.SetRequiredInputFuncByType(GlobalData.ElectricityId, () =>
                {
                    if (!((IMyFunctionalBlock)Block).Enabled || !((IMyFunctionalBlock)Block).IsFunctional)
                        return 0;

                    float totalDraw = 0;
                    foreach (var sensor in Sensors.Values)
                        totalDraw += (float) sensor.Definition.MaxPowerDraw;
                    Block.ResourceSink.SetMaxRequiredInputByType(GlobalData.ElectricityId, totalDraw / 1000000);
                    return totalDraw / 1000000;
                });
                
                resourceSink.Update();
                ((IMyFunctionalBlock)Block).EnabledChanged += b => resourceSink.Update();
            }
        }

        public void Close()
        {
            foreach (var sensorId in Sensors.Keys)
                SensorBlockManager.BlockSensorIdMap.Remove(sensorId);

            if (SensorBlockManager.SensorBlocks.ContainsKey(Block.CubeGrid))
            {
                SensorBlockManager.SensorBlocks[Block.CubeGrid].Remove(Block);
                if (SensorBlockManager.SensorBlocks[Block.CubeGrid].Count == 0)
                    SensorBlockManager.SensorBlocks.Remove(Block.CubeGrid);
            }
        }

        public void UpdateAfterSimulation()
        {
            if (!(Block.IsWorking || (Block.IsFunctional && ((IMyFunctionalBlock)Block).Enabled && CurrentDefinition.MaxPowerDraw <= 0)))
                return;
            foreach (var sensor in Sensors.Values)
            {
                sensor.Update(sensor.Id == CurrentSensorId);
            }
        }

        public void RegisterSensor(uint sensorId, int definitionId)
        {
            Sensors[sensorId] = new ClientSensorData(
                sensorId,
                DefinitionManager.GetSensorDefinition(definitionId),
                (IMyFunctionalBlock) Block,
                _colorSet.Count > 0 ? (Color?)_colorSet.Dequeue() : null
            );
            if (CurrentSensorId == uint.MaxValue)
                CurrentSensorId = sensorId;
            SensorBlockManager.BlockSensorIdMap[sensorId] = this;
        }

        public void UpdateFromNetwork(BlockLogicUpdatePacket updateData)
        {
            var packet = (SensorUpdatePacket) updateData;
            if (!Sensors.ContainsKey(packet.Id))
                return;

            var data = Sensors[packet.Id];
            packet.SetField(FieldId.Aperture, ref data.Aperture);
            packet.SetField(FieldId.Azimuth, ref data._desiredAzimuth);
            packet.SetField(FieldId.Elevation, ref data._desiredElevation);
            packet.SetField(FieldId.MinAzimuth, ref data._minAzimuth);
            packet.SetField(FieldId.MaxAzimuth, ref data._maxAzimuth);
            packet.SetField(FieldId.MinElevation, ref data._minElevation);
            packet.SetField(FieldId.MaxElevation, ref data._maxElevation);
            packet.SetField(FieldId.AllowMechanicalControl, ref data.AllowMechanicalControl);

            if (packet.Id == CurrentSensorId && MyAPIGateway.Gui.GetCurrentScreen == MyTerminalPageEnum.ControlPanel && Block.EntityId == ControlBlockManager.I.TerminalSelectedBlock)
            {
                List<IMyTerminalControl> controls;
                MyAPIGateway.TerminalControls.GetControls<IMyFunctionalBlock>(out controls);
                foreach (var control in controls)
                    control.UpdateVisual();
            }


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
    }
}
