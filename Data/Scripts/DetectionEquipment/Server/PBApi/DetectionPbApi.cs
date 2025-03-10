using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace IngameScript
{
    /// <summary>
    /// Programmable Block interface for Aristeas's Detection Equipment mod.
    /// <para>
    ///     To use, copy this class into your script and instantiate it.
    /// </para>
    /// </summary>
    public class DetectionPbApi
    {
        /// <summary>
        /// Instantiates the PBApi.
        /// </summary>
        /// <param name="program">Use 'this' (this program instance)</param>
        public DetectionPbApi(MyGridProgram program)
        {
            if (I != null)
                throw new Exception("Only one DetectionPbApi should be active at at time!");

            // Shamelessly adapted from the WcPbAPI
            Program = program;
            if (Program == null)
                throw new Exception("Invalid Program instance!");

            _methodMap = program.Me.GetProperty("DetectionPbApi")?.As<IReadOnlyDictionary<string, Delegate>>()?.GetValue(program.Me);
            if (_methodMap == null)
                throw new Exception("Failed to get DetectionPbApi!"); // This is expected to occur once on world load; Detection Equipment automatically recompiles affected programmable blocks.
            InitializeApi();
            _methodMap = null;
        }

        #region Public Methods

        public List<PbSensorBlock> GetSensors(IMyCubeBlock block)
        {
            if (!_hasSensor.Invoke(block))
                return new List<PbSensorBlock>(0);

            List<PbSensorBlock> list = new List<PbSensorBlock>();
            foreach (uint id in _getSensorIds.Invoke(block))
                list.Add(new PbSensorBlock(block, id));
            return list;
        }

        public bool HasSensor(IMyCubeBlock block) => _hasSensor.Invoke(block);

        public PbAggregatorBlock GetAggregator(IMyCubeBlock block)
        {
            if (!_hasAggregator.Invoke(block))
                return null;

            return new PbAggregatorBlock(block);
        }

        public bool HasAggregator(IMyCubeBlock block) => _hasAggregator.Invoke(block);

        #endregion

        #region Delegates

        // Sensors
        private Func<IMyCubeBlock, uint[]> _getSensorIds;
        private Func<IMyCubeBlock, bool> _hasSensor;
        private Func<uint, Vector3D> _getSensorPosition;
        private Func<uint, Vector3D> _getSensorDirection;
        private Func<uint, double> _getSensorAperture;
        private Action<uint, double> _setSensorAperture;
        private Func<uint, double> _getSensorAzimuth;
        private Action<uint, double> _setSensorAzimuth;
        private Func<uint, double> _getSensorElevation;
        private Action<uint, double> _setSensorElevation;
        private Func<uint, MyTuple<int, double, double, MyTuple<double, double, double, double, double, double>?, double, double>> _getSensorDefinition;
        private Func<uint, MyTuple<double, double, double, double, Vector3D>[]> _getSensorDetections;
        private Action<uint, Action<MyTuple<double, double, double, double, Vector3D>>> _registerInvokeOnDetection;
        private Action<uint, Action<MyTuple<double, double, double, double, Vector3D>>> _unregisterInvokeOnDetection;

        // Aggregator
        private Func<IMyCubeBlock, bool> _hasAggregator;
        private Func<IMyCubeBlock, float> _getAggregatorTime;
        private Action<IMyCubeBlock, float> _setAggregatorTime;
        private Func<IMyCubeBlock, float> _getAggregatorDistance;
        private Action<IMyCubeBlock, float> _setAggregatorDistance;
        private Func<IMyCubeBlock, float> _getAggregatorVelocity;
        private Action<IMyCubeBlock, float> _setAggregatorVelocity;
        private Func<IMyCubeBlock, float> _getAggregatorRcs;
        private Action<IMyCubeBlock, float> _setAggregatorRcs;
        private Func<IMyCubeBlock, bool> _getAggregatorTypes;
        private Action<IMyCubeBlock, bool> _setAggregatorTypes;
        private Func<IMyCubeBlock, MyTuple<int, double, double, Vector3D, Vector3D?, double?>[]> _getAggregatorInfo;
        private Func<IMyCubeBlock, bool> _getAggregatorUseAllSensors;
        private Action<IMyCubeBlock, bool> _setAggregatorUseAllSensors;
        private Func<IMyCubeBlock, List<IMyTerminalBlock>> _getAggregatorActiveSensors;
        private Action<IMyCubeBlock, List<IMyTerminalBlock>> _setAggregatorActiveSensors;

        #endregion

        #region API Internals

        private MyGridProgram Program;
        private IReadOnlyDictionary<string, Delegate> _methodMap;
        public static DetectionPbApi I { get; private set; }

        private void InitializeApi()
        {
            I = this;

            SetApiMethod("GetSensorIds", ref _getSensorIds);
            SetApiMethod("HasSensor", ref _hasSensor);
            SetApiMethod("GetSensorPosition", ref _getSensorPosition);
            SetApiMethod("GetSensorDirection", ref _getSensorDirection);
            SetApiMethod("GetSensorAperture", ref _getSensorAperture);
            SetApiMethod("SetSensorAperture", ref _setSensorAperture);
            SetApiMethod("GetSensorAzimuth", ref _getSensorAzimuth);
            SetApiMethod("SetSensorAzimuth", ref _setSensorAzimuth);
            SetApiMethod("GetSensorElevation", ref _getSensorElevation);
            SetApiMethod("SetSensorElevation", ref _setSensorElevation);
            SetApiMethod("GetSensorDefinition", ref _getSensorDefinition);
            SetApiMethod("GetSensorDetections", ref _getSensorDetections);
            SetApiMethod("RegisterInvokeOnDetection", ref _registerInvokeOnDetection);
            SetApiMethod("UnregisterInvokeOnDetection", ref _unregisterInvokeOnDetection);

            SetApiMethod("HasAggregator", ref _hasAggregator);
            SetApiMethod("GetAggregatorTime", ref _getAggregatorTime);
            SetApiMethod("SetAggregatorTime", ref _setAggregatorTime);
            SetApiMethod("GetAggregatorDistance", ref _getAggregatorDistance);
            SetApiMethod("SetAggregatorDistance", ref _setAggregatorDistance);
            SetApiMethod("GetAggregatorVelocity", ref _getAggregatorVelocity);
            SetApiMethod("SetAggregatorVelocity", ref _setAggregatorVelocity);
            SetApiMethod("GetAggregatorRcs", ref _getAggregatorRcs);
            SetApiMethod("SetAggregatorRcs", ref _setAggregatorRcs);
            SetApiMethod("GetAggregatorTypes", ref _getAggregatorTypes);
            SetApiMethod("SetAggregatorTypes", ref _setAggregatorTypes);
            SetApiMethod("GetAggregatorInfo", ref _getAggregatorInfo);
            SetApiMethod("GetAggregatorUseAllSensors", ref _getAggregatorUseAllSensors);
            SetApiMethod("SetAggregatorUseAllSensors", ref _setAggregatorUseAllSensors);
            SetApiMethod("GetAggregatorActiveSensors", ref _getAggregatorActiveSensors);
            SetApiMethod("SetAggregatorActiveSensors", ref _setAggregatorActiveSensors);
        }

        /// <summary>
        ///     Assigns a single API endpoint.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name">Shared endpoint name; matches with the framework mod.</param>
        /// <param name="method">Method to assign.</param>
        /// <exception cref="Exception"></exception>
        private void SetApiMethod<T>(string methodTag, ref T method) where T : class
        {
            if (_methodMap == null)
            {
                method = null;
                return;
            }

            if (!_methodMap.ContainsKey(methodTag))
                throw new Exception("Method Map does not contain method " + methodTag);
            var del = _methodMap[methodTag];
            if (del.GetType() != typeof(T))
                throw new Exception(
                    $"Method {methodTag} type mismatch! [MapMethod: {del.GetType().Name} | ApiMethod: {typeof(T).Name}]");
            method = _methodMap[methodTag] as T;
        }

        #endregion

        #region Classes

        /// <summary>
        /// Interface class for Detection Equipment sensors.
        /// </summary>
        public class PbSensorBlock
        {
            public readonly PbSensorDefinition Definition;
            public readonly IMyCubeBlock Block;
            public readonly uint Id;
            private Action<PbDetectionInfo> _onDetection = null;

            /// <summary>
            /// Constructor - equivalent to DetectionPbApi.GetSensor(block);
            /// </summary>
            /// <param name="block"></param>
            public PbSensorBlock(IMyCubeBlock block, uint id)
            {
                Id = id;
                Block = block;
                Definition = (PbSensorDefinition)I._getSensorDefinition.Invoke(Id);

                if (Definition == null)
                    throw new Exception($"No sensor exists for block {block.DisplayName}!");
            }

            /// <summary>
            /// Gets sensor detections made in the last tick.
            /// </summary>
            /// <returns></returns>
            public PbDetectionInfo[] GetDetections()
            {
                var tuples = I._getSensorDetections.Invoke(Id);
                var detections = new PbDetectionInfo[tuples.Length];
                for (int i = 0; i < tuples.Length; i++)
                    detections[i] = (PbDetectionInfo)tuples[i];
                return detections;
            }

            /// <summary>
            /// Action invoked whenever the sensor detects something.
            /// </summary>
            public Action<PbDetectionInfo> OnDetection
            {
                get
                {
                    return _onDetection;
                }
                set
                {
                    // Only have an action registered if we need it - this avoids the mod profiler hit.
                    if (_onDetection == null && value != null)
                        I._registerInvokeOnDetection.Invoke(Id, InvokeOnDetection);
                    else if (value == null)
                        I._unregisterInvokeOnDetection.Invoke(Id, InvokeOnDetection);
                    _onDetection = value;
                }
            }

            /// <summary>
            /// Global position of the sensor.
            /// </summary>
            public Vector3D Position => I._getSensorPosition.Invoke(Id);
            /// <summary>
            /// Global forward direction of the sensor.
            /// </summary>
            public Vector3D Direction => I._getSensorDirection.Invoke(Id);

            /// <summary>
            /// The sensor's half field of view in radians.
            /// </summary>
            public double Aperture
            {
                get
                {
                    return I._getSensorAperture.Invoke(Id);
                }
                set
                {
                    I._setSensorAperture.Invoke(Id, value);
                }
            }

            /// <summary>
            /// Azimuth of the sensor gimbal, in radians.
            /// </summary>
            public double Azimuth
            {
                get
                {
                    return I._getSensorAzimuth.Invoke(Id);
                }
                set
                {
                    I._setSensorAzimuth.Invoke(Id, value);
                }
            }

            /// <summary>
            /// Elevation of the sensor gimbal, in radians.
            /// </summary>
            public double Elevation
            {
                get
                {
                    return I._getSensorElevation.Invoke(Id);
                }
                set
                {
                    I._setSensorElevation.Invoke(Id, value);
                }
            }

            /// <summary>
            /// Converts tuple data into a pb-usable format.
            /// </summary>
            /// <param name="tuple"></param>
            private void InvokeOnDetection(MyTuple<double, double, double, double, Vector3D> tuple) => _onDetection?.Invoke((PbDetectionInfo)tuple);
        }

        /// <summary>
        /// Internal definition for a block sensor.
        /// </summary>
        public class PbSensorDefinition
        {
            public SensorType Type;
            public double MaxAperture;
            public double MinAperture;
            public SensorMovementDefinition Movement;
            public double DetectionThreshold;
            public double MaxPowerDraw;

            public class SensorMovementDefinition
            {
                public double MinAzimuth;
                public double MaxAzimuth;
                public double MinElevation;
                public double MaxElevation;
                public double AzimuthRate;
                public double ElevationRate;

                public bool CanRotateFull => MaxAzimuth >= Math.PI && MinAzimuth <= -Math.PI;
                public bool CanElevateFull => MaxElevation >= Math.PI && MinElevation <= -Math.PI;
            }

            public enum SensorType
            {
                Radar = 0,
                PassiveRadar = 1,
                Optical = 2,
                Infrared = 3,
            }

            public static explicit operator PbSensorDefinition(MyTuple<int, double, double, MyTuple<double, double, double, double, double, double>?, double, double> tuple) => new PbSensorDefinition()
            {
                Type = (SensorType)tuple.Item1,
                MaxAperture = tuple.Item2,
                MinAperture = tuple.Item3,
                Movement = tuple.Item4 == null ? null : new SensorMovementDefinition()
                {
                    MinAzimuth = tuple.Item4.Value.Item1,
                    MaxAzimuth = tuple.Item4.Value.Item2,
                    MinElevation = tuple.Item4.Value.Item3,
                    MaxElevation = tuple.Item4.Value.Item4,
                    AzimuthRate = tuple.Item4.Value.Item5,
                    ElevationRate = tuple.Item4.Value.Item6,
                },
                DetectionThreshold = tuple.Item5,
                MaxPowerDraw = tuple.Item6,
            };
        }

        /// <summary>
        /// Generic single-target detection information, raw from a sensor.
        /// </summary>
        public struct PbDetectionInfo
        {
            public double CrossSection, Range, RangeError, BearingError;
            public Vector3D Bearing;
            public Vector3D Position => Bearing * Range;

            /// <summary>
            /// Averages out a set of detection infos.
            /// </summary>
            /// <param name="args"></param>
            /// <returns></returns>
            public static PbDetectionInfo AverageDetection(ICollection<PbDetectionInfo> args)
            {
                double totalBearingError = args.Sum(info => info.BearingError);
                double totalRangeError = args.Sum(info => info.RangeError);

                Vector3D averageBearing = Vector3D.Zero;
                double averageRange = 0;
                foreach (var info in args)
                {
                    averageBearing += info.Bearing * (info.BearingError / totalBearingError);
                    averageRange += info.Range * (info.RangeError / totalRangeError);
                }

                PbDetectionInfo result = new PbDetectionInfo()
                {
                    Bearing = averageBearing.Normalized(),
                    BearingError = totalBearingError / args.Count,
                    Range = averageRange,
                    RangeError = totalRangeError / args.Count,
                };

                return result;
            }

            public static explicit operator PbDetectionInfo(MyTuple<double, double, double, double, Vector3D> tuple) => new PbDetectionInfo()
            {
                CrossSection = tuple.Item1,
                Range = tuple.Item2,
                RangeError = tuple.Item3,
                BearingError = tuple.Item4,
                Bearing = tuple.Item5,
            };

            public override string ToString()
            {
                return $"Range: {Range:N0} +-{RangeError:N1}m\nBearing: {Bearing.ToString("N0")} +-{MathHelper.ToDegrees(BearingError):N1}°";
            }
        }

        /// <summary>
        /// Generic single-target detection information, processed by a controller block.
        /// </summary>
        public struct PbWorldDetectionInfo
        {
            public PbWorldDetectionInfo(PbDetectionInfo info, PbSensorBlock sensor)
            {
                CrossSection = info.CrossSection;
                Position = sensor.Position + info.Bearing * info.Range;

                Error = Math.Tan(info.BearingError) * info.Range; // planar error; base width of right triangle
                Error *= Error;
                Error += info.RangeError * info.RangeError; // normal error
                Error = Math.Sqrt(Error);

                DetectionType = sensor.Definition.Type;

                Velocity = null;
                VelocityVariance = null;
            }

            public PbWorldDetectionInfo(MyTuple<int, double, double, Vector3D, Vector3D?, double?> tuple)
            {
                DetectionType = (PbSensorDefinition.SensorType) tuple.Item1;
                CrossSection = tuple.Item2;
                Error = tuple.Item3;
                Position = tuple.Item4;
                Velocity = tuple.Item5;
                VelocityVariance = tuple.Item6;
            }

            public double CrossSection, Error;
            public Vector3D Position;
            public Vector3D? Velocity;
            public double? VelocityVariance;
            public PbSensorDefinition.SensorType DetectionType;

            public override string ToString()
            {
                return $"Position: {Position.ToString("N0")} +- {Error:N1}m\nVelocity: {Velocity:N0} R^2={VelocityVariance:F1}";
            }

            public static PbWorldDetectionInfo Average(ICollection<PbWorldDetectionInfo> args)
            {
                if (args.Count == 0)
                    throw new Exception("No detection infos provided!");

                double totalError = 0;

                foreach (var info in args)
                {
                    totalError += info.Error;
                }

                Vector3D averagePos = Vector3D.Zero;
                double totalCrossSection = 0;
                foreach (var info in args)
                {
                    if (totalError > 0)
                        averagePos += info.Position * (info.Error/totalError);
                    else
                        averagePos += info.Position;
                    totalCrossSection += info.CrossSection;
                }

                if (totalError <= 0)
                    averagePos /= args.Count;

                double avgDiff = 0;
                foreach (var info in args)
                    avgDiff += Vector3D.DistanceSquared(info.Position, averagePos);
                avgDiff = Math.Sqrt(avgDiff)/args.Count;

                PbWorldDetectionInfo result = new PbWorldDetectionInfo()
                {
                    CrossSection = totalCrossSection / args.Count,
                    Position = averagePos,
                    Error = avgDiff,
                    DetectionType = 0,
                };

                return result;
            }

            public static PbWorldDetectionInfo AverageWeighted(ICollection<KeyValuePair<PbWorldDetectionInfo, int>> args)
            {
                if (args.Count == 0)
                    throw new Exception("No detection infos provided!");

                double totalError = 0;
                double totalWeight = 0;
                double highestError = 0;

                foreach (var info in args)
                {
                    totalError += info.Key.Error;
                    totalWeight += info.Value;
                    if (info.Key.Error > highestError)
                        highestError = info.Key.Error;
                }

                Vector3D averagePos = Vector3D.Zero;
                double avgCrossSection = 0;
                foreach (var info in args)
                {
                    double infoWeightPct = info.Value / totalWeight;

                    averagePos += info.Key.Position * infoWeightPct;

                    avgCrossSection += info.Key.CrossSection * infoWeightPct;
                }

                double avgDiff = 0;
                foreach (var info in args)
                    avgDiff += Vector3D.Distance(info.Key.Position, averagePos);
                avgDiff /= args.Count;

                PbWorldDetectionInfo result = new PbWorldDetectionInfo()
                {
                    CrossSection = avgCrossSection,
                    Position = averagePos,
                    Error = avgDiff,
                    DetectionType = 0,
                };

                return result;
            }
        }

        public class PbAggregatorBlock
        {
            public readonly IMyCubeBlock Block;

            public PbAggregatorBlock(IMyCubeBlock block)
            {
                Block = block;
            }

            /// <summary>
            /// Amount of time, in seconds, the aggregator block stores data.
            /// </summary>
            public float AggregationTime
            {
                get
                {
                    return I._getAggregatorTime.Invoke(Block);
                }
                set
                {
                    I._setAggregatorTime.Invoke(Block, value);
                }
            }

            /// <summary>
            /// Scalar for position error over which to combine detections.
            /// <para>
            ///     Ex: Error of 5m and DistanceThreshold of 2 means detections within 10m are combined.
            /// </para>
            /// </summary>
            public float DistanceThreshold
            {
                get
                {
                    return I._getAggregatorDistance.Invoke(Block);
                }
                set
                {
                    I._setAggregatorDistance.Invoke(Block, value);
                }
            }

            /// <summary>
            /// Maximum velocity variation at which to incorporate into position estimate.
            /// <seealso cref="PbWorldDetectionInfo.VelocityVariance"/>
            /// <para>
            ///     The aggregator block will use velocity to provide a more accurate position estimate with high aggregation times, but when the detection info is inaccurate the velocity can be wildly inaccurate.
            /// </para>
            /// </summary>
            public float VelocityErrorThreshold
            {
                get
                {
                    return I._getAggregatorVelocity.Invoke(Block);
                }
                set
                {
                    I._setAggregatorVelocity.Invoke(Block, value);
                }
            }

            /// <summary>
            /// Scalar for RCS difference at which to combine detections.
            /// <para>
            ///     Ex: RCS_1 of 10m^2 and RCS_2 of 12m^2 has an "error" of |10-12|/Max(10, 12) = 1/6. As such, the RcsThreshold must be above 1/6 for these detection infos to be combined.
            /// </para>
            /// </summary>
            public float RcsThreshold
            {
                get
                {
                    return I._getAggregatorRcs.Invoke(Block);
                }
                set
                {
                    I._setAggregatorRcs.Invoke(Block, value);
                }
            }

            /// <summary>
            /// Whether the aggregator should combine info from different sensor types.
            /// </summary>
            public bool AggregateTypes
            {
                get
                {
                    return I._getAggregatorTypes.Invoke(Block);
                }
                set
                {
                    I._setAggregatorTypes.Invoke(Block, value);
                }
            }

            /// <summary>
            /// Whether the aggregator should use data from all sensors on the grid.
            /// </summary>
            public bool UseAllGridSensors
            {
                get
                {
                    return I._getAggregatorUseAllSensors.Invoke(Block);
                }
                set
                {
                    I._setAggregatorUseAllSensors.Invoke(Block, value);
                }
            }

            /// <summary>
            /// Sensors being used by the aggregator. Ignored if <see cref="UseAllGridSensors"/> is true.
            /// </summary>
            public List<IMyTerminalBlock> ActiveSensors
            {
                get
                {
                    return I._getAggregatorActiveSensors.Invoke(Block);
                }
                set
                {
                    I._setAggregatorActiveSensors.Invoke(Block, value);
                }
            }

            /// <summary>
            /// Retreieve aggregated detection info from a block.
            /// </summary>
            /// <returns></returns>
            public PbWorldDetectionInfo[] GetAggregatedInfo()
            {
                var tupleSet = I._getAggregatorInfo.Invoke(Block);
                var toReturn = new PbWorldDetectionInfo[tupleSet.Length];
                for (int i = 0; i < toReturn.Length; i++)
                {
                    toReturn[i] = new PbWorldDetectionInfo(tupleSet[i]);
                }
                return toReturn;
            }
        }

        #endregion
    }
}
