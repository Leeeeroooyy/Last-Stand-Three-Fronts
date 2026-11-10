using LastStand.Core;
using LastStand.Map;
using LastStand.Units;
using UnityEngine;

namespace LastStand.Creeps
{
    /// <summary>
    /// Поведение крипа: идёт по своей линии, атакует ближайших врагов, не уходит далеко от линии.
    /// Правило Dota 2: если вражеский герой атакует союзного героя рядом, крипы переключаются на обидчика.
    /// </summary>
    [RequireComponent(typeof(Unit), typeof(UnitMotor), typeof(UnitAttack))]
    [DisallowMultipleComponent]
    public sealed class CreepAI : MonoBehaviour
    {
        [Tooltip("Насколько далеко от линии крип может преследовать цель, м.")]
        [SerializeField] private float leashDistance = 10f;
        [Tooltip("Радиус, в котором крип реагирует на атаку врага по союзному герою.")]
        [SerializeField] private float heroAggroRadius = 7f;
        [SerializeField] private float heroAggroDuration = 2.5f;
        [SerializeField] private float waypointReach = 2.5f;

        private Unit unit;
        private UnitMotor motor;
        private UnitAttack attack;

        private LanePath lane;
        private int waypoint;
        private Unit target;
        private float nextThink;
        private float forcedUntil;
        private Vector3 laneAnchor;

        public LanePath Lane => lane;
        public Unit Target => target;

        private void Awake()
        {
            unit = GetComponent<Unit>();
            motor = GetComponent<UnitMotor>();
            attack = GetComponent<UnitAttack>();
        }

        private void OnEnable() => Unit.AnyDamaged += OnAnyDamaged;
        private void OnDisable() => Unit.AnyDamaged -= OnAnyDamaged;

        /// <summary>Назначить линию. Крип начинает с ближайшей точки маршрута впереди.</summary>
        public void AssignLane(LanePath path)
        {
            lane = path;
            waypoint = 0;
            if (lane == null || lane.Count == 0)
                return;

            float best = float.MaxValue;
            for (int i = 0; i < lane.Count; i++)
            {
                float d = CombatMath.FlatDistance(transform.position, lane.GetPoint(i, unit.Team));
                if (d < best)
                {
                    best = d;
                    waypoint = i;
                }
            }
            if (best < waypointReach && waypoint < lane.Count - 1)
                waypoint++;
            laneAnchor = lane.GetPoint(waypoint, unit.Team);
            nextThink = Time.time + Random.Range(0f, 0.2f);
        }

        private void Update()
        {
            if (!unit.IsAlive || lane == null)
                return;

            if (Time.time >= nextThink)
            {
                nextThink = Time.time + 0.2f;
                Think();
            }

            if (target != null)
            {
                AttackStatus status = attack.Tick(target);
                if (status == AttackStatus.OutOfRange)
                    motor.MoveTo(attack.ApproachPoint(target), 0.05f);
                else if (status == AttackStatus.NoTarget)
                    target = null;
                return;
            }

            FollowLane();
        }

        private void Think()
        {
            if (target != null)
            {
                bool invalid = !target.IsAlive || target.IsInvulnerable;
                bool strayed = CombatMath.FlatDistance(transform.position, laneAnchor) > leashDistance;
                bool tooFar = CombatMath.EdgeDistance(unit, target) > unit.AcquisitionRange * 1.5f;
                if (invalid || (Time.time > forcedUntil && (strayed || tooFar)))
                {
                    target = null;
                    attack.Cancel();
                }
            }

            if (target == null)
            {
                target = UnitRegistry.FindClosest(transform.position, unit.Team, TeamFilter.Enemies,
                    unit.AcquisitionRange, unit, includeStructures: true);
            }
        }

        private void FollowLane()
        {
            Vector3 point = lane.GetPoint(waypoint, unit.Team);
            if (CombatMath.FlatDistance(transform.position, point) <= waypointReach && waypoint < lane.Count - 1)
            {
                waypoint++;
                point = lane.GetPoint(waypoint, unit.Team);
            }
            laneAnchor = ClosestOnSegment(waypoint);
            motor.MoveTo(point, 0.5f);
        }

        private Vector3 ClosestOnSegment(int index)
        {
            Vector3 b = lane.GetPoint(index, unit.Team);
            if (index == 0)
                return b;
            Vector3 a = lane.GetPoint(index - 1, unit.Team);
            Vector3 ab = CombatMath.Flat(b - a);
            float t = ab.sqrMagnitude > 0.001f ? Mathf.Clamp01(Vector3.Dot(CombatMath.Flat(transform.position - a), ab) / ab.sqrMagnitude) : 1f;
            return a + ab * t;
        }

        private void OnAnyDamaged(Unit victim, DamageInfo info, float amount)
        {
            // Вражеский герой атаковал нашего героя рядом — переключаемся на него.
            if (!unit.IsAlive || !info.IsAttack || info.Source == null || !victim.IsHero)
                return;
            if (victim.Team != unit.Team || !info.Source.IsHero || !CombatMath.IsEnemy(unit.Team, info.Source.Team))
                return;
            if (CombatMath.FlatDistance(transform.position, victim.Position) > heroAggroRadius)
                return;
            if (CombatMath.EdgeDistance(unit, info.Source) > unit.AcquisitionRange)
                return;

            target = info.Source;
            forcedUntil = Time.time + heroAggroDuration;
        }
    }
}
