using System.Collections.Generic;
using LastStand.Core;
using LastStand.Map;
using UnityEngine;

namespace LastStand.Structures
{
    /// <summary>Фонтан базы: быстро лечит союзников и сжигает врагов в радиусе.</summary>
    [DisallowMultipleComponent]
    public sealed class FountainAura : MonoBehaviour
    {
        [SerializeField] private TeamSide team = TeamSide.Dawn;
        [SerializeField] private float radius = 8f;
        [SerializeField] private float healPercentPerSecond = 6f;
        [SerializeField] private float manaPercentPerSecond = 6f;
        [SerializeField] private float damagePerSecond = 300f;

        private readonly List<Unit> buffer = new List<Unit>(16);

        public void Configure(TeamSide side, float auraRadius, float healPercent, float manaPercent, float dps)
        {
            team = side;
            radius = auraRadius;
            healPercentPerSecond = healPercent;
            manaPercentPerSecond = manaPercent;
            damagePerSecond = dps;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UnitRegistry.Query(transform.position, radius, team, TeamFilter.All, buffer);
            for (int i = 0; i < buffer.Count; i++)
            {
                Unit u = buffer[i];
                if (u.Team == team)
                {
                    u.Heal(u.MaxHealth * healPercentPerSecond * 0.01f * dt);
                    u.RestoreMana(u.MaxMana * manaPercentPerSecond * 0.01f * dt);
                }
                else if (CombatMath.IsEnemy(team, u.Team))
                {
                    u.ApplyDamage(new DamageInfo(damagePerSecond * dt, DamageType.Pure, null));
                }
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 1f, 0.6f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
