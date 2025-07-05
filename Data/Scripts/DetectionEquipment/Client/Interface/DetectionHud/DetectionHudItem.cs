using System.Collections.Generic;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Structs;
using DetectionEquipment.Shared.Utils;
using RichHudFramework.UI;
using Sandbox.ModAPI;
using RichHudFramework.UI.Rendering;
using VRage.Game.Entity;
using VRage.ModAPI;
using VRageMath;
using static DetectionEquipment.Shared.Structs.WorldDetectionInfo;

namespace DetectionEquipment.Client.Interface.DetectionHud
{
    internal class DetectionHudItem : HudElementBase
    {
        public HudDetectionInfo Detection;
        private HudMarker OutlineBox;
        internal LabelBox InfoLabel;
        private LabelBox NewInfoLabel => new LabelBox(OutlineBox)
        {
            ParentAlignment = ParentAlignments.Right | ParentAlignments.Top | ParentAlignments.InnerV,
            Color = Color.Transparent,
            Format = GlyphFormat.White.WithColor(UserData.HudTextColor),
            Padding = new Vector2(2, 1),
            TextBoard =
            {
                BuilderMode = TextBuilderModes.Lined,
            }
        };

        public const int MaxBoxSize = 400, MinBoxSize = 25;

        public DetectionHudItem(HudParentBase parent, HudDetectionInfo info, int visible) : base(parent)
        {
            Detection = info;

            OutlineBox = new HudMarker(this, info);
            InfoLabel = NewInfoLabel;

            Update();
            SetVisible(visible);
        }

        public void SetVisible(int value)
        {
            // don't set visible if we don't have to
            if ((OutlineBox.Visible == (value != 0)) && (InfoLabel.Visible == (value == 1)))
                return;

            OutlineBox.Visible = value != 0;
            InfoLabel.Visible = value == 1;
            Visible = value != 0;
        }

        public void Update(HudDetectionInfo detection)
        {
            Detection = detection;
            if (HudMarker.GetMarkerType(detection) != OutlineBox.Type || HudMarker.GetMarkerColor(detection) != OutlineBox.Color)
            {
                RemoveChild(OutlineBox);
                OutlineBox = new HudMarker(this, detection);
                InfoLabel.Parent.RemoveChild(InfoLabel);
                OutlineBox.RegisterChild(InfoLabel);
            }
            if (InfoLabel.Format.Color != UserData.HudTextColor)
                InfoLabel.Format = GlyphFormat.White.WithColor(UserData.HudTextColor);
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
            if (MyAPIGateway.Session.Player?.Character == null)
                return "";
            var dist = Vector3D.Distance(MyAPIGateway.Session.Player.Character.GetPosition(), Detection.Position);
            return $"{(dist > 1000 ? dist / 1000 : dist):N1}{(dist > 1000 ? "km" : "m")} \u00b1{(Detection.Error > 1000 ? Detection.Error / 1000 : Detection.Error):N1}{(Detection.Error > 1000 ? "km" : "m")}\n";
        }

        private string IffStr =>
            $"IFF: {(Detection.IffCodes.Length == 0 ? "N/A" : string.Join(", ", Detection.IffCodes))}\n";

        private string VelocityStr =>
            $"Vel: {(Detection.Velocity == null ? "NOLOC" : $"{Detection.Velocity.Value.Length():N0}m/s")}\n";

        private string CrossSectionStr()
        {
            List<string> detectionTypePrefix = new List<string>();
            string detectionTypeSuffix;
            if ((Detection.DetectionType & DetectionFlags.Radar) == DetectionFlags.Radar)
                detectionTypePrefix.Add("RCS");
            if ((Detection.DetectionType & DetectionFlags.PassiveRadar) == DetectionFlags.PassiveRadar)
                detectionTypePrefix.Add("RWR");
            if ((Detection.DetectionType & DetectionFlags.Optical) == DetectionFlags.Optical)
                detectionTypePrefix.Add("VCS");
            if ((Detection.DetectionType & DetectionFlags.Infrared) == DetectionFlags.Infrared)
                detectionTypePrefix.Add("IRS");

            switch (Detection.DetectionType)
            {
                case DetectionFlags.Radar:
                    detectionTypeSuffix = "m^2";
                    break;
                case DetectionFlags.PassiveRadar:
                    detectionTypeSuffix = "dB";
                    break;
                case DetectionFlags.Optical:
                    detectionTypeSuffix = "m^2";
                    break;
                case DetectionFlags.Infrared:
                    detectionTypeSuffix = "Wm^2";
                    break;
                default:
                    detectionTypeSuffix = "(?)";
                    break;
            }
            return $"{string.Join("|", detectionTypePrefix)}: {Detection.CrossSection:N0} {detectionTypeSuffix}\n";
        }

        private class HudMarker : HudElementBase
        {
            private const string FullTargetTexture = "TargetBoxCorner"; // DON'T FORGET THE TRANSPARENT MATERIAL DEF NERD
            private const string NoIffTargetTexture = "TargetOctCorner";
            private const string RwrTargetTexture = "TargetTri";

            public readonly MarkerType Type;
            public readonly Color Color;

            public HudMarker(HudParentBase parent, HudDetectionInfo detection) : base(parent)
            {
                Size = Vector2.One * MinBoxSize * 2;
                string material;
                Color = GetMarkerColor(detection);
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
                            Color = Color,
                            Size = Vector2.One * MinBoxSize,
                            Material = new Material(material + "_LT", Vector2.One * 32),
                            ParentAlignment = ParentAlignments.Inner | ParentAlignments.Left | ParentAlignments.Top,
                        };
                        new TexturedBox(this)
                        {
                            Color = Color,
                            Size = Vector2.One * MinBoxSize,
                            Material = new Material(material + "_RT", Vector2.One * 32),
                            ParentAlignment = ParentAlignments.Inner | ParentAlignments.Right | ParentAlignments.Top,
                        };
                        new TexturedBox(this)
                        {
                            Color = Color,
                            Size = Vector2.One * MinBoxSize,
                            Material = new Material(material + "_RB", Vector2.One * 32),
                            ParentAlignment = ParentAlignments.Inner | ParentAlignments.Right | ParentAlignments.Bottom,
                        };
                        new TexturedBox(this)
                        {
                            Color = Color,
                            Size = Vector2.One * MinBoxSize,
                            Material = new Material(material + "_LB", Vector2.One * 32),
                            ParentAlignment = ParentAlignments.Inner | ParentAlignments.Left | ParentAlignments.Bottom,
                        };
                        break;
                    case MarkerType.Rwr:
                        Size = Vector2.One * 32;
                        new TexturedBox(this)
                        {
                            Color = Color,
                            Size = Vector2.One * MinBoxSize,
                            Material = new Material(material, Vector2.One * 32),
                            ParentAlignment = ParentAlignments.Center,
                        };
                        break;
                }
            }

            public void ScaleOutline(HudDetectionInfo detection, IReadOnlyHudSpaceNode hudSpace)
            {
                if (Type == MarkerType.Rwr)
                    return;

                if (detection.Entity == null)
                {
                    Size = Vector2.One * MinBoxSize;
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

            public static MarkerType GetMarkerType(HudDetectionInfo info)
            {
                if (info.IffCodes?.Length > 0)
                    return MarkerType.Full;
                if (info.DetectionType == DetectionFlags.PassiveRadar)
                    return MarkerType.Rwr;
                return MarkerType.NoIff;
            }

            public static Color GetMarkerColor(HudDetectionInfo info)
            {
                switch (info.Relations)
                {
                    case MyRelationsBetweenPlayers.Allies:
                    case MyRelationsBetweenPlayers.Self:
                        return UserData.HudFriendlyColor;
                    case MyRelationsBetweenPlayers.Enemies:
                        return UserData.HudEnemyColor;
                    default:
                        return UserData.HudNeutralColor;
                }
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
