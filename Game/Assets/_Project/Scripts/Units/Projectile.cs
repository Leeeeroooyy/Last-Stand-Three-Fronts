using LastStand.Core;
using UnityEngine;

namespace LastStand.Units
{
    /// <summary>Самонаводящийся снаряд автоатаки. Живёт в пуле VfxPool.</summary>
    [DisallowMultipleComponent]
    public sealed class Projectile : MonoBehaviour
    {
        private Unit target;
        private Vector3 targetPoint;
        private float speed;
        private DamageInfo damage;
        private GameObject impactVfx;
        private bool active;

        public static Projectile Launch(GameObject prefab, Vector3 origin, Unit target, float speed, DamageInfo damage, GameObject impactVfx)
        {
            Quaternion rot = Quaternion.LookRotation((target.HitPosition - origin).normalized + Vector3.up * 0.0001f);
            GameObject go = VfxPool.Spawn(prefab, origin, rot);
            if (go == null)
                return null;
            var p = go.GetComponent<Projectile>();
            if (p == null) p = go.AddComponent<Projectile>();
            p.target = target;
            p.targetPoint = target.HitPosition;
            p.speed = speed;
            p.damage = damage;
            p.impactVfx = impactVfx;
            p.active = true;
            return p;
        }

        private void Update()
        {
            if (!active)
                return;

            bool targetAlive = target != null && target.IsAlive;
            if (targetAlive)
                targetPoint = target.HitPosition;

            Vector3 pos = transform.position;
            Vector3 to = targetPoint - pos;
            float step = speed * Time.deltaTime;
            if (to.magnitude <= step + 0.1f)
            {
                transform.position = targetPoint;
                Arrive(targetAlive);
                return;
            }

            transform.position = pos + to.normalized * step;
            transform.rotation = Quaternion.LookRotation(to);
        }

        private void Arrive(bool hit)
        {
            active = false;
            if (hit)
            {
                target.ApplyDamage(damage);
                if (impactVfx != null)
                    VfxPool.Spawn(impactVfx, targetPoint, Quaternion.identity);
            }
            target = null;
            VfxPool.Release(gameObject);
        }
    }
}
