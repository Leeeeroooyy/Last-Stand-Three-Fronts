using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastStand.EditorTools.MapGeneration
{
    /// <summary>
    /// Построитель стилизованных low-poly мешей с плоским затенением (у каждого треугольника свои вершины).
    /// Все примитивы выпуклые, поэтому ориентация треугольников выправляется автоматически — наружу от центра.
    /// </summary>
    public sealed class LowPolyMeshBuilder
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<List<int>> submeshes = new List<List<int>>();

        public LowPolyMeshBuilder(int submeshCount = 1)
        {
            for (int i = 0; i < Mathf.Max(1, submeshCount); i++)
                submeshes.Add(new List<int>());
        }

        /// <summary>Треугольник, развёрнутый лицевой стороной от точки center (в локальных координатах примитива).</summary>
        private void TriOutward(int sub, Matrix4x4 m, Vector3 a, Vector3 b, Vector3 c, Vector3 center)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-10f)
                return;
            if (Vector3.Dot(n, (a + b + c) / 3f - center) < 0f)
                (b, c) = (c, b);

            int i = vertices.Count;
            vertices.Add(m.MultiplyPoint3x4(a));
            vertices.Add(m.MultiplyPoint3x4(b));
            vertices.Add(m.MultiplyPoint3x4(c));
            submeshes[sub].Add(i);
            submeshes[sub].Add(i + 1);
            submeshes[sub].Add(i + 2);
        }

        /// <summary>Усечённый конус / призма / конус (r1 = 0) от y = 0 до y = height.</summary>
        public LowPolyMeshBuilder Frustum(int sub, Matrix4x4 m, int sides, float r0, float r1, float height,
            bool capBottom = false, bool capTop = true, float angleOffsetDeg = 0f)
        {
            Vector3 center = new Vector3(0f, height * 0.5f, 0f);
            var bottom = new Vector3[sides];
            var top = new Vector3[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = (i / (float)sides) * Mathf.PI * 2f + angleOffsetDeg * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                bottom[i] = dir * r0;
                top[i] = dir * r1 + Vector3.up * height;
            }

            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                // Вырожденные треугольники (у конусов) отбрасываются внутри TriOutward.
                TriOutward(sub, m, bottom[i], top[i], top[j], center);
                TriOutward(sub, m, bottom[i], top[j], bottom[j], center);
                if (capTop && r1 > 0f) TriOutward(sub, m, Vector3.up * height, top[j], top[i], center);
                if (capBottom && r0 > 0f) TriOutward(sub, m, Vector3.zero, bottom[i], bottom[j], center);
            }
            return this;
        }

        /// <summary>Кристалл: двойная пирамида с экватором на y = 0.</summary>
        public LowPolyMeshBuilder Crystal(int sub, Matrix4x4 m, int sides, float radius, float heightUp, float heightDown)
        {
            Vector3 up = Vector3.up * heightUp;
            Vector3 down = Vector3.down * heightDown;
            Vector3 center = new Vector3(0f, (heightUp - heightDown) * 0.25f, 0f);
            for (int i = 0; i < sides; i++)
            {
                float a0 = i / (float)sides * Mathf.PI * 2f;
                float a1 = (i + 1) / (float)sides * Mathf.PI * 2f;
                var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
                var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
                TriOutward(sub, m, p0, up, p1, center);
                TriOutward(sub, m, p0, p1, down, center);
            }
            return this;
        }

        /// <summary>Бесформенный камень/крона: икосаэдр со случайно сдвинутыми вершинами.</summary>
        public LowPolyMeshBuilder Blob(int sub, Matrix4x4 m, float radius, float jitter, System.Random rng, float squashY = 1f)
        {
            float t = (1f + Mathf.Sqrt(5f)) * 0.5f;
            var v = new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1)
            };
            for (int i = 0; i < v.Length; i++)
            {
                float k = radius * (1f + ((float)rng.NextDouble() * 2f - 1f) * jitter);
                v[i] = v[i].normalized * k;
                v[i].y *= squashY;
            }

            int[] f =
            {
                0, 11, 5, 0, 5, 1, 0, 1, 7, 0, 7, 10, 0, 10, 11,
                1, 5, 9, 5, 11, 4, 11, 10, 2, 10, 7, 6, 7, 1, 8,
                3, 9, 4, 3, 4, 2, 3, 2, 6, 3, 6, 8, 3, 8, 9,
                4, 9, 5, 2, 4, 11, 6, 2, 10, 8, 6, 7, 9, 8, 1
            };
            for (int i = 0; i < f.Length; i += 3)
                TriOutward(sub, m, v[f[i]], v[f[i + 1]], v[f[i + 2]], Vector3.zero);
            return this;
        }

        public Mesh Build(string name)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000)
                mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.subMeshCount = submeshes.Count;
            for (int i = 0; i < submeshes.Count; i++)
                mesh.SetTriangles(submeshes[i], i);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        public static Matrix4x4 TRS(Vector3 pos, float yawDeg = 0f, float scale = 1f) =>
            Matrix4x4.TRS(pos, Quaternion.Euler(0f, yawDeg, 0f), Vector3.one * scale);

        public static Matrix4x4 TRS(Vector3 pos, Quaternion rot, Vector3 scale) => Matrix4x4.TRS(pos, rot, scale);
    }
}
