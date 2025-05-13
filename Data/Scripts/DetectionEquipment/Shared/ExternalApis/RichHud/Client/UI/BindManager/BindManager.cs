using System;
using System.Collections.Generic;
using RichHudFramework.Client;
using VRage;
using VRage.Input;
using VRageMath;
using ApiMemberAccessor = System.Func<object, int, object>;

namespace RichHudFramework
{
    namespace UI.Client
    {
        using BindClientMembers = MyTuple<
            ApiMemberAccessor, // GetOrSetMember
            MyTuple<Func<int, object, int, object>, Func<int>>, // GetOrSetGroupMember, GetGroupCount
            MyTuple<Func<Vector2I, object, int, object>, Func<int, int>>, // GetOrSetBindMember, GetBindCount
            Func<Vector2I, int, bool>, // IsBindPressed
            MyTuple<Func<int, int, object>, Func<int>>, // GetControlMember, GetControlCount
            Action // Unload
        >;

        /// <summary>
        ///     Manages custom keybinds; singleton
        /// </summary>
        public sealed partial class BindManager : RichHudClient.ApiModule<BindClientMembers>
        {
            private static BindManager _instance;

            private static SeBlacklistModes lastBlacklist, tmpBlacklist;
            private readonly ReadOnlyApiCollection<IControl> controls;
            private readonly Func<int, int> GetBindCountFunc;
            private readonly Func<int> GetControlCountFunc;

            // Control list
            private readonly Func<int, int, object> GetControlMember;
            private readonly Func<int> GetGroupCountFunc;

            // Bind lists
            private readonly Func<Vector2I, object, int, object> GetOrSetBindMemberFunc;

            // Group list
            private readonly Func<int, object, int, object> GetOrSetGroupMemberFunc;

            private readonly ApiMemberAccessor GetOrSetMemberFunc;

            private readonly ReadOnlyApiCollection<IBindGroup> groups;
            private readonly Func<Vector2I, int, bool> IsBindPressedFunc;
            private readonly Action UnloadAction;

            private BindManager() : base(ApiModuleTypes.BindManager, false, true)
            {
                var clientData = GetApiData();

                GetOrSetMemberFunc = clientData.Item1;
                UnloadAction = clientData.Item6;

                // Group list
                GetOrSetGroupMemberFunc = clientData.Item2.Item1;
                GetGroupCountFunc = clientData.Item2.Item2;

                // Bind lists
                IsBindPressedFunc = clientData.Item4;
                GetOrSetBindMemberFunc = clientData.Item3.Item1;
                GetBindCountFunc = clientData.Item3.Item2;

                // Control list
                GetControlMember = clientData.Item5.Item1;
                GetControlCountFunc = clientData.Item5.Item2;

                groups = new ReadOnlyApiCollection<IBindGroup>(x => new BindGroup(x), GetGroupCountFunc);
                controls = new ReadOnlyApiCollection<IControl>(x => new Control(x), GetControlCountFunc);
            }

            /// <summary>
            ///     Read-only collection of bind groups registered
            /// </summary>
            public static IReadOnlyList<IBindGroup> Groups => Instance.groups;

            /// <summary>
            ///     Read-only collection of all available controls for use with key binds
            /// </summary
            public static IReadOnlyList<IControl> Controls => Instance.controls;

            /// <summary>
            ///     Specifies blacklist mode for SE controls
            /// </summary>
            public static SeBlacklistModes BlacklistMode
            {
                get
                {
                    if (_instance == null) Init();
                    return (SeBlacklistModes)_instance.GetOrSetMemberFunc(null,
                        (int)BindClientAccessors.RequestBlacklistMode);
                }
                set
                {
                    if (_instance == null)
                        Init();

                    lastBlacklist = value;
                    _instance.GetOrSetMemberFunc(value, (int)BindClientAccessors.RequestBlacklistMode);
                }
            }

            /// <summary>
            ///     MyAPIGateway.Gui.ChatEntryVisible, but actually usable for input polling
            /// </summary>
            public static bool IsChatOpen =>
                (bool)_instance.GetOrSetMemberFunc(null, (int)BindClientAccessors.IsChatOpen);

            private static BindManager Instance
            {
                get
                {
                    Init();
                    return _instance;
                }
            }

            public static void Init()
            {
                if (_instance == null) _instance = new BindManager();
            }

            public override void Close()
            {
                UnloadAction?.Invoke();
                _instance = null;
            }

            /// <summary>
            ///     Sets a temporary control blacklist cleared after every frame. Blacklists set via
            ///     property will persist regardless.
            /// </summary>
            public static void RequestTempBlacklist(SeBlacklistModes mode)
            {
                tmpBlacklist |= mode;
            }

            public override void Draw()
            {
                GetOrSetMemberFunc(lastBlacklist | tmpBlacklist, (int)BindClientAccessors.RequestBlacklistMode);
                tmpBlacklist = SeBlacklistModes.None;
            }

            /// <summary>
            ///     Returns the bind group with the given name and/or creates one with the name given
            ///     if one doesn't exist.
            /// </summary>
            public static IBindGroup GetOrCreateGroup(string name)
            {
                var index = (int)Instance.GetOrSetMemberFunc(name, (int)BindClientAccessors.GetOrCreateGroup);
                return index != -1 ? Groups[index] : null;
            }

            /// <summary>
            ///     Returns the bind group with the name igven.
            /// </summary>
            public static IBindGroup GetBindGroup(string name)
            {
                var index = (int)Instance.GetOrSetMemberFunc(name, (int)BindClientAccessors.GetBindGroup);
                return index != -1 ? Groups[index] : null;
            }

            /// <summary>
            ///     Returns the control associated with the given name.
            /// </summary>
            public static IControl GetControl(string name)
            {
                var index = (int)Instance.GetOrSetMemberFunc(name, (int)BindClientAccessors.GetControlByName);
                return index != -1 ? Controls[index] : null;
            }

            /// <summary>
            ///     Generates a list of controls from a list of control names.
            /// </summary>
            public static IControl[] GetCombo(IList<string> names)
            {
                var combo = new IControl[names.Count];

                for (var n = 0; n < names.Count; n++)
                    combo[n] = GetControl(names[n]);

                return combo;
            }

            /// <summary>
            ///     Generates a combo array using the corresponding control indices.
            /// </summary>
            public static IControl[] GetCombo(IList<ControlData> indices)
            {
                var combo = new IControl[indices.Count];

                for (var n = 0; n < indices.Count; n++)
                    combo[n] = Controls[indices[n].index];

                return combo;
            }

            /// <summary>
            ///     Generates a combo array using the corresponding control indices.
            /// </summary>
            public static IControl[] GetCombo(IList<int> indices)
            {
                var combo = new IControl[indices.Count];

                for (var n = 0; n < indices.Count; n++)
                    combo[n] = Controls[indices[n]];

                return combo;
            }

            /// <summary>
            ///     Generates a list of control indices using a list of control names.
            /// </summary>
            public static int[] GetComboIndices(IList<string> controlNames)
            {
                return Instance.GetOrSetMemberFunc(controlNames, (int)BindClientAccessors.GetComboIndices) as int[];
            }

            /// <summary>
            ///     Returns the control associated with the given <see cref="MyKeys" /> enum.
            /// </summary>
            public static IControl GetControl(MyKeys seKey)
            {
                return Controls[(int)seKey];
            }

            /// <summary>
            ///     Returns the control associated with the given custom <see cref="RichHudControls" /> enum.
            /// </summary>
            public static IControl GetControl(RichHudControls rhdKey)
            {
                return Controls[(int)rhdKey];
            }

            /// <summary>
            ///     Generates a list of control indices from a list of controls.
            /// </summary>
            public static int[] GetComboIndices(IList<IControl> controls)
            {
                var indices = new int[controls.Count];

                for (var n = 0; n < controls.Count; n++)
                    indices[n] = controls[n].Index;

                return indices;
            }

            /// <summary>
            ///     Generates a list of control indices from a list of controls.
            /// </summary>
            public static int[] GetComboIndices(IList<ControlData> controls)
            {
                var indices = new int[controls.Count];

                for (var n = 0; n < controls.Count; n++)
                    indices[n] = controls[n].index;

                return indices;
            }
        }
    }
}