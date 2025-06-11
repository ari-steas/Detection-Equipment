using System;
using System.Collections.Generic;
using System.Text;
using DetectionEquipment.Client.BlockLogic;
using DetectionEquipment.Client.BlockLogic.Sensors;
using DetectionEquipment.Shared;
using DetectionEquipment.Shared.Definitions;
using DetectionEquipment.Shared.Utils;
using RichHudFramework;
using Sandbox.Game.GameSystems.TextSurfaceScripts;
using Sandbox.ModAPI;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI;
using VRageMath;

namespace DetectionEquipment.Client.Interface.Apps
{
    [MyTextSurfaceScript("DetEq_SensorSearchApp", "Sensor Sweep")]
    public class SensorSearchApp : MyTSSCommon
    {
        public override ScriptUpdate NeedsUpdate => ScriptUpdate.Update10; // doesn't need to be updated often

        public new readonly IMyTerminalBlock Block;

        public SensorSearchApp(IMyTextSurface surface, IMyCubeBlock block, Vector2 size) : base(surface, block, size)
        {
            Block = (IMyTerminalBlock) block;
        }

        public override void Run()
        {
            base.Run();
            
            try
            {
                Draw();
            }
            catch (Exception ex)
            {
                Log.Exception("SensorSearchApp", ex);
            }
        }

        private void Draw()
        {
            Vector2 screenSize = Surface.SurfaceSize;
            Vector2 screenCorner = (Surface.TextureSize - screenSize) * 0.5f;
            Vector2 center = Surface.TextureSize / 2;
            float scale = 1;

            var frame = Surface.DrawFrame();

            // Drawing sprites works exactly like in PB API.
            // Therefore this guide applies: https://github.com/malware-dev/MDK-SE/wiki/Text-Panels-and-Drawing-Sprites

            // there are also some helper methods from the MyTSSCommon that this extends.
            // like: AddBackground(frame, Surface.ScriptBackgroundColor); - a grid-textured background

            // the colors in the terminal are Surface.ScriptBackgroundColor and Surface.ScriptForegroundColor, the other ones without Script in name are for text/image mode.

            var lcdScale = ClampToLcd(Block.CubeGrid.LocalAABB.Size).Abs();
            lcdScale *= new Vector2(50 / lcdScale.X)/lcdScale;
            var gridLcdCenter = lcdScale * -ClampToLcd(Block.CubeGrid.LocalAABB.Center) + center;

            {
                // Ship box scaled to ship size
                frame.Add(new MySprite
                {
                    Type = SpriteType.TEXTURE,
                    Alignment = TextAlignment.CENTER,
                    Data = "SquareHollow",
                    Size = ClampToLcd(Block.CubeGrid.LocalAABB.Size).Abs() * lcdScale + 8 * Vector2.One,
                    Color = ForegroundColor.SetAlphaPct(0.5f),
                    RotationOrScale = 0f
                });
            }

            HashSet<IMyCubeBlock> sensorBlocks;
            var sensorCount = new Dictionary<string, int>();
            if (SensorBlockManager.SensorBlocks.TryGetValue(Block.CubeGrid, out sensorBlocks))
            {
                foreach (var sensorBlock in sensorBlocks)
                {
                    var logic = sensorBlock.GetLogic<ClientSensorLogic>();
                    var sensorScreenPos = ClampToLcd(sensorBlock.LocalMatrix.Translation) * lcdScale + gridLcdCenter;

                    // Sensor block sprite
                    frame.Add(new MySprite
                    {
                        Type = SpriteType.TEXTURE,
                        Alignment = TextAlignment.CENTER,
                        Data = GetSensorShape(logic),
                        Position = sensorScreenPos,
                        Size = new Vector2(12, 12),
                        Color = new Color(0, 255, 0, 255),
                        RotationOrScale = 0f
                    });


                    foreach (var subSensor in logic.Sensors.Values)
                    {
                        var sensorStr = subSensor.Definition.Type.ToString();
                        if (sensorCount.ContainsKey(sensorStr))
                            sensorCount[sensorStr]++;
                        else
                            sensorCount[sensorStr] = 1;

                        if (subSensor.Aperture >= Math.PI)
                            continue;

                        var clampDirection = ClampToLcd(Vector3D.Rotate(subSensor.Direction, Block.CubeGrid.WorldMatrix));
                        float centerAngle = clampDirection.Angle();
                        //var size = new Vector2(2, screenSize.X/2);
                        var size = new Vector2(2, ClampToLcd(Vector3D.Rotate(subSensor.Direction, Block.CubeGrid.WorldMatrix)).Length() * screenSize.Length());

                        frame.Add(new MySprite
                        {
                            Type = SpriteType.TEXTURE,
                            Alignment = TextAlignment.CENTER,
                            Data = "SquareSimple",
                            Position = sensorScreenPos + MathUtils.FromPolar(centerAngle + subSensor.Aperture, size.Y/2),
                            Size = size,
                            Color = ForegroundColor,
                            RotationOrScale = centerAngle + (float) Math.PI/2 + subSensor.Aperture
                        });
                        frame.Add(new MySprite
                        {
                            Type = SpriteType.TEXTURE,
                            Alignment = TextAlignment.CENTER,
                            Data = "SquareSimple",
                            Position = sensorScreenPos + MathUtils.FromPolar(centerAngle - subSensor.Aperture, size.Y/2),
                            Size = size,
                            Color = ForegroundColor,
                            RotationOrScale = centerAngle + (float) Math.PI/2 - subSensor.Aperture
                        });
                    }
                }
            }

            var sensorCountSb = new StringBuilder();
            foreach (var type in sensorCount)
                sensorCountSb.Append($"{type.Value}x {type.Key}\n");

            var text = MySprite.CreateText(sensorCountSb.ToString(), "Monospace", Surface.ScriptForegroundColor, 0.5f, TextAlignment.LEFT);
            text.Position = screenCorner + new Vector2(16, 16); // 16px from topleft corner of the visible surface
            frame.Add(text);


            frame.Dispose();
        }

        private Vector2 ClampToLcd(Vector3 gridPos) => new Vector2((gridPos * Block.LocalMatrix.Right).Sum, (gridPos * Block.LocalMatrix.Backward).Sum);

        private string GetSensorShape(ClientSensorLogic logic)
        {
            switch (logic.CurrentDefinition?.Type)
            {
                case SensorDefinition.SensorType.Radar:
                    return "SquareSimple";
                case SensorDefinition.SensorType.PassiveRadar:
                    return "Triangle";
                case SensorDefinition.SensorType.Optical:
                    return "Circle";
                case SensorDefinition.SensorType.Infrared:
                    return "CircleHollow";
                default:
                    return "Arrow";
            }
        }
    }
}
