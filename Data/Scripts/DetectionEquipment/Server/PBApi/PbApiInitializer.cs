using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DetectionEquipment.Server.PBApi
{
    internal static class PbApiInitializer
    {
        public static void Init()
        {
            var property = MyAPIGateway.TerminalControls.CreateProperty<IReadOnlyDictionary<string, Delegate>, IMyProgrammableBlock>("DetectionPbApi");
            property.Getter = b => PbApiMethods.SafeMethods;
            MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(property);
        }

        public static void Unload()
        {
            // I don't think this is needed...
        }
    }
}
