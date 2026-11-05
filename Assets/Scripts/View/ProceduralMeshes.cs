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
