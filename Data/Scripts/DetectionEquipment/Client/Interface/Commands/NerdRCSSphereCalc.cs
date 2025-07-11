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
        private static Vector3D[] values;
        private static Vector3D maxVals;
        private static IMyCubeGrid CurrentGrid = null;
        private static MatrixD InvRenderMatrix;

        private static int numDone;
        private static Vector3D? VisualizationPosition;
        private static IMyCubeGrid ValueVisualizationGrid;
        private static List<IMySlimBlock> BlocksToRemove = new List<IMySlimBlock>();
        private static List<MyObjectBuilder_CubeBlock> BlockedBlocks = new List<MyObjectBuilder_CubeBlock>();
        private static IEnumerator<bool> GridEnumerator;
        private static int num = 0;
        private static int numBlocksToAdd = 2000;
        private static float VisScale;
        private static string VisUnit;

        private static Vector3D[] positions;
        private static Color[] colors;
        public static void RenderSphere(string[] args)
        {

            if (GridEnumerator != null || IsJobActive)
            {
                MyAPIGateway.Utilities.ShowMessage("DetEq", "Error: Job is already in progress. Please wait for it to complete.");
                return;
            }
            if (CurrentGrid == null | values == null)
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
            if (values.Length < 16384)
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
                    if (ValueVisualizationGrid != null)
                    {
                        ValueVisualizationGrid.Close();
                        ValueVisualizationGrid = null;
                    }
                    colors = null;
                    positions = null;
                    VisualizationPosition = null;
                    if (GridEnumerator != null)
                    {
                        GridEnumerator.Dispose();
                        GridEnumerator = null;
                    }
                    BlocksToRemove.Clear();
                    BlockedBlocks.Clear();
                    break;
                default:
                    MyAPIGateway.Utilities.ShowMessage("DetEq", $"Error: second parameter '{arg}' not recognized. Please list 'RCS', 'VCS', or 'IRS' to render their appropriate spheres.");
                    return;
            }
        }
        public static void CalculateSphere(string[] args)
        {
            if (ValueVisualizationGrid != null)
            {
                ValueVisualizationGrid.Close();
                ValueVisualizationGrid = null;
            }

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
                InvRenderMatrix = MatrixD.Invert(CurrentGrid.WorldMatrixNormalizedInv);
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
                float percentDone = (float)numDone / PointCount * 100f;

                percentDone = (float)Math.Round(percentDone, 4);

                MyAPIGateway.Utilities.ShowNotification($"{percentDone}% done ({numDone}/{PointCount}", 1);
            }
            if (GridEnumerator != null && MyAPIGateway.Session.GameplayFrameCounter % 5 == 0)
            {
                num = 0;

                // make sure game stays above like 0.15 sim
                // once the grid gets large enough even adding 1 block still brings sim down to 0.2 so ensure its atleast 300 blocks/call
                if (MyAPIGateway.Physics.ServerSimulationRatio < 0.8f && numBlocksToAdd >= 400 /* 300 (min) + 100 (add/sub val) */)
                {
                    numBlocksToAdd -= 100;
                }
                else
                {
                    numBlocksToAdd += 100;
                }

                if (!GridEnumerator.MoveNext() || !GridEnumerator.Current)
                {
                    GridEnumerator.Dispose();
                    GridEnumerator = null;
                }
            }

            if ((CurrentGrid == null || CurrentGrid.Closed) && ValueVisualizationGrid != null)
            {
                CurrentGrid = null;
                ValueVisualizationGrid.Close();
                ValueVisualizationGrid = null;
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
            if (colors != null)
            {
                Vector3D camPos = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
                for (int i = 0; i < colors.Length; i++)
                {
                    float distMult = Math.Max((float)Vector3D.Distance(camPos, positions[i]) / 100f, 0.5f);
                    MyTransparentGeometry.AddPointBillboard(DebugDraw.MaterialDot, colors[i], positions[i], 0.35f * distMult,
                        0,
                        blendType: VRageRender.MyBillboard.BlendTypeEnum.AdditiveTop);
                }
            }
        }
        public static void Close()
        {
            if (ValueVisualizationGrid != null)
            {
                ValueVisualizationGrid.Close();
                ValueVisualizationGrid = null;
            }
            SpherePoints = null;
            values = null;
            CurrentGrid = null;
        }
        private static void SphereWorkFunc()
        {

            IsJobActive = true;
            SpherePoints = new Vector3D[PointCount];
            values = new Vector3D[PointCount];

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

                SpherePoints[i] = Vector3D.Rotate(new Vector3D(x, y, z), InvRenderMatrix);
                numDone++;
            });
            numDone = 0;
            MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Utilities.ShowMessage("DetEq", "Calculated point unit vectors."));

            List<GridTrack> tracks = new List<GridTrack>();
            foreach (var grid in attached)
            {
                tracks.Add(new GridTrack(grid));
            }
            maxVals = new Vector3D();

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

                        if (!TrackingUtils.HasLoSDir(SpherePoints[i], MyAPIGateway.Session.Player?.Character, track.Grid))
                            continue;

                        double trackVcs, trackRcs;
                        track.CalculateRcs(SpherePoints[i], out trackRcs, out trackVcs);
                        values[i].X += trackRcs;
                        values[i].Y += trackVcs;
                        values[i].Z += track.InfraredVisibility(SpherePoints[i] + track.Position, trackVcs);
                    }

                    if (values[i].X > maxVals.X)
                    {
                        maxVals.X = values[i].X;
                    }
                    if (values[i].Y > maxVals.Y)
                    {
                        maxVals.Y = values[i].Y;
                    }
                    if (values[i].Z > maxVals.Z)
                    {
                        maxVals.Z = values[i].Z;
                    }
                }
                catch (Exception ex)
                {
                    MyLog.Default.WriteLine($"{SpherePoints[i]} / {values[i]}");
                    MyLog.Default.Error(ex.ToString());
                }
                numDone++;
            });
            numDone = 0;

            if (IsValid)
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Utilities.ShowMessage("DetEq", "Calculated all DetEq values."));
            }
            else
            {
                MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Utilities.ShowMessage("DetEq", "Error: Grid was removed during operation. Stopping sphere angle calc."));
                CurrentGrid = null;
                SpherePoints = null;
                values = null;
                return;
            }

            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < PointCount; i++)
            {
                builder.AppendLine($"{SpherePoints[i].X},{SpherePoints[i].Y},{SpherePoints[i].Z}:" +
                    $"{values[i].X},{values[i].Y},{values[i].Z}");
                numDone++;
            }
            numDone = 0;

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
            if (ValueVisualizationGrid != null)
            {
                ValueVisualizationGrid.Close();
                ValueVisualizationGrid = null;
            }
            VisualizationPosition = CurrentGrid.WorldAABB.Center;

            positions = new Vector3D[PointCount];
            colors = new Color[PointCount];
            double maxValComparison = 0;

            switch (t)
            {
                case VisType.RCS:
                    maxValComparison = maxVals.X / VisScale;
                    VisUnit = "m^2";
                    break;
                case VisType.VCS:
                    maxValComparison = maxVals.Y / VisScale;
                    VisUnit = "m^2";
                    break;
                case VisType.IRS:
                    maxValComparison = maxVals.Z / VisScale;
                    VisUnit = "Wm^2";
                    break;
            }

            MyAPIGateway.Parallel.For(0, PointCount, (i) =>
            {
                double value = 0;
                switch (t)
                {
                    case VisType.RCS:
                        value = values[i].X / VisScale;
                        break;
                    case VisType.VCS:
                        value = values[i].Y / VisScale;
                        break;
                    case VisType.IRS:
                        value = values[i].Z / VisScale;
                        break;

                }

                positions[i] = -SpherePoints[i] * value + VisualizationPosition.Value;
                colors[i] = new Vector3(1 - (float)(value / maxValComparison), 1f, 1f).HSVtoColor().SetAlphaPct(1f) * DebugDraw.OnTopColorMul;
            });

            MyAPIGateway.Utilities.ShowMessage("DetEq", $"Visualizer made.");
            yield return false;
        }
        static IEnumerable<bool> ValuesToGrid(VisType t)
        {
            IsJobActive = true;
            VisualizationPosition = CurrentGrid.WorldAABB.Center;

            MyAPIGateway.Utilities.ShowMessage("DetEq", $"Started visualizing grid with a 1m:{VisScale}m^2 scale.");

            if (ValueVisualizationGrid != null)
            {
                ValueVisualizationGrid.Close();
                ValueVisualizationGrid = null;
            }


            var gridRCS = (MyObjectBuilder_CubeGrid)CurrentGrid.GetObjectBuilder(true);

            gridRCS.GridSizeEnum = MyCubeSize.Small;
            gridRCS.DisplayName = $"{gridRCS.Name} RCS Calc Visualized";

            MyAPIGateway.Entities.RemapObjectBuilder(gridRCS);
            ValueVisualizationGrid = (IMyCubeGrid)MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridRCS);

            ValueVisualizationGrid.GetBlocks(BlocksToRemove);

            double maxValComparison = 0;

            switch (t)
            {
                case VisType.RCS:
                    maxValComparison = maxVals.X / VisScale;
                    VisUnit = "m^2";
                    break;
                case VisType.VCS:
                    maxValComparison = maxVals.Y / VisScale;
                    VisUnit = "m^2";
                    break;
                case VisType.IRS:
                    maxValComparison = maxVals.Z / VisScale;
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
                        value = values[i].X / VisScale;
                        break;
                    case VisType.VCS:
                        value = values[i].Y / VisScale;
                        break;
                    case VisType.IRS:
                        value = values[i].Z / VisScale;
                        break;

                }

                Vector3D worldPos = -SpherePoints[i] * value + VisualizationPosition.Value;

                Vector3I gridPos = ValueVisualizationGrid.WorldToGridInteger(worldPos);
                block.Min = gridPos;

                block.ColorMaskHSV = PaintUtils.HSVToColorMask(new Vector3(1 - (float)(value / maxValComparison), 1f, 1f));

                if (ValueVisualizationGrid.CanAddCube(gridPos))
                {
                    ValueVisualizationGrid.AddBlock(block, false);
                    numDone++;
                }
                else
                {
                    BlockedBlocks.Add(block);
                }
                num++;
                if (num % numBlocksToAdd == 0)
                {
                    yield return true;
                }
            }
            foreach (var block in BlocksToRemove)
            {
                ValueVisualizationGrid.RemoveBlock(block, false);
            }
            foreach (var b in BlockedBlocks)
            {
                if (ValueVisualizationGrid.CanAddCube(b.Min))
                {
                    ValueVisualizationGrid.AddBlock(b, false);
                }
                numDone++;
            }
            yield return true;

            MyAPIGateway.Utilities.ShowMessage("DetEq", $"Visualizer made.");

            IsJobActive = false;
            BlocksToRemove.Clear();
            BlockedBlocks.Clear();

            yield return false;
        }
    }
}