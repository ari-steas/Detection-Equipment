using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;

namespace DetectionEquipment.Server.PBApi
{
    internal static class PbApiInitializer
    {
        public static void Init()
        {
            var property = MyAPIGateway.TerminalControls.CreateProperty<IReadOnlyDictionary<string, Delegate>, IMyProgrammableBlock>("DetectionPbApi");
            property.Getter = b => PbApiMethods.SafeMethods;
            MyAPIGateway.TerminalControls.AddControl<IMyProgrammableBlock>(property);

            MyAPIGateway.Entities.GetEntities(null, ent =>
            {
                var grid = ent as IMyCubeGrid;
                if (grid == null)
                    return false;
                
                // Workaround for scripts crashing when loading before the API is ready (i.e. on world load)
                foreach (var pb in grid.GetFatBlocks<IMyProgrammableBlock>())
                {
                    if (!pb.IsRunning && pb.ProgramData.Contains("DetectionPbApi"))
                        pb.Recompile();
                }
                return false;
            });
        }

        public static void Unload()
        {
            // I don't think this is needed...
        }
    }
}
