using System;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Server.PBApi;
using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System.Collections.Generic;
using DetectionEquipment.Server.Countermeasures;
using DetectionEquipment.Shared.ExternalApis;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace DetectionEquipment.Server
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    // ReSharper disable once ClassNeverInstantiated.Global
    internal class ServerMain : MySessionComponentBase
    {
        public static ServerMain I;

        public Dictionary<IMyEntity, ITrack> Tracks = new Dictionary<IMyEntity, ITrack>();
        public Dictionary<IMyCubeGrid, GridSensorManager> GridSensorMangers = new Dictionary<IMyCubeGrid, GridSensorManager>();

        public Dictionary<uint, ISensor> SensorIdMap = new Dictionary<uint, ISensor>();
        public Dictionary<uint, BlockSensor> BlockSensorIdMap = new Dictionary<uint, BlockSensor>();
        public Action<IMyCubeBlock> OnBlockPlaced = null;
        public uint HighestSensorId = 0;

        private bool _doneDelayedInit = false;

        public override void LoadData()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            try
            {
                Log.Info("ServerMain", "Start initialize...");
                Log.IncreaseIndent();

                I = this;

                new ServerNetwork().LoadData();
                CountermeasureManager.Init();

                {
                    MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
                    MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;

                    int ct = 0;
                    MyAPIGateway.Entities.GetEntities(null, e =>
                    {
                        OnEntityAdd(e);
                        ct++;
                        return false;
                    });
                    Log.Info("ServerMain", $"{ct} entities pre-registered.");
                }

                Log.DecreaseIndent();
                Log.Info("ServerMain", "Initialized.");
            }
            catch (Exception ex)
            {
                Log.Exception("ServerMain", ex, true);
            }
        }

        public void DelayedInit()
        {
            PbApiInitializer.Init();
            if (ApiManager.WcApi.IsReady)
                OnWcApiReady();
            else
                ApiManager.WcApi.ReadyCallback += OnWcApiReady;

            _doneDelayedInit = true;
        }

        protected override void UnloadData()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            try
            {
                Log.Info("ServerMain", "Start unload...");
                Log.IncreaseIndent();

                CountermeasureManager.Close();
                ServerNetwork.I.UnloadData();

                PbApiInitializer.Unload();

                MyAPIGateway.Entities.OnEntityAdd -= OnEntityAdd;
                MyAPIGateway.Entities.OnEntityRemove -= OnEntityRemove;

                foreach (var manager in GridSensorMangers.Values)
                    manager.Close();

                I = null;
                Log.DecreaseIndent();
                Log.Info("ServerMain", "Unloaded.");
            }
            catch (Exception ex)
            {
                Log.Exception("ServerMain", ex, true);
            }
        }


        private List<IMyEntity> _deadTracks = new List<IMyEntity>();
        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            try
            {
                if (!_doneDelayedInit)
                    DelayedInit();

                // Safety check for closed tracks that didn't notify
                {
                    foreach (var ent in Tracks.Keys)
                        if (ent.Closed)
                            _deadTracks.Add(ent);
                    foreach (var ent in _deadTracks)
                        Tracks.Remove(ent);
                    _deadTracks.Clear();
                }

                foreach (var manager in GridSensorMangers.Values)
                    manager.Update();

                CountermeasureManager.Update();

                ServerNetwork.I.Update();
            }
            catch (Exception ex)
            {
                Log.Exception("ServerMain", ex);
            }
        }

        private void OnWcApiReady()
        {
            ApiManager.WcApi.AddScanTargetsAction(GridSensorManager.ScanTargetsAction);
        }

        private void OnEntityAdd(IMyEntity obj)
        {
            try
            {
                if (obj.Physics == null)
                    return;

                var grid = obj as IMyCubeGrid;
                if (grid != null)
                {
                    Tracks.Add(obj, new GridTrack(grid));
                    GridSensorMangers.Add(grid, new GridSensorManager(grid));
                    grid.OnBlockAdded += InvokeOnBlockPlaced;
                    grid.GetBlocks(null, b =>
                    {
                        InvokeOnBlockPlaced(b);
                        return false;
                    });
                }
                else
                    Tracks.Add(obj, new EntityTrack((MyEntity)obj));
            }
            catch (Exception ex)
            {
                Log.Exception("ServerMain", ex, true);
            }
        }

        private void OnEntityRemove(IMyEntity obj)
        {
            try
            {
                if (obj.Physics == null)
                    return;

                Tracks.Remove(obj);

                var grid = obj as IMyCubeGrid;
                if (grid != null)
                {
                    grid.OnBlockAdded -= InvokeOnBlockPlaced;
                    GridSensorMangers[grid].Close();
                    GridSensorMangers.Remove(grid);
                }
            }
            catch (Exception ex)
            {
                Log.Exception("ServerMain", ex, true);
            }
        }

        private void InvokeOnBlockPlaced(IMySlimBlock block)
        {
            try
            {
                if (block.FatBlock != null)
                    OnBlockPlaced?.Invoke(block.FatBlock);
            }
            catch (Exception ex)
            {
                Log.Exception("ServerMain", ex, true);
            }
        }
    }
}
