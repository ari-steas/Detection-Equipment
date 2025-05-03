using System.Collections.Generic;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace DetectionEquipment.Shared.BlockLogic.GenericControls
{
    public static class HideSorterControls
    {
        static bool _done = false;

        public static void DoOnce()
        {
            if (_done)
                return;
            _done = true;

            EditControls();
            EditActions();

            Log.Info("HideSorterControls", "Removed sorter controls.");
        }

        static bool AppendedCondition(IMyTerminalBlock block) => block?.GameLogic?.GetAs<IControlBlockBase>() == null;

        static void EditControls()
        {
            List<IMyTerminalControl> controls;
            MyAPIGateway.TerminalControls.GetControls<IMyConveyorSorter>(out controls);

            foreach (IMyTerminalControl c in controls)
            {
                switch (c.Id)
                {
                    case "DrainAll":
                    case "blacklistWhitelist":
                    case "CurrentList":
                    case "removeFromSelectionButton":
                    case "candidatesList":
                    case "addToSelectionButton":
                    case "ShowInInventory":
                    case "SearchField":
                        {
                            //c.Enabled = TerminalChainedDelegate.Create(c.Enabled, AppendedCondition); // grays out
                            c.Visible = TerminalChainedDelegate.Create(c.Visible, AppendedCondition); // hides
                            break;
                        }
                }
            }
        }

        static void EditActions()
        {
            List<IMyTerminalAction> actions;
            MyAPIGateway.TerminalControls.GetActions<IMyConveyorSorter>(out actions);

            foreach (IMyTerminalAction a in actions)
            {
                switch (a.Id)
                {
                    case "DrainAll":
                    case "DrainAll_On":
                    case "DrainAll_Off":
                        {
                            // appends a custom condition after the original condition with an AND.

                            a.Enabled = TerminalChainedDelegate.Create(a.Enabled, AppendedCondition);
                            // action.Enabled hides it, there is no grayed-out for actions.

                            break;
                        }
                }
            }
        }
    }
}