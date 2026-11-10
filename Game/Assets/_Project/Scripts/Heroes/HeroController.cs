using System;
using LastStand.Abilities;
using LastStand.Core;
using LastStand.Units;
using UnityEngine;

namespace LastStand.Heroes
{
    public enum OrderType
    {
        Idle = 0,
        Move = 1,
        AttackUnit = 2,
        AttackMove = 3,
        Hold = 4,
        Cast = 5
    }

    /// <summary>
    /// Исполняет приказы героя в стиле Dota 2: движение, атака цели, атака с ходу (A),
    /// удержание позиции (H), стоп (S), применение способностей с подходом на дальность.
    /// Приказы отдаёт игрок (PlayerCommander) или в будущем ИИ бота — через одни и те же методы.
    /// </summary>
    [RequireComponent(typeof(Unit), typeof(UnitMotor), typeof(UnitAttack))]
    [DisallowMultipleComponent]
    public sealed class HeroController : MonoBehaviour
    {
        [Tooltip("Насколько дальше радиуса обнаружения герой преследует цель автоатаки.")]
        [SerializeField] private float chaseLeash = 4f;

        private Unit unit;
        private UnitMotor motor;
        private UnitAttack attack;
        private HeroAbilities abilities;
        private UnitAnimator animator;

        private OrderType order;
        private Vector3 orderPoint;
        private Unit orderTarget;
        private Unit autoTarget;
        private int castSlot = -1;
        private float castEnd = -1f;
        private float nextAcquire;

        public OrderType CurrentOrder => order;
        public Unit AttackTarget => order == OrderType.AttackUnit ? orderTarget : autoTarget;
        public bool IsCasting => castEnd > 0f;
        public int CastingSlot => castSlot;

        /// <summary>Сообщения для игрока (нет маны, на перезарядке и т.д.).</summary>
        public event Action<string> OrderError;

        private void Awake()
        {
            unit = GetComponent<Unit>();
            motor = GetComponent<UnitMotor>();
            attack = GetComponent<UnitAttack>();
            abilities = GetComponent<HeroAbilities>();
            animator = GetComponent<UnitAnimator>();
        }

        private void OnEnable()
        {
            unit.Died += OnDied;
        }

        private void OnDisable()
        {
            unit.Died -= OnDied;
        }

        private void OnDied(Unit u, Unit killer) => ClearOrder();

        // ================= Приказы =================

        public void IssueMove(Vector3 point)
        {
            if (!unit.IsAlive) return;
            ClearOrder();
            order = OrderType.Move;
            orderPoint = point;
            motor.MoveTo(point);
        }

        public void IssueAttack(Unit target)
        {
            if (!unit.IsAlive || target == null || !target.IsAlive) return;
            if (target == unit) return;
            ClearOrder();
            order = OrderType.AttackUnit;
            orderTarget = target;
        }

        public void IssueAttackMove(Vector3 point)
        {
            if (!unit.IsAlive) return;
            ClearOrder();
            order = OrderType.AttackMove;
            orderPoint = point;
        }

        public void IssueStop()
        {
            ClearOrder();
            motor.Stop();
        }

        public void IssueHold()
        {
            ClearOrder();
            motor.Stop();
            order = OrderType.Hold;
        }

        /// <summary>Применить способность слота. Для NoTarget цель и точка не нужны.</summary>
        public bool IssueCast(int slot, Unit target, Vector3 point)
        {
            if (abilities == null) return false;
            AbilityCastError error = abilities.CanCast(slot);
            if (error != AbilityCastError.None)
            {
                OrderError?.Invoke(HeroAbilities.ErrorText(error));
                return false;
            }

            AbilityDefinition def = abilities[slot].Definition;
            if (def.targeting == AbilityTargeting.UnitTarget && !def.IsValidTarget(unit, target))
            {
                OrderError?.Invoke(HeroAbilities.ErrorText(AbilityCastError.InvalidTarget));
                return false;
            }

            ClearOrder();
            order = OrderType.Cast;
            castSlot = slot;
            orderTarget = target;
            orderPoint = point;
            return true;
        }

        private void ClearOrder()
        {
            order = OrderType.Idle;
            orderTarget = null;
            autoTarget = null;
            castSlot = -1;
            castEnd = -1f;
            attack.Cancel();
        }

        // ================= Исполнение =================

        private void Update()
        {
            if (!unit.IsAlive)
                return;

            if (unit.IsStunned)
            {
                // Оглушение прерывает каст, но не отменяет сам приказ.
                castEnd = -1f;
                attack.Cancel();
                return;
            }

            if (motor.IsDashing)
                return;

            switch (order)
            {
                case OrderType.Move:
                    if (!motor.HasDestination && motor.IsReady)
                        order = OrderType.Idle;
                    else
                        motor.MoveTo(orderPoint);
                    break;

                case OrderType.AttackUnit:
                    if (!CanAttack(orderTarget))
                    {
                        ClearOrder();
                        break;
                    }
                    Chase(orderTarget);
                    break;

                case OrderType.AttackMove:
                    UpdateAttackMove();
                    break;

                case OrderType.Hold:
                    UpdateHold();
                    break;

                case OrderType.Cast:
                    UpdateCast();
                    break;

                default:
                    UpdateIdle();
                    break;
            }
        }

        private bool CanAttack(Unit target) =>
            target != null && target.IsAlive && !target.IsInvulnerable && CombatMath.IsEnemy(unit.Team, target.Team);

        private void Chase(Unit target)
        {
            AttackStatus status = attack.Tick(target);
            if (status == AttackStatus.OutOfRange)
                motor.MoveTo(attack.ApproachPoint(target), 0.05f);
        }

        private void UpdateIdle()
        {
            if (autoTarget != null)
            {
                bool lost = !CanAttack(autoTarget) ||
                            CombatMath.EdgeDistance(unit, autoTarget) > unit.AcquisitionRange + chaseLeash;
                if (lost)
                    autoTarget = null;
                else
                {
                    Chase(autoTarget);
                    return;
                }
            }

            if (Time.time >= nextAcquire)
            {
                nextAcquire = Time.time + 0.25f;
                autoTarget = UnitRegistry.FindClosest(unit.Position, unit.Team, TeamFilter.Enemies,
                    unit.AcquisitionRange, unit, includeStructures: true);
            }
        }

        private void UpdateAttackMove()
        {
            if (autoTarget != null && !CanAttack(autoTarget))
                autoTarget = null;

            if (autoTarget == null && Time.time >= nextAcquire)
            {
                nextAcquire = Time.time + 0.2f;
                autoTarget = UnitRegistry.FindClosest(unit.Position, unit.Team, TeamFilter.Enemies,
                    unit.AcquisitionRange, unit, includeStructures: true);
            }

            if (autoTarget != null)
            {
                Chase(autoTarget);
                return;
            }

            motor.MoveTo(orderPoint);
            if (!motor.HasDestination && CombatMath.FlatDistance(unit.Position, orderPoint) < 1f)
                order = OrderType.Idle;
        }

        private void UpdateHold()
        {
            if (autoTarget == null || !CanAttack(autoTarget) || !attack.InRange(autoTarget))
            {
                autoTarget = Time.time >= nextAcquire
                    ? UnitRegistry.FindClosest(unit.Position, unit.Team, TeamFilter.Enemies, unit.AttackRange, unit)
                    : null;
                nextAcquire = Time.time + 0.15f;
            }
            if (autoTarget != null)
                attack.Tick(autoTarget);
        }

        private void UpdateCast()
        {
            AbilitySlot slot = abilities[castSlot];
            AbilityDefinition def = slot.Definition;

            if (def.targeting == AbilityTargeting.UnitTarget && !def.IsValidTarget(unit, orderTarget))
            {
                ClearOrder();
                return;
            }

            Vector3 aim = def.targeting == AbilityTargeting.UnitTarget ? orderTarget.Position : orderPoint;
            bool needsRange = def.targeting == AbilityTargeting.UnitTarget || def.targeting == AbilityTargeting.Point;

            if (needsRange && castEnd < 0f)
            {
                float distance = def.targeting == AbilityTargeting.UnitTarget
                    ? CombatMath.EdgeDistance(unit, orderTarget)
                    : CombatMath.FlatDistance(unit.Position, orderPoint);
                if (distance > slot.CastRange)
                {
                    motor.MoveTo(aim, 0.05f);
                    return;
                }
            }

            if (castEnd < 0f)
            {
                motor.Stop();
                bool facingNeeded = def.targeting != AbilityTargeting.NoTarget;
                if (facingNeeded)
                {
                    motor.FaceTowards(aim);
                    if (!motor.IsFacing(aim, 20f))
                        return;
                }

                AbilityCastError error = abilities.CanCast(castSlot);
                if (error != AbilityCastError.None)
                {
                    OrderError?.Invoke(HeroAbilities.ErrorText(error));
                    ClearOrder();
                    return;
                }

                castEnd = Time.time + def.castPoint;
                if (animator != null) animator.PlayCast(def.castPoint);
            }

            if (Time.time < castEnd)
                return;

            int index = castSlot;
            Unit target = orderTarget;
            Vector3 point = def.targeting == AbilityTargeting.UnitTarget ? orderTarget.Position : orderPoint;
            ClearOrder();
            abilities.Commit(index, target, point);
        }
    }
}
