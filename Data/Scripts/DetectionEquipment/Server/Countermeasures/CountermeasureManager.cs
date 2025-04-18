using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game.Weapons;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Server.Countermeasures
{
    internal static class CountermeasureManager
    {
        public static Dictionary<uint, Countermeasure> CountermeasureIdMap;
        private static List<Countermeasure> _deadCountermeasures;
        public static uint HighestCountermeasureId = 0;

        public static Dictionary<uint, CountermeasureEmitterBlock> CountermeasureEmitterIdMap;
        private static List<CountermeasureEmitterBlock> _deadEmitters;
        public static uint HighestCountermeasureEmitterId = 0;


        public static void Init()
        {
            CountermeasureIdMap = new Dictionary<uint, Countermeasure>();
            _deadCountermeasures = new List<Countermeasure>();
            HighestCountermeasureId = 0;

            CountermeasureEmitterIdMap = new Dictionary<uint, CountermeasureEmitterBlock>();
            _deadEmitters = new List<CountermeasureEmitterBlock>();
            HighestCountermeasureEmitterId = 0;

            ServerMain.I.OnBlockPlaced += OnBlockPlaced;

            Log.Info("CountermeasureManager", "Ready.");
        }

        public static void Update()
        {
            try
            {
                foreach (var countermeasure in CountermeasureIdMap.Values)
                {
                    countermeasure.Update();
                    if (!countermeasure.IsActive)
                        _deadCountermeasures.Add(countermeasure);
                }

                foreach (var deadCountermeasure in _deadCountermeasures)
                    CountermeasureIdMap.Remove(deadCountermeasure.Id);
                _deadCountermeasures.Clear();

                foreach (var emitter in CountermeasureEmitterIdMap.Values)
                {
                    emitter.Update();
                    if (emitter.Block.Closed)
                        _deadEmitters.Add(emitter);
                }

                foreach (var deadEmitter in _deadEmitters)
                    CountermeasureEmitterIdMap.Remove(deadEmitter.Id);
                _deadEmitters.Clear();
            }
            catch (Exception ex)
            {
                Log.Exception("CountermeasureManager", ex);
            }
        }

        public static void Close()
        {
            ServerMain.I.OnBlockPlaced -= OnBlockPlaced;

            CountermeasureIdMap = null;
            _deadCountermeasures = null;

            CountermeasureEmitterIdMap = null;
            _deadEmitters = null;

            Log.Info("CountermeasureManager", "Closed.");
        }

        public static double GetNoise(ISensor sensor)
        {
            double totalNoise = 0;
            foreach (var counter in CountermeasureIdMap.Values)
                totalNoise += counter.GetSensorNoise(sensor);
            if (totalNoise > 0)
                MyAPIGateway.Utilities.ShowNotification($"{sensor.GetType().Name}: {MathUtils.ToDecibels(totalNoise):F}dB noise ({CountermeasureIdMap.Count} source[s])", 1000/60);
            return totalNoise;
        }

        private static void OnBlockPlaced(IMyCubeBlock obj)
        {
            var block = obj as IMyConveyorSorter;
            if (block == null)
                return;

            var emitters = DefinitionManager.TryCreateCountermeasureEmitters(block);
            foreach (var emitter in emitters)
                CountermeasureEmitterIdMap[emitter.Id] = emitter;
        }
    }
}
