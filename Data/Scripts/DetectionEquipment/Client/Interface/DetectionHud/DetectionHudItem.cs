using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using RichHudFramework.UI;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RichHudFramework;
using RichHudFramework.UI.Rendering;
using VRage.ModAPI;
using VRageMath;

namespace DetectionEquipment.Client.Interface.DetectionHud
{
    internal class DetectionHudItem : HudElementBase
    {
        public WorldDetectionInfo Detection;
        private HudMarker OutlineBox;
        internal LabelBox InfoLabel;
        private LabelBox NewInfoLabel => new LabelBox(OutlineBox)
        {
            ParentAlignment = ParentAlignments.Right | ParentAlignments.Top | ParentAlignments.InnerV,
            Color = Color.Transparent,
            Format = GlyphFormat.White.WithColor(Color.Lime),
            Padding = new Vector2(2, 1),
            TextBoard =
            {
                BuilderMode = TextBuilderModes.Lined,
            }
        };

        public const int MaxBoxSize = 400, MinBoxSize = 25;

        public DetectionHudItem(HudParentBase parent, WorldDetectionInfo info) : base(parent)
        {
            Detection = info;

            OutlineBox = new HudMarker(this, info);
            InfoLabel = NewInfoLabel;

            Update();
        }

        public void Update(WorldDetectionInfo detection)
        {
            Detection = detection;
            if (HudMarker.GetMarkerType(detection) != OutlineBox.Type)
            {
                RemoveChild(OutlineBox);
                OutlineBox = new HudMarker(this, detection);
                InfoLabel = NewInfoLabel;
            }
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
            {
                Visible = true;
            }

            Offset = new Vector2((float)(offsetPos.X / scalar), (float)(offsetPos.Y / scalar));
            OutlineBox.ScaleOutline(Detection, Parent.HudSpace);
            //MyAPIGateway.Utilities.ShowMessage("HudItem", $"Offset: {Offset} | {offsetPos}");



            InfoLabel.Text =
                DistanceStr() +
                IffStr +
                VelocityStr +
                CrossSectionStr();
        }

        private string DistanceStr()
        {
            var dist = Vector3D.Distance(MyAPIGateway.Session.Player.Character.GetPosition(), Detection.Position);
            return $"{(dist > 1000 ? dist / 1000 : dist):N1}{(dist > 1000 ? "km" : "m")} \u00b1{Detection.Error / dist * 100d:N0}%\n";
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

        private class HudMarker : HudElementBase
        {
            private const string FullTargetTexture = "TargetBoxCorner"; // DON'T FORGET THE TRANSPARENT MATERIAL DEF NERD
            private const string NoIffTargetTexture = "TargetOctCorner";
            private const string RwrTargetTexture = "TargetTri";

            public readonly MarkerType Type;
            private Color _color = Color.Red;

            public HudMarker(HudParentBase parent, WorldDetectionInfo detection) : base(parent)
            {
                Size = Vector2.One * MinBoxSize * 2;
                string material;
                Type = GetMarkerType(detection);

                switch (Type)
                {
                    case MarkerType.Full:
                        material = FullTargetTexture;
                        break;
                    case MarkerType.Rwr:
                        material = RwrTargetTexture;
                        break;
                    default:
                        material = NoIffTargetTexture;
                        break;
                }

                switch (Type)
                {
                    case MarkerType.NoIff:
                    case MarkerType.Full:
                        new TexturedBox(this)
                        {
                            Color = _color,
                            Size = Vector2.One * MinBoxSize,
                            Material = new Material(material + "_LT", Vector2.One * 32),
                            ParentAlignment = ParentAlignments.Inner | ParentAlignments.Left | ParentAlignments.Top,
                        };
                        new TexturedBox(this)
                        {
                            Color = _color,
                            Size = Vector2.One * MinBoxSize,
                            Material = new Material(material + "_RT", Vector2.One * 32),
                            ParentAlignment = ParentAlignments.Inner | ParentAlignments.Right | ParentAlignments.Top,
                        };
                        new TexturedBox(this)
                        {
                            Color = _color,
                            Size = Vector2.One * MinBoxSize,
                            Material = new Material(material + "_RB", Vector2.One * 32),
                            ParentAlignment = ParentAlignments.Inner | ParentAlignments.Right | ParentAlignments.Bottom,
                        };
                        new TexturedBox(this)
                        {
                            Color = _color,
                            Size = Vector2.One * MinBoxSize,
                            Material = new Material(material + "_LB", Vector2.One * 32),
                            ParentAlignment = ParentAlignments.Inner | ParentAlignments.Left | ParentAlignments.Bottom,
                        };
                        break;
                    case MarkerType.Rwr:
                        Size = Vector2.One * 32;
                        new TexturedBox(this)
                        {
                            Color = _color,
                            Size = Vector2.One * MinBoxSize,
                            Material = new Material(material, Vector2.One * 32),
                            ParentAlignment = ParentAlignments.Center,
                        };
                        break;
                }
            }

            public void ScaleOutline(WorldDetectionInfo detection, IReadOnlyHudSpaceNode hudSpace)
            {
                if (Type == MarkerType.Rwr)
                    return;

                if (detection.Entity == null)
                {
                    Size = Vector2.One * 50;
                    return;
                }

                var invMatrix = detection.Entity.WorldMatrix * MatrixD.Invert(hudSpace.PlaneToWorld);
                var box = ((IMyEntity)detection.Entity).LocalAABB;
                var nearPlane = MyAPIGateway.Session.Camera.NearPlaneDistance;
                Vector2D min = new Vector2(float.MaxValue, float.MaxValue);
                Vector2D max = new Vector2(float.MinValue, float.MinValue);
                foreach (var corner in box.Corners)
                {
                    var projectedCorner = Vector3D.Transform(corner, invMatrix);
                    var scalar = -projectedCorner.Z / nearPlane;
                    var x = projectedCorner.X / scalar;
                    var y = projectedCorner.Y / scalar;
                    if (x < min.X)
                        min.X = x;
                    if (y < min.Y)
                        min.Y = y;
                    if (x > max.X)
                        max.X = x;
                    if (y > max.Y)
                        max.Y = y;
                }

                Size = new Vector2((float)MathUtils.ClampAbs(max.X - min.X, MinBoxSize, MaxBoxSize), (float)MathUtils.ClampAbs(max.Y - min.Y, 25, 400));
            }

            public static MarkerType GetMarkerType(WorldDetectionInfo info)
            {
                if (info.IffCodes?.Length > 0)
                    return MarkerType.Full;
                if (info.DetectionType == SensorDefinition.SensorType.PassiveRadar)
                    return MarkerType.Rwr;
                return MarkerType.NoIff;
            }

            public enum MarkerType
            {
                Full,
                NoIff,
                Rwr,
            }
        }
    }
}
