using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage;
using VRage.Game;
using VRage.Game.ModAPI.Ingame;
using VRageMath;

namespace DetectionEquipment.Server.PBApi
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
        public DetectionPbApi(Sandbox.ModAPI.Ingame.MyGridProgram program)
        {
            // Shamelessly adapted from the WcPbAPI
            Program = program;
            if (Program == null)
                throw new Exception("Invalid Program instance!");

            _methodMap = program.Me.GetProperty("DetectionPbApi")?.As<IReadOnlyDictionary<string, Delegate>>()?.GetValue(program.Me);
            if (_methodMap == null)
                throw new Exception("Failed to get DetectionPbApi!");
            InitializeApi();
            _methodMap = null;
        }

        #region Public Methods

        public PbSensor GetSensor(IMyCubeBlock block) => _hasSensor.Invoke(block) ? new PbSensor(block) : null;

        public bool HasSensor(IMyCubeBlock block) => _hasSensor.Invoke(block);

        #endregion

        #region Delegates

        private Func<IMyCubeBlock, bool> _hasSensor;
        private Func<IMyCubeBlock, Vector3D> _getSensorPosition;
        private Func<IMyCubeBlock, Vector3D> _getSensorDirection;
        private Func<IMyCubeBlock, double> _getSensorAperture;
        private Action<IMyCubeBlock, double> _setSensorAperture;
        private Func<IMyCubeBlock, double> _getSensorAzimuth;
        private Action<IMyCubeBlock, double> _setSensorAzimuth;
        private Func<IMyCubeBlock, double> _getSensorElevation;
        private Action<IMyCubeBlock, double> _setSensorElevation;
        private Func<string, MyTuple<int, double, double, MyTuple<double, double, double, double, double, double>?, double, double>> _getSensorDefinition;
        private Func<IMyCubeBlock, List<MyTuple<double, double, double, double, Vector3D>>> _getSensorDetections;
        private Action<IMyCubeBlock, Action<MyTuple<double, double, double, double, Vector3D>>> _registerInvokeOnDetection;
        private Action<IMyCubeBlock, Action<MyTuple<double, double, double, double, Vector3D>>> _unregisterInvokeOnDetection;

        #endregion

        #region API Internals

        private MyGridProgram Program;
        private IReadOnlyDictionary<string, Delegate> _methodMap;
        private static DetectionPbApi I;

        private void InitializeApi()
        {
            I = this;

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
        public class PbSensor
        {
            public readonly PbSensorDefinition Definition;
            public readonly IMyCubeBlock Block;
            private Action<PbDetectionInfo> _onDetection = null;

            /// <summary>
            /// Constructor - equivalent to DetectionPbApi.GetSensor(block);
            /// </summary>
            /// <param name="block"></param>
            public PbSensor(IMyCubeBlock block)
            {
                Block = block;
                Definition = (PbSensorDefinition) I._getSensorDefinition.Invoke(Block.BlockDefinition.SubtypeName);

                if (Definition == null)
                    throw new Exception($"No sensor exists for block {block.DisplayName}!");
            }

            /// <summary>
            /// Gets sensor detections made in the last tick.
            /// </summary>
            /// <returns></returns>
            public List<PbDetectionInfo> GetDetections()
            {
                var tuples = I._getSensorDetections.Invoke(Block);
                var detections = new List<PbDetectionInfo>(tuples.Count);
                foreach (var detection in tuples)
                    detections.Add((PbDetectionInfo) detection);
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
                        I._registerInvokeOnDetection.Invoke(Block, InvokeOnDetection);
                    else if (value == null)
                        I._unregisterInvokeOnDetection.Invoke(Block, InvokeOnDetection);
                    _onDetection = value;
                }
            }

            /// <summary>
            /// Global position of the sensor.
            /// </summary>
            public Vector3D Position => I._getSensorPosition.Invoke(Block);
            /// <summary>
            /// Global forward direction of the sensor.
            /// </summary>
            public Vector3D Direction => I._getSensorDirection.Invoke(Block);

            /// <summary>
            /// The sensor's half field of view in radians.
            /// </summary>
            public double Aperture
            {
                get
                {
                    return I._getSensorAperture.Invoke(Block);
                }
                set
                {
                    I._setSensorAperture.Invoke(Block, value);
                }
            }

            /// <summary>
            /// Azimuth of the sensor gimbal, in radians.
            /// </summary>
            public double Azimuth
            {
                get
                {
                    return I._getSensorAzimuth.Invoke(Block);
                }
                set
                {
                    I._setSensorAzimuth.Invoke(Block, value);
                }
            }

            /// <summary>
            /// Elevation of the sensor gimbal, in radians.
            /// </summary>
            public double Elevation
            {
                get
                {
                    return I._getSensorElevation.Invoke(Block);
                }
                set
                {
                    I._setSensorElevation.Invoke(Block, value);
                }
            }

            /// <summary>
            /// Converts tuple data into a pb-usable format.
            /// </summary>
            /// <param name="tuple"></param>
            private void InvokeOnDetection(MyTuple<double, double, double, double, Vector3D> tuple) => _onDetection?.Invoke((PbDetectionInfo) tuple);
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
                Type = (SensorType) tuple.Item1,
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
        /// Generic single-target detection information.
        /// </summary>
        public class PbDetectionInfo
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
                    averageBearing += info.Bearing * (info.BearingError/totalBearingError);
                    averageRange += info.Range * (info.RangeError/totalRangeError);
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
        }

        #endregion
    }
}
