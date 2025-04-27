using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DetectionEquipment.Shared.Networking;
using DetectionEquipment.Shared.Utils;

namespace DetectionEquipment.Client.Countermeasures
{
    internal static class CountermeasureManager
    {
        private static HashSet<Countermeasure> _countermeasures;
        private static List<Countermeasure> _deadCountermeasures;

        public static void Init()
        {
            _countermeasures = new HashSet<Countermeasure>();
            _deadCountermeasures = new List<Countermeasure>();

            Log.Info("CountermeasureManager", "Ready.");
        }

        public static void Update()
        {
            try
            {
                foreach (var countermeasure in _countermeasures)
                {
                    countermeasure.Update();
                    if (!countermeasure.IsActive)
                        _deadCountermeasures.Add(countermeasure);
                }

                foreach (var deadCountermeasure in _deadCountermeasures)
                    _countermeasures.Remove(deadCountermeasure);
                _deadCountermeasures.Clear();
            }
            catch (Exception ex)
            {
                Log.Exception("CountermeasureManager", ex);
            }
        }

        public static void Close()
        {
            _countermeasures = null;
            _deadCountermeasures = null;

            Log.Info("CountermeasureManager", "Closed.");
        }

        public static void RegisterNew(Countermeasure countermeasure)
        {
            _countermeasures.Add(countermeasure);
        }
    }
}
