using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Utils;
using RichHudFramework;
using Sandbox.Game.Entities;
using Sandbox.Game.Replication;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using VRageRender;

namespace DetectionEquipment.Client.Interface.Commands
{
    using static DetectionEquipment.Shared.Utils.IcoSphereConstructor.Shape;
    using static DetectionEquipment.Shared.Utils.IcoSphereConstructor;
    /// <summary>
    /// Terribly written (cleanliness wise) code, have fun if you want to read it
    /// </summary>
    public static class RCSVisualizer
    {
        enum VisType
        {
            RCS = 0,
            VCS = 1,
            IRS = 2,
        }

        /// <summary>
        /// (sqrt(5)-1) * PI
        /// </summary>
        //private static readonly double PHI = (Math.Sqrt(5) - 1) * Math.PI;

        private static bool IsJobActive;

        private static int UniquePointCount;
        private static Triangle[] SphereTris;
        /// <summary>
        /// X is RCS, Y is VCS, Z is IRS from 1m away
        /// </summary>
        private static Triangle[] DetectionValues;
        private static ColorableShape RenderShape;
        class ColorableShape
        {
            public MyBillboard[] Billboards;
            public ColorableShape(Triangle[] pts, Triangle[] values, VisType type, Vector3 maxValues, Vector3D Offset, float mult)
            {
                Billboards = new MyBillboard[values.Length];

                for (int i = 0; i < values.Length; i++)
                {
                    Triangle Triangle = pts[i].MultiplyWith(values[i], (int)type);
                    Billboards[i] = new MyBillboard()
                    {
                        BlendType = MyBillboard.BlendTypeEnum.Standard,
                        Material = MyStringId.GetOrCompute("Square"),
                        Position0 = -Triangle.V1 * mult + Offset,
                        Position1 = -Triangle.V2 * mult + Offset,
                        Position2 = -Triangle.V3 * mult + Offset,
                        Position3 = -Triangle.V3 * mult + Offset,
                        UVOffset = Vector2.Zero,
                        UVSize = Vector2.One,
                        LocalType = MyBillboard.LocalTypeEnum.Custom,

                        CustomViewProjection = -1,

                        Color = new Vector3(
                        (
                        (1 - (float)(values[i].V1.GetDim((int)type) / maxValues.GetDim((int)type))) +
                        (1 - (float)(values[i].V2.GetDim((int)type) / maxValues.GetDim((int)type))) +
                        (1 - (float)(values[i].V3.GetDim((int)type) / maxValues.GetDim((int)type)))
                        ) / 3,
                        1f, 1f).HSVtoColor().SetAlphaPct(1f) * DebugDraw.OnTopColorMul,
                        ColorIntensity = 1f,

                    };
                }
            }
        }
        private static Vector3 MaximumValues;
        private static IMyCubeGrid CurrentGrid = null;

        private static int OperationsDone;
        private static Vector3D? VisualizationPosition;
        private static IEnumerator<bool> GridEnumerator;
        private static float VisScale;
        private static string VisUnit;

        static class GridDrawData
        {
            public static IMyCubeGrid ValueVisualizationGrid;
            public static List<IMySlimBlock> BlocksToRemove = new List<IMySlimBlock>();
            public static List<MyObjectBuilder_CubeBlock> BlockedBlocks = new List<MyObjectBuilder_CubeBlock>();
            public static int blocksAddedThisTick = 0;
            public static int numBlocksToAdd = 2000;

            public static void CloseGridIfPossible()
            {
                if (ValueVisualizationGrid != null)
                {
                    ValueVisualizationGrid.Close();
                    ValueVisualizationGrid = null;
                }
            }
        }

        private static BillboardPointDrawData billboardPositions;
        class BillboardPointDrawData
        {
            public int PointCount;
            public bool[] renderPos;
            public Vector3D[] positions;
            public Color[] colors;
            public float[] positionSizes;

            public BillboardPointDrawData(int count)
            {
                PointCount = count;
                renderPos = new bool[count];
                positions = new Vector3D[count];
                colors = new Color[count];
                positionSizes = new float[count];
            }
        }

        public static void RenderSphere(string[] args)
        {

            if (GridEnumerator != null || IsJobActive)
            {
                MyAPIGateway.Utilities.ShowMessage("DetEq", "Error: Job is already in progress. Please wait for it to complete.");
                return;
            }
            if (CurrentGrid == null || DetectionValues == null)
            {
                MyAPIGateway.Utilities.ShowMessage("DetEq", "Error: no data to visualize!");
                return;

            }

            if (args.Length < 2)
            {
                MyAPIGateway.Utilities.ShowMessage("DetEq", "Error: second parameter not listed. Please list 'RCS', 'VCS', or 'IRS' to render their appropriate spheres.");
                return;
            }

            float Scale = -1;
            if (args.Length >= 3)
            {
                if (!float.TryParse(args[2], out Scale))
                {
                    Scale = -1;
                }
                else
                {
                    VisScale = Scale;
                }
            }

            Func<VisType, IEnumerable<bool>> type;
            if (DetectionValues.Length < 50000)
            {
                if (args.Length >= 4 && args[3].ToLowerInvariant() == "points")
                {
                    type = ValuesToPoints;
                }
                else
                {
                    type = ValuesToTriangles;
                }



            }
            else
            {
                type = ValuesToGrid;
            }

            string arg = args[1].ToLowerInvariant();
            switch (arg)
            {
                case "rcs":
                    if (Scale == -1)
                        VisScale = 10;

                    GridEnumerator = type(VisType.RCS).GetEnumerator();
                    break;
                case "vcs":
                    if (Scale == -1)
                        VisScale = 10;

                    GridEnumerator = type(VisType.VCS).GetEnumerator();
                    break;
                case "irs":
                    if (Scale == -1)
                        VisScale = 100000;// divide by 100000 because these values are in the MW

                    GridEnumerator = type(VisType.IRS).GetEnumerator();
                    break;
                case "reset":
                    ResetRender();

                    if (GridEnumerator != null)
                    {
                        GridEnumerator.Dispose();
                        GridEnumerator = null;
                    }
                    break;
                default:
                    MyAPIGateway.Utilities.ShowMessage("DetEq", $"Error: second parameter '{arg}' not recognized. Please list 'RCS', 'VCS', or 'IRS' to render their appropriate spheres.");
                    return;
            }
        }

        private static void ResetRender()
        {
            GridDrawData.CloseGridIfPossible();

            RenderShape = null;
            billboardPositions = null;
            VisualizationPosition = null;
            GridDrawData.BlocksToRemove.Clear();
            GridDrawData.BlockedBlocks.Clear();
        }

        public static void CalculateSphere(string[] args)
        {
            GridDrawData.CloseGridIfPossible();

            var castMatrix = GlobalData.DebugLevel > 0 && MyAPIGateway.Session.Player?.Character != null ? MyAPIGateway.Session.Player.Character.WorldMatrix : MyAPIGateway.Session.Camera.WorldMatrix;
            var castEnt = MiscUtils.RaycastEntityFromMatrix(castMatrix);
            if (castEnt == null)
            {
                MyAPIGateway.Utilities.ShowMessage("DetEq", "Error: no entity found.");
                return;
            }

            Vector3D position = castMatrix.Translation;




            var castGrid = castEnt as IMyCubeGrid;
            if (castGrid != null)
            {
                CurrentGrid = castGrid;

                MyAPIGateway.Parallel.Start(SphereWorkFunc);
                MyAPIGateway.Utilities.ShowMessage("DetEq", $"Started calculations for {TrackingUtils.VisibilityDirectionCache.Length} points.");
                return;
            }
            MyAPIGateway.Utilities.ShowMessage("DetEq", "Error: no grid found.");
        }
        public static void Update()
        {
            if (CurrentGrid != null && !CurrentGrid.IsStatic)
            {
                CurrentGrid.IsStatic = true;
            }

            if (IsJobActive)
            {
                float percentDone = (float)OperationsDone / TrackingUtils.VisibilityDirectionCache.Length * 100f;

                percentDone = (float)Math.Round(percentDone, 4);

                MyAPIGateway.Utilities.ShowNotification($"{percentDone}% done ({OperationsDone}/{TrackingUtils.VisibilityDirectionCache.Length}", 1);
            }
            if (GridEnumerator != null && MyAPIGateway.Session.GameplayFrameCounter % 5 == 0)
            {
                GridDrawData.blocksAddedThisTick = 0;

                // make sure game stays above like 0.15 sim
                // once the grid gets large enough even adding 1 block still brings sim down to 0.2 so ensure its atleast 300 blocks/call
                if (MyAPIGateway.Physics.ServerSimulationRatio < 0.8f && GridDrawData.numBlocksToAdd >= 400 /* 300 (min) + 100 (add/sub val) */)
                {
                    GridDrawData.numBlocksToAdd -= 100;
                }
                else
                {
                    GridDrawData.numBlocksToAdd += 100;
                }

                if (!GridEnumerator.MoveNext() || !GridEnumerator.Current)
                {
                    GridEnumerator.Dispose();
                    GridEnumerator = null;
                }
            }

            if (CurrentGrid == null || CurrentGrid.Closed)
            {
                CurrentGrid = null;
                GridDrawData.CloseGridIfPossible();
            }
            if (VisualizationPosition != null)
            {
                double value = Vector3D.Distance(VisualizationPosition.Value, MyAPIGateway.Session.Camera.WorldMatrix.Translation) * VisScale;
                value = Math.Round(value, 4);
                MyAPIGateway.Utilities.ShowNotification($"Camera position is at {value}{VisUnit} in the visualization.", 1);

                
            }
        }

        public static void Draw()
        {
            if (CurrentGrid == null)
            {
                ResetRender();
            }

            if (billboardPositions != null)
            {
                Vector3D camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
                if (billboardPositions.positions.Length < 16384)
                {
                    MyAPIGateway.Parallel.For(0, billboardPositions.PointCount, (i) =>
                    {
                        billboardPositions.positionSizes[i] = Math.Max((float)Vector3D.Distance(camPos, billboardPositions.positions[i]) / 100f, 0.5f);

                        billboardPositions.renderPos[i] = true;
                    });
                }
                else
                {
                    MatrixD viewProjectionMat = MyAPIGateway.Session.Camera.ViewMatrix * MyAPIGateway.Session.Camera.ProjectionMatrix;
                    int count = 0;
                    MyAPIGateway.Parallel.For(0, billboardPositions.PointCount, (i) =>
                    {


                        billboardPositions.renderPos[i] = count < 16384 && IsVisible(billboardPositions.positions[i], viewProjectionMat, camPos, MyAPIGateway.Session.Camera.WorldMatrix.Forward);
                        if (billboardPositions.renderPos[i])
                        {
                            billboardPositions.positionSizes[i] = Math.Max((float)Vector3D.Distance(camPos, billboardPositions.positions[i]) / 100f, 0.5f);
                            count++;
                        }
                    });
                }
                for (int i = 0; i < billboardPositions.colors.Length; i++)
                {
                    if (billboardPositions.renderPos[i])
                    {
                        MyTransparentGeometry.AddPointBillboard(DebugDraw.MaterialDot, billboardPositions.colors[i], billboardPositions.positions[i], 0.35f * billboardPositions.positionSizes[i],
                            0,
                            blendType: VRageRender.MyBillboard.BlendTypeEnum.AdditiveTop);
                    }

                }

            }
            else if (RenderShape != null)
            {

                for (int i = 0; i < RenderShape.Billboards.Length; i++)
                {
                    MyBillboard board = RenderShape.Billboards[i];
                    board.DistanceSquared = (float)Vector3D.Distance(
                        (board.Position0 + board.Position1 + board.Position2 + board.Position3) / 4,
                        MyAPIGateway.Session.Camera.Position
                        );
                }

                MyTransparentGeometry.AddBillboards(RenderShape.Billboards, false);
            }

        }
        private static bool IsVisible(Vector3D pos, MatrixD viewProjectionMat, Vector3D camPosition, Vector3D camForwards)
        {
            Vector3D ScreenCoords = Vector3D.Transform(pos, viewProjectionMat);
            bool OffScreen = ScreenCoords.X > 1 || ScreenCoords.X < -1 || ScreenCoords.Y > 1 || ScreenCoords.Y < -1;
            bool Behind = Vector3D.Dot((pos - camPosition).Normalized(), camForwards) < 0;

            return !OffScreen && !Behind;
        }
        public static void Close()
        {
            ResetRender();
            //SpherePoints = null;
            DetectionValues = null;
            CurrentGrid = null;
        }
        private static void SphereWorkFunc()
        {

            IsJobActive = true;

            int Len = TrackingUtils.CurrentConstructor.Sphere.Tris.Length;
            UniquePointCount = TrackingUtils.VisibilityDirectionCache.Length; // going to hope this is always right
            SphereTris = new Triangle[Len];
            TrackingUtils.CurrentConstructor.Sphere.Tris.CopyTo(SphereTris, 0);
            DetectionValues = new Triangle[Len];


            var attached = new List<IMyCubeGrid>();
            CurrentGrid.GetGridGroup(GridLinkTypeEnum.Physical).GetGrids(attached);

            MatrixD gridWM = CurrentGrid.WorldMatrix;
            MyAPIGateway.Parallel.For(0, Len, (i) =>
            {
                SphereTris[i] = new Triangle(
                    Vector3D.Rotate(SphereTris[i].V1, gridWM),
                    Vector3D.Rotate(SphereTris[i].V2, gridWM),
                    Vector3D.Rotate(SphereTris[i].V3, gridWM)
                    );
            });
            
            //// Fibbonachi Sphere Algo
            //MyAPIGateway.Parallel.For(0, PointCount, (i) =>
            //{
            //    double y = 1 - (((double)i / (PointCount - 1)) * 2);
            //    double r = Math.Sqrt(1 - y * y);

            //    double theta = PHI * i;

            //    double x = Math.Cos(theta) * r;
            //    double z = Math.Sin(theta) * r;

            //    SpherePoints[i] = new Vector3D(x, y, z);
            //    OperationsDone++;
            //});
            //OperationsDone = 0;
            //MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Utilities.ShowMessage("DetEq", "Calculated point unit vectors."));

            List<GridTrack> tracks = new List<GridTrack>();
            foreach (var grid in attached)
            {
                tracks.Add(new GridTrack(grid));
            }
            MaximumValues = new Vector3();

            bool IsValid = true;
            Dictionary<Vector3, Vector3> CachedPoints = new Dictionary<Vector3, Vector3>();
            for (int i = 0; i < Len; i++)
            {
                try
                {
                    DetectionValues[i] = new Triangle(
                        GetValuesForDirection(tracks, CachedPoints, ref IsValid, SphereTris[i].V1),
                        GetValuesForDirection(tracks, CachedPoints, ref IsValid, SphereTris[i].V2),
                        GetValuesForDirection(tracks, CachedPoints, ref IsValid, SphereTris[i].V3)
                        );
                }
                catch (Exception ex)
                {
                    MyLog.Default.WriteLine($"{SphereTris[i]} / {DetectionValues[i]}");
                    MyLog.Default.Error(ex.ToString());
                }
                OperationsDone++;
            };
            OperationsDone = 0;

            if (IsValid)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Utilities.ShowMessage("DetEq", "Calculated all DetEq values."));
            }
            else
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Utilities.ShowMessage("DetEq", "Error: Grid was removed during operation. Stopping sphere angle calc."));
                CurrentGrid = null;
                SphereTris = null;
                DetectionValues = null;
                return;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < CachedPoints.Count; i++)
            {
                var element = CachedPoints.ElementAt(i);
                builder.AppendLine($"{element.Key.X},{element.Key.Y},{element.Key.Z}:" +
                    $"{element.Value.X},{element.Value.Y},{element.Value.Z}");
                OperationsDone++;
            }
            OperationsDone = 0;

            TextWriter w = MyAPIGateway.Utilities.WriteFileInWorldStorage("DetEq_RCS_Sphere.txt", typeof(RCSVisualizer));
            lock (w)
            {
                w.Write(builder.ToString());
                w.Close();
                w.Dispose();
            }

            MyAPIGateway.Utilities.ShowMessage("DetEq", "Sent to file 'DetEq_RCS_Sphere.txt'.");
            IsJobActive = false;
        }

        private static Vector3 GetValuesForDirection(List<GridTrack> tracks, Dictionary<Vector3, Vector3> cachedValues, ref bool IsValid, Vector3 dir)
        {
            Vector3 values;
            if (cachedValues.TryGetValue(dir, out values))
            {
                return values;
            }
            values = Vector3.Zero;
            foreach (var track in tracks)
            {
                if (track.Grid == null)
                {
                    IsValid = false;
                    cachedValues.Add(dir, values);
                    return values;
                }

                if (!TrackingUtils.HasLoSDir(dir, MyAPIGateway.Session.Player?.Character, track.Grid))
                    continue;

                double trackVcs, trackRcs;
                track.CalculateRcs(dir, out trackRcs, out trackVcs);
                values.X += (float)trackRcs;
                values.Y += (float)trackVcs;
                values.Z += (float)track.InfraredVisibility(dir + values, trackVcs);
            }

            if (values.X > MaximumValues.X)
            {
                MaximumValues.X = values.X;
            }
            if (values.Y > MaximumValues.Y)
            {
                MaximumValues.Y = values.Y;
            }
            if (values.Z > MaximumValues.Z)
            {
                MaximumValues.Z = values.Z;
            }
            cachedValues.Add(dir, values);
            return values;
        }
        static IEnumerable<bool> ValuesToTriangles(VisType t)
        {
            
            ResetRender();
            VisualizationPosition = CurrentGrid.WorldAABB.Center;

            RenderShape = new ColorableShape(SphereTris, DetectionValues, t, MaximumValues, VisualizationPosition.Value, 1/VisScale);
            switch (t)
            {
                case VisType.RCS:
                    VisUnit = "m^2";
                    break;
                case VisType.VCS:
                    VisUnit = "m^2";
                    break;
                case VisType.IRS:
                    VisUnit = "Wm^2";
                    break;
            }
            MyAPIGateway.Utilities.ShowMessage("DetEq", $"Started visualizing grid with a 1m:{VisScale}{VisUnit} scale.");
            yield return false;
        }
        static IEnumerable<bool> ValuesToPoints(VisType t)
        {
            
            ResetRender();
            VisualizationPosition = CurrentGrid.WorldAABB.Center;
            
            billboardPositions = new BillboardPointDrawData(UniquePointCount);
            HashSet<Vector3> positionsTraversed = new HashSet<Vector3>();
            double maxValComparison = 0;

            switch (t)
            {
                case VisType.RCS:
                    maxValComparison = MaximumValues.X / VisScale;
                    VisUnit = "m^2";
                    break;
                case VisType.VCS:
                    maxValComparison = MaximumValues.Y / VisScale;
                    VisUnit = "m^2";
                    break;
                case VisType.IRS:
                    maxValComparison = MaximumValues.Z / VisScale;
                    VisUnit = "Wm^2";
                    break;
            }
            MyAPIGateway.Utilities.ShowMessage("DetEq", $"Started visualizing grid with a 1m:{VisScale}{VisUnit} scale.");
            int index = 0;
            for (int i = 0; i < SphereTris.Length * 3; i++)
            {
                if (positionsTraversed.Add(SphereTris[i / 3].GetVertex(i)))
                {
                    double value = 0;
                    switch (t)
                    {
                        case VisType.RCS:
                            value = DetectionValues[i / 3].GetVertex(i).X / VisScale;
                            break;
                        case VisType.VCS:
                            value = DetectionValues[i / 3].GetVertex(i).Y / VisScale;
                            break;
                        case VisType.IRS:
                            value = DetectionValues[i / 3].GetVertex(i).Z / VisScale;
                            break;

                    }

                    billboardPositions.positions[index] = -(Vector3D)SphereTris[i / 3].GetVertex(i) * value + VisualizationPosition.Value;
                    billboardPositions.colors[index] = new Vector3(1 - (float)(value / maxValComparison), 1f, 1f).HSVtoColor().SetAlphaPct(1f) * DebugDraw.OnTopColorMul;
                    index++;
                }
            }

            MyAPIGateway.Utilities.ShowMessage("DetEq", $"Visualizer made.");
            yield return false;
        }
        static IEnumerable<bool> ValuesToGrid(VisType t)
        {
            IsJobActive = true;
            VisualizationPosition = CurrentGrid.WorldAABB.Center;

            

            ResetRender();

            var gridRCS = (MyObjectBuilder_CubeGrid)CurrentGrid.GetObjectBuilder(true);

            gridRCS.GridSizeEnum = MyCubeSize.Small;
            gridRCS.DisplayName = $"{gridRCS.Name} RCS Calc Visualized";

            MyAPIGateway.Entities.RemapObjectBuilder(gridRCS);
            GridDrawData.ValueVisualizationGrid = (IMyCubeGrid)MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridRCS);

            GridDrawData.ValueVisualizationGrid.GetBlocks(GridDrawData.BlocksToRemove);
            HashSet<Vector3> positionsTraversed = new HashSet<Vector3>();
            double maxValComparison = 0;

            switch (t)
            {
                case VisType.RCS:
                    maxValComparison = MaximumValues.X / VisScale;
                    VisUnit = "m^2";
                    break;
                case VisType.VCS:
                    maxValComparison = MaximumValues.Y / VisScale;
                    VisUnit = "m^2";
                    break;
                case VisType.IRS:
                    maxValComparison = MaximumValues.Z / VisScale;
                    VisUnit = "Wm^2";
                    break;
            }
            MyAPIGateway.Utilities.ShowMessage("DetEq", $"Started visualizing grid with a 1m:{VisScale}{VisUnit} scale.");
            yield return true;
            int index = 0;
            MyObjectBuilder_CubeBlock blockInit = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_CubeBlock>("SmallBlockArmorBlock");
            for (int i = 0; i < SphereTris.Length * 3; i++)
            {
                if (positionsTraversed.Add(SphereTris[i / 3].GetVertex(i)))
                {
                    double value = 0;
                    switch (t)
                    {
                        case VisType.RCS:
                            value = DetectionValues[i / 3].GetVertex(i).X / VisScale;
                            break;
                        case VisType.VCS:
                            value = DetectionValues[i / 3].GetVertex(i).Y / VisScale;
                            break;
                        case VisType.IRS:
                            value = DetectionValues[i / 3].GetVertex(i).Z / VisScale;
                            break;

                    }

                    MyObjectBuilder_CubeBlock block = (MyObjectBuilder_CubeBlock)blockInit.Clone();
                    Vector3D worldPos = -(Vector3D)SphereTris[i / 3].GetVertex(i) * value + VisualizationPosition.Value;

                    Vector3I gridPos = GridDrawData.ValueVisualizationGrid.WorldToGridInteger(worldPos);
                    block.Min = gridPos;

                    block.ColorMaskHSV = PaintUtils.HSVToColorMask(new Vector3(1 - (float)(value / maxValComparison), 1f, 1f));

                    if (GridDrawData.ValueVisualizationGrid.CanAddCube(gridPos))
                    {
                        GridDrawData.ValueVisualizationGrid.AddBlock(block, false);
                        OperationsDone++;
                    }
                    else
                    {
                        GridDrawData.BlockedBlocks.Add(block);
                    }
                    GridDrawData.blocksAddedThisTick++;
                    if (GridDrawData.blocksAddedThisTick % GridDrawData.numBlocksToAdd == 0)
                    {
                        yield return true;
                    }

                    index++;
                }
            }
            foreach (var block in GridDrawData.BlocksToRemove)
            {
                GridDrawData.ValueVisualizationGrid.RemoveBlock(block, false);
            }
            foreach (var b in GridDrawData.BlockedBlocks)
            {
                if (GridDrawData.ValueVisualizationGrid.CanAddCube(b.Min))
                {
                    GridDrawData.ValueVisualizationGrid.AddBlock(b, false);
                }
                OperationsDone++;
            }
            yield return true;

            MyAPIGateway.Utilities.ShowMessage("DetEq", $"Visualizer made.");

            IsJobActive = false;
            GridDrawData.BlocksToRemove.Clear();
            GridDrawData.BlockedBlocks.Clear();

            yield return false;
        }
    }
}