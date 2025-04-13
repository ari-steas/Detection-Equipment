using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DetectionEquipment.Server.Networking;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game.Weapons;

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
            CountermeasureIdMap = null;
            CountermeasureEmitterIdMap = null;

            Log.Info("CountermeasureManager", "Closed.");
        }
    }
}
