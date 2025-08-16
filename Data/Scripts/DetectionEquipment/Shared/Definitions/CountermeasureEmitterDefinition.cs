using DetectionEquipment.Shared.Utils;
using ProtoBuf;
using Sandbox.Definitions;
using System;
using System.Collections.Generic;
using System.Data;
using VRage.Game;
using VRageMath;

namespace DetectionEquipment.Shared.Definitions
{
    /// <summary>
    /// Definition for a countermeasure emitter; i.e. the block countermeasures are launched from.
    /// </summary>
    [ProtoContract]
    public class CountermeasureEmitterDefinition : DefinitionBase
    {
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

        /// <summary>
        /// Power draw while active, in megawatts.
        /// </summary>
        [ProtoMember(11)] public float ActivePowerDraw;

        /// <summary>
        /// Inventory size, kiloliters
        /// </summary>
        [ProtoMember(12)] public float InventorySize;

        
        [ProtoIgnore] public MyDefinitionId MagazineItemDefinition;

        public override bool Verify(out string reason)
        {
            bool isValid = true;
            reason = "";

            if (BlockSubtypes == null || BlockSubtypes.Length == 0)
            {
                reason += "BlockSubtypes unset!\n";
                isValid = false;
            }
            if (Muzzles == null || Muzzles.Length == 0)
            {
                reason += "Muzzles unset! Defaulting to center of block.\n";
                Muzzles = Array.Empty<string>();
            }
            if (CountermeasureIds == null || CountermeasureIds.Length == 0)
            {
                reason += "CountermeasureIds unset!\n";
                isValid = false;
            }
            if (string.IsNullOrEmpty(MagazineItem))
            {
                reason += "MagazineItem unset! Defaulting to no item.\n";
            }

            if (InventorySize > 0 && BlockSubtypes != null)
            {
                bool didSetItemDef = string.IsNullOrEmpty(MagazineItem);

                // this is pretty silly but it works
                var inventorySize = new Vector3((float)Math.Pow(InventorySize, 1 / 3d));

                int remainingSubtypes = BlockSubtypes.Length;
                foreach (var gameDef in MyDefinitionManager.Static.GetAllDefinitions())
                {
                    if (!didSetItemDef && gameDef.Id.SubtypeName == MagazineItem)
                    {
                        MagazineItemDefinition = gameDef.Id;
                        didSetItemDef = true;
                        continue;
                    }

                    var sorterDef = gameDef as MyConveyorSorterDefinition;
                    if (sorterDef == null || !BlockSubtypes.Contains(sorterDef.Id.SubtypeName))
                        continue;

                    sorterDef.InventorySize = inventorySize;
                    if (--remainingSubtypes <= 0 && didSetItemDef)
                        break;
                }

                if (!didSetItemDef)
                {
                    reason += $"Failed to find magazine item definition \"{MagazineItem}\"!\n";
                    isValid = false;
                }
            }

            if (ActivePowerDraw <= 0)
                ActivePowerDraw = 0.000001f;

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
