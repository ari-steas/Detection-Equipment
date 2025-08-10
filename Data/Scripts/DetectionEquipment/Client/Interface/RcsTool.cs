using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Utils;
using Sandbox.Game;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Weapons;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRageMath;

namespace DetectionEquipment.Client.Interface
{
    internal static class RcsTool
    {
        private const string RcsToolSubtype = "DetectionToolGun";
        private static bool _questlogDisposed = true;

        public static void Init()
        {
            _questlogDisposed = true;
        }

        private static int _ticks = 0;
        private static string _name = "";
        private static double _rcs = 0, _vcs = 0, _irs = 0, _rawIrs = 0, _commSig, _rawCommSig;
        private static bool _shouldShow = false;
        public static void Update()
        {
            if (_ticks++ % 10 == 0 || GlobalData.DebugLevel > 0)
                _shouldShow = GetData(out _name, out _rcs, out _vcs, out _irs, out _rawIrs, out _commSig, out _rawCommSig);

            // Updating every tick to prevent flashing
            if (_shouldShow)
            {
                ModderNotification.Hide();
                MyVisualScriptLogicProvider.SetQuestlogLocal(true,
                    $"Detection Equipment - Visibility Tool");

                MyVisualScriptLogicProvider.AddQuestlogDetailLocal(
                    $"{_name}\n" +
                    $"    RCS: {_rcs:N} m^2\n" +
                    $"    VCS: {_vcs:N} m^2\n" +
                    $"    IRS: {_rawIrs:N} Wm^2\n" +
                    $"      @ Camera: {_irs:N} Wm^2\n" +
                    $"    COMM: {_rawCommSig:N0} W\n" +
                    $"      @ Camera: {_commSig:N} W",
                    false, false);

                _questlogDisposed = false;
            }
            else if (!_questlogDisposed)
            {
                MyVisualScriptLogicProvider.SetQuestlogLocal(false, "Detection Equipment - Visibility Tool");
                _questlogDisposed = true;
            }

            //MyAPIGateway.Utilities.ShowNotification($"{name}: RCS: {rcs:F} VCS: {vcs:F} IRS: {irs:F}", 950/6);
        }

        private static bool GetData(out string name, out double rcs, out double vcs, out double irs, out double rawIrs, out double commSig, out double rawCommSig)
        {
            name = "No target";
            vcs = 0;
            rcs = 0;
            irs = 0;
            rawIrs = 0;
            commSig = 0;
            rawCommSig = 0;

            var rifle = MyAPIGateway.Session.Player?.Character?.EquippedTool as IMyAutomaticRifleGun;
            //MyAPIGateway.Utilities.ShowNotification($"{rifle?.DefinitionId.SubtypeName ?? "None"}", 950/6);
            if (rifle == null || rifle.DefinitionId.SubtypeName != RcsToolSubtype)
                return false;

            // raycast for grid
            var castMatrix = GlobalData.DebugLevel > 0 && MyAPIGateway.Session.Player?.Character != null ? MyAPIGateway.Session.Player.Character.WorldMatrix : MyAPIGateway.Session.Camera.WorldMatrix;
            var castEnt = MiscUtils.RaycastEntityFromMatrix(castMatrix);
            if (castEnt == null)
                return true;

            //Vector3D position = MyAPIGateway.Session.Camera.WorldMatrix.Translation - MyAPIGateway.Session.Camera.WorldMatrix.Forward * 500;
            Vector3D camPos = castMatrix.Translation;

            var castGrid = castEnt as IMyCubeGrid;
            if (castGrid != null)
            {
                var attached = new List<IMyCubeGrid>();
                castGrid.GetGridGroup(GridLinkTypeEnum.Physical).GetGrids(attached);
                foreach (var grid in attached)
                {
                    // Only track objects the grid can see
                    if (!TrackingUtils.HasLoS(camPos, grid))
                        continue;

                    var track = new Server.Tracking.GridTrack(grid);
                    double trackVcs, trackRcs;
                    track.CalculateRcs(castGrid.WorldAABB.Center - camPos, out trackRcs, out trackVcs);
                    rcs += trackRcs;
                    vcs += trackVcs;
                    irs += track.InfraredVisibility(camPos, trackVcs);
                    rawIrs += irs * Vector3D.DistanceSquared(camPos, track.Position);
                    commSig += 4 * Math.PI * track.CommsVisibility(camPos) / Vector3D.Distance(camPos, track.Position);
                    rawCommSig += track.CommsVisibility(grid.WorldAABB.Center);
                }
                name = $"\"{castGrid.CustomName}\" & {attached.Count-1} subgrids";
            }
            else
            {
                var track = new Server.Tracking.EntityTrack((MyEntity) castEnt);
                rcs = track.RadarVisibility(camPos);
                vcs = track.OpticalVisibility(camPos);
                irs = track.InfraredVisibility(camPos, vcs);
                rawIrs = irs * Vector3D.DistanceSquared(camPos, track.Position);
                commSig = track.CommsVisibility(camPos);
                rawCommSig = track.CommsVisibility(castEnt.WorldAABB.Center);

                name = $"\"{castEnt.DisplayName}\"";
            }

            return true;
        }

        public static void Close()
        {

        }
    }
}
