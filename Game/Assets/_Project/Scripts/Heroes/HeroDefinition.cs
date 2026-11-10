using LastStand.Abilities;
using LastStand.Core;
using UnityEngine;

namespace LastStand.Heroes
{
    public enum HeroRole
    {
        Carry = 0,
        Tank = 1,
        Support = 2
    }

    /// <summary>Описание героя: префаб, характеристики, четыре способности, портрет.</summary>
    [CreateAssetMenu(menuName = "Last Stand/Hero Definition", fileName = "Hero_")]
    public sealed class HeroDefinition : ScriptableObject
    {
        public string heroName = "Hero";
        public string title = "";
        public HeroRole role;
        [TextArea(2, 4)] public string lore;
        public Sprite portrait;
        public Color accentColor = new Color(0.95f, 0.78f, 0.42f);

        [Header("Модель")]
        public GameObject prefab;

        [Header("Характеристики")]
        public UnitStats stats = new UnitStats
        {
            maxHealth = 620f, healthRegen = 2f, maxMana = 300f, manaRegen = 1.2f,
            armor = 3f, damageMin = 48f, damageMax = 54f, attackRange = 1.3f,
            baseAttackTime = 1.7f, attackPoint = 0.35f, attackBackswing = 0.3f,
            moveSpeed = 6f, turnRate = 900f, acquisitionRange = 7f, visionRange = 14f
        };
        public float bodyRadius = 0.5f;
        public float barHeight = 2.9f;

        [Header("Способности (Q, W, E, R)")]
        public AbilityDefinition[] abilities = new AbilityDefinition[HeroAbilities.SlotCount];
    }
}
