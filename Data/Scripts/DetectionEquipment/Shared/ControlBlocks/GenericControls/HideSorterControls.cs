﻿using System.Collections.Generic;
using DetectionEquipment.Shared.ControlBlocks;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace DetectionEquipment.Shared.ControlBlocks.Controls
{
    public static class HideSorterControls
    {
        static bool Done = false;

        public static void DoOnce()
        {
            if (Done)
                return;
            Done = true;

            EditControls();
            EditActions();
        }

        static bool AppendedCondition(IMyTerminalBlock block)
        {
            return block?.GameLogic?.GetAs<ControlBlockBase>() == null;
        }

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