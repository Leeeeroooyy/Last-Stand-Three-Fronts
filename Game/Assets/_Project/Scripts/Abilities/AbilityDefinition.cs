using LastStand.Core;
using UnityEngine;

namespace LastStand.Abilities
{
    public enum AbilityTargeting
    {
        [Tooltip("Применяется сразу (на себя / вокруг себя).")]
        NoTarget = 0,
        [Tooltip("Нужно выбрать юнита.")]
        UnitTarget = 1,
        [Tooltip("Точка на земле (герой подходит на дальность каста).")]
        Point = 2,
        [Tooltip("Направление (снаряд-линия от героя).")]
        Direction = 3,
        Passive = 4
    }

    public enum AbilityCastError
    {
        None = 0,
        NotLearned,
        Passive,
        OnCooldown,
        NotEnoughMana,
        Silenced,
        Stunned,
        Dead,
        InvalidTarget
    }

    /// <summary>Параметры конкретного применения способности.</summary>
    public readonly struct AbilityCastContext
    {
        public readonly Unit Caster;
        public readonly int Level;
        public readonly Unit Target;
        public readonly Vector3 Point;

        public AbilityCastContext(Unit caster, int level, Unit target, Vector3 point)
        {
            Caster = caster;
            Level = level;
            Target = target;
            Point = point;
        }

        /// <summary>Горизонтальное направление от героя к точке/цели.</summary>
        public Vector3 Direction
        {
            get
            {
                Vector3 to = (Target != null ? Target.Position : Point) - Caster.Position;
                to.y = 0f;
                return to.sqrMagnitude > 0.0001f ? to.normalized : Caster.transform.forward;
            }
        }
    }

    /// <summary>
    /// Базовое описание способности (ScriptableObject). Значения по уровням — массивы, индекс = уровень − 1.
    /// Конкретное поведение — в наследниках (Skillshot, Leap, SelfBuff, DelayedArea, TargetNuke).
    /// </summary>
    public abstract class AbilityDefinition : ScriptableObject
    {
        [Header("Описание")]
        public string displayName = "Ability";
        [TextArea(2, 5)] public string description;
        public Sprite icon;
        public Element element;
        public bool isUltimate;
        [Range(1, 4)] public int maxLevel = 4;

        [Header("Применение")]
        public AbilityTargeting targeting = AbilityTargeting.NoTarget;
        public TeamFilter targetTeam = TeamFilter.Enemies;
        [Tooltip("Время каста до срабатывания, с.")]
        public float castPoint = 0.25f;
        public float[] manaCost = { 100f };
        public float[] cooldown = { 10f };
        [Tooltip("Дальность применения, м.")]
        public float[] castRange = { 8f };

        [Header("Индикатор прицеливания")]
        [Tooltip("Радиус области у курсора (0 — без круга).")]
        public float[] areaRadius = { 0f };
        [Tooltip("Ширина линии для Direction-способностей.")]
        public float lineWidth;

        public float ManaCost(int level) => CombatMath.ByLevel(manaCost, level);
        public float Cooldown(int level) => CombatMath.ByLevel(cooldown, level);
        public float CastRange(int level) => CombatMath.ByLevel(castRange, level);
        public float AreaRadius(int level) => CombatMath.ByLevel(areaRadius, level);

        /// <summary>Подходит ли юнит как цель (для UnitTarget).</summary>
        public virtual bool IsValidTarget(Unit caster, Unit target) =>
            target != null && target.IsAlive && !target.IsStructure &&
            CombatMath.Matches(targetTeam, caster.Team, target.Team) &&
            (targetTeam == TeamFilter.Allies || !target.IsInvulnerable);

        /// <summary>Сработать (вызывается после castPoint, мана и перезарядка уже списаны).</summary>
        public abstract void Execute(in AbilityCastContext context);

        /// <summary>Краткое описание значений для подсказки.</summary>
        public virtual string GetTooltip(int level) => description;
    }
}
