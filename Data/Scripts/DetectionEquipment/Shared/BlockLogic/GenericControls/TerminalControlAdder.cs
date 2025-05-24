using Sandbox.Game.Localization;
using Sandbox.ModAPI.Interfaces.Terminal;
using Sandbox.ModAPI;
using System;
using System.Text;
using VRage.Utils;
using VRage.Game.Components;
using System.Collections.Generic;
using VRage.ModAPI;
using DetectionEquipment.Shared.Utils;

namespace DetectionEquipment.Shared.BlockLogic.GenericControls
{
    internal abstract class TerminalControlAdder<TLogicType, TBlockType> : ITerminalControlAdder
        where TLogicType : MyGameLogicComponent, IControlBlockBase
        where TBlockType : IMyTerminalBlock, IMyFunctionalBlock
    {
        protected static bool _isDone = false;
        private static readonly Func<IMyTerminalBlock, bool> VisibleFunc = (block) => block.GameLogic.GetAs<TLogicType>() != null;
        public static readonly string IdPrefix = typeof(TLogicType).Name + "_";

        public virtual void DoOnce(IControlBlockBase thisLogic)
        {
            if (_isDone) return;

            CreateTerminalActions();
            CreateTerminalProperties();

            _isDone = true;
            Log.Info(GetType().Name, $"Created terminal actions and properties for {typeof(TBlockType).Name}/{typeof(TLogicType).Name}.");
        }

        protected abstract void CreateTerminalActions();
        protected abstract void CreateTerminalProperties();

        public static IMyTerminalControlOnOffSwitch CreateToggle(string id, string displayName, string toolTip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter)
        {
            var toggle = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, TBlockType>(IdPrefix + id);
            toggle.Title = MyStringId.GetOrCompute(displayName);
            toggle.Tooltip = MyStringId.GetOrCompute(toolTip);
            toggle.SupportsMultipleBlocks = true;
            toggle.Visible = VisibleFunc;
            //c.Enabled = CustomVisibleCondition;
            toggle.OnText = MySpaceTexts.SwitchText_On;
            toggle.OffText = MySpaceTexts.SwitchText_Off;
            // setters and getters should both be assigned on all controls that have them, to avoid errors in mods or PB scripts getting exceptions from them.
            toggle.Getter = tb =>
            {
                try
                {
                    return getter.Invoke(tb);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_GETTER", ex, true);
                    return false;
                }
            };
            toggle.Setter = (tb, v) =>
            {
                try
                {
                    setter.Invoke(tb, v);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_SETTER", ex, true);
                }
            };

            MyAPIGateway.TerminalControls.AddControl<TBlockType>(toggle);

            return toggle;
        }

        public static IMyTerminalControlSlider CreateSlider(string id, string displayName, string toolTip, float min, float max, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Action<IMyTerminalBlock, StringBuilder> writer)
        {
            var slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, TBlockType>(IdPrefix + id);
            slider.Title = MyStringId.GetOrCompute(displayName);
            slider.Tooltip = MyStringId.GetOrCompute(toolTip);
            slider.SetLimits(min, max);
            slider.Getter = tb =>
            {
                try
                {
                    return getter.Invoke(tb);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_GETTER", ex, true);
                    return 0;
                }
            };
            slider.Setter = (tb, v) =>
            {
                try
                {
                    setter.Invoke(tb, v);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_SETTER", ex, true);
                }
            };
            slider.Writer = (b, sb) =>
            {
                try
                {
                    writer.Invoke(b, sb);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_WRITER", ex, true);
                }
            };

            slider.Visible = VisibleFunc;
            slider.SupportsMultipleBlocks = true;

            MyAPIGateway.TerminalControls.AddControl<TBlockType>(slider);
            return slider;
        }

        public static IMyTerminalControlButton CreateButton(string id, string displayName, string toolTip, Action<IMyTerminalBlock> action)
        {
            var button = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, TBlockType>(IdPrefix + id);
            button.Title = MyStringId.GetOrCompute(displayName);
            button.Tooltip = MyStringId.GetOrCompute(toolTip);
            button.SupportsMultipleBlocks = true;

            button.Visible = VisibleFunc;
            button.Action = action;
            button.Action = tb =>
            {
                try
                {
                    action.Invoke(tb);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_ACTION", ex, true);
                }
            };

            MyAPIGateway.TerminalControls.AddControl<TBlockType>(button);

            return button;
        }

        public static IMyTerminalControlCheckbox CreateCheckbox(string id, string displayName, string toolTip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter)
        {
            var box = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, TBlockType>(IdPrefix + id);
            box.Title = MyStringId.GetOrCompute(displayName);
            box.Tooltip = MyStringId.GetOrCompute(toolTip);
            box.SupportsMultipleBlocks = true;

            box.Visible = VisibleFunc;
            box.Getter = tb =>
            {
                try
                {
                    return getter.Invoke(tb);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_GETTER", ex, true);
                    return false;
                }
            };
            box.Setter = (tb, v) =>
            {
                try
                {
                    setter.Invoke(tb, v);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_SETTER", ex, true);
                }
            };

            MyAPIGateway.TerminalControls.AddControl<TBlockType>(box);
            return box;
        }

        public static IMyTerminalControlCombobox CreateCombobox(string id, string displayName, string toolTip, Action<List<MyTerminalControlComboBoxItem>> content, Func<IMyTerminalBlock, long> getter, Action<IMyTerminalBlock, long> setter)
        {
            var box = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, TBlockType>(IdPrefix + id);
            box.Title = MyStringId.GetOrCompute(displayName);
            box.Tooltip = MyStringId.GetOrCompute(toolTip);
            box.SupportsMultipleBlocks = true;

            box.Visible = VisibleFunc;
            box.ComboBoxContent = (contentList) =>
            {
                try
                {
                    content.Invoke(contentList);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_CONTENT", ex, true);
                }
            };
            box.Getter = tb =>
            {
                try
                {
                    return getter.Invoke(tb);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_GETTER", ex, true);
                    return 0;
                }
            };
            box.Setter = (tb, v) =>
            {
                try
                {
                    setter.Invoke(tb, v);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_SETTER", ex, true);
                }
            };

            MyAPIGateway.TerminalControls.AddControl<TBlockType>(box);
            return box;
        }

        public static IMyTerminalControlListbox CreateListbox(string id, string displayName, string toolTip, bool multiSelect, Action<IMyTerminalBlock, List<MyTerminalControlListBoxItem>, List<MyTerminalControlListBoxItem>> content, Action<IMyTerminalBlock, List<MyTerminalControlListBoxItem>> itemSelected)
        {
            var box = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, TBlockType>(IdPrefix + id);
            box.Title = MyStringId.GetOrCompute(displayName);
            box.Tooltip = MyStringId.GetOrCompute(toolTip);
            box.SupportsMultipleBlocks = true;

            box.Visible = VisibleFunc;

            box.ListContent = (tb, contentList, selectedList) =>
            {
                try
                {
                    content.Invoke(tb, contentList, selectedList);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_CONTENT", ex, true);
                }
            };
            box.ItemSelected = (tb, selectedList) =>
            {
                try
                {
                    itemSelected.Invoke(tb, selectedList);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_ITEMSELECTED", ex, true);
                }
            };

            box.Multiselect = multiSelect;
            box.VisibleRowsCount = 10;

            MyAPIGateway.TerminalControls.AddControl<TBlockType>(box);
            return box;
        }

        public static IMyTerminalControlTextbox CreateTextbox(string id, string displayName, string toolTip, Func<IMyTerminalBlock, StringBuilder> getter, Action<IMyTerminalBlock, StringBuilder> setter)
        {
            var box = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, TBlockType>(IdPrefix + id);
            box.Title = MyStringId.GetOrCompute(displayName);
            box.Tooltip = MyStringId.GetOrCompute(toolTip);
            box.SupportsMultipleBlocks = true;

            box.Visible = VisibleFunc;
            box.Getter = tb =>
            {
                try
                {
                    return getter.Invoke(tb);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_GETTER", ex, true);
                    return new StringBuilder();
                }
            };
            box.Setter = (tb, v) =>
            {
                try
                {
                    setter.Invoke(tb, v);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_SETTER", ex, true);
                }
            };


            MyAPIGateway.TerminalControls.AddControl<TBlockType>(box);
            return box;
        }

        public static IMyTerminalControlLabel CreateLabel(string id, string text)
        {
            var label = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlLabel, TBlockType>(IdPrefix + id);
            label.Label = MyStringId.GetOrCompute(text);

            label.Visible = VisibleFunc;

            MyAPIGateway.TerminalControls.AddControl<TBlockType>(label);
            return label;
        }

        public static IMyTerminalControlSeparator CreateSeperator(string id)
        {
            var seperator = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSeparator, TBlockType>(IdPrefix + id);

            seperator.Visible = VisibleFunc;

            MyAPIGateway.TerminalControls.AddControl<TBlockType>(seperator);
            return seperator;
        }

        /// <summary>
        /// Adds a toolbar action to the block type.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="displayName"></param>
        /// <param name="action"></param>
        /// <param name="writer"></param>
        /// <param name="icon"></param>
        /// <returns></returns>
        public static IMyTerminalAction CreateAction(string id, string displayName, Action<IMyTerminalBlock> action, Action<IMyTerminalBlock, StringBuilder> writer, string icon)
        {
            var act = MyAPIGateway.TerminalControls.CreateAction<TBlockType>(IdPrefix + id);
            act.Name = new StringBuilder(displayName);
            act.Action = action;
            act.Writer = writer;
            act.Icon = icon;
            act.ValidForGroups = true;

            act.Enabled = tb =>
            {
                try
                {
                    return VisibleFunc.Invoke(tb);
                }
                catch (Exception ex)
                {
                    Log.Exception("TerminalControlAdder::" + IdPrefix + id + "_ACTION", ex, true);
                    return false;
                }
            };

            MyAPIGateway.TerminalControls.AddAction<TBlockType>(act);

            return act;
        }

        /// <summary>
        /// Adds a hidden property to the block type that can be accessed by Programmable Blocks or mods.
        /// </summary>
        /// <typeparam name="TValue"></typeparam>
        /// <param name="id"></param>
        /// <param name="getter"></param>
        /// <param name="setter"></param>
        /// <returns></returns>
        public static IMyTerminalControlProperty<TValue> CreateProperty<TValue>(string id, Func<IMyTerminalBlock, TValue> getter, Action<IMyTerminalBlock, TValue> setter)
        {
            var prop = MyAPIGateway.TerminalControls.CreateProperty<TValue, TBlockType>(IdPrefix + id);
            prop.SupportsMultipleBlocks = true;

            prop.Getter = getter;
            prop.Setter = setter;

            MyAPIGateway.TerminalControls.AddControl<TBlockType>(prop);
            return prop;
        }
    }

    /// <summary>
    /// Mildly useless interface to ensure that ControlBlockBase can statically store its controls.
    /// </summary>
    internal interface ITerminalControlAdder
    {
        void DoOnce(IControlBlockBase thisLogic);
    }
}
