using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Utils;
using RichHudFramework;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;

namespace DetectionEquipment.Client.Interface.Commands
{
    public static class NerdRCSSphereCalc
    {
        enum VisType
        {
            RCS,
            VCS,
            IRS
        }

        /// <summary>
        /// (sqrt(5)-1) * PI
        /// </summary>
        private static readonly double PHI = (Math.Sqrt(5) - 1) * Math.PI;

        private static bool IsJobActive;
        private static int PointCount;

        private static Vector3D[] SpherePoints;
        /// <summary>
        /// X is RCS, Y is VCS, Z is IRS from 1m away
        /// </summary>
        private static Vector3D[] DetectionValues;
        private static Vector3D MaximumValues;
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

        private static BillboardDrawData billboardPositions;
        class BillboardDrawData
        {
            public bool[] renderPos;
            public Vector3D[] positions;
            public Color[] colors;
            public float[] positionSizes;

            public BillboardDrawData(int count)
            {
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
            if (CurrentGrid == null | DetectionValues == null)
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
                type = ValuesToBillboards;
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

            billboardPositions = null;
            VisualizationPosition = null;
            GridDrawData.BlocksToRemove.Clear();
            GridDrawData.BlockedBlocks.Clear();
        }

        public static void CalculateSphere(string[] args)
        {
            GridDrawData.CloseGridIfPossible();

            int count = 1000;
            if (args.Length >= 2 && int.TryParse(args[1], out count))
            {

            }
            else
            {
                count = 1000;
            }

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
                PointCount = count;
                CurrentGrid = castGrid;

                MyAPIGateway.Parallel.Start(SphereWorkFunc);
                MyAPIGateway.Utilities.ShowMessage("DetEq", $"Started calculations for {PointCount} points.");
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
                float percentDone = (float)OperationsDone / PointCount * 100f;

                percentDone = (float)Math.Round(percentDone, 4);

                MyAPIGateway.Utilities.ShowNotification($"{percentDone}% done ({OperationsDone}/{PointCount}", 1);
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
            if (billboardPositions != null)
            {
                Vector3D camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
                if (billboardPositions.positions.Length < 16384)
                {
                    MyAPIGateway.Parallel.For(0, PointCount, (i) =>
                    {
                        billboardPositions.positionSizes[i] = Math.Max((float)Vector3D.Distance(camPos, billboardPositions.positions[i]) / 100f, 0.5f);

                        billboardPositions.renderPos[i] = true;
                    });
                }
                else
                {
                    MatrixD viewProjectionMat = MyAPIGateway.Session.Camera.ViewMatrix * MyAPIGateway.Session.Camera.ProjectionMatrix;
                    int count = 0;
                    MyAPIGateway.Parallel.For(0, PointCount, (i) =>
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
            SpherePoints = null;
            DetectionValues = null;
            CurrentGrid = null;
        }
        private static void SphereWorkFunc()
        {

            IsJobActive = true;
            SpherePoints = new Vector3D[PointCount];
            DetectionValues = new Vector3D[PointCount];


            var attached = new List<IMyCubeGrid>();
            CurrentGrid.GetGridGroup(GridLinkTypeEnum.Physical).GetGrids(attached);

            // Fibbonachi Sphere Algo
            MyAPIGateway.Parallel.For(0, PointCount, (i) =>
            {
                double y = 1 - (((double)i / (PointCount - 1)) * 2);
                double r = Math.Sqrt(1 - y * y);

                double theta = PHI * i;

                double x = Math.Cos(theta) * r;
                double z = Math.Sin(theta) * r;

                SpherePoints[i] = new Vector3D(x, y, z);
                OperationsDone++;
            });
            OperationsDone = 0;
            MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Utilities.ShowMessage("DetEq", "Calculated point unit vectors."));

            List<GridTrack> tracks = new List<GridTrack>();
            foreach (var grid in attached)
            {
                tracks.Add(new GridTrack(grid));
            }
            MaximumValues = new Vector3D();

            bool IsValid = true;
            // RCS calc for all the unit vectors from above
            MyAPIGateway.Parallel.For(0, PointCount, (i) =>
            {
                try
                {
                    foreach (var track in tracks)
                    {
                        if (track.Grid == null)
                        {
                            IsValid = false;
                            return;
                        }

                        if (!TrackingUtils.HasLoSDir(SpherePoints[i], track.Grid))
                            continue;

                        double trackVcs, trackRcs;
                        track.CalculateRcs(SpherePoints[i], out trackRcs, out trackVcs);
                        DetectionValues[i].X += trackRcs;
                        DetectionValues[i].Y += trackVcs;
                        DetectionValues[i].Z += track.InfraredVisibility(SpherePoints[i] + track.Position, trackVcs);
                    }

                    if (DetectionValues[i].X > MaximumValues.X)
                    {
                        MaximumValues.X = DetectionValues[i].X;
                    }
                    if (DetectionValues[i].Y > MaximumValues.Y)
                    {
                        MaximumValues.Y = DetectionValues[i].Y;
                    }
                    if (DetectionValues[i].Z > MaximumValues.Z)
                    {
                        MaximumValues.Z = DetectionValues[i].Z;
                    }
                }
                catch (Exception ex)
                {
                    MyLog.Default.WriteLine($"{SpherePoints[i]} / {DetectionValues[i]}");
                    MyLog.Default.Error(ex.ToString());
                }
                OperationsDone++;
            });
            OperationsDone = 0;

            if (IsValid)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Utilities.ShowMessage("DetEq", "Calculated all DetEq values."));
            }
            else
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Utilities.ShowMessage("DetEq", "Error: Grid was removed during operation. Stopping sphere angle calc."));
                CurrentGrid = null;
                SpherePoints = null;
                DetectionValues = null;
                return;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < PointCount; i++)
            {
                builder.AppendLine($"{SpherePoints[i].X},{SpherePoints[i].Y},{SpherePoints[i].Z}:" +
                    $"{DetectionValues[i].X},{DetectionValues[i].Y},{DetectionValues[i].Z}");
                OperationsDone++;
            }
            OperationsDone = 0;

            TextWriter w = MyAPIGateway.Utilities.WriteFileInWorldStorage("DetEq_RCS_Sphere.txt", typeof(NerdRCSSphereCalc));
            lock (w)
            {
                w.Write(builder.ToString());
                w.Close();
                w.Dispose();
            }

            MyAPIGateway.Utilities.ShowMessage("DetEq", "Sent to file.");
            IsJobActive = false;
        }
        static IEnumerable<bool> ValuesToBillboards(VisType t)
        {
            MyAPIGateway.Utilities.ShowMessage("DetEq", $"Started visualizing grid with a 1m:{VisScale}m^2 scale.");
            ResetRender();
            VisualizationPosition = CurrentGrid.WorldAABB.Center;
            
            billboardPositions = new BillboardDrawData(PointCount);
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

            MyAPIGateway.Parallel.For(0, PointCount, (i) =>
            {
                double value = 0;
                switch (t)
                {
                    case VisType.RCS:
                        value = DetectionValues[i].X / VisScale;
                        break;
                    case VisType.VCS:
                        value = DetectionValues[i].Y / VisScale;
                        break;
                    case VisType.IRS:
                        value = DetectionValues[i].Z / VisScale;
                        break;

                }

                billboardPositions.positions[i] = -SpherePoints[i] * value + VisualizationPosition.Value;
                billboardPositions.colors[i] = new Vector3(1 - (float)(value / maxValComparison), 1f, 1f).HSVtoColor().SetAlphaPct(1f) * DebugDraw.OnTopColorMul;
            });

            MyAPIGateway.Utilities.ShowMessage("DetEq", $"Visualizer made.");
            yield return false;
        }
        static IEnumerable<bool> ValuesToGrid(VisType t)
        {
            IsJobActive = true;
            VisualizationPosition = CurrentGrid.WorldAABB.Center;

            MyAPIGateway.Utilities.ShowMessage("DetEq", $"Started visualizing grid with a 1m:{VisScale}m^2 scale.");

            ResetRender();

            var gridRCS = (MyObjectBuilder_CubeGrid)CurrentGrid.GetObjectBuilder(true);

            gridRCS.GridSizeEnum = MyCubeSize.Small;
            gridRCS.DisplayName = $"{gridRCS.Name} RCS Calc Visualized";

            MyAPIGateway.Entities.RemapObjectBuilder(gridRCS);
            GridDrawData.ValueVisualizationGrid = (IMyCubeGrid)MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridRCS);

            GridDrawData.ValueVisualizationGrid.GetBlocks(GridDrawData.BlocksToRemove);

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
            yield return true;
            for (int i = 0; i < PointCount; i++)
            {
                MyObjectBuilder_CubeBlock block = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_CubeBlock>("SmallBlockArmorBlock");

                double value = 0;
                switch (t)
                {
                    case VisType.RCS:
                        value = DetectionValues[i].X / VisScale;
                        break;
                    case VisType.VCS:
                        value = DetectionValues[i].Y / VisScale;
                        break;
                    case VisType.IRS:
                        value = DetectionValues[i].Z / VisScale;
                        break;

                }

                Vector3D worldPos = -SpherePoints[i] * value + VisualizationPosition.Value;

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