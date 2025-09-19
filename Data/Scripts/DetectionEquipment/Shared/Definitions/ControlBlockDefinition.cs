using ProtoBuf;
using System;
using System.Collections.Generic;

namespace DetectionEquipment.Shared.Definitions
{
    /// <summary>
    /// Definition for control block logic.
    /// </summary>
    [ProtoContract(UseProtoMembersOnly = true)]
    public class ControlBlockDefinition : DefinitionBase
    {
        /// <summary>
        /// These must be conveyor sorters. Do not put multiple logics on one block.
        /// </summary>
        [ProtoMember(1)] public string[] SubtypeIds;
        [ProtoMember(2)] public LogicType Type;


        [ProtoContract]
        public enum LogicType
        {
            None = 0,
            Aggregator = 1,
            IffAggregator = 2,
            HudController = 3,
            IffReflector = 4,
            Searcher = 5,
            Tracker = 6,
        }

        public override bool Verify(out string reason)
        {
            bool isValid = true;
            reason = "";

            if (SubtypeIds == null || SubtypeIds.Length == 0)
            {
                reason += "Invalid SubtypeId array!\n";
                isValid = false;
            }

            return isValid;
        }

        protected override void AssignDelegates(Dictionary<string, Delegate> delegates)
        {
            // unused
        }

        public override Dictionary<string, Delegate> GenerateDelegates()
        {
            // unused
            return null;
        }
    }
}
