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
using VRage.Game.ModAPI;

namespace DetectionEquipment.Server.Countermeasures
{
    internal static class CountermeasureManager
    {
        public static Dictionary<uint, Countermeasure> CountermeasureIdMap;
        public static Dictionary<uint, CountermeasureEmitterBlock> CountermeasureEmitterIdMap;
        public static uint HighestCountermeasureId = 0;
        public static uint HighestCountermeasureEmitterId = 0;

        private static List<Countermeasure> _deadCountermeasures;

        public static void Init()
        {
            CountermeasureIdMap = new Dictionary<uint, Countermeasure>();
            CountermeasureEmitterIdMap = new Dictionary<uint, CountermeasureEmitterBlock>();
            _deadCountermeasures = new List<Countermeasure>();
            HighestCountermeasureId = 0;
            HighestCountermeasureEmitterId = 0;
            ServerMain.I.OnBlockPlaced += OnBlockPlaced;

            Log.Info("CountermeasureManager", "Ready.");
        }

        public static void Update()
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
        }

        public static void Close()
        {
            ServerMain.I.OnBlockPlaced -= OnBlockPlaced;
            CountermeasureIdMap = null;
            CountermeasureEmitterIdMap = null;

            Log.Info("CountermeasureManager", "Closed.");
        }

        public static float GetNoise(ISensor sensor)
        {
            float totalNoise = 0;
            foreach (var counter in CountermeasureIdMap.Values)
                totalNoise += counter.GetSensorNoise(sensor);
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
