using System;
using System.Collections.Generic;
using DetectionEquipment.Shared.Utils;
using Sandbox.ModAPI;
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

        private readonly List<GridDrawPoint> _queuedGridPoints = new List<GridDrawPoint>();
        private readonly List<LineDrawPoint> _queuedLinePoints = new List<LineDrawPoint>();
        private readonly List<DrawPoint> _queuedPoints = new List<DrawPoint>();

        private struct GridDrawPoint
        {
            public Vector3I Position;
            public long EndOfLife;
            public Color Color;
            public IMyCubeGrid Grid;
        }

        private struct LineDrawPoint
        {
            public Vector3D Start;
            public Vector3D End;
            public long EndOfLife;
            public Color Color;
        }

        private struct DrawPoint
        {
            public Vector3D Position;
            public long EndOfLife;
            public Color Color;
        }

        public override void LoadData()
        {
            if (!MyAPIGateway.Utilities.IsDedicated)
                I = this;
        }

        protected override void UnloadData()
        {
            Log.GameUnloading = true;
            I = null;
        }

        public static void AddPoint(Vector3D globalPos, Color color, float duration)
        {
            if (I == null)
                return;

            lock (I._queuedGridPoints)
            {
                I._queuedPoints.Add(new DrawPoint
                {
                    Position = globalPos,
                    Color = color,
                    EndOfLife = DateTime.UtcNow.Ticks + (long)(duration * TimeSpan.TicksPerSecond)
                });
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
                I._queuedGridPoints.Add(new GridDrawPoint
                {
                    Position = blockPos,
                    EndOfLife = DateTime.UtcNow.Ticks + (long)(duration * TimeSpan.TicksPerSecond),
                    Color = color,
                    Grid = grid
                });
            }
        }

        public static void AddLine(Vector3D origin, Vector3D destination, Color color, float duration)
        {
            if (I == null)
                return;

            lock (I._queuedLinePoints)
            {
                I._queuedLinePoints.Add(new LineDrawPoint
                {
                    Start = origin,
                    End = destination,
                    EndOfLife = DateTime.UtcNow.Ticks + (long)(duration * TimeSpan.TicksPerSecond),
                    Color = color,
                });
            }
        }

        public override void Draw()
        {
            long nowTicks = DateTime.UtcNow.Ticks;

            lock (_queuedPoints)
            {
                for (var i = _queuedPoints.Count - 1; i >= 0; i--)
                {
                    var point = _queuedPoints[i];
                    DrawPoint0(point.Position, point.Color);

                    if (nowTicks > point.EndOfLife)
                        _queuedPoints.RemoveAt(i);
                }
            }

            lock (_queuedGridPoints)
            {
                for (var i = _queuedGridPoints.Count - 1; i >= 0; i--)
                {
                    var gridPoint = _queuedGridPoints[i];
                    DrawGridPoint0(gridPoint.Position, gridPoint.Grid, gridPoint.Color);

                    if (nowTicks > gridPoint.EndOfLife)
                        _queuedGridPoints.RemoveAt(i);
                }
            }

            lock (_queuedLinePoints)
            {
                for (var i = _queuedLinePoints.Count - 1; i >= 0; i--)
                {
                    var linePoint = _queuedLinePoints[i];
                    DrawLine0(linePoint.Start, linePoint.End, linePoint.Color);

                    if (nowTicks > linePoint.EndOfLife)
                        _queuedLinePoints.RemoveAt(i);
                }
            }
        }

        private void DrawPoint0(Vector3D globalPos, Color color)
        {
            //MyTransparentGeometry.AddPointBillboard(MaterialDot, color, globalPos, 1.25f, 0, blendType: BlendTypeEnum.PostPP);
            var depthScale = ToAlwaysOnTop(ref globalPos);
            MyTransparentGeometry.AddPointBillboard(MaterialDot, color * OnTopColorMul, globalPos, 0.35f * depthScale,
                0,
                blendType: BlendTypeEnum.LDR);
        }

        private void DrawGridPoint0(Vector3I blockPos, IMyCubeGrid grid, Color color)
        {
            DrawPoint0(GridToGlobal(blockPos, grid), color);
        }

        private void DrawLine0(Vector3D origin, Vector3D destination, Color color)
        {
            var length = (float)(destination - origin).Length();
            var direction = (destination - origin) / length;

            MyTransparentGeometry.AddLineBillboard(MaterialSquare, color, origin, direction, length, 0.15f);

            var depthScale = ToAlwaysOnTop(ref origin);
            direction *= depthScale;

            MyTransparentGeometry.AddLineBillboard(MaterialSquare, color * OnTopColorMul, origin, direction, length,
                0.15f * depthScale);
        }

        private static Vector3D GridToGlobal(Vector3I position, IMyCubeGrid grid)
        {
            return Vector3D.Rotate((Vector3D)position * 2.5f, grid.WorldMatrix) + grid.GetPosition();
        }

        private static float ToAlwaysOnTop(ref Vector3D position)
        {
            var camMatrix = MyAPIGateway.Session.Camera.WorldMatrix;
            position = camMatrix.Translation + (position - camMatrix.Translation) * DepthRatioF;

            return DepthRatioF;
        }
    }
}