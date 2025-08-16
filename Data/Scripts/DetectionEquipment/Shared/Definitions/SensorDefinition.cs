using ProtoBuf;
using System;
using System.Collections.Generic;
using DetectionEquipment.Shared.Structs;
using System.ComponentModel;

namespace DetectionEquipment.Shared.Definitions
{
    /// <summary>
    /// Basic definition for a single sensor.
    /// </summary>
    [ProtoContract(UseProtoMembersOnly = true)]
    public class SensorDefinition : DefinitionBase, IPackageable
    {
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
        /// Dummy empty name for the sensor. If blank or invalid, defaults to the elevation subpart.
        /// </summary>
        [ProtoMember(11)] public string SensorEmpty;
        /// <summary>
        /// Name that shows in the sensor's terminal.
        /// </summary>
        [ProtoMember(12)] public string TerminalName;

        /// <summary>
        /// Defines properties for subpart-based movement.
        /// </summary>
        [ProtoContract]
        public class SensorMovementDefinition : IPackageable
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
            /// <summary>
            /// Rest azimuth for this sensor
            /// </summary>
            [ProtoMember(9)] public double HomeAzimuth = 0;
            /// <summary>
            /// Rest elevation for this sensor
            /// </summary>
            [ProtoMember(10)] public double HomeElevation = 0;

            [ProtoIgnore] public bool CanRotateFull => MaxAzimuth >= Math.PI && MinAzimuth <= -Math.PI;
            [ProtoIgnore] public bool CanElevateFull => MaxElevation >= Math.PI && MinElevation <= -Math.PI;


            public int FieldCount => 8;
            public void Package(object[] fieldArray)
            {
                fieldArray[0] = AzimuthPart;
                fieldArray[1] = ElevationPart;
                fieldArray[2] = MinAzimuth;
                fieldArray[3] = MaxAzimuth;
                fieldArray[4] = MinElevation;
                fieldArray[5] = MaxElevation;
                fieldArray[6] = AzimuthRate;
                fieldArray[7] = ElevationRate;
            }
        }

        /// <summary>
        /// Defines radar-specific properties for passive and active radars.
        /// </summary>
        [ProtoContract(UseProtoMembersOnly = true)]
        public class RadarPropertiesDefinition : IPackageable
        {
            /// <summary>
            /// Receiver area, in square meters. Applies to active radar, passive radar, and comms sensors.
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

            /// <summary>
            /// Determines whether the angle of the radar's cone matters for the reciever area in gain calcs.
            /// </summary>
            [ProtoMember(5)] [DefaultValue(true)] public bool AccountForRadarAngle = true;

            public int FieldCount => 5;
            public void Package(object[] fieldArray)
            {
                fieldArray[0] = ReceiverArea;
                fieldArray[1] = PowerEfficiencyModifier;
                fieldArray[2] = Bandwidth;
                fieldArray[3] = Frequency;
                fieldArray[4] = AccountForRadarAngle;
            }
        }

        [ProtoContract]
        public enum SensorType
        {
            None = 0,
            Radar = 1,
            PassiveRadar = 2,
            Optical = 3,
            Infrared = 4,
            Antenna = 5,
        }

        public int FieldCount => 10;
        public void Package(object[] fieldArray)
        {
            fieldArray[0] = BlockSubtypes;
            fieldArray[1] = (int) Type;
            fieldArray[2] = MaxAperture;
            fieldArray[3] = MinAperture;
            fieldArray[4] = ObjectPackager.Package(Movement);
            fieldArray[5] = DetectionThreshold;
            fieldArray[6] = MaxPowerDraw;
            fieldArray[7] = BearingErrorModifier;
            fieldArray[8] = RangeErrorModifier;
            fieldArray[9] = ObjectPackager.Package(RadarProperties);
        }

        public override bool Verify(out string reason)
        {
            bool isValid = true;
            reason = "";

            if (BlockSubtypes == null || BlockSubtypes.Length == 0)
            {
                reason += "BlockSubtypes unset!\n";
                isValid = false;
            }
            if (MinAperture > MaxAperture || MinAperture < 0 || MaxAperture < 0)
            {
                reason += "Aperture invalid! Make sure both Min and Max are greater than zero, and min is less than max.\n";
                isValid = false;
            }
            if (RadarProperties == null && Type == SensorType.Radar)
            {
                reason += "Radar properties are null on a radar sensor!\n";
                isValid = false;
            }

            if (Movement != null)
            {
                if (Movement.AzimuthPart.StartsWith("subpart"))
                {
                    reason += "Azimuth subpart starts with \"subpart_\" - this will likely result in part location failure.\n";
                    isValid = false;
                }
                if (Movement.ElevationPart.StartsWith("subpart"))
                {
                    reason += "Elevation subpart starts with \"subpart_\" - this will likely result in part location failure.\n";
                    isValid = false;
                }
            }

            if (MaxPowerDraw <= 0)
                MaxPowerDraw = 1;
            // TODO: more & better validation

            return isValid;
        }

        protected override void AssignDelegates(Dictionary<string, Delegate> delegates)
        {
            // no delegates to assign
        }

        public override Dictionary<string, Delegate> GenerateDelegates()
        {
            return null;
        }
    }
}
