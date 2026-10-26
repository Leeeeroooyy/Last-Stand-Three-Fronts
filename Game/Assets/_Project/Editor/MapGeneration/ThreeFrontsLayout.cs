using System.Collections.Generic;
using UnityEngine;

namespace LastStand.EditorTools.MapGeneration
{
    /// <summary>
    /// Геометрия карты «Three Fronts»: квадрат, три линии, река по диагонали, две базы по углам.
    /// Всё задано в плоскости XZ (Vector2.x = X, Vector2.y = Z) и центрально-симметрично:
    /// половина Dusk = зеркало половины Dawn относительно центра (x, z) → (−x, −z).
    /// </summary>
    public static class ThreeFrontsLayout
    {
        public const float TerrainHalf = 110f;  // террейн 220 × 220 м (с запасом под обзор камеры)
        public const float PlayHalf = 78f;      // за этой границей — скалы
        public const float BaseEdge = 44f;      // база Dawn: max(x, z) < −44
        public const float LaneHalfWidth = 4.6f;
        public const float BaseGround = 2f;
        public const float BasePlateau = 0.9f;
        public const float RiverDepth = 1.5f;
        public const float WaterLevel = 1.25f;
        public const float TerrainHeight = 12f;

        // ---------- Линии (направление: от базы Dawn к базе Dusk) ----------

        public static readonly Vector2[] Top =
        {
            new Vector2(-64, -44), new Vector2(-66, 0), new Vector2(-64, 58),
            new Vector2(-58, 64), new Vector2(0, 66), new Vector2(44, 64)
        };

        public static readonly Vector2[] Mid = { new Vector2(-44, -44), new Vector2(44, 44) };
        public static readonly Vector2[] Bot = MirrorDiag(Top);
        public static readonly Vector2[][] Lanes = { Top, Mid, Bot };

        // ---------- База Dawn (Dusk — зеркало) ----------

        public static readonly Vector2 AncientDawn = new Vector2(-62, -62);
        public static readonly Vector2 FountainDawn = new Vector2(-72, -72);
        public static readonly Vector2 ShopDawn = new Vector2(-73, -61);
        public static readonly Vector2[] Tier4Dawn = { new Vector2(-57.5f, -67.5f), new Vector2(-67.5f, -57.5f) };

        // ---------- Лес: святыни, сундуки, тропы (задаётся одна четверть, остальное зеркалится) ----------

        private static readonly Vector2 ShrineQuarter = new Vector2(8, -34);
        private static readonly Vector2[] ChestsQuarter = { new Vector2(-20, -46), new Vector2(28, -46) };
        private static readonly Vector2[][] CorridorsQuarter =
        {
            new[] { new Vector2(-20, -62), new Vector2(-20, -46), new Vector2(-6, -38), ShrineQuarter, new Vector2(16, -16) },
            new[] { new Vector2(-24, -24), new Vector2(-12, -36), ShrineQuarter },
            new[] { ShrineQuarter, new Vector2(28, -46), new Vector2(34, -62) },
            new[] { new Vector2(28, -46), new Vector2(44, -44) }
        };

        public static readonly Vector2[] Shrines = FourWay(ShrineQuarter);
        public static readonly Vector2[] Chests = BuildChests();
        public static readonly Vector2[][] Corridors = BuildCorridors();

        // ---------- Зеркала ----------

        public static Vector2 MirrorDiag(Vector2 p) => new Vector2(p.y, p.x);
        public static Vector2 MirrorPoint(Vector2 p) => -p;

        private static Vector2[] MirrorDiag(Vector2[] line)
        {
            var r = new Vector2[line.Length];
            for (int i = 0; i < line.Length; i++) r[i] = MirrorDiag(line[i]);
            return r;
        }

        private static Vector2[] FourWay(Vector2 p) =>
            new[] { p, MirrorDiag(p), MirrorPoint(p), MirrorPoint(MirrorDiag(p)) };

        private static Vector2[] BuildChests()
        {
            var list = new List<Vector2>();
            foreach (var c in ChestsQuarter) list.AddRange(FourWay(c));
            list.Add(new Vector2(-30, 30)); // сундуки в реке
            list.Add(new Vector2(30, -30));
            return list.ToArray();
        }

        private static Vector2[][] BuildCorridors()
        {
            var list = new List<Vector2[]>();
            foreach (var line in CorridorsQuarter)
            {
                var a = new Vector2[line.Length];
                var b = new Vector2[line.Length];
                var c = new Vector2[line.Length];
                var d = new Vector2[line.Length];
                for (int i = 0; i < line.Length; i++)
                {
                    var f = FourWay(line[i]);
                    a[i] = f[0]; b[i] = f[1]; c[i] = f[2]; d[i] = f[3];
                }
                list.Add(a); list.Add(b); list.Add(c); list.Add(d);
            }
            return list.ToArray();
        }

        // ---------- Поля расстояний ----------

        public static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        public static float DistToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float l = ab.sqrMagnitude;
            float t = l < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / l);
            return Vector2.Distance(p, a + ab * t);
        }

        public static float DistToPolyline(Vector2 p, Vector2[] line)
        {
            float d = float.MaxValue;
            for (int i = 0; i < line.Length - 1; i++)
                d = Mathf.Min(d, DistToSegment(p, line[i], line[i + 1]));
            return d;
        }

        public static float LaneDistance(Vector2 p)
        {
            float d = float.MaxValue;
            foreach (var l in Lanes) d = Mathf.Min(d, DistToPolyline(p, l));
            return d;
        }

        public static float CorridorDistance(Vector2 p)
        {
            float d = float.MaxValue;
            foreach (var l in Corridors) d = Mathf.Min(d, DistToPolyline(p, l));
            return d;
        }

        public static float RiverDistance(Vector2 p) => Mathf.Abs(p.x + p.y) * 0.70710678f;

        public static float BaseFactor(Vector2 p) =>
            Mathf.Max(1f - SmoothStep(-BaseEdge, -BaseEdge + 6f, Mathf.Max(p.x, p.y)),
                      1f - SmoothStep(-BaseEdge, -BaseEdge + 6f, Mathf.Max(-p.x, -p.y)));

        public static float BorderFactor(Vector2 p) =>
            SmoothStep(PlayHalf, PlayHalf + 8f, Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)));

        /// <summary>0 = сторона Dawn, 1 = сторона Dusk (мягкий переход через реку).</summary>
        public static float DuskFactor(Vector2 p) => SmoothStep(-22f, 22f, p.x + p.y);

        // ---------- Высота и покраска ----------

        public static float Height(Vector2 p)
        {
            float lane = 1f - SmoothStep(LaneHalfWidth - 0.5f, LaneHalfWidth + 2.5f, LaneDistance(p));
            float river = 1f - SmoothStep(5f, 9.5f, RiverDistance(p));
            float baseF = BaseFactor(p);
            float border = BorderFactor(p);

            float h = BaseGround;
            h += BasePlateau * baseF;
            h -= RiverDepth * river * (1f - baseF);

            // лёгкие холмы в лесу
            float wild = (1f - lane) * (1f - river) * (1f - baseF) * (1f - border);
            h += (Mathf.PerlinNoise(p.x * 0.06f + 13.1f, p.y * 0.06f + 7.7f) - 0.5f) * 0.9f * wild;

            // скалы по периметру; южный край низкий, чтобы не перекрывать обзор камеры (она смотрит на север)
            float rough = Mathf.PerlinNoise(p.x * 0.11f + 3.3f, p.y * 0.11f + 5.5f);
            float south = SmoothStep(PlayHalf, PlayHalf + 6f, -p.y);
            h += border * (6.5f + rough * 3f) * (1f - 0.8f * south);
            return h;
        }

        public const int LayerGrass = 0, LayerJungle = 1, LayerDuskGrass = 2, LayerDirt = 3,
                         LayerSand = 4, LayerStone = 5, LayerRock = 6, LayerCount = 7;

        public static void SplatWeights(Vector2 p, float[] w)
        {
            for (int i = 0; i < LayerCount; i++) w[i] = 0f;

            float remaining = 1f;
            void Take(int layer, float amount)
            {
                float a = Mathf.Clamp01(amount) * remaining;
                w[layer] += a;
                remaining -= a;
            }

            Take(LayerRock, BorderFactor(p));
            Take(LayerStone, SmoothStep(0.35f, 0.75f, BaseFactor(p)));
            Take(LayerDirt, 1f - SmoothStep(LaneHalfWidth - 1f, LaneHalfWidth + 0.8f, LaneDistance(p) +
                                            (Mathf.PerlinNoise(p.x * 0.4f, p.y * 0.4f) - 0.5f) * 1.2f));
            Take(LayerSand, 1f - SmoothStep(6f, 9f, RiverDistance(p)));
            Take(LayerDirt, 0.75f * (1f - SmoothStep(1.0f, 2.6f, CorridorDistance(p))));

            float dusk = DuskFactor(p);
            float jungle = Mathf.Clamp01(SmoothStep(7f, 14f, LaneDistance(p)) *
                                         (0.55f + Mathf.PerlinNoise(p.x * 0.05f + 40f, p.y * 0.05f + 40f) * 0.6f));
            float grass = remaining;
            w[LayerDuskGrass] += grass * dusk;
            w[LayerJungle] += grass * (1f - dusk) * jungle;
            w[LayerGrass] += grass * (1f - dusk) * (1f - jungle);
        }

        // ---------- Утилиты для полилиний ----------

        public static float Length(Vector2[] line)
        {
            float l = 0f;
            for (int i = 0; i < line.Length - 1; i++) l += Vector2.Distance(line[i], line[i + 1]);
            return l;
        }

        /// <summary>Точка и направление на расстоянии dist вдоль полилинии.</summary>
        public static Vector2 PointAt(Vector2[] line, float dist, out Vector2 dir)
        {
            for (int i = 0; i < line.Length - 1; i++)
            {
                float seg = Vector2.Distance(line[i], line[i + 1]);
                if (dist <= seg || i == line.Length - 2)
                {
                    dir = (line[i + 1] - line[i]).normalized;
                    return Vector2.Lerp(line[i], line[i + 1], Mathf.Clamp01(dist / seg));
                }
                dist -= seg;
            }
            dir = Vector2.up;
            return line[line.Length - 1];
        }

        public static Vector2 Left(Vector2 dir) => new Vector2(-dir.y, dir.x);
    }
}
