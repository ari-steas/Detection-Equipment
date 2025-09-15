using System.Collections.Generic;
using DetectionEquipment.Client.BlockLogic;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;

namespace DetectionEquipment.Shared.BlockLogic.GenericControls
{
    public static class HideSorterControls
    {
        static bool _done = false;

        public static void DoOnce(IMyTerminalBlock block)
        {
            // Auto-hide logic blocks without an inventory.
            if (block != null)
                block.ShowInInventory = HasNoLogicOrInventory(block);

            if (_done)
                return;
            _done = true;

            EditControls();
            EditActions();

            Log.Info("HideSorterControls", "Removed sorter controls.");
        }

        static bool HasNoLogic(IMyTerminalBlock block) => block == null || (ControlBlockManager.GetLogic<IControlBlockBase>(block) == null && !block.HasLogic());
        static bool HasNoLogicOrInventory(IMyTerminalBlock block) => block == null || ((ControlBlockManager.GetLogic<IControlBlockBase>(block) == null && !block.HasLogic()) || block.GetInventory()?.MaxVolume > 0);

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
                    case "SearchField":
                        //case "": // keen doesn't name their terminal control separators. :(
                        //c.Enabled = TerminalChainedDelegate.Create(c.Enabled, AppendedCondition); // grays out
                        c.Visible = TerminalChainedDelegate.Create(c.Visible, HasNoLogic); // hides
                        break;
                    case "ShowInInventory":
                        c.Visible = TerminalChainedDelegate.Create(c.Visible, HasNoLogicOrInventory);
                        break;
                    //default:
                    //    Log.Info("HideSorterControls", $"Ignored control {c.GetType().Name}/{c.Id}.");
                    //    break;
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

                            a.Enabled = TerminalChainedDelegate.Create(a.Enabled, HasNoLogic);
                            // action.Enabled hides it, there is no grayed-out for actions.

                            break;
                        }
                }
            }
        }
    }
}