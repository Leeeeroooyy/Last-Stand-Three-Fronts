using System;
using UnityEngine;

namespace LastStand.Core
{
    /// <summary>
    /// Базовые характеристики юнита. Единицы — метры и секунды
    /// (ориентир: 1 м ≈ 65 единиц Dota 2, скорость героя ≈ 6 м/с).
    /// </summary>
    [Serializable]
    public sealed class UnitStats
    {
        [Header("Здоровье и мана")]
        [Min(1f)] public float maxHealth = 500f;
        public float healthRegen = 1f;
        [Min(0f)] public float maxMana;
        public float manaRegen;

        [Header("Защита")]
        public float armor;
        [Range(0f, 1f), Tooltip("Доля поглощаемого магического урона.")]
        public float magicResist;

        [Header("Атака")]
        public float damageMin = 20f;
        public float damageMax = 24f;
        [Tooltip("Дальность атаки от края до края, м.")]
        public float attackRange = 1.3f;
        [Tooltip("Базовый интервал атаки (BAT) при скорости атаки 100.")]
        public float baseAttackTime = 1.7f;
        [Tooltip("Скорость атаки (100 = базовая).")]
        public float attackSpeed = 100f;
        [Tooltip("Замах до удара, с (при скорости атаки 100).")]
        public float attackPoint = 0.35f;
        [Tooltip("Время после удара, с.")]
        public float attackBackswing = 0.3f;
        [Tooltip("0 — ближний бой; иначе скорость снаряда, м/с.")]
        public float projectileSpeed;

        [Header("Движение и обзор")]
        public float moveSpeed = 6f;
        [Tooltip("Скорость поворота, град/с.")]
        public float turnRate = 900f;
        [Tooltip("Радиус, в котором юнит сам замечает врагов.")]
        public float acquisitionRange = 8f;
        public float visionRange = 14f;

        [Header("Награда (этап 2)")]
        public int goldBounty;
        public int xpBounty;

        public bool IsRanged => projectileSpeed > 0f;

        public UnitStats Clone() => (UnitStats)MemberwiseClone();

        /// <summary>Копия с умноженными здоровьем и уроном (усиленные крипы).</summary>
        public UnitStats Scaled(float healthMultiplier, float damageMultiplier)
        {
            UnitStats s = Clone();
            s.maxHealth *= healthMultiplier;
            s.damageMin *= damageMultiplier;
            s.damageMax *= damageMultiplier;
            return s;
        }
    }
}
