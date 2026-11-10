using LastStand.Core;
using UnityEngine;
using UnityEngine.AI;

namespace LastStand.Units
{
    /// <summary>
    /// Что происходит с телом после смерти: отключение агента (чтобы не мешал проходу),
    /// эффект смерти и удаление объекта через destroyDelay (для крипов). Герои не удаляются.
    /// </summary>
    [RequireComponent(typeof(Unit))]
    public sealed class UnitDeathHandler : MonoBehaviour
    {
        [Tooltip("Через сколько секунд удалить тело. 0 — не удалять (герои).")]
        [SerializeField] private float destroyDelay = 3.2f;
        [SerializeField] private GameObject deathVfx;

        private Unit unit;

        public float DestroyDelay
        {
            get => destroyDelay;
            set => destroyDelay = value;
        }

        public GameObject DeathVfx
        {
            get => deathVfx;
            set => deathVfx = value;
        }

        private void Awake() => unit = GetComponent<Unit>();
        private void OnEnable() => unit.Died += OnDied;
        private void OnDisable() => unit.Died -= OnDied;

        private void OnDied(Unit u, Unit killer)
        {
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null && agent.enabled)
            {
                if (agent.isOnNavMesh) agent.ResetPath();
                agent.enabled = false;
            }

            if (deathVfx != null)
                VfxPool.Spawn(deathVfx, unit.HitPosition, Quaternion.identity);

            if (destroyDelay > 0f)
                Destroy(gameObject, destroyDelay);
        }
    }
}
