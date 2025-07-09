using DetectionEquipment.Server.Tracking;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Utils;
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
        private static IMyCubeGrid RCSCalcGrid;
        private static List<IMySlimBlock> BlocksToRemove = new List<IMySlimBlock>();
        private static List<MyObjectBuilder_CubeBlock> BlockedBlocks = new List<MyObjectBuilder_CubeBlock>();
        private static IEnumerator<bool> GridEnumerator;
        private static int num = 0;
        private static int numBlocksToAdd = 2000;
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


            string arg = args[1].ToLowerInvariant();
            switch (arg)
            {
                case "rcs":
                    GridEnumerator = ValuesToGrid(VisType.RCS).GetEnumerator();
                    GridEnumerator.MoveNext();
                    break;
                case "vcs":
                    GridEnumerator = ValuesToGrid(VisType.VCS).GetEnumerator();
                    GridEnumerator.MoveNext();
                    break;
                case "irs":
                    GridEnumerator = ValuesToGrid(VisType.IRS).GetEnumerator();
                    GridEnumerator.MoveNext();
                    break;
                default:
                    MyAPIGateway.Utilities.ShowMessage("DetEq", $"Error: second parameter '{arg}' not recognize. Please list 'RCS', 'VCS', or 'IRS' to render their appropriate spheres.");
                    return;
            }
        }
        public static void CalculateSphere(string[] args)
        {
            if (RCSCalcGrid != null)
            {
                RCSCalcGrid.Close();
                RCSCalcGrid = null;
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
        }
        public static void Close()
        {
            if (RCSCalcGrid != null)
            {
                RCSCalcGrid.Close();
                RCSCalcGrid = null;
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
            // RCS calc for all the unit vectors from above
            MyAPIGateway.Parallel.For(0, PointCount, (i) =>
            {
                try
                {
                    foreach (var track in tracks)
                    {
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
            MyAPIGateway.Utilities.InvokeOnGameThread(() => MyAPIGateway.Utilities.ShowMessage("DetEq", "Calculated all DetEq values."));

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

        static IEnumerable<bool> ValuesToGrid(VisType t)
        {
            IsJobActive = true;
            MyAPIGateway.Utilities.ShowMessage("DetEq", "Started visualizing grid.");
            if (RCSCalcGrid != null)
            {
                RCSCalcGrid.Close();
                RCSCalcGrid = null;
            }
            yield return true;

            var gridRCS = (MyObjectBuilder_CubeGrid)CurrentGrid.GetObjectBuilder(true);

            gridRCS.GridSizeEnum = MyCubeSize.Small;
            gridRCS.DisplayName = $"{gridRCS.Name} RCS Calc Visualized";

            MyAPIGateway.Entities.RemapObjectBuilder(gridRCS);
            RCSCalcGrid = (IMyCubeGrid)MyAPIGateway.Entities.CreateFromObjectBuilderAndAdd(gridRCS);

            RCSCalcGrid.GetBlocks(BlocksToRemove);

            double maxValComparison = 0;

            switch (t)
            {
                case VisType.RCS:
                    maxValComparison = maxVals.X;
                    break;
                case VisType.VCS:
                    maxValComparison = maxVals.Y;
                    break;
                case VisType.IRS:
                    maxValComparison = maxVals.Z;
                    break;
            }

            for (int i = 0; i < PointCount; i++)
            {
                MyObjectBuilder_CubeBlock block = MyObjectBuilderSerializer.CreateNewObject<MyObjectBuilder_CubeBlock>("SmallBlockArmorBlock");

                double value = 0;
                switch (t)
                {
                    case VisType.RCS:
                        value = values[i].X / 10f;
                        break;
                    case VisType.VCS:
                        value = values[i].Y / 10f;
                        break;
                    case VisType.IRS:
                        value = values[i].Z / 100000f; // divide by 100000 because these values are in the MW
                        break;

                }

                Vector3D worldPos = SpherePoints[i] * value + CurrentGrid.WorldAABB.Center;

                Vector3I gridPos = RCSCalcGrid.WorldToGridInteger(worldPos);
                block.Min = gridPos;

                block.ColorMaskHSV = MyColorPickerConstants.HSVToHSVOffset(new Vector3(1 - (float)(value / maxValComparison), 1f, 1f));

                if (RCSCalcGrid.CanAddCube(gridPos))
                {
                    RCSCalcGrid.AddBlock(block, false);
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
                RCSCalcGrid.RemoveBlock(block, false);
            }
            foreach (var b in BlockedBlocks)
            {
                if (RCSCalcGrid.CanAddCube(b.Min))
                {
                    RCSCalcGrid.AddBlock(b, false);
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