using LastStand.Core;
using LastStand.Map;
using UnityEngine;

namespace LastStand.Creeps
{
    public enum CreepType
    {
        Melee = 0,
        Ranged = 1
    }

    /// <summary>Тип крипа: характеристики и префабы для каждой стороны.</summary>
    [CreateAssetMenu(menuName = "Last Stand/Creep Definition", fileName = "Creep_")]
    public sealed class CreepDefinition : ScriptableObject
    {
        public string creepName = "Creep";
        public CreepType type;
        public GameObject dawnPrefab;
        public GameObject duskPrefab;
        public UnitStats stats = new UnitStats
        {
            maxHealth = 550f, healthRegen = 0.5f, armor = 2f, damageMin = 19f, damageMax = 23f,
            attackRange = 1.2f, baseAttackTime = 1f, attackPoint = 0.47f, attackBackswing = 0.53f,
            moveSpeed = 5f, turnRate = 720f, acquisitionRange = 7.5f, visionRange = 12f,
            goldBounty = 40, xpBounty = 57
        };
        public float bodyRadius = 0.45f;
        public float barHeight = 2.1f;

        public GameObject PrefabFor(TeamSide side) => side == TeamSide.Dusk ? duskPrefab : dawnPrefab;
    }
}
