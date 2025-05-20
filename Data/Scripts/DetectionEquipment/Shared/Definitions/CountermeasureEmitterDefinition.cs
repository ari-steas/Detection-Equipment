using System;
using DetectionEquipment.Shared.Utils;
using ProtoBuf;

namespace DetectionEquipment.Shared.Definitions
{
    /// <summary>
    /// Definition for a countermeasure emitter; i.e. the block countermeasures are launched from.
    /// </summary>
    [ProtoContract]
    public class CountermeasureEmitterDefinition
    {
        // can't define preprocessor directives, otherwise would have
        [ProtoIgnore] public int Id; // DO NOT NETWORK THIS!!! Hashcode of the definition name.
        // /// <summary>
        // /// Unique name for this definition.
        // /// </summary>
        // [ProtoIgnore] public string Name;

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
        /// Magazine item consumed on reload.
        /// </summary>
        [ProtoMember(10)] public string MagazineItem;

        /// <summary>
        /// Additive ejection velocity.
        /// </summary>
        [ProtoMember(8)] public float EjectionVelocity;

        /// <summary>
        /// Particle id triggered on firing.
        /// </summary>
        [ProtoMember(9)] public string FireParticle;

        
        public static bool Verify(CountermeasureEmitterDefinition def)
        {
            bool isValid = true;

            if (def == null)
            {
                Log.Info("CountermeasureEmitterDefinition", "Definition null!");
                return false;
            }
            if (def.BlockSubtypes == null || def.BlockSubtypes.Length == 0)
            {
                Log.Info("CountermeasureEmitterDefinition", "BlockSubtypes unset!");
                isValid = false;
            }
            if (def.Muzzles == null || def.Muzzles.Length == 0)
            {
                Log.Info("CountermeasureEmitterDefinition", "Muzzles unset! Defaulting to center of block.");
                def.Muzzles = Array.Empty<string>();
            }
            if (def.CountermeasureIds == null || def.CountermeasureIds.Length == 0)
            {
                Log.Info("CountermeasureEmitterDefinition", "CountermeasureIds unset!");
                isValid = false;
            }

            return isValid;
        }
    }
}
