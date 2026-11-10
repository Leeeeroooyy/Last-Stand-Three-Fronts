using LastStand.Core;
using LastStand.Units;
using UnityEngine;

namespace LastStand.Structures
{
    /// <summary>
    /// Башня: держит цель, пока та в радиусе; ищет ближайшего врага;
    /// переключается на вражеского героя, атакующего союзного героя в зоне башни.
    /// </summary>
    [RequireComponent(typeof(Unit), typeof(UnitAttack))]
    [DisallowMultipleComponent]
    public sealed class TowerAI : MonoBehaviour
    {
        private Unit unit;
        private UnitAttack attack;
        private Unit target;
        private float nextThink;

        public Unit Target => target;

        private void Awake()
        {
            unit = GetComponent<Unit>();
            attack = GetComponent<UnitAttack>();
        }

        private void OnEnable() => Unit.AnyDamaged += OnAnyDamaged;
        private void OnDisable() => Unit.AnyDamaged -= OnAnyDamaged;

        private void Update()
        {
            if (!unit.IsAlive)
                return;

            if (Time.time >= nextThink)
            {
                nextThink = Time.time + 0.2f;
                if (target != null && (!target.IsAlive || target.IsInvulnerable || !attack.InRange(target)))
                    target = null;
                if (target == null)
                    target = UnitRegistry.FindClosest(unit.Position, unit.Team, TeamFilter.Enemies,
                        unit.AttackRange + unit.Radius, unit, includeStructures: false);
            }

            if (target != null && attack.Tick(target) == AttackStatus.NoTarget)
                target = null;
        }

        private void OnAnyDamaged(Unit victim, DamageInfo info, float amount)
        {
            if (!unit.IsAlive || !info.IsAttack || info.Source == null || !victim.IsHero || victim.Team != unit.Team)
                return;
            Unit aggressor = info.Source;
            if (!aggressor.IsHero || !CombatMath.IsEnemy(unit.Team, aggressor.Team) || aggressor == target)
                return;
            if (attack.InRange(aggressor) && attack.InRange(victim, 2f))
                target = aggressor;
        }
    }
}
