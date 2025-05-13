using System.Collections.Generic;

namespace RichHudFramework
{
    namespace UI
    {
        public enum RebindPageAccessors
        {
            Add = 10
        }

        public interface IRebindPage : ITerminalPage, IEnumerable<IBindGroup>
        {
            /// <summary>
            ///     Bind groups registered to the rebind page.
            /// </summary>
            IReadOnlyList<IBindGroup> BindGroups { get; }

            /// <summary>
            ///     Adds the given bind group to the page.
            /// </summary>
            void Add(IBindGroup bindGroup);

            /// <summary>
            ///     Adds the given bind group to the page along with its associated default configuration.
            /// </summary>
            void Add(IBindGroup bindGroup, BindDefinition[] defaultBinds);
        }
    }
}