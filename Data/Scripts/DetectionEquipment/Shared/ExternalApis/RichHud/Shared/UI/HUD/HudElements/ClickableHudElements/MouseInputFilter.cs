using System.Collections.Generic;
using VRageMath;

namespace RichHudFramework.UI
{
    /// <summary>
    ///     Filters cursor input to the elements behind it. If any bind in its list is pressed, then cursor
    ///     input to those elements will be blocked.
    /// </summary>
    public class MouseInputFilter : HudElementBase
    {
        public MouseInputFilter(HudParentBase parent) : base(parent)
        {
            UseCursor = true;
            ShareCursor = true;
        }

        public MouseInputFilter() : this(null)
        {
        }

        /// <summary>
        ///     True if any bind in the bind list is pressed
        /// </summary>
        public bool IsControlPressed { get; private set; }

        /// <summary>
        ///     List of binds used in filtering input
        /// </summary>
        public IReadOnlyList<IBind> Binds { get; set; }

        protected override void HandleInput(Vector2 cursorPos)
        {
            IsControlPressed = false;

            if (IsMousedOver && Binds != null)
                for (var n = 0; n < Binds.Count; n++)
                    if (Binds[n].IsPressed)
                    {
                        IsControlPressed = true;
                        break;
                    }

            ShareCursor = !IsControlPressed;
        }
    }
}