using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.ExternalApis;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using RichHudFramework;
using RichHudFramework.UI;
using RichHudFramework.UI.Client;
using Sandbox.ModAPI;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Client.Interface
{
    internal static class DetectionHud
    {
        private static Dictionary<long, DetectionHudItem> _hudItems;

        public static void Init()
        {
            _hudItems = new Dictionary<long, DetectionHudItem>();
            ApiManager.RichHudOnLoadRegisterOrInvoke(OnRichHudReady);
            Log.Info("DetectionHud", "Initialized.");
        }

        public static void Close()
        {
            _hudItems = null;
            Log.Info("DetectionHud", "Closed.");
        }

        public static void Draw()
        {
            foreach (var item in _hudItems.Values)
                item.Update();
        }

        public static void UpdateDetections(ICollection<WorldDetectionInfo> detections)
        {
            // TODO cache these
            var deadItems = new HashSet<long>(_hudItems.Keys);
            foreach (var detection in detections)
            {
                if (_hudItems.ContainsKey(detection.EntityId))
                {
                    _hudItems[detection.EntityId].Update(detection);
                    deadItems.Remove(detection.EntityId);
                }
                else
                {
                    _hudItems[detection.EntityId] = new DetectionHudItem(HudMain.HighDpiRoot, detection);
                }
            }

            foreach (var deadItem in deadItems)
            {
                HudMain.HighDpiRoot.RemoveChild(_hudItems[deadItem]);
                _hudItems.Remove(deadItem);
            }
        }

        private static void OnRichHudReady()
        {
            Log.Info("DetectionHud", "RichHud notified ready!");
        }

        private class DetectionHudItem : HudElementBase
        {
            public WorldDetectionInfo Detection;
            internal TexturedBox OutlineBox;
            internal LabelBox InfoLabel;

            public DetectionHudItem(HudParentBase parent, WorldDetectionInfo info) : base(parent)
            {
                Detection = info;

                OutlineBox = new TexturedBox(this)
                {
                    Color = Color.Red.SetAlphaPct(0.5f),
                    Size = Vector2.One * 50,
                };
                InfoLabel = new LabelBox(OutlineBox)
                {
                    ParentAlignment = ParentAlignments.Right | ParentAlignments.Top | ParentAlignments.InnerV,
                    Color = Color.Transparent,
                    Format = GlyphFormat.White.WithColor(Color.Lime),
                    Padding = new Vector2(2, 1),
                    TextBoard =
                    {
                        BuilderMode = TextBuilderModes.Lined
                    }
                };

                Update();
            }

            public void Update(WorldDetectionInfo detection)
            {
                Detection = detection;
                Update();
            }

            public void Update()
            {
                var offsetPos = Vector3D.Transform(Detection.Position, MatrixD.Invert(Parent.HudSpace.PlaneToWorld));
                var scalar = -offsetPos.Z / MyAPIGateway.Session.Camera.NearPlaneDistance;

                if (scalar < 0)
                {
                    Visible = false;
                    return;
                }
                else
                    Visible = true;

                Offset = new Vector2((float) (offsetPos.X/scalar), (float) (offsetPos.Y/scalar));
                ScaleOutline();
                //MyAPIGateway.Utilities.ShowMessage("HudItem", $"Offset: {Offset} | {offsetPos}");

                

                InfoLabel.Text =
                    DistanceStr() +
                    IffStr +
                    VelocityStr +
                    CrossSectionStr();
            }

            private void ScaleOutline()
            {
                if (Detection.Entity == null)
                {
                    OutlineBox.Size = Vector2.One * 50;
                    return;
                }

                var invMatrix = MatrixD.Invert(Parent.HudSpace.PlaneToWorld);
                var box = ((IMyEntity)Detection.Entity).WorldAABB;
                var nearPlane = MyAPIGateway.Session.Camera.NearPlaneDistance;
                Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2 max = new Vector2(float.MinValue, float.MinValue);
                for (int i = 0; i < BoundingBoxD.NUMBER_OF_CORNERS; ++i)
                {
                    var projectedCorner = Vector3D.Transform(box.GetCorner(i), invMatrix);
                    var scalar = -projectedCorner.Z / nearPlane;
                    var x = (float) (projectedCorner.X / scalar);
                    var y = (float) (projectedCorner.Y / scalar);
                    if (x < min.X)
                        min.X = x;
                    if (y < min.Y)
                        min.Y = y;
                    if (x > max.X)
                        max.X = x;
                    if (y > max.Y)
                        max.Y = y;
                }

                OutlineBox.Size = new Vector2(max.X - min.X, max.Y - min.Y);
            }

            private string DistanceStr()
            {
                var dist = Vector3D.Distance(MyAPIGateway.Session.Player.Character.GetPosition(), Detection.Position);
                return $"{(dist > 1000 ? dist/1000 : dist):N1}{(dist > 1000 ? "km" : "m")} \u00b1{(Detection.Error/dist)*100d:N0}%\n";
            }

            private string IffStr =>
                $"IFF: {(Detection.IffCodes.Length == 0 ? "N/A" : string.Join(", ", Detection.IffCodes))}\n";

            private string VelocityStr =>
                $"Vel: {(Detection.Velocity == null ? "NOLOC" : $"{Detection.Velocity.Value.Length():N0}m/s")}\n";

            private string CrossSectionStr()
            {
                string detectionTypePrefix = "DCS", detectionTypeSuffix = "";
                switch (Detection.DetectionType)
                {
                    case SensorDefinition.SensorType.Radar:
                        detectionTypePrefix = "RCS";
                        detectionTypeSuffix = "m^2";
                        break;
                    case SensorDefinition.SensorType.PassiveRadar:
                        detectionTypePrefix = "RWR";
                        detectionTypeSuffix = "dB";
                        break;
                    case SensorDefinition.SensorType.Optical:
                        detectionTypePrefix = "VCS";
                        detectionTypeSuffix = "m^2";
                        break;
                    case SensorDefinition.SensorType.Infrared:
                        detectionTypePrefix = "IRS";
                        detectionTypeSuffix = "Wm^2";
                        break;
                    case SensorDefinition.SensorType.None:
                        return "";
                }
                return $"{detectionTypePrefix}: {Detection.CrossSection:N0} {detectionTypeSuffix}\n";
            }
        }
    }
}
