using System.Collections;
using System.Collections.Generic;
using LastStand.Core;
using UnityEngine;

namespace LastStand.Abilities
{
    /// <summary>Общие операции способностей: урон по области, эффекты, отложенный запуск.</summary>
    public static class AbilityEffects
    {
        private static readonly List<Unit> buffer = new List<Unit>(32);
        private static AbilityRunner runner;

        /// <summary>Объект для корутин способностей: эффект продолжается, даже если герой погиб.</summary>
        public static AbilityRunner Runner
        {
            get
            {
                if (runner == null)
                {
                    var go = new GameObject("[AbilityRunner]");
                    runner = go.AddComponent<AbilityRunner>();
                }
                return runner;
            }
        }

        public static Coroutine Run(IEnumerator routine) => Runner.StartCoroutine(routine);

        /// <summary>Урон и эффект всем подходящим юнитам в круге. Возвращает число задетых.</summary>
        public static int DamageArea(Unit source, Vector3 center, float radius, float damage, DamageType type,
            Element element, TeamFilter filter, StatusEffectData status = null, float statusDuration = -1f,
            GameObject hitVfx = null)
        {
            UnitRegistry.Query(center, radius, source.Team, filter, buffer);
            for (int i = 0; i < buffer.Count; i++)
                HitUnit(source, buffer[i], damage, type, element, status, statusDuration, hitVfx);
            return buffer.Count;
        }

        public static void HitUnit(Unit source, Unit target, float damage, DamageType type, Element element,
            StatusEffectData status = null, float statusDuration = -1f, GameObject hitVfx = null)
        {
            if (target == null || !target.IsAlive)
                return;
            if (status != null && !status.IsEmpty)
                target.AddStatus(status, source, statusDuration);
            if (damage > 0f)
                target.ApplyDamage(new DamageInfo(damage, type, source, false, element));
            if (hitVfx != null)
                VfxPool.Spawn(hitVfx, target.HitPosition, Quaternion.identity);
        }

        /// <summary>Высота земли под точкой (террейн), чтобы эффекты стояли на поверхности.</summary>
        public static Vector3 OnGround(Vector3 point)
        {
            Terrain t = Terrain.activeTerrain;
            if (t != null)
                point.y = t.SampleHeight(point) + t.transform.position.y;
            return point;
        }
    }
}
