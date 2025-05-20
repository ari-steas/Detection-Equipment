#define MAINMOD
using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using System;
using VRage;
using SensorDefTuple = VRage.MyTuple<int, double, double, VRage.MyTuple<double, double, double, double, double, double>?, double, double>;

namespace DetectionEquipment.Shared.Definitions
{
    /// <summary>
    /// Basic definition for a single sensor.
    /// </summary>
    [ProtoContract]
    public class SensorDefinition
    {
        #if MAINMOD
        [ProtoIgnore] public int Id; // DO NOT NETWORK THIS!!! Hashcode of the definition name.
        #else
        /// <summary>
        /// Unique name for this definition.
        /// </summary>
        [ProtoIgnore] public string Name;
        #endif

        /// <summary>
        /// Subtypes this sensor is attached to.
        /// </summary>
        [ProtoMember(1)] public string[] BlockSubtypes;
        /// <summary>
        /// Sensor type.
        /// </summary>
        [ProtoMember(2)] public SensorType Type;
        /// <summary>
        /// Maximum aperture cone radius, in radians.
        /// </summary>
        [ProtoMember(3)] public double MaxAperture;
        /// <summary>
        /// Minimum aperture cone radius, in radians.
        /// </summary>
        [ProtoMember(4)] public double MinAperture;
        /// <summary>
        /// Movement definition. Set to null if unused.
        /// </summary>
        [ProtoMember(5)] public SensorMovementDefinition Movement = null;
        /// <summary>
        /// Minimum detection threshold - function varies by sensor type.
        /// </summary>
        [ProtoMember(6)] public double DetectionThreshold;
        /// <summary>
        /// Maximum active power draw.
        /// </summary>
        [ProtoMember(7)] public double MaxPowerDraw = 0;
        /// <summary>
        /// Multiplier for bearing error
        /// </summary>
        [ProtoMember(8)] public double BearingErrorModifier = 1;
        /// <summary>
        /// Multiplier for range error
        /// </summary>
        [ProtoMember(9)] public double RangeErrorModifier = 1;
        /// <summary>
        /// Radar properties. Set to null if unused.
        /// </summary>
        [ProtoMember(10)] public RadarPropertiesDefinition RadarProperties = new RadarPropertiesDefinition();

        /// <summary>
        /// Defines properties for subpart-based movement.
        /// </summary>
        [ProtoContract]
        public class SensorMovementDefinition
        {
            /// <summary>
            /// Azimuth subpart name. Can be anywhere in hierarchy.
            /// </summary>
            [ProtoMember(1)] public string AzimuthPart = "";
            /// <summary>
            /// Elevation subpart name - required for movement. Can be anywhere in hierarchy.
            /// </summary>
            [ProtoMember(2)] public string ElevationPart = "";
            /// <summary>
            /// Minimum azimuth, in radians.
            /// </summary>
            [ProtoMember(3)] public double MinAzimuth = -Math.PI;
            /// <summary>
            /// Maximum azimuth, in radians.
            /// </summary>
            [ProtoMember(4)] public double MaxAzimuth = Math.PI;
            /// <summary>
            /// Minimum elevation, in radians.
            /// </summary>
            [ProtoMember(5)] public double MinElevation = -Math.PI/2;
            /// <summary>
            /// Maximum elevation, in radians.
            /// </summary>
            [ProtoMember(6)] public double MaxElevation = Math.PI;
            /// <summary>
            /// Azimuth rotation rate, in radians per second.
            /// </summary>
            [ProtoMember(7)] public double AzimuthRate = 8 * Math.PI / 60;
            /// <summary>
            /// Elevation rotation rate, in radians per second.
            /// </summary>
            [ProtoMember(8)] public double ElevationRate = 8 * Math.PI / 60;

            [ProtoIgnore] public bool CanRotateFull => MaxAzimuth >= Math.PI && MinAzimuth <= -Math.PI;
            [ProtoIgnore] public bool CanElevateFull => MaxElevation >= Math.PI && MinElevation <= -Math.PI;
        }

        /// <summary>
        /// Defines radar-specific properties for passive and active radars.
        /// </summary>
        [ProtoContract]
        public class RadarPropertiesDefinition
        {
            /// <summary>
            /// Receiver area, in square meters.
            /// </summary>
            [ProtoMember(1)] public double ReceiverArea = 2.5 * 2.5;
            /// <summary>
            /// Multiplier on power output to power input. Only applies to active radars.
            /// </summary>
            [ProtoMember(2)] public double PowerEfficiencyModifier = 2.5E-16;
            /// <summary>
            /// Radar bandwidth. Only applies to active radars.
            /// </summary>
            [ProtoMember(3)] public double Bandwidth = 1.67E6;
            /// <summary>
            /// Radar frequency. Only applies to active radars.
            /// </summary>
            [ProtoMember(4)] public double Frequency = 2800E6;
        }

        [ProtoContract]
        public enum SensorType
        {
            None = 0,
            Radar = 1,
            PassiveRadar = 2,
            Optical = 3,
            Infrared = 4,
        }

        #if MAINMOD
        // TODO: Add new properties
        public static explicit operator SensorDefTuple(SensorDefinition d) => new SensorDefTuple(
                (int) d.Type,
                d.MaxAperture,
                d.MinAperture,
                d.Movement == null ? null : new MyTuple<double, double, double, double, double, double>?(new MyTuple<double, double, double, double, double, double>(
                    d.Movement.MinAzimuth,
                    d.Movement.MaxAzimuth,
                    d.Movement.MinElevation,
                    d.Movement.MaxElevation,
                    d.Movement.AzimuthRate,
                    d.Movement.ElevationRate
                    )),
                d.DetectionThreshold,
                d.MaxPowerDraw
                );

        public static bool Verify(SensorDefinition def)
        {
            bool isValid = true;

            if (def == null)
            {
                Log.Info("SensorDefinition", "Definition null!");
                return false;
            }
            if (def.BlockSubtypes == null || def.BlockSubtypes.Length == 0)
            {
                Log.Info("SensorDefinition", "BlockSubtypes unset!");
                isValid = false;
            }
            if (def.MinAperture > def.MaxAperture || def.MinAperture < 0 || def.MaxAperture < 0)
            {
                Log.Info("SensorDefinition", "Aperture invalid! Make sure both Min and Max are greater than zero, and min is less than max.");
                isValid = false;
            }
            if (def.RadarProperties == null && def.Type == SensorType.Radar)
            {
                Log.Info("SensorDefinition", "Radar properties are null on a radar sensor!");
                isValid = false;
            }
            // TODO: more & better validation

            return isValid;
        }
        #endif
    }
}
