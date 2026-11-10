using LastStand.Core;
using UnityEngine;

namespace LastStand.Controls
{
    /// <summary>
    /// Выбор юнита под курсором без коллайдеров: луч проверяется против вертикального «цилиндра» юнита.
    /// Небольшой запас делает клик по мелким крипам удобным.
    /// </summary>
    public static class UnitPicker
    {
        public static Unit Pick(Ray ray, float padding = 0.35f, Unit ignore = null)
        {
            Unit best = null;
            float bestT = float.MaxValue;
            var units = UnitRegistry.All;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u == ignore || !u.IsAlive)
                    continue;

                Vector3 bottom = u.Position;
                float height = Mathf.Max(1.2f, u.BarHeight - 0.6f);
                float radius = u.Radius + (u.IsStructure ? 0f : padding);
                if (RayVsSegment(ray, bottom, bottom + Vector3.up * height, out float t, out float distance) &&
                    distance <= radius && t < bestT)
                {
                    bestT = t;
                    best = u;
                }
            }
            return best;
        }

        /// <summary>Ближайшие точки луча и отрезка. t — параметр на луче, distance — расстояние между ними.</summary>
        private static bool RayVsSegment(Ray ray, Vector3 a, Vector3 b, out float t, out float distance)
        {
            Vector3 d1 = ray.direction;
            Vector3 d2 = b - a;
            Vector3 r = ray.origin - a;
            float aa = Vector3.Dot(d1, d1);
            float ee = Vector3.Dot(d2, d2);
            float f = Vector3.Dot(d2, r);
            float c = Vector3.Dot(d1, r);
            float bb = Vector3.Dot(d1, d2);
            float denom = aa * ee - bb * bb;

            float s = denom > 1e-6f ? Mathf.Clamp((bb * f - c * ee) / denom, 0f, 10000f) : 0f;
            float u = Mathf.Clamp01((bb * s + f) / ee);
            s = Mathf.Max(0f, (bb * u - c) / aa);

            Vector3 p1 = ray.origin + d1 * s;
            Vector3 p2 = a + d2 * u;
            t = s;
            distance = Vector3.Distance(p1, p2);
            return true;
        }
    }
}
