using System.Collections;
using LastStand.Core;
using UnityEngine;

namespace LastStand.Abilities
{
    /// <summary>Удар по выбранной цели (мгновенно или самонаводящимся снарядом) с эффектом. Для будущих героев.</summary>
    [CreateAssetMenu(menuName = "Last Stand/Abilities/Target Nuke", fileName = "Ability_TargetNuke")]
    public sealed class TargetNukeAbility : AbilityDefinition
    {
        [Header("Удар")]
        public float[] damage = { 120f };
        public DamageType damageType = DamageType.Magical;
        [Tooltip("0 — мгновенно, иначе скорость снаряда, м/с.")]
        public float projectileSpeed = 18f;
        public StatusEffectData onHit = new StatusEffectData { id = "nuke", duration = 0f };

        [Header("Визуал")]
        public GameObject projectileVfx;
        public GameObject hitVfx;

        private void Reset() => targeting = AbilityTargeting.UnitTarget;

        public override void Execute(in AbilityCastContext ctx)
        {
            if (ctx.Target == null)
                return;
            float dmg = CombatMath.ByLevel(damage, ctx.Level);
            if (projectileSpeed <= 0f || projectileVfx == null)
                AbilityEffects.HitUnit(ctx.Caster, ctx.Target, dmg, damageType, element, onHit, -1f, hitVfx);
            else
                AbilityEffects.Run(Fly(ctx.Caster, ctx.Target, dmg));
        }

        private IEnumerator Fly(Unit caster, Unit target, float dmg)
        {
            Vector3 pos = caster.HitPosition;
            GameObject vfx = VfxPool.Spawn(projectileVfx, pos, Quaternion.identity);
            while (target != null && target.IsAlive)
            {
                Vector3 to = target.HitPosition - pos;
                float step = projectileSpeed * Time.deltaTime;
                if (to.magnitude <= step + 0.1f)
                {
                    AbilityEffects.HitUnit(caster, target, dmg, damageType, element, onHit, -1f, hitVfx);
                    break;
                }
                pos += to.normalized * step;
                if (vfx != null) vfx.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(to));
                yield return null;
            }
            if (vfx != null)
                VfxPool.Release(vfx);
        }
    }
}
