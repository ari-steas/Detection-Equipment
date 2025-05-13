using System;
using GlyphFormatMembers = VRage.MyTuple<byte, float, VRageMath.Vector2I, VRageMath.Color>;

namespace RichHudFramework.UI.Client
{
    public enum TextFieldAccessors
    {
        CharFilterFunc = 16
    }

    /// <summary>
    ///     One-line text field with a configurable input filter delegate. Designed to mimic the appearance of the text field
    ///     in the SE terminal.
    /// </summary>
    public class TerminalTextField : TerminalValue<string>
    {
        public TerminalTextField() : base(MenuControls.TextField)
        {
        }

        /// <summary>
        ///     Restricts the range of characters allowed for input.
        /// </summary>
        public Func<char, bool> CharFilterFunc
        {
            get { return GetOrSetMember(null, (int)TextFieldAccessors.CharFilterFunc) as Func<char, bool>; }
            set { GetOrSetMember(value, (int)TextFieldAccessors.CharFilterFunc); }
        }
    }
}