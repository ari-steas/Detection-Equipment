using System;
using System.Collections.Generic;
using System.Linq;
using VRage.Game;
using VRageMath;

namespace DetectionEquipment.Shared.Utils
{
    internal class IcoSphereConstructor
    {
        public Shape Sphere = new Shape(
            new[]
            {
                new Vector3( 0.000000f, -1.000000f,  0.000000f),
                new Vector3( 0.723600f, -0.447215f,  0.525720f),
                new Vector3(-0.276385f, -0.447215f,  0.850640f),
                new Vector3(-0.894425f, -0.447215f,  0.000000f),
                new Vector3(-0.276385f, -0.447215f, -0.850640f),
                new Vector3( 0.723600f, -0.447215f, -0.525720f),
                new Vector3( 0.276385f,  0.447215f,  0.850640f),
                new Vector3(-0.723600f,  0.447215f,  0.525720f),
                new Vector3(-0.723600f,  0.447215f, -0.525720f),
                new Vector3( 0.276385f,  0.447215f, -0.850640f),
                new Vector3( 0.894425f,  0.447215f,  0.000000f),
                new Vector3( 0.000000f,  1.000000f,  0.000000f),
            },
            new[]
            {
                new Vector3I( 1,  2,  3),
                new Vector3I( 2,  1,  6),
                new Vector3I( 1,  3,  4),
                new Vector3I( 1,  4,  5),
                new Vector3I( 1,  5,  6),
                new Vector3I( 2,  6, 11),
                new Vector3I( 3,  2,  7),
                new Vector3I( 4,  3,  8),
                new Vector3I( 5,  4,  9),
                new Vector3I( 6,  5, 10),
                new Vector3I( 2, 11,  7),
                new Vector3I( 3,  7,  8),
                new Vector3I( 4,  8,  9),
                new Vector3I( 5,  9, 10),
                new Vector3I( 6, 10, 11),
                new Vector3I( 7, 11, 12),
                new Vector3I( 8,  7, 12),
                new Vector3I( 9,  8, 12),
                new Vector3I(10,  9, 12),
                new Vector3I(11, 10, 12),
            }
            );

        public IcoSphereConstructor(int numDivisions)
        {
            if (numDivisions <= 0)
                return;

            List<Shape.Triangle> tris = new List<Shape.Triangle>(20 * (int)Math.Pow(4, numDivisions));
            List<Shape.Triangle> trisB = new List<Shape.Triangle>(20 * (int)Math.Pow(4, numDivisions));

            tris.AddRange(Sphere.Tris);

            for (int div = 0; div < numDivisions; div++)
            {
                foreach (var tri in tris)
                    tri.SubdivideNormal(trisB);

                var shuffle = tris;
                tris = trisB;
                shuffle.Clear();
                trisB = shuffle;
            }

            Sphere = new Shape(tris);
        }

        public class Shape
        {
            public Triangle[] Tris;

            public Shape(Vector3[] vertices, Vector3I[] tris)
            {
                Tris = new Triangle[tris.Length];
                for (int i = 0; i < tris.Length; i++)
                {
                    Tris[i] = new Triangle(
                        vertices[tris[i].X - 1],
                        vertices[tris[i].Y - 1],
                        vertices[tris[i].Z - 1]
                        );
                }
            }

            public Shape(ICollection<Triangle> triangles)
            {
                var triArr = triangles as Triangle[];
                if (triArr != null)
                    Tris = triArr;
                else
                    Tris = triangles.ToArray();
            }

            public Vector3[] GenerateVertexSet()
            {
                HashSet<Vector3> normals = new HashSet<Vector3>(Tris.Length/3);

                foreach (var tri in Tris)
                {
                    normals.Add(tri.V1);
                    normals.Add(tri.V2);
                    normals.Add(tri.V3);
                }

                return normals.ToArray();
            }

            public void DrawDebug(float scale, MatrixD transform, Color color, float duration)
            {
                foreach (var tri in Tris)
                {
                    DebugDraw.AddLine(Vector3D.Transform(scale * tri.V1, transform), Vector3D.Transform(scale * tri.V2, transform), color, duration);
                    DebugDraw.AddLine(Vector3D.Transform(scale * tri.V1, transform), Vector3D.Transform(scale * tri.V3, transform), color, duration);
                    DebugDraw.AddLine(Vector3D.Transform(scale * tri.V2, transform), Vector3D.Transform(scale * tri.V3, transform), color, duration);
                }
            }

            public struct Triangle
            {
                public readonly Vector3 V1;
                public readonly Vector3 V2;
                public readonly Vector3 V3;

                public Triangle(Vector3 v1, Vector3 v2, Vector3 v3)
                {
                    V1 = v1;
                    V2 = v2;
                    V3 = v3;
                }

                public Triangle[] Subdivide()
                {
                    Vector3 v12 = (V1 + V2) / 2;
                    Vector3 v13 = (V1 + V3) / 2;
                    Vector3 v23 = (V2 + V3) / 2;

                    return new[]
                    {
                        new Triangle(V1, v12, v13),
                        new Triangle(V2, v12, v23),
                        new Triangle(V3, v23, v13),
                        new Triangle(v12, v23, v13),
                    };
                }

                public void SubdivideNormal(ICollection<Triangle> collection)
                {
                    Vector3 v12 = Vector3.Normalize((V1 + V2) / 2);
                    Vector3 v13 = Vector3.Normalize((V1 + V3) / 2);
                    Vector3 v23 = Vector3.Normalize((V2 + V3) / 2);

                    collection.Add(new Triangle(V1, v12, v13));
                    collection.Add(new Triangle(V2, v12, v23));
                    collection.Add(new Triangle(V3, v23, v13));
                    collection.Add(new Triangle(v12, v23, v13));
                }

                public Triangle MultiplyWith(Triangle other, int DirToUse)
                {
                    return new Triangle(
                        V1 * other.V1.GetDim(DirToUse),
                        V2 * other.V2.GetDim(DirToUse),
                        V3 * other.V3.GetDim(DirToUse)
                        );
                }

                public Vector3 GetVertex(int i)
                {
                    switch (i)
                    {
                        case 0:
                            return V1;
                        case 1:
                            return V2;
                        case 2:
                            return V3;
                        default:
                            return GetVertex((i % 3 + 3) % 3);

                    }
                }
            }
        }
    }
}
