using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;

namespace DetectionEquipment.Shared.Definitions
{
    [ProtoContract]
    public class CountermeasureEmitterDefinition
    {
        [ProtoIgnore] public int Id; // DO NOT NETWORK THIS!!! Hashcode of the definition name.

        /// <summary>
        /// Subtypes this emitter is attached to.
        /// </summary>
        [ProtoMember(1)] public string[] BlockSubtypes;

        /// <summary>
        /// Muzzle dummies to fire countermeasures from. Resets on reload. Uses center of block if none specified.
        /// </summary>
        [ProtoMember(2)] public string[] Muzzles;

        /// <summary>
        /// ID strings for countermeasures to use. Fires in the order here.
        /// </summary>
        [ProtoMember(3)] public string[] CountermeasureIds;

        /// <summary>
        /// Should countermeasures be "stuck" to this emitter?
        /// </summary>
        [ProtoMember(4)] public bool IsCountermeasureAttached;

        /// <summary>
        /// Fractional shots per second. Make sure this is greater than 0.
        /// </summary>
        [ProtoMember(5)] public float ShotsPerSecond;

        /// <summary>
        /// Number of shots in the magazine. Set less than or equal to 0 to ignore.
        /// </summary>
        [ProtoMember(6)] public int MagazineSize;

        /// <summary>
        /// Reload time. Set to less than or equal to 1/60f to ignore.
        /// </summary>
        [ProtoMember(7)] public float ReloadTime;

        /// <summary>
        /// Additive ejection velocity.
        /// </summary>
        [ProtoMember(8)] public float EjectionVelocity;

        /// <summary>
        /// Particle id triggered on firing.
        /// </summary>
        [ProtoMember(9)] public string FireParticle;


        [ProtoIgnore] public bool ContinuousMagazine => MagazineSize <= 0 || ReloadTime <= 1/60f;
    }
}
