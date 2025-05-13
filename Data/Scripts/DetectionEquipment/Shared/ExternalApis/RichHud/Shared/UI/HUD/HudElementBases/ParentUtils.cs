namespace RichHudFramework
{
    namespace UI
    {
        public abstract partial class HudParentBase
        {
            /// <summary>
            ///     Utilities used internally to access parent node members
            /// </summary>
            protected static class ParentUtils
            {
                /// <summary>
                ///     Calculates the full z-offset using the public offset and inner offset.
                /// </summary>
                public static ushort GetFullZOffset(HudLayerData nodeData, HudParentBase parent = null)
                {
                    var outerOffset = (byte)(nodeData.zOffset - sbyte.MinValue);
                    var innerOffset = (ushort)(nodeData.zOffsetInner << 8);

                    if (parent != null)
                    {
                        var parentFull = parent.layerData.fullZOffset;

                        outerOffset += (byte)((parentFull & 0x00FF) + sbyte.MinValue);
                        innerOffset += (ushort)(parentFull & 0xFF00);
                    }

                    return (ushort)(innerOffset | outerOffset);
                }
            }
        }
    }
}