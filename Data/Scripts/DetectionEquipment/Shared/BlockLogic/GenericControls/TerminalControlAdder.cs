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
    internal abstract class TerminalControlAdder<LogicType, BlockType> : ITerminalControlAdder
        where LogicType : MyGameLogicComponent, IControlBlockBase
        where BlockType : IMyTerminalBlock, IMyFunctionalBlock
    {
        private static bool _isDone = false;
        protected static Func<IMyTerminalBlock, bool> VisibleFunc = (block) => block.GameLogic.GetAs<LogicType>() != null;
        protected static string IdPrefix = nameof(LogicType) + "_";

        public virtual void DoOnce(LogicType thisLogic)
        {
            if (_isDone) return;

            ControlBlockBase<BlockType>.Controls = this;

            CreateTerminalActions();
            CreateTerminalProperties();

            _isDone = true;
            Log.Info(GetType().Name, $"Created terminal actions and properties for {typeof(BlockType).Name}/{typeof(LogicType).Name}.");
        }

        protected abstract void CreateTerminalActions();
        protected abstract void CreateTerminalProperties();

        public static IMyTerminalControlOnOffSwitch CreateToggle(string id, string displayName, string toolTip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter)
        {
            var toggle = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlOnOffSwitch, BlockType>(IdPrefix + id);
            toggle.Title = MyStringId.GetOrCompute(displayName);
            toggle.Tooltip = MyStringId.GetOrCompute(toolTip);
            toggle.SupportsMultipleBlocks = true; // wether this control should be visible when multiple blocks are selected (as long as they all have this control).
                                                  // callbacks to determine if the control should be visible or not-grayed-out(Enabled) depending on whatever custom condition you want, given a block instance.
                                                  // optional, they both default to true.
            toggle.Visible = VisibleFunc;
            //c.Enabled = CustomVisibleCondition;
            toggle.OnText = MySpaceTexts.SwitchText_On;
            toggle.OffText = MySpaceTexts.SwitchText_Off;
            // setters and getters should both be assigned on all controls that have them, to avoid errors in mods or PB scripts getting exceptions from them.
            toggle.Getter = getter;  // Getting the value
            toggle.Setter = setter; // Setting the value

            MyAPIGateway.TerminalControls.AddControl<BlockType>(toggle);

            return toggle;
        }

        public static IMyTerminalControlSlider CreateSlider(string id, string displayName, string toolTip, float min, float max, Func<IMyTerminalBlock, float> getter, Action<IMyTerminalBlock, float> setter, Action<IMyTerminalBlock, StringBuilder> writer)
        {
            var slider = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlSlider, BlockType>(IdPrefix + id);
            slider.Title = MyStringId.GetOrCompute(displayName);
            slider.Tooltip = MyStringId.GetOrCompute(toolTip);
            slider.SetLimits(min, max); // Set the minimum and maximum values for the slider
            slider.Getter = getter; // Replace with your property
            slider.Setter = setter; // Replace with your property
            slider.Writer = writer; // Replace with your property

            slider.Visible = VisibleFunc;
            slider.Enabled = (b) => true; // or your custom condition
            slider.SupportsMultipleBlocks = true;

            MyAPIGateway.TerminalControls.AddControl<BlockType>(slider);
            return slider;
        }

        public static IMyTerminalControlButton CreateButton(string id, string displayName, string toolTip, Action<IMyTerminalBlock> action)
        {
            var button = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlButton, BlockType>(IdPrefix + id);
            button.Title = MyStringId.GetOrCompute(displayName);
            button.Tooltip = MyStringId.GetOrCompute(toolTip);
            button.SupportsMultipleBlocks = true;

            button.Visible = VisibleFunc;
            button.Action = action;

            MyAPIGateway.TerminalControls.AddControl<BlockType>(button);

            return button;
        }

        public static IMyTerminalControlCheckbox CreateCheckbox(string id, string displayName, string toolTip, Func<IMyTerminalBlock, bool> getter, Action<IMyTerminalBlock, bool> setter)
        {
            var box = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCheckbox, BlockType>(IdPrefix + id);
            box.Title = MyStringId.GetOrCompute(displayName);
            box.Tooltip = MyStringId.GetOrCompute(toolTip);
            box.SupportsMultipleBlocks = true;

            box.Visible = VisibleFunc;
            box.Getter = getter;
            box.Setter = setter;

            MyAPIGateway.TerminalControls.AddControl<BlockType>(box);
            return box;
        }

        public static IMyTerminalControlCombobox CreateCombobox(string id, string displayName, string toolTip, Action<List<MyTerminalControlComboBoxItem>> content, Func<IMyTerminalBlock, long> getter, Action<IMyTerminalBlock, long> setter)
        {
            var box = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlCombobox, BlockType>(IdPrefix + id);
            box.Title = MyStringId.GetOrCompute(displayName);
            box.Tooltip = MyStringId.GetOrCompute(toolTip);
            box.SupportsMultipleBlocks = true;

            box.Visible = VisibleFunc;
            box.ComboBoxContent = content;
            box.Getter = getter;
            box.Setter = setter;

            MyAPIGateway.TerminalControls.AddControl<BlockType>(box);
            return box;
        }

        public static IMyTerminalControlListbox CreateListbox(string id, string displayName, string toolTip, bool multiSelect, Action<IMyTerminalBlock, List<MyTerminalControlListBoxItem>, List<MyTerminalControlListBoxItem>> content, Action<IMyTerminalBlock, List<MyTerminalControlListBoxItem>> itemSelected)
        {
            var box = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlListbox, BlockType>(IdPrefix + id);
            box.Title = MyStringId.GetOrCompute(displayName);
            box.Tooltip = MyStringId.GetOrCompute(toolTip);
            box.SupportsMultipleBlocks = true;

            box.Visible = VisibleFunc;
            box.ListContent = content;
            box.Multiselect = multiSelect;
            box.ItemSelected = itemSelected;
            box.VisibleRowsCount = 10;

            MyAPIGateway.TerminalControls.AddControl<BlockType>(box);
            return box;
        }

        public static IMyTerminalControlTextbox CreateTextbox(string id, string displayName, string toolTip, Func<IMyTerminalBlock, StringBuilder> getter, Action<IMyTerminalBlock, StringBuilder> setter)
        {
            var box = MyAPIGateway.TerminalControls.CreateControl<IMyTerminalControlTextbox, BlockType>(IdPrefix + id);
            box.Title = MyStringId.GetOrCompute(displayName);
            box.Tooltip = MyStringId.GetOrCompute(toolTip);
            box.SupportsMultipleBlocks = true;

            box.Visible = VisibleFunc;
            box.Getter = getter;
            box.Setter = setter;

            MyAPIGateway.TerminalControls.AddControl<BlockType>(box);
            return box;
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
            var act = MyAPIGateway.TerminalControls.CreateAction<BlockType>(IdPrefix + id);
            act.Name = new StringBuilder(displayName);
            act.Action = action;
            act.Writer = writer;
            act.Icon = icon;
            act.ValidForGroups = true;

            act.Enabled = VisibleFunc;
            MyAPIGateway.TerminalControls.AddAction<BlockType>(act);

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
            var prop = MyAPIGateway.TerminalControls.CreateProperty<TValue, BlockType>(IdPrefix + id);
            prop.SupportsMultipleBlocks = true;

            prop.Getter = getter;
            prop.Setter = setter;

            MyAPIGateway.TerminalControls.AddControl<BlockType>(prop);
            return prop;
        }
    }

    /// <summary>
    /// Mildly useless interface to ensure that ControlBlockBase can statically store its controls.
    /// </summary>
    public interface ITerminalControlAdder
    {

    }
}
