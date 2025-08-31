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
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.ExternalApis;
using Sandbox.Game.Entities;
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

        public readonly Type[] ValidEntityTypes =
        {
            typeof(IMyCubeGrid),
            typeof(IMyCharacter),
            typeof(MyVoxelMap),
        };

        public Dictionary<IMyEntity, ITrack> Tracks = new Dictionary<IMyEntity, ITrack>();
        private double _gridTrackUpdatesPerTick = 0;
        private double _currentUpdateAccumulator = 0;
        private Queue<GridTrack> _gridTracksToUpdate = new Queue<GridTrack>();
        public Dictionary<IMyCubeGrid, GridSensorManager> GridSensorMangers = new Dictionary<IMyCubeGrid, GridSensorManager>();

        public Dictionary<uint, ISensor> SensorIdMap = new Dictionary<uint, ISensor>();
        public Dictionary<uint, BlockSensor> BlockSensorIdMap = new Dictionary<uint, BlockSensor>();
        public Action<IMyCubeBlock> OnBlockPlaced = null;
        public uint HighestSensorId = 0;

        private bool _doneDelayedInit = false;

        public override void LoadData()
        {
            if (!MyAPIGateway.Session.IsServer || GlobalData.Killswitch)
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
            PbApiInitializer.DelayedInit();
            ApiManager.WcOnLoadRegisterOrInvoke(OnWcApiReady);

            _doneDelayedInit = true;
        }

        protected override void UnloadData()
        {
            if (!MyAPIGateway.Session.IsServer || GlobalData.Killswitch)
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
            if (!MyAPIGateway.Session.IsServer || GlobalData.Killswitch)
                return;

            try
            {
                if (!_doneDelayedInit)
                    DelayedInit();

                // Safety check for closed tracks that didn't notify
                {
                    foreach (var ent in Tracks.Keys)
                    {
                        if (ent.Closed)
                        {
                            _deadTracks.Add(ent);
                        }
                    }
                        
                    foreach (var ent in _deadTracks)
                        Tracks.Remove(ent);
                    _deadTracks.Clear();
                }

                // Update grid tracks
                {
                    if (MyAPIGateway.Session.GameplayFrameCounter % 293 == 0) // only update RCS every 5 seconds or so
                    {
                        foreach (var track in Tracks.Values)
                        {
                            var gT = track as GridTrack;
                            if (gT == null)
                                continue;

                            if (gT.NeedsUpdate && !_gridTracksToUpdate.Contains(gT))
                                _gridTracksToUpdate.Enqueue(gT);
                        }

                        _gridTrackUpdatesPerTick = _gridTracksToUpdate.Count / 293d; // process updates over the next 5 seconds
                    }

                    if (_gridTracksToUpdate.Count > 0)
                        _currentUpdateAccumulator += _gridTrackUpdatesPerTick;
                    else
                        _currentUpdateAccumulator = 0;

                    while (_currentUpdateAccumulator >= 1 && _gridTracksToUpdate.Count > 0)
                    {
                        var t = _gridTracksToUpdate.Dequeue();
                        t.UpdateVisibilityCache();
                        _currentUpdateAccumulator--;
                    }

                    if (GlobalData.DebugLevel >= 4)
                        MyAPIGateway.Utilities.ShowNotification($"GridTrack Update Accumulator: {_currentUpdateAccumulator:N} + {_gridTrackUpdatesPerTick*60:N}", 1000/60);
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
            GlobalData.ContributeWcTargeting.AddOnChanged(AddTargetActions);
        }

        private static void AddTargetActions(bool contributeTargeting)
        {
            ApiManager.WcOnLoadRegisterOrInvoke(() =>
            {
                if (contributeTargeting)
                {
                    ApiManager.WcApi.AddScanTargetsAction(GridSensorManager.ScanTargetsAction);
                    ApiManager.WcApi.SetValidateWeaponTargetFunc(GridSensorManager.ValidateWeaponTarget);
                    Log.Info("ServerMain", "WeaponCore targeting overridden.");
                }
                else
                {
                    ApiManager.WcApi.RemoveScanTargetsAction(GridSensorManager.ScanTargetsAction);
                    ApiManager.WcApi.SetValidateWeaponTargetFunc(null);
                    Log.Info("ServerMain", "WeaponCore targeting override disabled.");
                }
            });
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
                    var gT = new GridTrack(grid);
                    Tracks.Add(obj, gT);
                    _gridTracksToUpdate.Enqueue(gT);
                    GridSensorMangers.Add(grid, new GridSensorManager(grid));
                    grid.OnBlockAdded += InvokeOnBlockPlaced;
                    grid.GetBlocks(null, b =>
                    {
                        InvokeOnBlockPlaced(b);
                        return false;
                    });
                }
                else
                {
                    // Ignore planet voxel maps
                    if (obj is MyVoxelMap && obj.Name == "") return;
                    if (!ValidEntityTypes.Contains(obj.GetType()))
                        return;

                    Tracks.Add(obj, new EntityTrack((MyEntity)obj));
                }
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
