using System.Collections;
using LastStand.Core;
using UnityEngine;

namespace LastStand.Abilities
{
    /// <summary>
    /// Удар по области с задержкой: сначала метка на земле (видна всем), затем урон и оглушение.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Stand/Abilities/Delayed Area", fileName = "Ability_DelayedArea")]
    public sealed class DelayedAreaAbility : AbilityDefinition
    {
        [Header("Удар")]
        public float delay = 1f;
        public float[] damage = { 200f };
        public DamageType damageType = DamageType.Magical;
        [Tooltip("Оглушение по уровням, с (0 — без оглушения).")]
        public float[] stunDuration = { 1.5f };
        public StatusEffectData stun = new StatusEffectData { id = "stun", duration = 1.5f, states = UnitStates.Stunned, vfxHeight = 1.05f };

        [Header("Визуал")]
        [Tooltip("Метка на земле на время задержки (масштаб 1 = радиус 1 м).")]
        public GameObject telegraphVfx;
        public GameObject impactVfx;
        public GameObject hitVfx;

        private void Reset()
        {
            targeting = AbilityTargeting.Point;
            areaRadius = new[] { 4f };
            isUltimate = true;
            maxLevel = 3;
        }

        public override void Execute(in AbilityCastContext ctx)
        {
            Vector3 point = AbilityEffects.OnGround(ctx.Point);
            AbilityEffects.Run(Strike(ctx.Caster, ctx.Level, point));
        }

        private IEnumerator Strike(Unit caster, int level, Vector3 point)
        {
            float radius = AreaRadius(level);
            GameObject telegraph = telegraphVfx != null
                ? VfxPool.Spawn(telegraphVfx, point + Vector3.up * 0.08f, Quaternion.identity, radius)
                : null;

            yield return new WaitForSeconds(delay);

            if (telegraph != null)
                VfxPool.Release(telegraph);
            if (impactVfx != null)
                VfxPool.Spawn(impactVfx, point, Quaternion.identity, radius / 4f);
            if (caster == null)
                yield break;

            float stunTime = CombatMath.ByLevel(stunDuration, level);
            AbilityEffects.DamageArea(caster, point, radius, CombatMath.ByLevel(damage, level), damageType, element,
                targetTeam, stunTime > 0f ? stun : null, stunTime, hitVfx);
        }

        public override string GetTooltip(int level) =>
            $"{description}\n\nDamage: {CombatMath.ByLevel(damage, level):0}  ·  Stun: {CombatMath.ByLevel(stunDuration, level):0.##}s";
    }
}
