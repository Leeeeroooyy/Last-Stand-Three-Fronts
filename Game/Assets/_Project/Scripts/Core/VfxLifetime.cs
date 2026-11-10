using UnityEngine;

namespace LastStand.Core
{
    /// <summary>Возвращает эффект в пул через заданное время (или по окончании частиц, если lifetime ≤ 0).</summary>
    public sealed class VfxLifetime : MonoBehaviour
    {
        [SerializeField] private float lifetime = 2f;

        private float endTime;
        private ParticleSystem[] systems;

        public float Lifetime
        {
            get => lifetime;
            set => lifetime = value;
        }

        private void Awake() => systems = GetComponentsInChildren<ParticleSystem>();

        private void OnEnable() => endTime = Time.time + (lifetime > 0f ? lifetime : 30f);

        private void Update()
        {
            if (lifetime <= 0f)
            {
                bool alive = false;
                for (int i = 0; i < systems.Length; i++)
                    alive |= systems[i].IsAlive(true);
                if (!alive)
                {
                    VfxPool.Release(gameObject);
                    return;
                }
            }

            if (Time.time >= endTime)
                VfxPool.Release(gameObject);
        }
    }
}
