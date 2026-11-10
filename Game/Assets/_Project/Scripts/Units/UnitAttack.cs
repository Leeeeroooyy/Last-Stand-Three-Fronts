using LastStand.Core;
using UnityEngine;

namespace LastStand.Units
{
    public enum AttackStatus
    {
        NoTarget = 0,
        OutOfRange = 1,
        InRange = 2,
        Attacking = 3
    }

    /// <summary>
    /// Автоатака: замах (attack point) → удар/снаряд → откат (backswing).
    /// Контроллер вызывает <see cref="Tick"/> каждый кадр, пока у юнита есть цель атаки.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    [DisallowMultipleComponent]
    public sealed class UnitAttack : MonoBehaviour
    {
        [Tooltip("Снаряд дальней атаки (нужен, если у юнита projectileSpeed > 0).")]
        [SerializeField] private GameObject projectilePrefab;
        [Tooltip("Точка вылета снаряда.")]
        [SerializeField] private Transform projectileOrigin;
        [Tooltip("Эффект попадания (необязательно).")]
        [SerializeField] private GameObject impactVfx;

        private Unit unit;
        private UnitMotor motor;
        private UnitAnimator animator;

        private Unit windupTarget;
        private float windupEnd;
        private float backswingEnd;
        private float nextReady;

        public Unit Owner => unit;
        public bool IsWindingUp => windupTarget != null;
        public bool InBackswing => Time.time < backswingEnd;
        /// <summary>Цель текущего или последнего удара.</summary>
        public Unit CurrentTarget { get; private set; }

        private void Awake()
        {
            unit = GetComponent<Unit>();
            motor = GetComponent<UnitMotor>();
            animator = GetComponent<UnitAnimator>();
        }

        public void Configure(GameObject projectile, Transform origin, GameObject impact = null)
        {
            projectilePrefab = projectile;
            projectileOrigin = origin;
            if (impact != null) impactVfx = impact;
        }

        public bool InRange(Unit target, float extra = 0f) =>
            target != null && CombatMath.EdgeDistance(unit, target) <= unit.AttackRange + extra;

        /// <summary>Точка, куда подойти для атаки цели (снаружи её радиуса, со стороны атакующего).</summary>
        public Vector3 ApproachPoint(Unit target)
        {
            Vector3 dir = CombatMath.Flat(unit.Position - target.Position);
            if (dir.sqrMagnitude < 0.0001f) dir = -target.transform.forward;
            float d = target.Radius + unit.Radius + unit.AttackRange * 0.6f;
            return target.Position + dir.normalized * d;
        }

        /// <summary>Один шаг логики атаки по цели. Если вне дальности — вызывающий сам ведёт юнит к цели.</summary>
        public AttackStatus Tick(Unit target)
        {
            ProcessWindup();

            if (target == null || !target.IsAlive)
            {
                Cancel();
                return AttackStatus.NoTarget;
            }

            if (windupTarget != null)
            {
                if (windupTarget == target)
                    return AttackStatus.Attacking;
                Cancel();
            }

            if (!InRange(target))
                return AttackStatus.OutOfRange;

            if (motor != null)
            {
                motor.Stop();
                motor.FaceTowards(target.Position);
            }

            if (unit.IsDisarmed || unit.IsStunned || !unit.IsAlive)
                return AttackStatus.InRange;

            bool facing = motor == null || motor.IsFacing(target.Position, 30f);
            if (facing && Time.time >= nextReady && Time.time >= backswingEnd - 0.01f)
            {
                BeginWindup(target);
                return AttackStatus.Attacking;
            }
            return AttackStatus.InRange;
        }

        public void Cancel()
        {
            if (windupTarget == null)
                return;
            windupTarget = null;
            nextReady = Time.time;
            if (animator != null) animator.CancelAttack();
        }

        private void Update() => ProcessWindup();

        private void BeginWindup(Unit target)
        {
            windupTarget = target;
            CurrentTarget = target;
            windupEnd = Time.time + unit.AttackWindup;
            nextReady = Time.time + unit.AttackInterval;
            if (animator != null) animator.PlayAttack(unit.AttackWindup);
        }

        private void ProcessWindup()
        {
            if (windupTarget == null)
                return;
            if (!unit.IsAlive || unit.IsStunned || !windupTarget.IsAlive || !InRange(windupTarget, 1.5f))
            {
                Cancel();
                return;
            }
            if (Time.time < windupEnd)
                return;

            Unit target = windupTarget;
            windupTarget = null;
            backswingEnd = Time.time + unit.AttackBackswing;
            Release(target);
        }

        private void Release(Unit target)
        {
            var info = new DamageInfo(unit.RollAttackDamage(), DamageType.Physical, unit, true);
            if (unit.BaseStats.IsRanged && projectilePrefab != null)
            {
                Vector3 origin = projectileOrigin != null ? projectileOrigin.position : unit.HitPosition;
                Projectile.Launch(projectilePrefab, origin, target, unit.BaseStats.projectileSpeed, info, impactVfx);
            }
            else
            {
                target.ApplyDamage(info);
                if (impactVfx != null)
                    VfxPool.Spawn(impactVfx, target.HitPosition, Quaternion.identity);
            }
        }
    }
}
