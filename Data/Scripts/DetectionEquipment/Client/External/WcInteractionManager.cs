using DetectionEquipment.Server.SensorBlocks;
using DetectionEquipment.Shared.ExternalApis;
using DetectionEquipment.Shared.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DetectionEquipment.Shared;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Client.External
{
    internal static class WcInteractionManager
    {
        public static Dictionary<MyCubeGrid, List<MyEntity>> VisibleTargets;

        public static void Init()
        {
            VisibleTargets = new Dictionary<MyCubeGrid, List<MyEntity>>();
            MyAPIGateway.Entities.OnEntityRemove += OnEntityRemove;
            ApiManager.WcOnLoadRegisterOrInvoke(OnWcApiReady);
        }

        public static void Close()
        {
            VisibleTargets = null;
        }

        private static void OnWcApiReady()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                Log.Info("WcInteractionManager", "WeaponCore targeting not overridden (is server).");
                return;
            }

            if (GlobalData.ContributeWcTargeting)
            {
                ApiManager.WcApi.AddScanTargetsAction(ScanTargetsAction);
                Log.Info("WcInteractionManager", "WeaponCore targeting overridden.");
            }
            else
            {
                Log.Info("WcInteractionManager", "WeaponCore targeting not overridden.");
            }
        }

        private static void ScanTargetsAction(MyCubeGrid grid, BoundingSphereD sphere, List<MyEntity> targets)
        {
            List<MyEntity> valid;
            if (!VisibleTargets.TryGetValue(grid, out valid))
                return;
            foreach (var target in valid)
            {
                targets.Add(target);
                DebugDraw.AddLine(grid.WorldMatrix.Translation, target.WorldMatrix.Translation, Color.Maroon, 10/6f);
            }
            
        }

        private static void OnEntityRemove(IMyEntity ent)
        {
            var grid = ent as MyCubeGrid;
            if (grid == null)
                return;
            VisibleTargets.Remove(grid);
        }
    }
}
