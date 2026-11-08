using System.Collections.Generic;
using UnityEngine;

namespace CQRa.View
{
    /// <summary>
    /// Small library of procedurally generated meshes. No imported art, no Unity
    /// primitive assets. Units are assembled from these parts so their silhouettes
    /// stay readable at RTS zoom.
    /// </summary>
    public static class ProceduralMeshes
    {
        public struct MeshPart
        {
            public Mesh Mesh;
            public Vector3 Position;
            public Quaternion Rotation;

            public MeshPart(Mesh mesh, Vector3 position)
                : this(mesh, position, Quaternion.identity)
            {
            }

            public MeshPart(Mesh mesh, Vector3 position, Quaternion rotation)
            {
                Mesh = mesh;
                Position = position;
                Rotation = rotation;
            }
        }

        /// <summary>Axis-aligned box centred on the origin.</summary>
        public static Mesh Box(Vector3 size)
        {
            var hx = size.x * 0.5f;
            var hy = size.y * 0.5f;
            var hz = size.z * 0.5f;

            var mesh = new Mesh { name = "Box" };

            var vertices = new[]
            {
                // -X
                new Vector3(-hx, -hy, -hz), new Vector3(-hx, hy, -hz), new Vector3(-hx, hy, hz), new Vector3(-hx, -hy, hz),
                // +X
                new Vector3(hx, -hy, hz), new Vector3(hx, hy, hz), new Vector3(hx, hy, -hz), new Vector3(hx, -hy, -hz),
                // -Y
                new Vector3(-hx, -hy, hz), new Vector3(hx, -hy, hz), new Vector3(hx, -hy, -hz), new Vector3(-hx, -hy, -hz),
                // +Y
                new Vector3(-hx, hy, -hz), new Vector3(hx, hy, -hz), new Vector3(hx, hy, hz), new Vector3(-hx, hy, hz),
                // -Z
                new Vector3(-hx, -hy, -hz), new Vector3(hx, -hy, -hz), new Vector3(hx, hy, -hz), new Vector3(-hx, hy, -hz),
                // +Z
                new Vector3(-hx, -hy, hz), new Vector3(-hx, hy, hz), new Vector3(hx, hy, hz), new Vector3(hx, -hy, hz),
            };

            var normals = new Vector3[24];
            for (var face = 0; face < 6; face++)
            {
                var normal = FaceNormal(face);
                for (var v = 0; v < 4; v++)
                {
                    normals[face * 4 + v] = normal;
                }
            }

            var uvs = new Vector2[24];
            for (var v = 0; v < 24; v++)
            {
                uvs[v] = new Vector2(v % 4 == 0 || v % 4 == 3 ? 0f : 1f, v % 4 < 2 ? 0f : 1f);
            }

            var triangles = new int[36];
            for (var face = 0; face < 6; face++)
            {
                var b = face * 4;
                var t = face * 6;
                triangles[t + 0] = b + 0;
                triangles[t + 1] = b + 2;
                triangles[t + 2] = b + 1;
                triangles[t + 3] = b + 0;
                triangles[t + 4] = b + 3;
                triangles[t + 5] = b + 2;
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Cylinder along +Y, centred on the origin.</summary>
        public static Mesh Cylinder(float radius, float height, int segments = 12)
        {
            segments = Mathf.Max(3, segments);

            var mesh = new Mesh { name = "Cylinder" };
            var halfHeight = height * 0.5f;

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            // Side
            for (var i = 0; i <= segments; i++)
            {
                var angle = (float)i / segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var u = (float)i / segments;

                vertices.Add(dir * radius + Vector3.down * halfHeight);
                vertices.Add(dir * radius + Vector3.up * halfHeight);
                normals.Add(dir);
                normals.Add(dir);
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));
            }

            for (var i = 0; i < segments; i++)
            {
                var a = i * 2;
                triangles.Add(a);
                triangles.Add(a + 1);
                triangles.Add(a + 2);
                triangles.Add(a + 1);
                triangles.Add(a + 3);
                triangles.Add(a + 2);
            }

            // Caps
            var topCenter = vertices.Count;
            vertices.Add(Vector3.up * halfHeight);
            normals.Add(Vector3.up);
            uvs.Add(new Vector2(0.5f, 0.5f));

            var bottomCenter = vertices.Count;
            vertices.Add(Vector3.down * halfHeight);
            normals.Add(Vector3.down);
            uvs.Add(new Vector2(0.5f, 0.5f));

            var topRing = vertices.Count;
            for (var i = 0; i < segments; i++)
            {
                var angle = (float)i / segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(dir * radius + Vector3.up * halfHeight);
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(dir.x * 0.5f + 0.5f, dir.z * 0.5f + 0.5f));
            }

            var bottomRing = vertices.Count;
            for (var i = 0; i < segments; i++)
            {
                var angle = (float)i / segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                vertices.Add(dir * radius + Vector3.down * halfHeight);
                normals.Add(Vector3.down);
                uvs.Add(new Vector2(dir.x * 0.5f + 0.5f, dir.z * 0.5f + 0.5f));
            }

            for (var i = 0; i < segments; i++)
            {
                var next = (i + 1) % segments;
                triangles.Add(topCenter);
                triangles.Add(topRing + next);
                triangles.Add(topRing + i);

                triangles.Add(bottomCenter);
                triangles.Add(bottomRing + i);
                triangles.Add(bottomRing + next);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Merges transformed parts into a single mesh. Part matrices are rigid.</summary>
        public static Mesh Merge(params MeshPart[] parts)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            foreach (var part in parts)
            {
                if (part.Mesh == null)
                {
                    continue;
                }

                var matrix = Matrix4x4.TRS(part.Position, part.Rotation, Vector3.one);
                var offset = vertices.Count;

                var partVertices = part.Mesh.vertices;
                var partNormals = part.Mesh.normals;
                var partUvs = part.Mesh.uv;
                var partTriangles = part.Mesh.triangles;

                for (var i = 0; i < partVertices.Length; i++)
                {
                    vertices.Add(matrix.MultiplyPoint3x4(partVertices[i]));
                    normals.Add(matrix.MultiplyVector(i < partNormals.Length ? partNormals[i] : Vector3.up));
                    uvs.Add(i < partUvs.Length ? partUvs[i] : Vector2.zero);
                }

                for (var i = 0; i < partTriangles.Length; i++)
                {
                    triangles.Add(partTriangles[i] + offset);
                }
            }

            var mesh = new Mesh { name = "Merged" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Flat ground ring (annulus) on the XZ plane, useful for order markers.</summary>
        public static Mesh Ring(float outerRadius, float innerRadius, int segments = 40)
        {
            segments = Mathf.Max(3, segments);
            innerRadius = Mathf.Clamp(innerRadius, 0f, outerRadius - 0.01f);

            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (var i = 0; i <= segments; i++)
            {
                var angle = (float)i / segments * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

                vertices.Add(dir * innerRadius);
                vertices.Add(dir * outerRadius);
                normals.Add(Vector3.up);
                normals.Add(Vector3.up);
                uvs.Add(new Vector2(dir.x * 0.5f + 0.5f, dir.z * 0.5f + 0.5f));
                uvs.Add(uvs[uvs.Count - 1]);
            }

            for (var i = 0; i < segments; i++)
            {
                var a = i * 2;
                triangles.Add(a);
                triangles.Add(a + 1);
                triangles.Add(a + 2);
                triangles.Add(a + 1);
                triangles.Add(a + 3);
                triangles.Add(a + 2);
            }

            var mesh = new Mesh { name = "Ring" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Shattered concrete / rock outcrop. Deterministic for a given seed so the
        /// scenery and the navigation blockers always agree.
        /// </summary>
        public static Mesh RockCluster(float radius, float height, int seed)
        {
            var random = new System.Random(seed * 7919 + 17);
            var parts = new List<MeshPart>();

            var chunks = 5 + random.Next(4);
            for (var i = 0; i < chunks; i++)
            {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var distance = radius * (0.10f + 0.55f * (float)random.NextDouble());
                var chunkHeight = height * (0.28f + 0.52f * (float)random.NextDouble());
                var chunkWidth = radius * (0.32f + 0.45f * (float)random.NextDouble());
                var chunkDepth = chunkWidth * (0.55f + 0.5f * (float)random.NextDouble());

                var position = new Vector3(
                    Mathf.Cos(angle) * distance,
                    chunkHeight * 0.5f - 0.25f,
                    Mathf.Sin(angle) * distance);

                var rotation = Quaternion.Euler(
                    ((float)random.NextDouble() - 0.5f) * 14f,
                    (float)random.NextDouble() * 360f,
                    ((float)random.NextDouble() - 0.5f) * 14f);

                parts.Add(new MeshPart(
                    Box(new Vector3(chunkWidth, chunkHeight, chunkDepth)),
                    position,
                    rotation));
            }

            // Central broken spire keeps a strong silhouette at RTS zoom.
            var spireHeight = height * (0.85f + 0.3f * (float)random.NextDouble());
            parts.Add(new MeshPart(
                Box(new Vector3(radius * 0.42f, spireHeight, radius * 0.34f)),
                new Vector3(0f, spireHeight * 0.5f, 0f),
                Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 3f)));

            return Merge(parts.ToArray());
        }

        // ---- Unit silhouettes (metres) ------------------------------------------------

        public static Mesh Infantry()
        {
            return Merge(
                new MeshPart(Box(new Vector3(0.55f, 0.55f, 0.38f)), new Vector3(0f, 0.275f, 0f)),
                new MeshPart(Box(new Vector3(0.68f, 0.42f, 0.42f)), new Vector3(0f, 0.76f, 0f)),
                new MeshPart(Box(new Vector3(0.30f, 0.28f, 0.30f)), new Vector3(0f, 1.10f, 0f)),
                new MeshPart(Box(new Vector3(0.44f, 0.36f, 0.22f)), new Vector3(0f, 0.78f, -0.26f)));
        }

        public static Mesh Tank()
        {
            return Merge(
                new MeshPart(Box(new Vector3(0.72f, 1.0f, 7.0f)), new Vector3(-1.36f, 0.5f, 0f)),
                new MeshPart(Box(new Vector3(0.72f, 1.0f, 7.0f)), new Vector3(1.36f, 0.5f, 0f)),
                new MeshPart(Box(new Vector3(3.2f, 1.5f, 6.4f)), new Vector3(0f, 1.05f, 0f)),
                new MeshPart(Box(new Vector3(2.4f, 1.0f, 2.9f)), new Vector3(0f, 2.25f, -0.25f)),
                new MeshPart(Cylinder(0.15f, 3.4f, 10), new Vector3(0f, 2.3f, 2.5f), Quaternion.Euler(90f, 0f, 0f)));
        }

        public static Mesh Harvester()
        {
            return Merge(
                new MeshPart(Box(new Vector3(4.2f, 1.6f, 7.2f)), new Vector3(0f, 1.25f, 0f)),
                new MeshPart(Box(new Vector3(2.0f, 1.25f, 1.9f)), new Vector3(0f, 2.55f, -2.2f)),
                new MeshPart(Cylinder(1.0f, 3.8f, 12), new Vector3(0f, 2.2f, 1.9f), Quaternion.Euler(0f, 0f, 90f)),
                new MeshPart(Box(new Vector3(3.6f, 0.4f, 1.2f)), new Vector3(0f, 0.55f, 3.7f)));
        }

        /// <summary>Mobile construction vehicle: a fat tracked hull with a cab and crane.</summary>
        public static Mesh Mcv()
        {
            return Merge(
                new MeshPart(Box(new Vector3(1.0f, 1.4f, 9.2f)), new Vector3(-2.1f, 0.7f, 0f)),
                new MeshPart(Box(new Vector3(1.0f, 1.4f, 9.2f)), new Vector3(2.1f, 0.7f, 0f)),
                new MeshPart(Box(new Vector3(4.6f, 2.5f, 8.4f)), new Vector3(0f, 2.2f, 0f)),
                new MeshPart(Box(new Vector3(3.0f, 1.6f, 3.2f)), new Vector3(0f, 4.1f, -2.2f)),
                new MeshPart(Cylinder(0.22f, 5.5f, 8), new Vector3(0.6f, 4.4f, 2.4f), Quaternion.Euler(58f, 0f, 0f)),
                new MeshPart(Box(new Vector3(0.5f, 0.5f, 5.0f)), new Vector3(0.6f, 5.6f, 4.4f)));
        }

        /// <summary>Attack gunship: fuselage, stub wings, tail boom and rotor mast.</summary>
        public static Mesh Gunship()
        {
            return Merge(
                new MeshPart(Box(new Vector3(2.0f, 1.6f, 8.5f)), new Vector3(0f, 0.8f, 0f)),
                new MeshPart(Box(new Vector3(9.5f, 0.35f, 2.0f)), new Vector3(0f, 1.0f, 0.5f)),
                new MeshPart(Box(new Vector3(1.2f, 1.0f, 2.4f)), new Vector3(0f, 1.2f, -4.0f)),
                new MeshPart(Box(new Vector3(0.3f, 2.4f, 1.6f)), new Vector3(0f, 2.2f, -4.4f)),
                new MeshPart(Cylinder(0.12f, 6.0f, 8), new Vector3(0f, 2.6f, 0f)),
                new MeshPart(Box(new Vector3(6.2f, 0.18f, 0.7f)), new Vector3(0f, 3.8f, 0f)),
                new MeshPart(Box(new Vector3(0.7f, 0.4f, 2.6f)), new Vector3(-3.2f, 0.9f, 0.5f)),
                new MeshPart(Box(new Vector3(0.7f, 0.4f, 2.6f)), new Vector3(3.2f, 0.9f, 0.5f)));
        }

        // ---- Buildings (metres) ------------------------------------------------------

        public static Mesh ConstructionYard()
        {
            return Merge(
                new MeshPart(Box(new Vector3(16f, 5.0f, 14f)), new Vector3(0f, 2.5f, 0f)),
                new MeshPart(Box(new Vector3(9f, 3.0f, 7f)), new Vector3(-4f, 6.5f, -3f)),
                new MeshPart(Box(new Vector3(11f, 1.2f, 11f)), new Vector3(2f, 5.6f, 2f)),
                new MeshPart(Cylinder(0.45f, 12f, 8), new Vector3(6f, 6.0f, -4f), Quaternion.Euler(0f, 0f, 18f)),
                new MeshPart(Box(new Vector3(0.8f, 0.8f, 9f)), new Vector3(7.8f, 9.4f, -4f)),
                new MeshPart(Box(new Vector3(2.0f, 1.5f, 2.0f)), new Vector3(-6f, 7.0f, 5f)));
        }

        public static Mesh PowerPlant()
        {
            return Merge(
                new MeshPart(Box(new Vector3(11f, 4.0f, 9f)), new Vector3(0f, 2.0f, 0f)),
                new MeshPart(Cylinder(2.0f, 9f, 12), new Vector3(-2.8f, 4.5f, 0.5f)),
                new MeshPart(Cylinder(2.0f, 9f, 12), new Vector3(2.8f, 4.5f, 0.5f)),
                new MeshPart(Box(new Vector3(1.2f, 6.0f, 1.2f)), new Vector3(0f, 7.0f, -3.2f)),
                new MeshPart(Box(new Vector3(3.2f, 1.0f, 4.0f)), new Vector3(0f, 4.6f, 3.2f)));
        }

        public static Mesh Refinery()
        {
            return Merge(
                new MeshPart(Box(new Vector3(14f, 5.0f, 12f)), new Vector3(0f, 2.5f, 0f)),
                new MeshPart(Cylinder(2.6f, 7f, 14), new Vector3(-4.5f, 7.0f, -2.5f)),
                new MeshPart(Cylinder(2.6f, 7f, 14), new Vector3(0f, 7.0f, -2.5f)),
                new MeshPart(Box(new Vector3(3.0f, 1.0f, 8f)), new Vector3(4.0f, 4.0f, 3.0f)),
                new MeshPart(Box(new Vector3(2.0f, 2.4f, 2.0f)), new Vector3(5.0f, 6.0f, 3.0f)));
        }

        /// <summary>Low barracks block with a flag mast and crates.</summary>
        public static Mesh Barracks()
        {
            return Merge(
                new MeshPart(Box(new Vector3(11f, 3.2f, 8f)), new Vector3(0f, 1.6f, 0f)),
                new MeshPart(Box(new Vector3(7f, 2.0f, 3f)), new Vector3(0f, 2.2f, 4.2f)),
                new MeshPart(Box(new Vector3(1.0f, 7f, 1.0f)), new Vector3(-4.4f, 3.5f, 2.6f)),
                new MeshPart(Box(new Vector3(1.8f, 0.6f, 0.12f)), new Vector3(-3.6f, 6.2f, 2.6f)),
                new MeshPart(Box(new Vector3(1.6f, 1.2f, 1.6f)), new Vector3(4.0f, 3.2f, -3.2f)),
                new MeshPart(Box(new Vector3(1.6f, 1.2f, 1.6f)), new Vector3(4.0f, 4.4f, -3.2f)));
        }

        /// <summary>Large factory hall with a vehicle door and roof gantry.</summary>
        public static Mesh WarFactory()
        {
            return Merge(
                new MeshPart(Box(new Vector3(18f, 7f, 14f)), new Vector3(0f, 3.5f, 0f)),
                new MeshPart(Box(new Vector3(6f, 5f, 1f)), new Vector3(4f, 2.5f, 7.2f)),
                new MeshPart(Box(new Vector3(20f, 1f, 15f)), new Vector3(0f, 7.4f, 0f)),
                new MeshPart(Box(new Vector3(1.2f, 6f, 1.2f)), new Vector3(-7f, 10f, 4f)),
                new MeshPart(Box(new Vector3(0.8f, 0.8f, 12f)), new Vector3(-7f, 12.4f, 0f)),
                new MeshPart(Box(new Vector3(3f, 2.4f, 3f)), new Vector3(6f, 9.1f, -4f)));
        }

        /// <summary>Defensive gun tower: squat base with a rotating-looking turret head.</summary>
        public static Mesh GunTower()
        {
            return Merge(
                new MeshPart(Box(new Vector3(4.4f, 2.2f, 4.4f)), new Vector3(0f, 1.1f, 0f)),
                new MeshPart(Box(new Vector3(3.2f, 1.4f, 3.2f)), new Vector3(0f, 2.9f, 0f)),
                new MeshPart(Cylinder(1.1f, 1.6f, 10), new Vector3(0f, 4.2f, 0f)),
                new MeshPart(Box(new Vector3(0.9f, 0.9f, 3.4f)), new Vector3(0f, 4.3f, 1.6f)),
                new MeshPart(Cylinder(0.18f, 2.6f, 8), new Vector3(0f, 4.4f, 3.4f), Quaternion.Euler(90f, 0f, 0f)));
        }

        /// <summary>Wall segment, roughly 4 m long and 2.4 m tall.</summary>
        public static Mesh Wall()
        {
            return Merge(
                new MeshPart(Box(new Vector3(4.4f, 2.4f, 1.1f)), new Vector3(0f, 1.2f, 0f)),
                new MeshPart(Box(new Vector3(5.0f, 0.4f, 1.4f)), new Vector3(0f, 2.5f, 0f)),
                new MeshPart(Box(new Vector3(1.0f, 0.5f, 1.3f)), new Vector3(-1.5f, 2.75f, 0f)),
                new MeshPart(Box(new Vector3(1.0f, 0.5f, 1.3f)), new Vector3(1.5f, 2.75f, 0f)));
        }

        /// <summary>Supply crate, roughly 1.8 m across.</summary>
        public static Mesh Crate()
        {
            return Merge(
                new MeshPart(Box(new Vector3(1.8f, 1.4f, 1.8f)), new Vector3(0f, 0.7f, 0f)),
                new MeshPart(Box(new Vector3(2.0f, 0.22f, 0.3f)), new Vector3(0f, 0.9f, 0f)),
                new MeshPart(Box(new Vector3(0.3f, 0.22f, 2.0f)), new Vector3(0f, 0.9f, 0f)),
                new MeshPart(Box(new Vector3(0.4f, 0.5f, 0.4f)), new Vector3(0.5f, 1.55f, 0.5f)));
        }

        /// <summary>Low ore crystal pile, deterministic per seed.</summary>
        public static Mesh OreCluster(int seed)
        {
            var random = new System.Random(seed * 104729 + 7);
            var parts = new List<MeshPart>();

            var chunks = 6 + random.Next(4);
            for (var i = 0; i < chunks; i++)
            {
                var angle = (float)random.NextDouble() * Mathf.PI * 2f;
                var distance = 1.2f + 4.2f * (float)random.NextDouble();
                var height = 0.9f + 1.8f * (float)random.NextDouble();
                var width = 0.8f + 1.1f * (float)random.NextDouble();

                parts.Add(new MeshPart(
                    Box(new Vector3(width, height, width * 0.85f)),
                    new Vector3(
                        Mathf.Cos(angle) * distance,
                        height * 0.45f,
                        Mathf.Sin(angle) * distance),
                    Quaternion.Euler(
                        ((float)random.NextDouble() - 0.5f) * 16f,
                        (float)random.NextDouble() * 360f,
                        ((float)random.NextDouble() - 0.5f) * 16f)));
            }

            return Merge(parts.ToArray());
        }

        private static Vector3 FaceNormal(int face)
        {
            switch (face)
            {
                case 0: return Vector3.left;
                case 1: return Vector3.right;
                case 2: return Vector3.down;
                case 3: return Vector3.up;
                case 4: return Vector3.back;
                default: return Vector3.forward;
            }
        }
    }
}
