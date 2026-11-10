using System;
using UnityEngine;

namespace LastStand.Core
{
    /// <summary>Описание временного эффекта (бафф/дебафф). Задаётся в способностях через инспектор.</summary>
    [Serializable]
    public sealed class StatusEffectData
    {
        [Tooltip("Идентификатор: эффекты с одинаковым id не складываются, а обновляют длительность.")]
        public string id = "effect";
        public float duration = 2f;
        public bool isDebuff = true;

        [Header("Модификаторы")]
        [Tooltip("Процент скорости движения: −30 = замедление на 30 %.")]
        public float moveSpeedPercent;
        public float attackSpeedBonus;
        public float armorBonus;
        public float damageBonus;
        public float healthRegenBonus;
        public UnitStates states;

        [Header("Периодический урон")]
        public float damagePerSecond;
        public DamageType damageType = DamageType.Magical;
        public Element element;

        [Header("Визуал")]
        [Tooltip("Эффект, прикрепляемый к юниту на время действия (необязательно).")]
        public GameObject attachedVfx;
        [Tooltip("Высота крепления эффекта: 0 — земля, 1 — над головой.")]
        [Range(0f, 1.2f)] public float vfxHeight = 0.5f;

        public bool IsEmpty => duration <= 0f;
    }

    /// <summary>Активный экземпляр эффекта на юните.</summary>
    public sealed class ActiveStatus
    {
        public StatusEffectData Data;
        public Unit Source;
        public float Remaining;
        public float Duration;
        public GameObject Vfx;

        public float Progress => Duration > 0f ? 1f - Remaining / Duration : 1f;
    }
}
