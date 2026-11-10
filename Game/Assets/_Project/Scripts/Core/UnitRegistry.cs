using System;
using System.Collections.Generic;
using LastStand.Map;
using UnityEngine;

namespace LastStand.Core
{
    /// <summary>
    /// Реестр активных юнитов и быстрые пространственные запросы без аллокаций.
    /// Юнитов на карте немного (≈ 100), поэтому линейный перебор дешевле физических запросов.
    /// </summary>
    public static class UnitRegistry
    {
        private static readonly List<Unit> units = new List<Unit>(128);

        public static IReadOnlyList<Unit> All => units;

        public static event Action<Unit> Registered;
        public static event Action<Unit> Unregistered;

        internal static void Register(Unit unit)
        {
            if (units.Contains(unit))
                return;
            units.Add(unit);
            Registered?.Invoke(unit);
        }

        internal static void Unregister(Unit unit)
        {
            if (units.Remove(unit))
                Unregistered?.Invoke(unit);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            units.Clear();
            Registered = null;
            Unregistered = null;
        }

        /// <summary>Ближайший живой юнит по фильтру команды в радиусе (от края до края).</summary>
        public static Unit FindClosest(Vector3 position, TeamSide relativeTo, TeamFilter filter, float range,
            Unit exclude = null, bool includeStructures = true, bool includeInvulnerable = false)
        {
            Unit best = null;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (u == exclude || !u.IsAlive || !CombatMath.Matches(filter, relativeTo, u.Team))
                    continue;
                if (!includeStructures && u.IsStructure)
                    continue;
                if (!includeInvulnerable && u.IsInvulnerable)
                    continue;

                float d = CombatMath.FlatDistance(position, u.Position) - u.Radius;
                if (d <= range && d < bestDistance)
                {
                    bestDistance = d;
                    best = u;
                }
            }
            return best;
        }

        /// <summary>Заполняет results юнитами в круге (учитывает радиус тела цели).</summary>
        public static void Query(Vector3 center, float radius, TeamSide relativeTo, TeamFilter filter, List<Unit> results,
            bool includeStructures = false)
        {
            results.Clear();
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (!u.IsAlive || !CombatMath.Matches(filter, relativeTo, u.Team))
                    continue;
                if (!includeStructures && u.IsStructure)
                    continue;
                if (CombatMath.FlatDistance(center, u.Position) - u.Radius <= radius)
                    results.Add(u);
            }
        }

        /// <summary>Юниты, задетые отрезком шириной width (для снарядов-линий).</summary>
        public static void QueryCapsule(Vector3 from, Vector3 to, float halfWidth, TeamSide relativeTo, TeamFilter filter,
            List<Unit> results, bool includeStructures = false)
        {
            results.Clear();
            Vector2 a = new Vector2(from.x, from.z);
            Vector2 b = new Vector2(to.x, to.z);
            Vector2 ab = b - a;
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (!u.IsAlive || !CombatMath.Matches(filter, relativeTo, u.Team))
                    continue;
                if (!includeStructures && u.IsStructure)
                    continue;
                Vector2 p = new Vector2(u.Position.x, u.Position.z);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                if ((a + ab * t - p).magnitude <= halfWidth + u.Radius)
                    results.Add(u);
            }
        }
    }
}
