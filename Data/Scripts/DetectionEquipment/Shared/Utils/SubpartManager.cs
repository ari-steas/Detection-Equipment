using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Shared.Utils
{
    public class SubpartManager
    {
        private readonly Dictionary<IMyEntity, Dictionary<string, MyEntitySubpart>> _cachedSubparts = new Dictionary<IMyEntity, Dictionary<string, MyEntitySubpart>>();

        public MyEntitySubpart GetSubpart(IMyEntity entity, string name)
        {
            if (entity == null) return null;

            // Add entity if missing
            if (!_cachedSubparts.ContainsKey(entity))
                _cachedSubparts.Add(entity, new Dictionary<string, MyEntitySubpart>());

            // Check if subpart is cached
            if (!_cachedSubparts[entity].ContainsKey(name))
            {
                MyEntitySubpart subpart;
                entity.TryGetSubpart(name, out subpart);
                if (subpart != null)
                    _cachedSubparts[entity].Add(name, subpart);
                else
                    return null;
            }

            // Return subpart
            if (_cachedSubparts[entity][name] == null)
            {
                MyEntitySubpart subpart = null;
                entity.TryGetSubpart(name, out subpart);

                if (_cachedSubparts[entity][name] == null)
                {
                    _cachedSubparts[entity].Remove(name);
                    return null;
                }
                else
                    _cachedSubparts[entity][name] = subpart;
            }

            return _cachedSubparts[entity][name];
        }

        /// <summary>
        /// Recursively find subparts.
        /// </summary>
        /// <param name="entity"></param>
        /// <param name="name"></param>
        /// <returns></returns>
        public MyEntitySubpart RecursiveGetSubpart(IMyEntity entity, string name)
        {
            if (entity == null) return null;

            MyEntitySubpart desiredSubpart = GetSubpart(entity, name);
            if (desiredSubpart == null)
                foreach (var subpart in ((MyEntity)entity).Subparts.Values)
                    return RecursiveGetSubpart(subpart, name);
            return desiredSubpart;
        }

        public static List<MyEntitySubpart> GetAllSubparts(IMyEntity entity)
        {
            return entity == null ? new List<MyEntitySubpart>(0) : ((MyEntity)entity).Subparts.Values.ToList();
        }

        public void LocalRotateSubpart(MyEntitySubpart subpart, Matrix matrix)
        {
            Matrix refMatrix = matrix * subpart.PositionComp.LocalMatrixRef;
            refMatrix.Translation = subpart.PositionComp.LocalMatrixRef.Translation;
            subpart.PositionComp.SetLocalMatrix(ref refMatrix);
        }
        public void LocalRotateSubpartAbs(MyEntitySubpart subpart, Matrix matrix)
        {
            matrix.Translation = subpart.PositionComp.LocalMatrixRef.Translation;
            subpart.PositionComp.SetLocalMatrix(ref matrix);
        }
    }
}
