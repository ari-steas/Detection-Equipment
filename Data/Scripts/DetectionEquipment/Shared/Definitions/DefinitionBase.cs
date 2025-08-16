using ProtoBuf;
using System;
using System.Collections.Generic;

namespace DetectionEquipment.Shared.Definitions
{
    public abstract class DefinitionBase
    {
        // can't define preprocessor directives, otherwise would have excluded this
        /// <summary>
        /// Unique ID for this definition.
        /// </summary>
        [ProtoIgnore] public int Id { get; private set; } // DO NOT NETWORK THIS!!! Hashcode of the definition name.
        /// <summary>
        /// Unique name for this definition.
        /// </summary>
        [ProtoIgnore] public string Name { get; private set; }

        public void Init<TDefinition>(string defName) where TDefinition : DefinitionBase
        {
            Name = defName;
            Id = Name.GetHashCode();
            RetrieveDelegates<TDefinition>();
        }

        public abstract bool Verify(out string reason);

        protected abstract void AssignDelegates(Dictionary<string, Delegate> delegates);

        public abstract Dictionary<string, Delegate> GenerateDelegates();

        public void RetrieveDelegates<TDefinition>() where TDefinition : DefinitionBase
        {
            var delSet = DefinitionManager.DefinitionApi.GetDelegates<TDefinition>(Name);
            if (delSet == null)
                return;
            AssignDelegates(delSet);
        }

        protected static void AssignDelegate<TDelegate>(Dictionary<string, Delegate> delMap, string key, out TDelegate del) where TDelegate : class
        {
            Delegate mapDelegate;
            if (delMap == null || !delMap.TryGetValue(key, out mapDelegate) || mapDelegate == null)
            {
                del = null;
                return;
            }

            if (mapDelegate.GetType() != typeof(TDelegate))
                throw new Exception($"Delegate {key} type mismatch! [MapDelegate: {mapDelegate.GetType().Name} | ApiDelegate: {typeof(TDelegate).Name}]");
            del = mapDelegate as TDelegate;
        }
    }
}
