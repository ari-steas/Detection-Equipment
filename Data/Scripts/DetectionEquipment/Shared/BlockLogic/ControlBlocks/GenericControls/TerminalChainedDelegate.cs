using System;
using Sandbox.ModAPI;

namespace DetectionEquipment.Shared.BlockLogic.ControlBlocks.GenericControls
{
    /// <summary>
    /// Designed for appending custom conditions to Visible/Enabled of terminal controls or toolbar actions so that they can be hidden for specific conditions/subtypes/whatever.
    /// </summary>
    public class TerminalChainedDelegate
    {
        /// <summary>
        /// <paramref name="originalFunc"/> should always be the delegate this replaces, to properly chain with other mods doing the same.
        /// <para><paramref name="customFunc"/> should be your custom condition to append to the chain.</para>
        /// <para>As for <paramref name="checkOr"/>, leave false if you want to hide controls by returning false with your <paramref name="customFunc"/>.</para>
        /// <para>Otherwise set to true if you want to force-show otherwise hidden controls by returning true with your <paramref name="customFunc"/>.</para> 
        /// </summary>
        public static Func<IMyTerminalBlock, bool> Create(Func<IMyTerminalBlock, bool> originalFunc, Func<IMyTerminalBlock, bool> customFunc, bool checkOr = false)
        {
            return new TerminalChainedDelegate(originalFunc, customFunc, checkOr).ResultFunc;
        }

        readonly Func<IMyTerminalBlock, bool> _originalFunc;
        readonly Func<IMyTerminalBlock, bool> _customFunc;
        readonly bool _checkOr;

        TerminalChainedDelegate(Func<IMyTerminalBlock, bool> originalFunc, Func<IMyTerminalBlock, bool> customFunc, bool checkOr)
        {
            _originalFunc = originalFunc;
            _customFunc = customFunc;
            _checkOr = checkOr;
        }

        bool ResultFunc(IMyTerminalBlock block)
        {
            if (block?.CubeGrid == null)
                return false;

            bool originalCondition = _originalFunc == null ? true : _originalFunc.Invoke(block);
            bool customCondition = _customFunc == null ? true : _customFunc.Invoke(block);

            if (_checkOr)
                return originalCondition || customCondition;
            else
                return originalCondition && customCondition;
        }
    }
}
