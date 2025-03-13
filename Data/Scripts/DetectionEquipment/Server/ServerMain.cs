using DetectionEquipment.Server.PBApi;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DetectionEquipment.Server
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class ServerMain : MySessionComponentBase
    {
        public static ServerMain I;

        public Dictionary<IMyEntity, ITrack> Tracks = new Dictionary<IMyEntity, ITrack>();
        public Dictionary<IMyCubeGrid, GridSensorManager> GridSensorMangers = new Dictionary<IMyCubeGrid, GridSensorManager>();

        public Dictionary<uint, ISensor> SensorIdMap = new Dictionary<uint, ISensor>();
        public Dictionary<uint, BlockSensor> BlockSensorIdMap = new Dictionary<uint, BlockSensor>();
        public uint HighestSensorId = 0;
        
        private bool _doneTickInit = false;

        public override void LoadData()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            I = this;

            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;

            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                OnEntityAdd(e);
                return false;
            });

            Log.Info("ServerMain", "Initialized.");
        }

        protected override void UnloadData()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            PbApiInitializer.Unload();

            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemove;

            foreach (var manager in GridSensorMangers.Values)
                manager.Close();

            I = null;
            Log.Info("ServerMain", "Unloaded.");
        }



        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            if (!_doneTickInit)
            {
                PbApiInitializer.Init();
                _doneTickInit = true;
            }

            foreach (var manager in GridSensorMangers.Values)
                manager.Update();
        }

        private void OnEntityAdd(IMyEntity obj)
        {
            if (obj.Physics == null)
                return;

            var grid = obj as IMyCubeGrid;
            if (grid != null)
            {
                Tracks.Add(obj, new GridTrack(grid));
                GridSensorMangers.Add(grid, new GridSensorManager(grid));
            }
            else
                Tracks.Add(obj, new EntityTrack((MyEntity)obj));
        }

        private void OnEntityRemove(IMyEntity obj)
        {
            if (obj.Physics == null)
                return;

            Tracks.Remove(obj);

            var grid = obj as IMyCubeGrid;
            if (grid != null)
            {
                GridSensorMangers[grid].Close();
                GridSensorMangers.Remove(grid);
            }
        }
    }
}
