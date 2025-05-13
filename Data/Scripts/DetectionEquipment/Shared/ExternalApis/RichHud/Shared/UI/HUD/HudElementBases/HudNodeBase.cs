using System;
using System.Collections.Generic;
using RichHudFramework.Internal;
using VRage;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    namespace UI
    {
        using HudUpdateAccessors = MyTuple<
            ApiMemberAccessor,
            MyTuple<Func<ushort>, Func<Vector3D>>, // ZOffset + GetOrigin
            Action, // DepthTest
            Action, // HandleInput
            Action<bool>, // BeforeLayout
            Action // BeforeDraw
        >;

        /// <summary>
        ///     Base class for hud elements that can be parented to other elements.
        /// </summary>
        public abstract partial class HudNodeBase : HudParentBase, IReadOnlyHudNode
        {
            protected const HudElementStates nodeVisible =
                    HudElementStates.IsVisible | HudElementStates.WasParentVisible | HudElementStates.IsRegistered,
                nodeInputEnabled = HudElementStates.IsInputEnabled | HudElementStates.WasParentInputEnabled;

            protected const int maxPreloadDepth = 5;

            protected HudParentBase _parent;

            public HudNodeBase(HudParentBase parent)
            {
                State &= ~HudElementStates.IsRegistered;
                ParentVisible = true;

                Register(parent);
            }

            /// <summary>
            ///     Parent object of the node.
            /// </summary>
            public virtual HudParentBase Parent
            {
                get { return _parent; }
                protected set { _parent = value; }
            }

            protected bool ParentVisible
            {
                get { return (State & HudElementStates.WasParentVisible) > 0; }
                set
                {
                    if (value)
                        State |= HudElementStates.WasParentVisible;
                    else
                        State &= ~HudElementStates.WasParentVisible;
                }
            }

            /// <summary>
            ///     Read-only parent object of the node.
            /// </summary>
            IReadOnlyHudParent IReadOnlyHudNode.Parent => _parent;

            /// <summary>
            ///     Determines whether or not an element will be drawn or process input. Visible by default.
            /// </summary>
            public override bool Visible => (State & nodeVisible) == nodeVisible;

            /// <summary>
            ///     Returns true if input is enabled can update
            /// </summary>
            public override bool InputEnabled => (State & nodeInputEnabled) == nodeInputEnabled;

            /// <summary>
            ///     Indicates whether or not the element has been registered to a parent.
            /// </summary>
            public bool Registered => (State & HudElementStates.IsRegistered) > 0;

            /// <summary>
            ///     Adds update delegates for members in the order dictated by the UI tree
            /// </summary>
            public override void GetUpdateAccessors(List<HudUpdateAccessors> UpdateActions, byte preloadDepth)
            {
                var wasSetVisible = (State & HudElementStates.IsVisible) > 0;
                State |= HudElementStates.WasParentVisible;

                if (!wasSetVisible && (State & HudElementStates.CanPreload) > 0)
                    preloadDepth++;

                if (preloadDepth < maxPreloadDepth && (State & HudElementStates.CanPreload) > 0)
                    State |= HudElementStates.IsVisible;

                if (Visible)
                {
                    HudSpace = _parent?.HudSpace;
                    layerData.fullZOffset = ParentUtils.GetFullZOffset(layerData, _parent);

                    UpdateActions.EnsureCapacity(UpdateActions.Count + children.Count + 1);
                    accessorDelegates.Item2.Item2 = HudSpace.GetNodeOriginFunc;

                    UpdateActions.Add(accessorDelegates);
                    ;

                    for (var n = 0; n < children.Count; n++)
                        children[n].GetUpdateAccessors(UpdateActions, preloadDepth);
                }

                if (!wasSetVisible)
                    State &= ~HudElementStates.IsVisible;
            }

            /// <summary>
            ///     Starts input update in a try-catch block. Useful for manually updating UI elements.
            ///     Exceptions are reported client-side. Do not override this unless you have a good reason for it.
            ///     If you need to update input, use HandleInput().
            /// </summary>
            public override void BeginInput()
            {
                if (!ExceptionHandler.ClientsPaused)
                    try
                    {
                        State &= ~HudElementStates.WasParentInputEnabled;

                        if (_parent != null)
                            State |= _parent.InputEnabled
                                ? HudElementStates.WasParentInputEnabled
                                : HudElementStates.None;

                        if (Visible && InputEnabled)
                        {
                            var cursorPos = HudSpace.CursorPos;
                            HandleInput(new Vector2(cursorPos.X, cursorPos.Y));
                        }
                    }
                    catch (Exception e)
                    {
                        ExceptionHandler.ReportException(e);
                    }
            }

            /// <summary>
            ///     Updates layout for the element and its children. Overriding this method is rarely necessary.
            ///     If you need to update layout, use Layout().
            /// </summary>
            public override void BeginLayout(bool refresh)
            {
                if (!ExceptionHandler.ClientsPaused)
                    try
                    {
                        layerData.fullZOffset = ParentUtils.GetFullZOffset(layerData, _parent);

                        if (_parent == null)
                            ParentVisible = false;
                        else
                            ParentVisible = _parent.Visible;

                        if (Visible || refresh)
                            Layout();
                    }
                    catch (Exception e)
                    {
                        ExceptionHandler.ReportException(e);
                    }
            }

            /// <summary>
            ///     Registers the element to the given parent object.
            /// </summary>
            /// <param name="canPreload">
            ///     Indicates whether or not the element's accessors can be loaded into the update tree
            ///     before the element is visible. Useful for preventing flicker in scrolling lists.
            /// </param>
            public virtual bool Register(HudParentBase newParent, bool canPreload = false)
            {
                if (newParent == this)
                    throw new Exception("Types of HudNodeBase cannot be parented to themselves!");

                if (newParent != null)
                {
                    Parent = newParent;

                    if (_parent.RegisterChild(this))
                        State |= HudElementStates.IsRegistered;
                    else
                        State &= ~HudElementStates.IsRegistered;
                }

                if ((State & HudElementStates.IsRegistered) > 0)
                {
                    if (canPreload)
                        State |= HudElementStates.CanPreload;
                    else
                        State &= ~HudElementStates.CanPreload;

                    return true;
                }

                return false;
            }

            /// <summary>
            ///     Unregisters the element from its parent, if it has one.
            /// </summary>
            public virtual bool Unregister()
            {
                if (Parent != null)
                {
                    var lastParent = Parent;
                    Parent = null;

                    lastParent.RemoveChild(this);
                    State &= ~(HudElementStates.IsRegistered | HudElementStates.WasParentVisible);
                }

                return !((State & HudElementStates.IsRegistered) > 0);
            }
        }
    }
}