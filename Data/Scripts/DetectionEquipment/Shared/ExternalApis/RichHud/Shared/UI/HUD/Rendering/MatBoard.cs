using VRageMath;

namespace RichHudFramework
{
    namespace UI
    {
        namespace Rendering
        {
            public class MatBoard
            {
                private readonly MaterialFrame matFrame;

                private Color color;

                private QuadBoard minBoard;
                private bool updateMatFit;

                /// <summary>
                ///     Initializes a new matboard with a size of 0 and a blank, white material.
                /// </summary>
                public MatBoard()
                {
                    matFrame = new MaterialFrame();
                    minBoard = QuadBoard.Default;

                    color = Color.White;
                    updateMatFit = true;
                }

                /// <summary>
                ///     Coloring applied to the material.
                /// </summary>
                public Color Color
                {
                    get { return color; }
                    set
                    {
                        if (value != color)
                            minBoard.materialData.bbColor = BillBoardUtils.GetBillBoardBoardColor(value);

                        color = value;
                    }
                }

                /// <summary>
                ///     Texture applied to the billboard.
                /// </summary>
                public Material Material
                {
                    get { return matFrame.Material; }
                    set
                    {
                        if (value != matFrame.Material)
                        {
                            updateMatFit = true;
                            matFrame.Material = value;
                            minBoard.materialData.textureID = value.TextureID;
                        }
                    }
                }

                /// <summary>
                ///     Determines how the texture scales with the MatBoard's dimensions.
                /// </summary>
                public MaterialAlignment MatAlignment
                {
                    get { return matFrame.Alignment; }
                    set
                    {
                        if (value != matFrame.Alignment)
                        {
                            updateMatFit = true;
                            matFrame.Alignment = value;
                        }
                    }
                }

                /// <summary>
                ///     Draws a billboard in world space using the quad specified.
                /// </summary>
                public void Draw(ref MyQuadD quad)
                {
                    minBoard.Draw(ref quad);
                }

                /// <summary>
                ///     Draws a billboard in world space facing the +Z direction of the matrix given. Units in meters,
                ///     matrix transform notwithstanding. Dont forget to compensate for perspective scaling!
                /// </summary
                public void Draw(ref CroppedBox box, MatrixD[] matrixRef)
                {
                    var containment = ContainmentType.Contains;

                    if (box.mask != null)
                        box.mask.Value.Contains(ref box.bounds, out containment);

                    if (containment != ContainmentType.Disjoint)
                    {
                        if (updateMatFit && matFrame.Material != Material.Default)
                        {
                            var boxSize = box.bounds.Size;
                            minBoard.materialData.texBounds = matFrame.GetMaterialAlignment(boxSize.X / boxSize.Y);
                            updateMatFit = false;
                        }

                        if (containment != ContainmentType.Disjoint)
                            minBoard.Draw(ref box, matrixRef);
                    }
                }
            }
        }
    }
}