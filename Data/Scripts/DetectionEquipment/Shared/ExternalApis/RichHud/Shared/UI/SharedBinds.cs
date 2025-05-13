using RichHudFramework.Internal;
using RichHudFramework.UI.Client;
using VRage.Input;

namespace RichHudFramework.UI
{
    /// <summary>
    ///     Wrapper used to provide easy access to library key binds.
    /// </summary>
    public sealed class SharedBinds : RichHudComponentBase
    {
        private static SharedBinds instance;
        private readonly IBindGroup sharedMain, sharedModifiers;

        private SharedBinds() : base(false, true)
        {
            sharedMain = BindManager.GetOrCreateGroup("SharedBinds");
            sharedMain.RegisterBinds(new BindGroupInitializer
            {
                { "leftbutton", MyKeys.LeftButton },
                { "rightbutton", MyKeys.RightButton },
                { "mousewheelup", RichHudControls.MousewheelUp },
                { "mousewheeldown", RichHudControls.MousewheelDown },

                { "enter", MyKeys.Enter },
                { "back", MyKeys.Back },
                { "delete", MyKeys.Delete },
                { "escape", MyKeys.Escape },

                { "selectall", MyKeys.Control, MyKeys.A },
                { "copy", MyKeys.Control, MyKeys.C },
                { "cut", MyKeys.Control, MyKeys.X },
                { "paste", MyKeys.Control, MyKeys.V },

                { "uparrow", MyKeys.Up },
                { "downarrow", MyKeys.Down },
                { "leftarrow", MyKeys.Left },
                { "rightarrow", MyKeys.Right },

                { "pageup", MyKeys.PageUp },
                { "pagedown", MyKeys.PageDown },
                { "space", MyKeys.Space }
            });
            sharedModifiers = BindManager.GetOrCreateGroup("SharedModifiers");
            sharedModifiers.RegisterBinds(new BindGroupInitializer
            {
                { "shift", MyKeys.Shift },
                { "control", MyKeys.Control },
                { "alt", MyKeys.Alt }
            });
        }

        public static IBind LeftButton => Instance.sharedMain[0];
        public static IBind RightButton => Instance.sharedMain[1];
        public static IBind MousewheelUp => Instance.sharedMain[2];
        public static IBind MousewheelDown => Instance.sharedMain[3];

        public static IBind Enter => Instance.sharedMain[4];
        public static IBind Back => Instance.sharedMain[5];
        public static IBind Delete => Instance.sharedMain[6];
        public static IBind Escape => Instance.sharedMain[7];

        public static IBind SelectAll => Instance.sharedMain[8];
        public static IBind Copy => Instance.sharedMain[9];
        public static IBind Cut => Instance.sharedMain[10];
        public static IBind Paste => Instance.sharedMain[11];

        public static IBind UpArrow => Instance.sharedMain[12];
        public static IBind DownArrow => Instance.sharedMain[13];
        public static IBind LeftArrow => Instance.sharedMain[14];
        public static IBind RightArrow => Instance.sharedMain[15];

        public static IBind PageUp => Instance.sharedMain[16];
        public static IBind PageDown => Instance.sharedMain[17];
        public static IBind Space => Instance.sharedMain[18];

        public static IBind Control => Instance.sharedModifiers[0];
        public static IBind Shift => Instance.sharedModifiers[1];
        public static IBind Alt => Instance.sharedModifiers[2];

        private static SharedBinds Instance
        {
            get
            {
                Init();
                return instance;
            }
            set { instance = value; }
        }

        private static void Init()
        {
            if (instance == null)
                instance = new SharedBinds();
        }

        public override void Close()
        {
            Instance = null;
        }
    }
}