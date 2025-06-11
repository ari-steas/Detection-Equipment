using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using VRage.Scripting.MemorySafeTypes;
using VRageMath;

using IMyCubeBlock = VRage.Game.ModAPI.Ingame.IMyCubeBlock;
using IMyTerminalBlock = Sandbox.ModAPI.Ingame.IMyTerminalBlock;

namespace IngameScript
{
    /// <summary>
    /// Programmable Block interface for Aristeas's Detection Equipment mod.
    /// <para>
    ///     To use, copy this class into your script and instantiate it. See <see href="https://github.com/ari-steas/Detection-Equipment"/> for detailed instructions.
    /// </para>
    /// </summary>
    public class DetectionPbApi
    {
        /*
         * Modify this class at your own risk, but have fun if you do!
         * 
         * Minimizing is *highly* recommended; the DetectionPbApi is very large and will take a significant portion of your character budget.
         *     Even if you have room to spare, minimizing will decrease the network & storage load of your ships (always a good thing).
         * 
         * If you have any questions or would like to see something added, feel free to message [@aristeas.] on Discord.
         *     Feedback is always welcome (she'll probably still call you a nerd).
         * 
         * Best of luck, scripter!
         */

        /// <summary>
        /// Instantiates the PBApi.
        /// </summary>
        /// <param name="program">Use 'this' (this program instance)</param>
        public DetectionPbApi(MyGridProgram program)
        {
            if (I != null)
                throw new Exception("Only one DetectionPbApi should be active at at time!");

            // Shamelessly adapted from the WcPbAPI
            if (program == null)
                throw new Exception("Invalid Program instance!");

            _methodMap = program.Me.GetProperty("DetectionPbApi")?.As<IReadOnlyDictionary<string, Delegate>>()?.GetValue(program.Me);
            if (_methodMap == null)
                throw new Exception("Failed to get DetectionPbApi!"); // This is expected to occur once on world load; Detection Equipment automatically recompiles affected programmable blocks.
            InitializeApi();
            _methodMap = null;
            program.Echo("DetectionPbApi loaded!");
        }

        #region Public Methods

        /// <summary>
        /// Retrieves a MemorySafeList of all sensors on a given block.
        /// <para>
        ///     Blocks can have multiple sensors! They are delineated by unique id.
        /// </para>
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public MemorySafeList<PbSensorBlock> GetSensors(IMyCubeBlock block)
        {
            if (!_hasSensor.Invoke(block))
                return new MemorySafeList<PbSensorBlock>(0);

            MemorySafeList<PbSensorBlock> MemorySafeList = new MemorySafeList<PbSensorBlock>();
            foreach (uint id in _getSensorIds.Invoke(block))
                MemorySafeList.Add(new PbSensorBlock(block, id));
            return MemorySafeList;
        }

        /// <summary>
        /// Does this block have a sensor on it?
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public bool HasSensor(IMyCubeBlock block) => _hasSensor.Invoke(block);

        /// <summary>
        /// Retrieve a block's aggregator, if present.
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public PbAggregatorBlock GetAggregator(IMyCubeBlock block)
        {
            if (!_hasAggregator.Invoke(block))
                return null;

            return new PbAggregatorBlock(block);
        }

        /// <summary>
        /// Does this block have an aggregator on it?
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public bool HasAggregator(IMyCubeBlock block) => _hasAggregator.Invoke(block);

        /// <summary>
        /// Retrieve a block's IFF reflector, if present.
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public PbIffReflectorBlock GetIffReflector(IMyCubeBlock block)
        {
            if (!_hasReflector.Invoke(block))
                return null;

            return new PbIffReflectorBlock(block);
        }

        /// <summary>
        /// Does this block have an IFF reflector on it?
        /// </summary>
        /// <param name="block"></param>
        /// <returns></returns>
        public bool HasIffReflector(IMyCubeBlock block) => _hasReflector.Invoke(block);

        #endregion

        #region Delegates

        // Misc
        private Action<object[]> _returnFieldArray;

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
        private Func<uint, object[]> _getSensorDefinition;
        private Func<uint, object[][]> _getSensorDetections;
        private Action<uint, Action<object[]>> _registerInvokeOnDetection;
        private Action<uint, Action<object[]>> _unregisterInvokeOnDetection;

        // Aggregator
        private Func<IMyCubeBlock, bool> _hasAggregator;
        private Func<IMyCubeBlock, float> _getAggregatorTime;
        private Action<IMyCubeBlock, float> _setAggregatorTime;
        private Func<IMyCubeBlock, float> _getAggregatorVelocity;
        private Action<IMyCubeBlock, float> _setAggregatorVelocity;
        private Func<IMyCubeBlock, object[][]> _getAggregatorInfo;
        private Func<IMyCubeBlock, bool> _getAggregatorUseAllSensors;
        private Action<IMyCubeBlock, bool> _setAggregatorUseAllSensors;
        private Func<IMyCubeBlock, MemorySafeList<IMyTerminalBlock>> _getAggregatorActiveSensors;
        private Action<IMyCubeBlock, MemorySafeList<IMyTerminalBlock>> _setAggregatorActiveSensors;

        // Iff Reflector
        private Func<IMyCubeBlock, bool> _hasReflector;
        private Func<IMyCubeBlock, string> _getIffCode;
        private Action<IMyCubeBlock, string> _setIffCode;
        private Func<IMyCubeBlock, bool> _getIffReturnHashed;
        private Action<IMyCubeBlock, bool> _setIffReturnHashed;

        #endregion

        #region API Internals

        private readonly IReadOnlyDictionary<string, Delegate> _methodMap;
        public static DetectionPbApi I { get; private set; }

        private void InitializeApi()
        {
            I = this;

            SetApiMethod("ReturnFieldArray", ref _returnFieldArray);

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
            SetApiMethod("GetAggregatorVelocity", ref _getAggregatorVelocity);
            SetApiMethod("SetAggregatorVelocity", ref _setAggregatorVelocity);
            SetApiMethod("GetAggregatorInfo", ref _getAggregatorInfo);
            SetApiMethod("GetAggregatorUseAllSensors", ref _getAggregatorUseAllSensors);
            SetApiMethod("SetAggregatorUseAllSensors", ref _setAggregatorUseAllSensors);
            SetApiMethod("GetAggregatorActiveSensors", ref _getAggregatorActiveSensors);
            SetApiMethod("SetAggregatorActiveSensors", ref _setAggregatorActiveSensors);

            SetApiMethod("HasReflector", ref _hasReflector);
            SetApiMethod("GetIffCode", ref _getIffCode);
            SetApiMethod("SetIffCode", ref _setIffCode);
            SetApiMethod("GetIffReturnHashed", ref _getIffReturnHashed);
            SetApiMethod("SetIffReturnHashed", ref _setIffReturnHashed);
        }

        /// <summary>
        ///     Assigns a single API endpoint.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="methodTag">Shared endpoint name; matches with the framework mod.</param>
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

        private static void SetField<T>(object dataSet, out T field)
        {
            field = (T) dataSet;
        }

        #endregion

        #region Classes

        /// <summary>
        /// Interface class for Detection Equipment sensors.
        /// </summary>
        public class PbSensorBlock
        {
            private PbSensorDefinition _definition = null;

            public PbSensorDefinition Definition
            {
                get
                {
                    if (_definition == null)
                        _definition = new PbSensorDefinition(I._getSensorDefinition.Invoke(Id));
                    return _definition;
                }
            }
            public readonly IMyCubeBlock Block;
            public readonly uint Id;
            private Action<PbDetectionInfo> _onDetection;

            /// <summary>
            /// Constructor - equivalent to DetectionPbApi.GetSensor(block);
            /// </summary>
            /// <param name="block"></param>
            /// <param name="id"></param>
            public PbSensorBlock(IMyCubeBlock block, uint id)
            {
                Id = id;
                Block = block;

                if (Definition == null)
                    throw new Exception($"No sensor exists for block {block.DisplayName}!");
            }

            /// <summary>
            /// Gets sensor detections made in the last tick.
            /// </summary>
            public PbDetectionInfo[] GetDetections()
            {
                var dataSets = I._getSensorDetections.Invoke(Id);
                var detections = new PbDetectionInfo[dataSets.Length];
                for (int i = 0; i < dataSets.Length; i++)
                    detections[i] = new PbDetectionInfo(dataSets[i]);
                return detections;
            }

            /// <summary>
            /// Gets sensor detections made in the last tick. Populates an existing collection - CLEAR MANUALLY!
            /// </summary>
            /// <param name="collection"></param>
            public void GetDetections(ICollection<PbDetectionInfo> collection)
            {
                foreach (var infoArray in I._getSensorDetections.Invoke(Id))
                    collection.Add(new PbDetectionInfo(infoArray));
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
            private void InvokeOnDetection(object[] dataSet) => _onDetection?.Invoke(new PbDetectionInfo(dataSet));
        }

        /// <summary>
        /// Internal definition for a block sensor.
        /// </summary>
        public class PbSensorDefinition
        {
            public readonly string[] BlockSubtypes;
            public readonly SensorType Type;
            public readonly double MaxAperture, MinAperture;
            public readonly SensorMovementDefinition Movement = null;
            public readonly double DetectionThreshold, MaxPowerDraw, BearingErrorModifier, RangeErrorModifier;
            public readonly RadarPropertiesDefinition RadarProperties = null;

            public PbSensorDefinition(object[] dataSet)
            {
                SetField(dataSet[0], out BlockSubtypes);
                SetField(dataSet[1], out Type);
                SetField(dataSet[2], out MaxAperture);
                SetField(dataSet[3], out MinAperture);
                if (dataSet[4] != null)
                {
                    Movement = new SensorMovementDefinition((object[]) dataSet[4]);
                    I._returnFieldArray.Invoke((object[]) dataSet[4]);
                }
                SetField(dataSet[5], out DetectionThreshold);
                SetField(dataSet[6], out MaxPowerDraw);
                SetField(dataSet[7], out BearingErrorModifier);
                SetField(dataSet[8], out RangeErrorModifier);
                if (dataSet[9] != null)
                {
                    RadarProperties = new RadarPropertiesDefinition((object[]) dataSet[9]);
                    I._returnFieldArray.Invoke((object[]) dataSet[9]);
                }
                I._returnFieldArray.Invoke(dataSet);
            }

            public class SensorMovementDefinition
            {
                public readonly string AzimuthPart, ElevationPart;
                public readonly double MinAzimuth, MaxAzimuth, MinElevation, MaxElevation, AzimuthRate, ElevationRate;

                public SensorMovementDefinition(object[] dataSet)
                {
                    SetField(dataSet[0], out AzimuthPart);
                    SetField(dataSet[1], out ElevationPart);
                    SetField(dataSet[2], out MinAzimuth);
                    SetField(dataSet[3], out MaxAzimuth);
                    SetField(dataSet[4], out MinElevation);
                    SetField(dataSet[5], out MaxElevation);
                    SetField(dataSet[6], out AzimuthRate);
                    SetField(dataSet[7], out ElevationRate);
                    I._returnFieldArray.Invoke(dataSet);
                }

                public bool CanRotateFull => MaxAzimuth >= Math.PI && MinAzimuth <= -Math.PI;
                public bool CanElevateFull => MaxElevation >= Math.PI && MinElevation <= -Math.PI;

            }

            public class RadarPropertiesDefinition
            {
                public readonly double ReceiverArea, PowerEfficiencyModifier, Bandwidth, Frequency;

                public RadarPropertiesDefinition(object[] dataSet)
                {
                    SetField(dataSet[0], out ReceiverArea);
                    SetField(dataSet[1], out PowerEfficiencyModifier);
                    SetField(dataSet[2], out Bandwidth);
                    SetField(dataSet[3], out Frequency);
                    I._returnFieldArray.Invoke(dataSet);
                }
            }

            public enum SensorType
            {
                None = 0,
                Radar = 1,
                PassiveRadar = 2,
                Optical = 3,
                Infrared = 4,
            }
        }

        /// <summary>
        /// Generic single-target detection information, raw from a sensor.
        /// </summary>
        public struct PbDetectionInfo
        {
            public readonly long UniqueId;
            public readonly double CrossSection, Range, RangeError, BearingError;
            public readonly Vector3D Bearing;
            public readonly string[] IffCodes;
            public readonly uint SensorId;

            public PbDetectionInfo(object[] dataSet)
            {
                SetField(dataSet[0], out CrossSection);
                SetField(dataSet[1], out Range);
                SetField(dataSet[2], out RangeError);
                SetField(dataSet[3], out BearingError);
                SetField(dataSet[4], out Bearing);
                SetField(dataSet[5], out IffCodes);
                SetField(dataSet[6], out UniqueId);
                SetField(dataSet[7], out SensorId);
                I._returnFieldArray.Invoke(dataSet);
            }

            public override string ToString()
            {
                return $"Range: {Range:N0} +-{RangeError:N1}m\nBearing: {Bearing.ToString("N0")} +-{MathHelper.ToDegrees(BearingError):N1}°\nIFF: {(IffCodes.Length == 0 ? "N/A" : string.Join(" | ", IffCodes))}";
            }
        }

        /// <summary>
        /// Generic single-target detection information, processed by a controller block.
        /// </summary>
        public struct PbWorldDetectionInfo : IComparable<PbWorldDetectionInfo>
        {
            public long UniqueId;
            public double CrossSection;
            public double BearingError;
            public double RangeError;
            public Vector3D Position;
            public Vector3D? Velocity;
            public double? VelocityVariance;
            public DetectionFlags DetectionType;
            public string[] IffCodes;
            public PbRelationBetweenPlayers? Relations;

            public PbWorldDetectionInfo(PbDetectionInfo info, PbSensorBlock sensor)
            {
                UniqueId = info.GetHashCode();
                CrossSection = info.CrossSection;
                Position = sensor.Position + info.Bearing * info.Range;

                BearingError = info.BearingError;
                RangeError = info.RangeError;

                DetectionType = (DetectionFlags) (1 << (int) sensor.Definition.Type - 1);

                Velocity = null;
                VelocityVariance = null;
                IffCodes = info.IffCodes;
                Relations = null;
            }

            public PbWorldDetectionInfo(object[] dataSet)
            {
                SetField(dataSet[0], out UniqueId);
                SetField(dataSet[1], out DetectionType);
                SetField(dataSet[2], out CrossSection);
                SetField(dataSet[3], out BearingError);
                SetField(dataSet[4], out Position);
                SetField(dataSet[5], out Velocity);
                SetField(dataSet[6], out VelocityVariance);
                SetField(dataSet[7], out IffCodes);
                int? tmpRelations;
                SetField(dataSet[8], out tmpRelations);
                Relations = (PbRelationBetweenPlayers?) tmpRelations;
                SetField(dataSet[0], out RangeError);
                I._returnFieldArray.Invoke(dataSet);
            }

            public override bool Equals(object obj) => obj is PbWorldDetectionInfo && Position.Equals(((PbWorldDetectionInfo)obj).Position);
            public override int GetHashCode() => UniqueId.GetHashCode();

            public override string ToString()
            {
                return $"UID: {UniqueId}\nPosition: {Position.ToString("N0")} +-{BearingError:N1}m\nIFF: {(IffCodes.Length == 0 ? "N/A" : string.Join(" | ", IffCodes))}\nRelations: {Relations?.ToString() ?? "N/A"}";
            }

            public static PbWorldDetectionInfo Average(params PbWorldDetectionInfo[] args) => Average((ICollection<PbWorldDetectionInfo>) args);

            public static PbWorldDetectionInfo Average(ICollection<PbWorldDetectionInfo> args)
            {
                if (args.Count == 0)
                    throw new Exception("No detection infos provided!");

                if (args.Count == 1)
                    return args.First();

                DetectionFlags proposedType = DetectionFlags.None;
                double totalError = 0;
                double minError = double.MaxValue;
                var allCodes = new MemorySafeList<string>();
                foreach (var info in args)
                {
                    totalError += info.BearingError;
                    if (info.BearingError < minError) minError = info.BearingError;
                    foreach (var code in info.IffCodes)
                        if (!allCodes.Contains(code))
                            allCodes.Add(code);
                    proposedType |= info.DetectionType;
                }

                Vector3D averagePos = Vector3D.Zero;
                double totalCrossSection = 0;
                double pctSum = 0;
                foreach (var info in args)
                {
                    pctSum += 1 - (info.BearingError / totalError);
                    if (totalError > 0)
                        averagePos += info.Position * (1 - (info.BearingError / totalError));
                    else
                        averagePos += info.Position;
                    totalCrossSection += info.CrossSection;
                }

                if (totalError > 0)
                    averagePos /= pctSum;
                else
                    averagePos /= args.Count;

                return new PbWorldDetectionInfo
                {
                    CrossSection = totalCrossSection / args.Count,
                    Position = averagePos,
                    BearingError = minError,
                    DetectionType = proposedType,
                    IffCodes = allCodes.ToArray(),
                };
            }

            public int CompareTo(PbWorldDetectionInfo other)
            {
                return other.CrossSection.CompareTo(this.CrossSection);
            }

            [Flags]
            public enum DetectionFlags
            {
                None = 0,
                Radar = 1,
                PassiveRadar = 2,
                Optical = 4,
                Infrared = 8
            }

            public enum PbRelationBetweenPlayers
            {
                Self = 0,
                Allies = 1,
                Neutral = 2,
                Enemies = 3,
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
            public MemorySafeList<IMyTerminalBlock> ActiveSensors
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
            /// Retrieve aggregated detection info from a block.
            /// </summary>
            /// <returns></returns>
            public PbWorldDetectionInfo[] GetAggregatedInfo()
            {
                var dataSet = I._getAggregatorInfo.Invoke(Block);
                var toReturn = new PbWorldDetectionInfo[dataSet.Length];
                for (int i = 0; i < toReturn.Length; i++)
                    toReturn[i] = new PbWorldDetectionInfo(dataSet[i]);
                return toReturn;
            }

            /// <summary>
            /// Retrieve aggregated detection info from a block. Populates an existing collection - CLEAR MANUALLY!
            /// </summary>
            /// <param name="collection"></param>
            public void GetAggregatedInfo(ICollection<PbWorldDetectionInfo> collection)
            {
                foreach (var infoArray in I._getAggregatorInfo.Invoke(Block))
                    collection.Add(new PbWorldDetectionInfo(infoArray));
            }
        }

        public class PbIffReflectorBlock
        {
            public readonly IMyCubeBlock Block;

            public PbIffReflectorBlock(IMyCubeBlock block)
            {
                Block = block;
            }

            public string IffCode
            {
                get
                {
                    return I._getIffCode(Block);
                }
                set
                {
                    I._setIffCode.Invoke(Block, value);
                }
            }

            public bool ReturnHash
            {
                get
                {
                    return I._getIffReturnHashed(Block);
                }
                set
                {
                    I._setIffReturnHashed(Block, value);
                }
            }

            public string GetActualIffString() => ReturnHash ? "H" + IffCode.GetHashCode() : "S" + IffCode;
        }

        #endregion
    }
}
