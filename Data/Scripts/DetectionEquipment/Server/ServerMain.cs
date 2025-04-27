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
using Sandbox.Definitions;
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

        public string[] LowRcsSubtypes = Array.Empty<string>();
        
        private bool _doneTickInit = false;

        public override void LoadData()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;
            Log.Info("ServerMain", "Start initialize...");
            Log.IncreaseIndent();

            I = this;

            new ServerNetwork().LoadData();
            CountermeasureManager.Init();

            {
                var lowRcsBlocksBuffer = new List<string>();
                foreach (var definition in MyDefinitionManager.Static.GetAllDefinitions())
                {
                    MyCubeBlockDefinition block = definition as MyCubeBlockDefinition;
                    if (block == null || !block.DisplayNameText.Contains("Light Armor"))
                        continue;
                    lowRcsBlocksBuffer.Add(block.Id.SubtypeName);
                }
                LowRcsSubtypes = lowRcsBlocksBuffer.ToArray();
                Log.Info("ServerMain", $"{LowRcsSubtypes.Length} low-RCS block definitions found.");
            }

            {
                MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;
                MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;

                MyAPIGateway.Entities.GetEntities(null, e =>
                {
                    OnEntityAdd(e);
                    return false;
                });
                Log.Info("ServerMain", "Entities pre-registered.");
            }

            Log.DecreaseIndent();
            Log.Info("ServerMain", "Initialized.");
        }

        protected override void UnloadData()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;
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


        private List<IMyEntity> _deadTracks = new List<IMyEntity>();
        public override void UpdateAfterSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            if (!_doneTickInit)
            {
                PbApiInitializer.Init();
                _doneTickInit = true;
            }

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

        private void OnEntityAdd(IMyEntity obj)
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

        private void OnEntityRemove(IMyEntity obj)
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

        private void InvokeOnBlockPlaced(IMySlimBlock block)
        {
            if (block.FatBlock != null)
                OnBlockPlaced?.Invoke(block.FatBlock);
        }
    }
}
