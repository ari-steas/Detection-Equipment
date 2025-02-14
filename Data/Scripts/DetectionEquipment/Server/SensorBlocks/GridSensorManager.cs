using DetectionEquipment.Server.Sensors;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Server.SensorBlocks
{
    internal class GridSensorManager
    {
        public readonly IMyCubeGrid Grid;
        public HashSet<BlockSensor> Sensors = new HashSet<BlockSensor>();

        public GridSensorManager(IMyCubeGrid grid)
        {
            Grid = grid;
            Grid.OnBlockAdded += OnBlockAdded;
            Grid.OnBlockRemoved += OnBlockRemoved;

            grid.GetBlocks(null, b =>
            {
                OnBlockAdded(b);
                return false;
            });
        }

        
        public void Update()
        {
            MyAPIGateway.Utilities.ShowNotification($"{Sensors.Count} sensors", 1000/60);
            foreach (var sensor in Sensors)
                sensor.Update();
        }

        public void Close()
        {
            Grid.OnBlockAdded -= OnBlockAdded;
            Grid.OnBlockRemoved -= OnBlockRemoved;

            foreach (var sensor in Sensors)
                sensor.Close();
        }


        private void OnBlockAdded(IMySlimBlock obj)
        {
            var cubeBlock = obj.FatBlock;
            if (cubeBlock == null) return;

            if (cubeBlock is IMyCameraBlock)
            {
                var sensor = new VisualSensor(false)
                {
                    Aperture = MathHelper.ToRadians(45),
                };
                Sensors.Add(new BlockSensor<VisualSensor>(sensor, cubeBlock));
            }
        }

        private void OnBlockRemoved(IMySlimBlock obj)
        {
            var cubeBlock = obj.FatBlock;
            if (cubeBlock != null)
                Sensors.RemoveWhere(sensor => sensor.Block == cubeBlock);
        }
    }
}
