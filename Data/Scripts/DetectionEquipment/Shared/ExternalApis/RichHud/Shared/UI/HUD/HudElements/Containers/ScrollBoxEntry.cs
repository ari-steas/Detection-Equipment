namespace RichHudFramework.UI
{
    /// <summary>
    ///     Base container class for scrollbox members. Can be extended to associate data with ScrollBox
    ///     elements.
    /// </summary>
    public class ScrollBoxEntry<TElement> : HudElementContainer<TElement>, IScrollBoxEntry<TElement>
        where TElement : HudElementBase
    {
        public ScrollBoxEntry()
        {
            Enabled = true;
        }

        public virtual bool Enabled { get; set; }
    }

    /// <summary>
    ///     Base container class for scrollbox members. Can be extended to associate data with ScrollBox
    ///     elements.
    /// </summary>
    public class ScrollBoxEntry : ScrollBoxEntry<HudElementBase>
    {
    }
}