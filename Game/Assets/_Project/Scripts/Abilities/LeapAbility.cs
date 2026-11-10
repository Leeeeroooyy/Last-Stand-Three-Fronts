using LastStand.Core;
using LastStand.Units;
using UnityEngine;

namespace LastStand.Abilities
{
    /// <summary>Прыжок в точку с уроном и эффектом по области при приземлении.</summary>
    [CreateAssetMenu(menuName = "Last Stand/Abilities/Leap", fileName = "Ability_Leap")]
    public sealed class LeapAbility : AbilityDefinition
    {
        [Header("Прыжок")]
        public float leapSpeed = 20f;
        public float arcHeight = 2.4f;

        [Header("Приземление")]
        public float[] damage = { 80f };
        public DamageType damageType = DamageType.Magical;
        public StatusEffectData onLand = new StatusEffectData { id = "leap_slow", duration = 2f, moveSpeedPercent = -30f };

        [Header("Визуал")]
        public GameObject takeoffVfx;
        public GameObject landVfx;
        public GameObject hitVfx;

        private void Reset()
        {
            targeting = AbilityTargeting.Point;
            areaRadius = new[] { 3f };
        }

        public override void Execute(in AbilityCastContext ctx)
        {
            Unit caster = ctx.Caster;
            var motor = caster.GetComponent<UnitMotor>();
            if (motor == null)
                return;

            Vector3 to = CombatMath.Flat(ctx.Point - caster.Position);
            float range = CastRange(ctx.Level);
            if (to.magnitude > range)
                to = to.normalized * range;
            Vector3 target = caster.Position + to;

            if (takeoffVfx != null)
                VfxPool.Spawn(takeoffVfx, caster.Position, Quaternion.identity);

            int level = ctx.Level;
            motor.Dash(target, leapSpeed, arcHeight, () => Land(caster, level));
        }

        private void Land(Unit caster, int level)
        {
            if (caster == null || !caster.IsAlive)
                return;
            float radius = AreaRadius(level);
            if (landVfx != null)
                VfxPool.Spawn(landVfx, caster.Position + Vector3.up * 0.05f, Quaternion.identity, Mathf.Max(0.5f, radius / 3f));
            AbilityEffects.DamageArea(caster, caster.Position, radius, CombatMath.ByLevel(damage, level),
                damageType, element, targetTeam, onLand, -1f, hitVfx);
        }

        public override string GetTooltip(int level) =>
            $"{description}\n\nDamage: {CombatMath.ByLevel(damage, level):0}";
    }
}
