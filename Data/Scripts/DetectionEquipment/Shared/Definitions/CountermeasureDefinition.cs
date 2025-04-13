using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DetectionEquipment.Shared.Definitions
{
    [ProtoContract]
    public class CountermeasureDefinition
    {
        [ProtoIgnore] public int Id; // DO NOT NETWORK THIS!!! Hashcode of the definition name.

        /// <summary>
        /// Sensor types this countermeasure affects.
        /// </summary>
        [ProtoMember(1)] public CountermeasureTypeEnum CountermeasureType;
        /// <summary>
        /// Longest range at which to affect sensors.
        /// </summary>
        [ProtoMember(2)] public float MaxRange;
        /// <summary>
        /// When not linear, noise scales by the formula [noise = -FalloffScalar / Range^FalloffType + MinNoise].<br/>
        /// When linear, noise scales by the formula [noise = FalloffScalar * (MaxRange - Range) + MinNoise].<br/>
        /// Can be negative if you want to be silly.
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
        [ProtoMember(6)] public float MinEffectAperture;
        /// <summary>
        /// Maximum aperture cone radius, in radians.
        /// </summary>
        [ProtoMember(6)] public float MaxEffectAperture;
        /// <summary>
        /// Ticks this countermeasure should last for. Set to -1 for guaranteed attached countermeasures.
        /// </summary>
        [ProtoMember(8)] public uint MaxLifetime;
        /// <summary>
        /// Whether this countermeasure should be affected by velocity, gravity, and drag.
        /// </summary>
        [ProtoMember(9)] public bool HasPhysics;
        /// <summary>
        /// Drag multiplier if <see cref="HasPhysics"/> is true. Can be negative if you want to be silly.
        /// </summary>
        [ProtoMember(10)] public float DragMultiplier;
        /// <summary>
        /// Continuous particle effect id
        /// </summary>
        [ProtoMember(11)] public string ParticleEffect;

        [Flags]
        public enum CountermeasureTypeEnum
        {
            None = 0,
            Radar = 1,
            Optical = 2,
            Infrared = 4,
        }

        public enum FalloffTypeEnum
        {
            None = 0,
            Linear = 1,
            Quadratic = 2,
        }
    }
}
