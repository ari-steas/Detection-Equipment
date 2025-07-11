using Sandbox.Definitions;
using Sandbox.Game.GUI;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Game;
using VRage.Utils;
using VRageMath;

namespace DetectionEquipment.Shared.Utils
{
    public class PaintUtils
    {
        #region Credit to digi's paint gun mod for these
        const string ARMOR_SUFFIX = "_Armor";
        const string TEST_ARMOR_SUBTYPE = "TestArmor";

        public static List<MyStringHash> ValidSkins;
        public static readonly MyStringHash NoSkin = MyStringHash.GetOrCompute("");
        /// <summary>
        /// Float HSV to game's color mask (0-1/-1-1/-1-1).
        /// </summary>
        public static Vector3 HSVToColorMask(Vector3 hsv)
        {
            return MyColorPickerConstants.HSVToHSVOffset(hsv);
        }

        /// <summary>
        /// Game's color mask (0-1/-1-1/-1-1) to float HSV.
        /// </summary>
        public static Vector3 ColorMaskToHSV(Vector3 colorMask)
        {
            return MyColorPickerConstants.HSVOffsetToHSV(colorMask);
        }

        /// <summary>
        /// Game's color mask (0-1/-1-1/-1-1) to HSV (0-360/0-100/0-100) for printing to users.
        /// </summary>
        public static Vector3 ColorMaskToFriendlyHSV(Vector3 colorMask)
        {
            Vector3 hsv = ColorMaskToHSV(colorMask);
            return new Vector3(Math.Round(hsv.X * 360, 1), Math.Round(hsv.Y * 100, 1), Math.Round(hsv.Z * 100, 1));
        }

        /// <summary>
        /// Game's color mask (0-1/-1-1/-1-1) to byte RGB.
        /// </summary>
        public static Color ColorMaskToRGB(Vector3 colorMask)
        {
            return ColorMaskToHSV(colorMask).HSVtoColor();
        }

        /// <summary>
        /// Byte RGB to game's color mask (0-1/-1-1/-1-1).
        /// </summary>
        public static Vector3 RGBToColorMask(Color rgb)
        {
            return HSVToColorMask(ColorExtensions.ColorToHSV(rgb));
        }

        public static bool ColorMaskEquals(Vector3 colorMask1, Vector3 colorMask2)
        {
            return colorMask1.PackHSVToUint() == colorMask2.PackHSVToUint();
        }

        public static Vector3 ColorMaskNormalize(Vector3 colorMask)
        {
            return ColorExtensions.UnpackHSVFromUint(colorMask.PackHSVToUint());
        }

        public static bool IsSkinAsset(MyAssetModifierDefinition assetDef)
        {
            if (assetDef == null)
                return false;

            try
            {
                if (assetDef.Id.SubtypeName == TEST_ARMOR_SUBTYPE)
                    return false; // skip unusable vanilla test armor

                if (assetDef.Id.SubtypeName.EndsWith(ARMOR_SUFFIX))
                    return true;

                if (assetDef.Icons != null)
                {
                    foreach (string icon in assetDef.Icons)
                    {
                        if (icon == null)
                            continue;

                        if (icon.IndexOf("armor", StringComparison.OrdinalIgnoreCase) != -1)
                            return true;
                    }
                }

                if (assetDef.Textures != null)
                {
                    foreach (MyObjectBuilder_AssetModifierDefinition.MyAssetTexture texture in assetDef.Textures)
                    {
                        if (texture.Location == null)
                            continue;

                        if (texture.Location.Equals("SquarePlate", StringComparison.OrdinalIgnoreCase))
                            return true;

                        if (texture.Location.Equals("PaintedMetal_Colorable", StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"Error in IsSkinAsset() for asset={assetDef.Id.ToString()}\n{e}");
            }

            return false;
        }

        public static void InitBlockSkins()
        {
            ValidSkins = new List<MyStringHash>();
            foreach (MyAssetModifierDefinition assetDef in MyDefinitionManager.Static.GetAssetModifierDefinitions())
            {
                if (IsSkinAsset(assetDef))
                {
                    
                    bool AlwaysOwned = assetDef == null || (!assetDef.Context.IsBaseGame && (assetDef.DLCs == null || assetDef.DLCs.Length == 0));

                    if (AlwaysOwned || MyAPIGateway.DLC.HasDefinitionDLC(assetDef, MyAPIGateway.Multiplayer.MyId))
                    {
                        ValidSkins.Add(assetDef.Id.SubtypeId);
                    }
                }
            }
        }
        #endregion
    }
}