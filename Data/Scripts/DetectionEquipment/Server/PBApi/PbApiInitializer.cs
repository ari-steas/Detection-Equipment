using DetectionEquipment.Shared.Utils;
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

            int recompileCount = 0;
            MyAPIGateway.Entities.GetEntities(null, ent =>
            {
                var grid = ent as IMyCubeGrid;
                if (grid == null)
                    return false;
                
                // Workaround for scripts crashing when loading before the API is ready (i.e. on world load)
                foreach (var pb in grid.GetFatBlocks<IMyProgrammableBlock>())
                {
                    if (pb == null) continue;
                    if (!pb.IsRunning && (pb.ProgramData?.Contains("DetectionPbApi") ?? false))
                    {
                        recompileCount++;
                        pb.Recompile();
                    }
                }
                return false;
            });
            Log.Info("PbApiInitializer", $"Packaged PbAPI methods and recompiled {recompileCount} failed script(s).");
        }

        public static void Unload()
        {
            // I don't think this is needed...
        }
    }
}
