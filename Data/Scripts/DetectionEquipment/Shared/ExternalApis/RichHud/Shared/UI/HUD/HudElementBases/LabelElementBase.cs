using RichHudFramework.UI.Rendering;

namespace RichHudFramework
{
    namespace UI
    {
        public abstract class LabelElementBase : HudElementBase, IMinLabelElement
        {
            public LabelElementBase(HudParentBase parent = null) : base(parent)
            {
            }

            /// <summary>
            ///     TextBoard backing the label element.
            /// </summary>
            public abstract ITextBoard TextBoard { get; }
        }
    }
}