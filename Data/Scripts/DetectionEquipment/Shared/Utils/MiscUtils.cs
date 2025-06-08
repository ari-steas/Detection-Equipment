using System;
using Sandbox.ModAPI;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using Sandbox.Game.Entities.Planet;
using VRageMath;

namespace DetectionEquipment.Shared.Utils
{
    internal static class MiscUtils
    {
        public static IMyEntity RaycastEntityFromCamera()
        {
            var camMat = MyAPIGateway.Session.Camera.WorldMatrix;
            var hits = new List<IHitInfo>();
            MyAPIGateway.Physics.CastRay(camMat.Translation + camMat.Forward, camMat.Translation + camMat.Forward * 500, hits);
            foreach (var hit in hits)
            {
                var ent = hit.HitEntity;

                if (ent?.Physics != null)
                    return ent;
            }

            return null;
        }

        public static string RemoveChars(this string str, params char[] excluded)
        {
            return str == null ? null : string.Join("", str.Split(excluded));
        }

        public static void SafeChat(string sender, string message)
        {
            if (Environment.CurrentManagedThreadId == GlobalData.MainThreadId)
                MyAPIGateway.Utilities.ShowMessage(sender, message);
            else
                MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Utilities.ShowMessage(sender, message));
        }

        public static long NextLong(this Random rand)
        {
            var buf = new byte[8];
            rand.NextBytes(buf);

            return BitConverter.ToInt64(buf, 0);
        }

        public static long NextLong(this Random rand, long min, long max)
        {
            var buf = new byte[8];
            rand.NextBytes(buf);
            long longRand = BitConverter.ToInt64(buf, 0);

            return (Math.Abs(longRand % (max - min)) + min);
        }

        public static float GetAtmosphereDensity(Vector3D position)
        {
            foreach (var planet in GlobalData.Planets)
            {
                if (planet.Closed || planet.MarkedForClose)
                    continue;

                if (Vector3D.DistanceSquared(position, planet.PositionComp.GetPosition()) >
                    planet.AtmosphereRadius * planet.AtmosphereRadius)
                    continue;
                return planet.GetAirDensity(position);
            }

            return 0;
        }
    }
}
