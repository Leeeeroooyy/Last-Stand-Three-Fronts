using System.Collections;
using System.Collections.Generic;
using LastStand.Core;
using UnityEngine;

namespace LastStand.Abilities
{
    /// <summary>Снаряд-линия в направлении курсора: летит на дальность каста, задевает врагов на пути.</summary>
    [CreateAssetMenu(menuName = "Last Stand/Abilities/Skillshot", fileName = "Ability_Skillshot")]
    public sealed class SkillshotAbility : AbilityDefinition
    {
        [Header("Снаряд")]
        public float[] damage = { 100f };
        public DamageType damageType = DamageType.Magical;
        public float speed = 26f;
        [Tooltip("Пробивает всех на пути (иначе останавливается на первом).")]
        public bool pierce = true;
        public float flightHeight = 1.2f;
        public StatusEffectData onHit = new StatusEffectData { id = "skillshot", duration = 0f };

        [Header("Визуал")]
        public GameObject projectileVfx;
        public GameObject hitVfx;
        public GameObject castVfx;

        private void Reset()
        {
            targeting = AbilityTargeting.Direction;
            lineWidth = 1.4f;
        }

        public override void Execute(in AbilityCastContext ctx)
        {
            Vector3 dir = ctx.Direction;
            Vector3 start = ctx.Caster.Position + dir * (ctx.Caster.Radius + 0.3f);
            float length = CastRange(ctx.Level);
            if (castVfx != null)
                VfxPool.Spawn(castVfx, start + Vector3.up * flightHeight, Quaternion.LookRotation(dir));
            AbilityEffects.Run(Fly(ctx.Caster, ctx.Level, start, dir, length));
        }

        private IEnumerator Fly(Unit caster, int level, Vector3 start, Vector3 dir, float length)
        {
            GameObject vfx = projectileVfx != null
                ? VfxPool.Spawn(projectileVfx, start + Vector3.up * flightHeight, Quaternion.LookRotation(dir))
                : null;
            var hits = new HashSet<Unit>();
            var buffer = new List<Unit>(8);
            float travelled = 0f;
            Vector3 prev = start;
            float dmg = CombatMath.ByLevel(damage, level);
            bool stopped = false;

            while (travelled < length && !stopped)
            {
                travelled = Mathf.Min(length, travelled + speed * Time.deltaTime);
                Vector3 cur = start + dir * travelled;

                UnitRegistry.QueryCapsule(prev, cur, lineWidth * 0.5f, caster.Team, targetTeam, buffer);
                for (int i = 0; i < buffer.Count && !stopped; i++)
                {
                    Unit u = buffer[i];
                    if (!hits.Add(u))
                        continue;
                    AbilityEffects.HitUnit(caster, u, dmg, damageType, element, onHit, -1f, hitVfx);
                    if (!pierce) stopped = true;
                }

                if (vfx != null)
                {
                    Vector3 ground = AbilityEffects.OnGround(cur);
                    vfx.transform.position = new Vector3(cur.x, ground.y + flightHeight, cur.z);
                }
                prev = cur;
                yield return null;
            }

            if (vfx != null)
                VfxPool.Release(vfx);
        }

        public override string GetTooltip(int level) =>
            $"{description}\n\nDamage: {CombatMath.ByLevel(damage, level):0}";
    }
}
