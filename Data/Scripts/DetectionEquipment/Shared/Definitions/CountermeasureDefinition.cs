using ProtoBuf;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage;
using VRageMath;

namespace DetectionEquipment.Shared.Definitions
{
    /// <summary>
    /// Definition for a countermeasure; i.e. an object that adds noise to sensors that can see it.
    /// </summary>
    [ProtoContract]
    public class CountermeasureDefinition : DefinitionBase
    {
        /// <summary>
        /// Sensor types this countermeasure affects.
        /// </summary>
        [ProtoMember(1)] public CountermeasureTypeEnum CountermeasureType;
        /// <summary>
        /// Longest range at which to affect sensors. Set to zero to disable noise.
        /// </summary>
        [ProtoMember(2)] public float MaxRange;
        /// <summary>
        /// When not linear, noise scales by the formula [noise = -FalloffScalar / Range^FalloffType + MinNoise].<br/>
        /// When linear, noise scales by the formula [noise = FalloffScalar * (MaxRange - Range) + MinNoise].<br/>
        /// Can be negative if you want to be silly. Set to zero to disable noise.
        /// </summary>
        [ProtoMember(3)] public float FalloffScalar;
        /// <summary>
        /// Sensor noise at maximum range.
        /// </summary>
        [ProtoMember(4)] public float MinNoise;
        /// <summary>
        /// Falloff rate - When not linear, noise scales by the formula [noise = -FalloffScalar / Range^FalloffType + MinNoise].<br/>
        /// When linear, noise scales by the formula [noise = FalloffScalar * (MaxRange - Range) + MinNoise]
        /// </summary>
        [ProtoMember(5)] public FalloffTypeEnum FalloffType;
        /// <summary>
        /// Minimum aperture cone radius, in radians.
        /// </summary>
        [ProtoMember(6)] public float MinEffectAperture; // TODO add functionality
        /// <summary>
        /// Maximum aperture cone radius, in radians. Default value for aperture.
        /// </summary>
        [ProtoMember(7)] public float MaxEffectAperture;
        /// <summary>
        /// Ticks this countermeasure should last for. Set to uint.MaxValue for guaranteed attached countermeasures.
        /// </summary>
        [ProtoMember(8)] public uint MaxLifetime;
        /// <summary>
        /// Whether this countermeasure should be affected by velocity, gravity, and drag.
        /// </summary>
        [ProtoMember(9)] public bool HasPhysics;
        /// <summary>
        /// Drag multiplier if <see cref="HasPhysics"/> is true and in atmosphere. Can be negative if you want to be silly.
        /// </summary>
        [ProtoMember(10)] public float DragMultiplier;
        /// <summary>
        /// Continuous particle effect id
        /// </summary>
        [ProtoMember(11)] public string ParticleEffect;
        /// <summary>
        /// Maximum range at which to invoke <see cref="DrfmEffects"/>. Set this as low as possible to save on performance. Independent of <see cref="MaxRange"/>, but affected by aperture.
        /// </summary>
        [ProtoMember(12)] public float MaxDrfmRange;
        /// <summary>
        /// If true, invokes DRFM on targets this countermeasure is not attached to. Saves performance if false.
        /// </summary>
        [ProtoMember(13)] public bool ApplyDrfmToOtherTargets;
        /// <summary>
        /// If true, noise and DRFM apply to any sensor within range and aperture of this countermeasure regardless of its own visibility.
        /// </summary>
        [ProtoMember(14)] public bool ApplyOutsideSensorCone;

        // why can't I just use global usings, or better yet custom delegates, or even better yet just invoke the function directly
        // one day keen shall pay for their sins

        /// <summary>
        /// Allows modifying sensor returns directly. Leave this null if unused.<br/>
        /// In: SensorId, CountermeasureId, AttachedEmitter?, TgtEntityId, TgtCrossSection, TgtRange, TgtMaxRangeErr, TgtBearing, TgtMaxBearingError, TgtIffCodes<br/>
        /// Out: CrossSectionOffset, RangeOffset, MaxRangeErrOffset, NewBearing, MaxBearingErrOffset, NewIffCodes
        /// </summary>
        public Func<uint, uint, IMyFunctionalBlock, long, double, double, double, Vector3D, double, string[], MyTuple<double, double, double, Vector3D, double, string[]>> DrfmEffects = null;

        /// <summary>
        /// Allows generating new sensor returns directly. Leave null if unused.<br/>
        /// In: SensorId, SensorBlock, CountermeasureId, AttachedEmitter?<br/>
        /// Out: TrackId, CrossSection, Vector2D(Range, RangeErr), Bearing, BearingErr, IffCodes
        /// </summary>
        public Func<uint, IMyFunctionalBlock, uint, IMyFunctionalBlock, IEnumerable<MyTuple<long, double, Vector2D, Vector3D, double, string[]>>> DrfmGenerator = null;

        [Flags]
        public enum CountermeasureTypeEnum
        {
            None = 0,
            Radar = 1,
            Optical = 2,
            Infrared = 4,
            /// <summary>
            /// Note - this only applies to SENSORS and has no effect on IGC.
            /// </summary>
            Antenna = 5,
        }

        public enum FalloffTypeEnum
        {
            None = 0,
            Linear = 1,
            Quadratic = 2,
        }

        public override bool Verify(out string reason)
        {
            bool isValid = true;
            reason = "";

            if (CountermeasureType == CountermeasureTypeEnum.None)
            {
                reason += "CountermeasureType is undefined!\n";
                isValid = false;
            }

            return isValid;
        }

        protected override void AssignDelegates(Dictionary<string, Delegate> delegates)
        {
            AssignDelegate(delegates, "DrfmEffects", out DrfmEffects);
            AssignDelegate(delegates, "DrfmGenerator", out DrfmGenerator);
        }

        public override Dictionary<string, Delegate> GenerateDelegates()
        {
            return new Dictionary<string, Delegate>
            {
                ["DrfmEffects"] = DrfmEffects,
                ["DrfmGenerator"] = DrfmGenerator,
            };
        }
    }
}
