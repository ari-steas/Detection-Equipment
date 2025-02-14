using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Runtime.Remoting;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Server
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    internal class ServerMain : MySessionComponentBase
    {
        public static ServerMain I;

        public Dictionary<IMyEntity, ITrack> Tracks = new Dictionary<IMyEntity, ITrack>();
        public Dictionary<IMyCubeGrid, GridSensorManager> GridSensorMangers = new Dictionary<IMyCubeGrid, GridSensorManager>();

        public PassiveRadarSensor PRSensor = new PassiveRadarSensor(null);

        public override void LoadData()
        {
            I = this;

            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;

            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                OnEntityAdd(e);
                return false;
            });
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemove;

            foreach (var manager in GridSensorMangers.Values)
                manager.Close();
            PRSensor.Close();

            I = null;
        }



        int _ticks = 0;
        public override void UpdateAfterSimulation()
        {
            //if (_ticks++ % 60 != 0)
            //    return;

            foreach (var manager in GridSensorMangers.Values)
                manager.Update();


            foreach (var track in Tracks.Values)
            {
                var b = PRSensor.GetDetectionInfo(track);
                if (b != null)
                {
                    var info = b.Value;

                    var gps = MyAPIGateway.Session.GPS.Create("", "", info.Bearing * info.Range + PRSensor.Position, true, true);
                    gps.GPSColor = Color.Red;
                    gps.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(1);

                    MyAPIGateway.Session.GPS.AddLocalGps(gps);
                    //MyAPIGateway.Utilities.ShowMessage("", info.ToString());
                }
            }
        }

        private void OnEntityAdd(IMyEntity obj)
        {
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
