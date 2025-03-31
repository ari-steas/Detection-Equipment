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
        [ProtoIgnore] public int Id; // DO NOT NETWORK THIS!!!
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

        [ProtoContract]
        public class SensorMovementDefinition
        {
            /// <summary>
            /// Azimuth subpart name. Can be anywhere in heirarchy.
            /// </summary>
            [ProtoMember(1)] public string AzimuthPart = "";
            /// <summary>
            /// Elevation subpart name - required for movement. Can be anywhere in heirarchy.
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

        public enum SensorType
        {
            None = 0,
            Radar = 1,
            PassiveRadar = 2,
            Optical = 3,
            Infrared = 4,
        }

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
    }
}
