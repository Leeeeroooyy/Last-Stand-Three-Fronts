using LastStand.Core;
using UnityEngine;

namespace LastStand.Abilities
{
    /// <summary>Временное усиление героя (броня, скорость атаки, скорость бега) по уровням.</summary>
    [CreateAssetMenu(menuName = "Last Stand/Abilities/Self Buff", fileName = "Ability_SelfBuff")]
    public sealed class SelfBuffAbility : AbilityDefinition
    {
        [Header("Усиление (по уровням)")]
        public string buffId = "self_buff";
        public float[] duration = { 6f };
        public float[] armorBonus = { 4f };
        public float[] attackSpeedBonus = { 30f };
        public float[] moveSpeedPercent = { 10f };
        public float[] damageBonus = { 0f };

        [Header("Визуал")]
        [Tooltip("Эффект на герое на всё время действия.")]
        public GameObject auraVfx;
        public GameObject castVfx;

        [System.NonSerialized] private StatusEffectData[] cache;

        private void Reset() => targeting = AbilityTargeting.NoTarget;

        private void OnValidate() => cache = null;

        private StatusEffectData ForLevel(int level)
        {
            if (cache == null || cache.Length != maxLevel)
                cache = new StatusEffectData[maxLevel];
            int i = Mathf.Clamp(level - 1, 0, maxLevel - 1);
            return cache[i] ??= new StatusEffectData
            {
                id = buffId,
                isDebuff = false,
                duration = CombatMath.ByLevel(duration, level),
                armorBonus = CombatMath.ByLevel(armorBonus, level),
                attackSpeedBonus = CombatMath.ByLevel(attackSpeedBonus, level),
                moveSpeedPercent = CombatMath.ByLevel(moveSpeedPercent, level),
                damageBonus = CombatMath.ByLevel(damageBonus, level),
                attachedVfx = auraVfx,
                vfxHeight = 0f
            };
        }

        public override void Execute(in AbilityCastContext ctx)
        {
            if (castVfx != null)
                VfxPool.Spawn(castVfx, ctx.Caster.Position + Vector3.up * 0.05f, Quaternion.identity);
            ctx.Caster.AddStatus(ForLevel(ctx.Level), ctx.Caster);
        }

        public override string GetTooltip(int level) =>
            $"{description}\n\nArmor +{CombatMath.ByLevel(armorBonus, level):0}  ·  Attack speed +{CombatMath.ByLevel(attackSpeedBonus, level):0}  ·  {CombatMath.ByLevel(duration, level):0.#}s";
    }
}
