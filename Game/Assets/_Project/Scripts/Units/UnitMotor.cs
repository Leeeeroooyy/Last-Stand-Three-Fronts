using System;
using System.Collections;
using LastStand.Core;
using UnityEngine;
using UnityEngine.AI;

namespace LastStand.Units
{
    /// <summary>
    /// Движение юнита по NavMesh: путь, поворот с ограниченной скоростью (как в Dota), рывки.
    /// Вращение делается вручную, чтобы юнит доворачивался к цели атаки/каста.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent), typeof(Unit))]
    [DisallowMultipleComponent]
    public sealed class UnitMotor : MonoBehaviour
    {
        private NavMeshAgent agent;
        private Unit unit;
        private UnitAnimator animator;

        private Vector3 destination;
        private bool hasDestination;
        private Vector3 faceTarget;
        private bool hasFaceTarget;
        private Coroutine dashRoutine;

        public bool IsDashing { get; private set; }
        public bool IsReady => agent != null && agent.enabled && agent.isOnNavMesh;
        public bool CanMove => unit.IsAlive && !unit.IsStunned && !unit.IsRooted && !IsDashing && IsReady;
        public bool HasDestination => hasDestination;
        public Vector3 Destination => destination;
        public Vector3 Velocity => IsReady ? agent.velocity : Vector3.zero;
        public NavMeshAgent Agent => agent;

        private void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            unit = GetComponent<Unit>();
            animator = GetComponent<UnitAnimator>();
            agent.updateRotation = false;
            agent.angularSpeed = 0f;
        }

        private void Start() => EnsureOnNavMesh();

        /// <summary>Ставит агента на ближайшую точку NavMesh (после спавна или телепорта).</summary>
        public bool EnsureOnNavMesh()
        {
            if (agent.enabled && agent.isOnNavMesh)
                return true;
            if (!NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
            {
                Debug.LogWarning($"{name}: рядом нет NavMesh.", this);
                return false;
            }
            transform.position = hit.position;
            agent.enabled = true;
            agent.Warp(hit.position);
            return agent.isOnNavMesh;
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            StopDash();
            if (NavMesh.SamplePosition(position, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                position = hit.position;
            transform.position = position;
            agent.enabled = true;
            agent.Warp(position);
            transform.rotation = rotation;
            hasDestination = false;
            hasFaceTarget = false;
        }

        /// <summary>Идти к точке. Повторный вызов с той же точкой не пересчитывает путь.</summary>
        public bool MoveTo(Vector3 point, float stoppingDistance = 0.1f)
        {
            if (!CanMove)
                return false;

            hasFaceTarget = false;
            if (hasDestination && (point - destination).sqrMagnitude < 0.09f && (agent.hasPath || agent.pathPending))
                return true;

            agent.stoppingDistance = stoppingDistance;
            agent.isStopped = false;
            if (!agent.SetDestination(point))
                return false;
            destination = point;
            hasDestination = true;
            return true;
        }

        public void Stop()
        {
            hasDestination = false;
            if (IsReady)
            {
                agent.ResetPath();
                agent.velocity = Vector3.zero;
            }
        }

        public void FaceTowards(Vector3 point)
        {
            faceTarget = point;
            hasFaceTarget = true;
        }

        public bool IsFacing(Vector3 point, float toleranceDeg = 25f)
        {
            Vector3 dir = CombatMath.Flat(point - transform.position);
            if (dir.sqrMagnitude < 0.01f)
                return true;
            return Vector3.Angle(transform.forward, dir) <= toleranceDeg;
        }

        public void FaceInstant(Vector3 point)
        {
            Vector3 dir = CombatMath.Flat(point - transform.position);
            if (dir.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }

        private void Update()
        {
            if (!IsReady || IsDashing)
                return;

            agent.speed = unit.MoveSpeed;
            bool blocked = !unit.IsAlive || unit.IsStunned || unit.IsRooted;
            if (agent.isStopped != blocked)
                agent.isStopped = blocked;
            if (blocked)
                agent.velocity = Vector3.zero;

            if (hasDestination && !agent.pathPending && agent.remainingDistance <= agent.stoppingDistance + 0.05f)
            {
                hasDestination = false;
                agent.ResetPath();
            }

            if (!unit.IsAlive || unit.IsStunned)
                return;

            Vector3 dir = Vector3.zero;
            Vector3 v = agent.velocity;
            v.y = 0f;
            if (v.sqrMagnitude > 0.04f)
                dir = v;
            else if (hasFaceTarget)
                dir = CombatMath.Flat(faceTarget - transform.position);

            if (dir.sqrMagnitude > 0.0001f)
            {
                Quaternion target = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, unit.BaseStats.turnRate * Time.deltaTime);
            }
        }

        // ---------- Рывок ----------

        /// <summary>
        /// Рывок/прыжок к точке. Точка обрезается по краю NavMesh.
        /// arcHeight > 0 — прыжок по дуге. onLand вызывается при приземлении.
        /// </summary>
        public void Dash(Vector3 target, float speed, float arcHeight, Action onLand)
        {
            if (!IsReady)
                return;
            StopDash();

            Vector3 start = transform.position;
            if (NavMesh.SamplePosition(target, out NavMeshHit sampled, 3f, NavMesh.AllAreas))
                target = sampled.position;
            else if (NavMesh.Raycast(start, target, out NavMeshHit edge, NavMesh.AllAreas))
                target = edge.position;

            dashRoutine = StartCoroutine(DashRoutine(start, target, Mathf.Max(1f, speed), arcHeight, onLand));
        }

        private IEnumerator DashRoutine(Vector3 start, Vector3 end, float speed, float arcHeight, Action onLand)
        {
            IsDashing = true;
            Stop();
            agent.enabled = false;
            FaceInstant(end);

            float duration = Mathf.Max(0.12f, Vector3.Distance(start, end) / speed);
            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / duration);
                transform.position = Vector3.Lerp(start, end, t);
                if (animator != null) animator.SetAirHeight(arcHeight * 4f * t * (1f - t));
                yield return null;
            }

            if (animator != null) animator.SetAirHeight(0f);
            agent.enabled = true;
            agent.Warp(end);
            IsDashing = false;
            dashRoutine = null;
            onLand?.Invoke();
        }

        private void StopDash()
        {
            if (dashRoutine == null)
                return;
            StopCoroutine(dashRoutine);
            dashRoutine = null;
            IsDashing = false;
            if (animator != null) animator.SetAirHeight(0f);
            agent.enabled = true;
        }

        private void OnDisable() => StopDash();
    }
}
