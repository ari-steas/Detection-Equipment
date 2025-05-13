using System.Collections;
using System.Collections.Generic;
using VRage;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;
using ApiMemberAccessor = System.Func<object, int, object>;
using BindDefinitionData = VRage.MyTuple<string, string[]>;

namespace RichHudFramework
{
    namespace UI.Client
    {
        /// <summary>
        ///     Scrollable list of bind group controls.
        /// </summary>
        public class RebindPage : TerminalPageBase, IRebindPage
        {
            private readonly List<IBindGroup> bindGroups;

            public RebindPage() : base(ModPages.RebindPage)
            {
                bindGroups = new List<IBindGroup>();
            }

            public RebindPage GroupContainer => this;

            /// <summary>
            ///     List of bind groups registered to the page.
            /// </summary>
            public IReadOnlyList<IBindGroup> BindGroups => bindGroups;

            /// <summary>
            ///     Adds the given bind group to the page.
            /// </summary>
            public void Add(IBindGroup bindGroup)
            {
                GetOrSetMemberFunc(bindGroup.ID, (int)RebindPageAccessors.Add);
                bindGroups.Add(bindGroup);
            }

            /// <summary>
            ///     Adds the given bind group to the page along with its associated default configuration.
            /// </summary>
            public void Add(IBindGroup bindGroup, BindDefinition[] defaultBinds)
            {
                var data = new BindDefinitionData[defaultBinds.Length];

                for (var n = 0; n < defaultBinds.Length; n++)
                    data[n] = defaultBinds[n];

                GetOrSetMemberFunc(new MyTuple<object, BindDefinitionData[]>(bindGroup.ID, data),
                    (int)RebindPageAccessors.Add);
                bindGroups.Add(bindGroup);
            }

            public IEnumerator<IBindGroup> GetEnumerator()
            {
                return bindGroups.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return bindGroups.GetEnumerator();
            }
        }
    }
}