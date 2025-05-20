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
            // TODO this is stupid, make it cleaner.
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
                MyEntitySubpart subpart;
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
            if (entity == null)
                return null;
            var myEnt = (MyEntity)entity;
            if (myEnt.Subparts.Count == 0)
                return GetSubpart(entity, name);

            var toCheck = new Queue<MyEntity>();
            toCheck.Enqueue(myEnt);

            while (toCheck.Count > 0)
            {
                var next = toCheck.Dequeue();
                var chk = GetSubpart(next, name);
                if (chk != null)
                    return chk;
                foreach (var item in next.Subparts.Values)
                    toCheck.Enqueue(item);
            }

            return null;
        }

        /// <summary>
        /// Returns a list of every subpart below this entity in its hierarchy.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static List<MyEntitySubpart> GetAllSubparts(IMyEntity entity)
        {
            if (entity == null)
                return new List<MyEntitySubpart>(0);
            var myEnt = (MyEntity)entity;
            if (myEnt.Subparts.Count == 0)
                return myEnt.Subparts.Values.ToList();

            var subparts = new List<MyEntitySubpart>();
            var toCheck = new Queue<MyEntity>();
            toCheck.Enqueue(myEnt);

            while (toCheck.Count > 0)
            {
                var all = toCheck.Dequeue().Subparts.Values;
                foreach (var item in all)
                {
                    subparts.Add(item);
                    toCheck.Enqueue(item);
                }
            }

            return subparts;
        }

        /// <summary>
        /// Returns a list of every subpart below this entity in its hierarchy.
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static Dictionary<string, MyEntitySubpart> GetAllSubpartsDict(IMyEntity entity)
        {
            if (entity == null)
                return new Dictionary<string, MyEntitySubpart>(0);
            var myEnt = (MyEntity)entity;
            if (myEnt.Subparts.Count == 0)
                return myEnt.Subparts;

            var subparts = new Dictionary<string, MyEntitySubpart>();
            var toCheck = new Queue<MyEntity>();
            toCheck.Enqueue(myEnt);

            while (toCheck.Count > 0)
            {
                var all = toCheck.Dequeue().Subparts;
                foreach (var item in all)
                {
                    subparts[item.Key] = item.Value;
                    toCheck.Enqueue(item.Value);
                }
            }

            return subparts;
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
