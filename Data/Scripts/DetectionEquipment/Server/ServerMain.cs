using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
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
        public Dictionary<IMyEntity, ITrack> Tracks = new Dictionary<IMyEntity, ITrack>();
        public VisualSensor TestSensor = new VisualSensor(true);

        public override void LoadData()
        {
            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;
            MyAPIGateway.Utilities.MessageEnteredSender += ChatHandler;

            MyAPIGateway.Entities.GetEntities(null, e =>
            {
                OnEntityAdd(e);
                return false;
            });
        }

        private void ChatHandler(ulong sender, string messageText, ref bool sendToOthers)
        {
            double value;
            if (!double.TryParse(messageText, out value))
                return;

            TestSensor.Aperture = MathHelper.ToRadians(value);
            sendToOthers = false;
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
            MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemove;
        }



        int _ticks = 0;
        public override void UpdateAfterSimulation()
        {
            //if (_ticks++ % 60 != 0)
            //    return;

            foreach (var track in Tracks.Values)
            {
                GridTrack gT = track as GridTrack;
                if (gT != null)
                {
                    double rcs, vcs;
                    gT.CalculateRcs(Vector3D.Normalize(gT.Grid.WorldAABB.Center - MyAPIGateway.Session.Camera.Position), out rcs, out vcs);

                    MyAPIGateway.Utilities.ShowNotification($"VCS: {rcs:N0} m^2", 1000/60);
                    var a = TestSensor.GetDetectionInfo(gT, rcs);
                    if (a == null)
                        continue;
                    var info = a.Value;

                    var gps = MyAPIGateway.Session.GPS.Create("", "", info.Bearing * info.Range + MyAPIGateway.Session.Camera.Position, true, true);
                    gps.GPSColor = Color.Ivory;
                    gps.DiscardAt = MyAPIGateway.Session.ElapsedPlayTime + TimeSpan.FromSeconds(1);

                    MyAPIGateway.Session.GPS.AddLocalGps(gps);
                    MyAPIGateway.Utilities.ShowMessage("", info.ToString());
                }
            }
        }

        private void OnEntityAdd(IMyEntity obj)
        {
            if (obj is IMyCubeGrid)
                Tracks.Add(obj, new GridTrack((IMyCubeGrid)obj));
            else
                Tracks.Add(obj, new EntityTrack((MyEntity)obj));
        }

        private void OnEntityRemove(IMyEntity obj)
        {
            Tracks.Remove(obj);
        }
    }
}
