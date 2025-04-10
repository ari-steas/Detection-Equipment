using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using VRage;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using static VRageRender.MyBillboard;

namespace DetectionEquipment.Shared
{
    [MySessionComponentDescriptor(MyUpdateOrder.NoUpdate)]
    public class DebugDraw : MySessionComponentBase
    {
        protected const float OnTopColorMul = 0.5f;

        private const float DepthRatioF = 0.01f;
        // i'm gonna kiss digi on the 

        public static DebugDraw I;
        public static readonly MyStringId MaterialDot = MyStringId.GetOrCompute("WhiteDot");
        public static readonly MyStringId MaterialSquare = MyStringId.GetOrCompute("Square");

        private readonly Dictionary<Vector3I, MyTuple<long, Color, IMyCubeGrid>> _queuedGridPoints =
            new Dictionary<Vector3I, MyTuple<long, Color, IMyCubeGrid>>();

        private readonly Dictionary<MyTuple<Vector3D, Vector3D>, MyTuple<long, Color>> _queuedLinePoints =
            new Dictionary<MyTuple<Vector3D, Vector3D>, MyTuple<long, Color>>();

        private readonly Dictionary<Vector3D, MyTuple<long, Color>> _queuedPoints =
            new Dictionary<Vector3D, MyTuple<long, Color>>();

        public override void LoadData()
        {
            if (!MyAPIGateway.Utilities.IsDedicated)
                I = this;
        }

        protected override void UnloadData()
        {
            I = null;
        }

        public static void AddPoint(Vector3D globalPos, Color color, float duration)
        {
            if (I == null)
                return;

            lock (I._queuedGridPoints)
            {
                I._queuedPoints[globalPos] = new MyTuple<long, Color>(DateTime.UtcNow.Ticks + (long)(duration * TimeSpan.TicksPerSecond), color);
            }
        }

        public static void AddGps(string name, Vector3D position, float duration)
        {
            var gps = MyAPIGateway.Session.GPS.Create(name, string.Empty, position, true, true);
            gps.DiscardAt =
                MyAPIGateway.Session.ElapsedPlayTime.Add(new TimeSpan((long)(duration * TimeSpan.TicksPerSecond)));
            MyAPIGateway.Session.GPS.AddLocalGps(gps);
        }

        public static void AddGridGps(string name, Vector3I gridPosition, IMyCubeGrid grid, float duration)
        {
            AddGps(name, GridToGlobal(gridPosition, grid), duration);
        }

        public static void AddGridPoint(Vector3I blockPos, IMyCubeGrid grid, Color color, float duration)
        {
            if (I == null)
                return;

            lock (I._queuedGridPoints)
            {
                I._queuedGridPoints[blockPos] =
                    new MyTuple<long, Color, IMyCubeGrid>(
                        DateTime.UtcNow.Ticks + (long)(duration * TimeSpan.TicksPerSecond), color, grid);
            }
        }

        public static void AddLine(Vector3D origin, Vector3D destination, Color color, float duration)
        {
            if (I == null)
                return;

            lock (I._queuedLinePoints)
            {
                var key = new MyTuple<Vector3D, Vector3D>(origin, destination);
                I._queuedLinePoints[key] =
                    new MyTuple<long, Color>(DateTime.UtcNow.Ticks + (long)(duration * TimeSpan.TicksPerSecond), color);
            }
        }

        public override void Draw()
        {
            foreach (var pair in _queuedPoints.ToList())
            {
                DrawPoint0(pair.Key, pair.Value.Item2);

                if (DateTime.UtcNow.Ticks > pair.Value.Item1)
                    _queuedPoints.Remove(pair.Key);
            }

            foreach (var kvp in _queuedGridPoints.ToList())
            {
                DrawGridPoint0(kvp.Key, kvp.Value.Item3, kvp.Value.Item2);

                if (DateTime.UtcNow.Ticks > kvp.Value.Item1)
                    _queuedGridPoints.Remove(kvp.Key);
            }

            foreach (var kvp in _queuedLinePoints.ToList())
            {
                DrawLine0(kvp.Key.Item1, kvp.Key.Item2, kvp.Value.Item2);

                if (DateTime.UtcNow.Ticks > kvp.Value.Item1)
                    _queuedLinePoints.Remove(kvp.Key);
            }
        }

        public void DrawPoint0(Vector3D globalPos, Color color)
        {
            //MyTransparentGeometry.AddPointBillboard(MaterialDot, color, globalPos, 1.25f, 0, blendType: BlendTypeEnum.PostPP);
            var depthScale = ToAlwaysOnTop(ref globalPos);
            MyTransparentGeometry.AddPointBillboard(MaterialDot, color * OnTopColorMul, globalPos, 0.35f * depthScale,
                0,
                blendType: BlendTypeEnum.LDR);
        }

        public void DrawGridPoint0(Vector3I blockPos, IMyCubeGrid grid, Color color)
        {
            DrawPoint0(GridToGlobal(blockPos, grid), color);
        }

        public void DrawLine0(Vector3D origin, Vector3D destination, Color color)
        {
            var length = (float)(destination - origin).Length();
            var direction = (destination - origin) / length;

            MyTransparentGeometry.AddLineBillboard(MaterialSquare, color, origin, direction, length, 0.15f);

            var depthScale = ToAlwaysOnTop(ref origin);
            direction *= depthScale;

            MyTransparentGeometry.AddLineBillboard(MaterialSquare, color * OnTopColorMul, origin, direction, length,
                0.15f * depthScale);
        }

        public static Vector3D GridToGlobal(Vector3I position, IMyCubeGrid grid)
        {
            return Vector3D.Rotate((Vector3D)position * 2.5f, grid.WorldMatrix) + grid.GetPosition();
        }

        protected static float ToAlwaysOnTop(ref Vector3D position)
        {
            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            position = camMatrix.Translation + (position - camMatrix.Translation) * DepthRatioF;

            return DepthRatioF;
        }
    }
}