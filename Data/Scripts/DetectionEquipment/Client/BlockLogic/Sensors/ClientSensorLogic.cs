using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRageMath;
using DetectionEquipment.Client.Networking;

namespace DetectionEquipment.Client.BlockLogic.Sensors
{
    internal class ClientSensorLogic : IBlockLogic
    {
        public readonly Dictionary<uint, ClientSensorData> Sensors = new Dictionary<uint, ClientSensorData>();
        public IMyCubeBlock Block { get; set; }
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
                Sensors[CurrentSensorId].Aperture = value;
                ClientNetwork.SendToServer(new SensorUpdatePacket(Block.EntityId, Sensors[CurrentSensorId]));
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
                ClientNetwork.SendToServer(new SensorUpdatePacket(Block.EntityId, Sensors[CurrentSensorId]));
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
                ClientNetwork.SendToServer(new SensorUpdatePacket(Block.EntityId, Sensors[CurrentSensorId]));
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

        public void Register(IMyCubeBlock block)
        {
            Block = block;
            if (SensorBlockManager.SensorBlocks.ContainsKey(Block.CubeGrid))
                SensorBlockManager.SensorBlocks[Block.CubeGrid].Add(Block);
            else
                SensorBlockManager.SensorBlocks[Block.CubeGrid] = new List<IMyCubeBlock> { Block };

            OnCustomDataChanged((IMyTerminalBlock) Block);
            ((IMyTerminalBlock)Block).CustomDataChanged += OnCustomDataChanged;

            new SensorControls().DoOnce();

            for (int i = 0; i < _sensorIds.Count; i++)
                RegisterSensor(_sensorIds[i], _definitionIds[i]);
            _sensorIds = null;
            _definitionIds = null;
        }

        public void Close()
        {
            foreach (var sensorId in Sensors.Keys)
                SensorBlockManager.BlockSensorIdMap.Remove(sensorId);

            SensorBlockManager.SensorBlocks[Block.CubeGrid].Remove(Block);
            if (SensorBlockManager.SensorBlocks[Block.CubeGrid].Count == 0)
                SensorBlockManager.SensorBlocks.Remove(Block.CubeGrid);
        }

        public void UpdateAfterSimulation()
        {
            if (!Block.IsWorking)
                return;
            foreach (var sensor in Sensors.Values)
            {
                sensor.Update(sensor.Id == CurrentSensorId);
            }
        }

        public void RegisterSensor(uint sensorId, int definitionId)
        {
            Log.Info("ClientSensorLogic", $"Registering sensor {sensorId}...");
            Sensors[sensorId] = new ClientSensorData(
                sensorId,
                DefinitionManager.GetSensorDefinition(definitionId),
                (IMyCameraBlock) Block,
                _colorSet.Count > 0 ? (Color?)_colorSet.Dequeue() : null
            );
            if (CurrentSensorId == uint.MaxValue)
                CurrentSensorId = sensorId;
            SensorBlockManager.BlockSensorIdMap[sensorId] = this;
        }

        public void UpdateFromNetwork(BlockLogicUpdatePacket updateData)
        {
            var packet = (SensorUpdatePacket) updateData;

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
    }
}
