using System;
using System.Collections.Generic;
using System.Linq;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Shared.Helpers
{
    internal static class IffHelper
    {
        private static Dictionary<IMyCubeGrid, HashSet<IIffComponent>> _iffMap;

        public static void Load()
        {
            _iffMap = new Dictionary<IMyCubeGrid, HashSet<IIffComponent>>();
        }

        public static void Unload()
        {
            _iffMap = null;
        }

        public static void RegisterComponent(IMyCubeGrid grid, IIffComponent component)
        {
            if (!_iffMap.ContainsKey(grid))
            {
                _iffMap.Add(grid, new HashSet<IIffComponent> { component });
                return;
            }
            _iffMap[grid].Add(component);
            Log.Info($"IffManager", string.Join(", ", grid.Components.GetComponentTypes().Select(t => t.Name)));
        }

        public static void RemoveComponent(IMyCubeGrid grid, IIffComponent component)
        {
            HashSet<IIffComponent> compSet;
            if (!_iffMap.TryGetValue(grid, out compSet))
                return;
            compSet.Remove(component);
            if (compSet.Count == 0 || grid.Closed)
                _iffMap.Remove(grid);
        }

        public static string[] GetIffCodes(IMyCubeGrid grid, SensorDefinition.SensorType sensorType)
        {
            HashSet<IIffComponent> map;
            if (!_iffMap.TryGetValue(grid, out map))
                return Array.Empty<string>();
            var codes = new HashSet<string>();
            foreach (var reflector in map)
                if (reflector.Enabled && (sensorType == SensorDefinition.SensorType.None || reflector.SensorType == sensorType))
                    codes.Add(reflector.IffCodeCache);

            var array = new string[codes.Count];
            int i = 0;
            foreach (var code in codes)
            {
                array[i] = code;
                i++;
            }

            return array;
        }
    }
}
