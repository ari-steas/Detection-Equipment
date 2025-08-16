using DetectionEquipment.Server.Sensors;
using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Reflection;
using VRage.Game.ModAPI;
using VRageMath;
using MyInventoryItem = VRage.Game.ModAPI.Ingame.MyInventoryItem;

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

        public static ObjectPool<List<MyInventoryItem>> InventoryItemPool;

        public static void Init()
        {
            CountermeasureIdMap = new Dictionary<uint, Countermeasure>();
            _deadCountermeasures = new List<Countermeasure>();
            HighestCountermeasureId = 0;

            CountermeasureEmitterIdMap = new Dictionary<uint, CountermeasureEmitterBlock>();
            _deadEmitters = new List<CountermeasureEmitterBlock>();
            HighestCountermeasureEmitterId = 0;

            InventoryItemPool = new ObjectPool<List<MyInventoryItem>>(() => new List<MyInventoryItem>(), null, list => list.Clear(), 100);

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

            InventoryItemPool = null;

            Log.Info("CountermeasureManager", "Closed.");
        }

        public static double GetNoise(ISensor sensor)
        {
            double totalNoise = 0;
            foreach (var counter in CountermeasureIdMap.Values)
                totalNoise += counter.GetSensorNoise(sensor);
            if (GlobalData.DebugLevel > 1 && totalNoise > 0)
                MyAPIGateway.Utilities.ShowNotification($"{sensor.GetType().Name}: {MathUtils.ToDecibels(totalNoise):F}dB noise ({CountermeasureIdMap.Count} source[s])", 1000/60);
            return totalNoise;
        }

        public static void ApplyDrfm(ISensor sensor, ITrack track, ref double trackCrossSection, ref double trackRange, ref double maxRangeError, ref Vector3D trackBearing, ref double maxBearingError, ref string[] iffCodes)
        {
            double crossSectionOffset = 0, rangeOffset = 0, rangeErrOffset = 0, bearingErrorOffset = 0;

            foreach (var counter in CountermeasureIdMap)
            {
                var cdef = counter.Value.Definition;
                if (cdef.MaxDrfmRange < trackRange || cdef.DrfmEffects == null)
                    continue;

                if (!counter.Value.CanApplyTo(sensor) || counter.Value.IsOutsideAperture(sensor))
                    return;

                if (!cdef.ApplyDrfmToOtherTargets)
                {
                    var gridTrack = track as GridTrack;
                    if (gridTrack == null || counter.Value.ParentEmitter == null || !counter.Value.ParentEmitter.Block.CubeGrid.IsInSameLogicalGroupAs(gridTrack.Grid))
                        continue;
                }

                var drfmResults = cdef.DrfmEffects.Invoke(sensor.Id, counter.Key, counter.Value.ParentEmitter?.Block, track.EntityId, trackCrossSection, trackRange, maxRangeError, trackBearing, maxBearingError, iffCodes);
                crossSectionOffset += drfmResults.Item1;
                rangeOffset += drfmResults.Item2;
                rangeErrOffset += drfmResults.Item3;
                trackBearing = drfmResults.Item4; // no clean way to combine this for multiple countermeasures
                bearingErrorOffset += drfmResults.Item5;
                iffCodes = drfmResults.Item6; // no clean way to combine this for multiple countermeasures
            }

            trackCrossSection += crossSectionOffset;
            trackRange += rangeOffset;
            maxRangeError += rangeErrOffset;
            maxBearingError += bearingErrorOffset;
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
