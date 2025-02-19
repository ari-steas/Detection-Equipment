using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DetectionEquipment.Server.PBApi
{
    internal static class PbApiMethods
    {
        public static ImmutableDictionary<string, Delegate> SafeMethods => ImmutableDictionary.CreateRange(_methods);

        private static Dictionary<string, Delegate> _methods = new Dictionary<string, Delegate>()
        {
            ["TestAction"] = new Action<string>(TestAction),
        };

        private static void TestAction(string input)
        {
            MyAPIGateway.Utilities.ShowMessage("PbApi", input);
        }
    }
}
