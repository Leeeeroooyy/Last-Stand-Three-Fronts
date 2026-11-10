using System;
using System.Collections.Generic;
using LastStand.Map;
using UnityEngine;

namespace LastStand.Core
{
    /// <summary>
    /// Любой боевой юнит: герой, крип, башня, казарма, Ancient.
    /// Хранит здоровье, ману, эффекты и считает итоговые характеристики.
    /// Движение, атака и поведение — в отдельных компонентах (UnitMotor, UnitAttack, *AI, HeroController).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)]
    public sealed class Unit : MonoBehaviour
    {
        [SerializeField] private string displayName = "Unit";
        [SerializeField] private UnitKind kind;
        [SerializeField] private TeamSide team = TeamSide.Dawn;
        [SerializeField] private UnitStats baseStats = new UnitStats();
        [Tooltip("Радиус тела, м (для дистанций атаки и выбора мышью).")]
        [SerializeField] private float radius = 0.5f;
        [Tooltip("Высота полоски здоровья над точкой опоры, м.")]
        [SerializeField] private float barHeight = 2.6f;
        [Tooltip("Точка попадания снарядов (если пусто — середина по высоте).")]
        [SerializeField] private Transform hitPoint;

        private readonly List<ActiveStatus> statuses = new List<ActiveStatus>();
        private int protectionLocks;
        private float health;
        private float mana;
        private bool initialized;

        // ---------- События ----------
        /// <summary>Юнит получил урон: (жертва, урон до защиты, итоговый урон).</summary>
        public event Action<Unit, DamageInfo, float> Damaged;
        public event Action<Unit, Unit> Died;
        public event Action<Unit> Revived;

        /// <summary>Глобальные события для систем, которым нужны все юниты (ИИ крипов, башни, UI).</summary>
        public static event Action<Unit, DamageInfo, float> AnyDamaged;
        public static event Action<Unit, Unit> AnyDied;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyDamaged = null;
            AnyDied = null;
        }

        // ---------- Свойства ----------
        public string DisplayName => displayName;
        public UnitKind Kind => kind;
        public TeamSide Team => team;
        public UnitStats BaseStats => baseStats;
        public float Radius => radius;
        public float BarHeight => barHeight;
        public bool IsAlive { get; private set; } = true;
        public bool IsHero => kind == UnitKind.Hero;
        public bool IsStructure => kind == UnitKind.Tower || kind == UnitKind.Barracks || kind == UnitKind.Ancient;
        public Vector3 Position => transform.position;
        public Vector3 HitPosition => hitPoint != null ? hitPoint.position : transform.position + Vector3.up * Mathf.Min(barHeight * 0.5f, 2.5f);
        public IReadOnlyList<ActiveStatus> Statuses => statuses;

        public float Health => health;
        public float MaxHealth => baseStats.maxHealth;
        public float Mana => mana;
        public float MaxMana => baseStats.maxMana;
        public float HealthFraction => MaxHealth > 0f ? health / MaxHealth : 0f;
        public float ManaFraction => MaxMana > 0f ? mana / MaxMana : 0f;

        public UnitStates States { get; private set; }
        public bool IsStunned => (States & UnitStates.Stunned) != 0;
        public bool IsSilenced => (States & UnitStates.Silenced) != 0;
        public bool IsRooted => (States & UnitStates.Rooted) != 0;
        public bool IsDisarmed => (States & UnitStates.Disarmed) != 0;
        /// <summary>Неуязвимость: защита построек (StructureGuard) или эффект.</summary>
        public bool IsInvulnerable => protectionLocks > 0 || (States & UnitStates.Invulnerable) != 0;
        public bool IsTargetable => IsAlive && isActiveAndEnabled;

        // ---------- Итоговые характеристики ----------
        public float Armor { get; private set; }
        public float MoveSpeed { get; private set; }
        public float AttackSpeed { get; private set; }
        public float BonusDamage { get; private set; }
        public float HealthRegen { get; private set; }
        public float AttackRange => baseStats.attackRange;
        public float AcquisitionRange => baseStats.acquisitionRange;
        public float AttackInterval => baseStats.baseAttackTime / (AttackSpeed / 100f);
        public float AttackWindup => baseStats.attackPoint / (AttackSpeed / 100f);
        public float AttackBackswing => baseStats.attackBackswing / (AttackSpeed / 100f);
        public float AverageDamage => (baseStats.damageMin + baseStats.damageMax) * 0.5f + BonusDamage;

        // =====================================================================

        /// <summary>Настройка юнита при создании из кода (бутстрап матча, спавн крипов).</summary>
        public void Setup(string unitName, UnitKind unitKind, TeamSide side, UnitStats stats, float bodyRadius, float healthBarHeight)
        {
            displayName = unitName;
            kind = unitKind;
            team = side;
            baseStats = stats.Clone();
            radius = bodyRadius;
            barHeight = healthBarHeight;
            ResetVitals();
        }

        public void SetTeam(TeamSide side) => team = side;

        private void Awake()
        {
            if (!initialized)
                ResetVitals();
        }

        private void OnEnable() => UnitRegistry.Register(this);
        private void OnDisable() => UnitRegistry.Unregister(this);

        private void ResetVitals()
        {
            initialized = true;
            IsAlive = true;
            health = baseStats.maxHealth;
            mana = baseStats.maxMana;
            ClearStatuses();
            RecalculateStats();
        }

        private void Update()
        {
            if (!IsAlive)
                return;

            float dt = Time.deltaTime;
            TickStatuses(dt);

            if (health < MaxHealth)
                health = Mathf.Min(MaxHealth, health + HealthRegen * dt);
            if (mana < MaxMana)
                mana = Mathf.Min(MaxMana, mana + baseStats.manaRegen * dt);
        }

        // ---------- Урон и лечение ----------

        /// <summary>Наносит урон с учётом брони/сопротивления. Возвращает фактический урон.</summary>
        public float ApplyDamage(DamageInfo info)
        {
            if (!IsAlive || IsInvulnerable || info.Amount <= 0f)
                return 0f;

            float amount = info.Amount;
            switch (info.Type)
            {
                case DamageType.Physical:
                    amount *= CombatMath.ArmorMultiplier(Armor);
                    break;
                case DamageType.Magical:
                    amount *= 1f - baseStats.magicResist;
                    break;
            }

            amount = Mathf.Min(amount, health);
            health -= amount;
            Damaged?.Invoke(this, info, amount);
            AnyDamaged?.Invoke(this, info, amount);

            if (health <= 0.001f)
                Die(info.Source);
            return amount;
        }

        public void Heal(float amount)
        {
            if (IsAlive && amount > 0f)
                health = Mathf.Min(MaxHealth, health + amount);
        }

        public void RestoreMana(float amount)
        {
            if (IsAlive && amount > 0f)
                mana = Mathf.Min(MaxMana, mana + amount);
        }

        public bool TrySpendMana(float amount)
        {
            if (amount <= 0f)
                return true;
            if (mana + 0.001f < amount)
                return false;
            mana = Mathf.Max(0f, mana - amount);
            return true;
        }

        public void Kill(Unit killer = null) => Die(killer);

        private void Die(Unit killer)
        {
            if (!IsAlive)
                return;
            IsAlive = false;
            health = 0f;
            ClearStatuses();
            Died?.Invoke(this, killer);
            AnyDied?.Invoke(this, killer);
        }

        /// <summary>Возрождение (герои): полное здоровье и мана.</summary>
        public void Revive()
        {
            ResetVitals();
            Revived?.Invoke(this);
        }

        // ---------- Защита построек ----------

        /// <summary>Включает/снимает неуязвимость от защиты построек (вызывает StructureGuard).</summary>
        public void SetProtected(bool value)
        {
            protectionLocks = value ? 1 : 0;
        }

        // ---------- Эффекты ----------

        public ActiveStatus AddStatus(StatusEffectData data, Unit source, float durationOverride = -1f)
        {
            if (data == null || !IsAlive)
                return null;
            float duration = durationOverride > 0f ? durationOverride : data.duration;
            if (duration <= 0f)
                return null;
            if (data.isDebuff && IsInvulnerable)
                return null;

            for (int i = 0; i < statuses.Count; i++)
            {
                ActiveStatus s = statuses[i];
                if (s.Data.id != data.id)
                    continue;
                s.Data = data;
                s.Source = source;
                s.Duration = Mathf.Max(s.Remaining, duration);
                s.Remaining = s.Duration;
                RecalculateStats();
                return s;
            }

            var status = new ActiveStatus { Data = data, Source = source, Remaining = duration, Duration = duration };
            if (data.attachedVfx != null)
            {
                Vector3 offset = Vector3.up * (barHeight * data.vfxHeight);
                status.Vfx = VfxPool.Spawn(data.attachedVfx, transform.position + offset, Quaternion.identity, transform);
            }
            statuses.Add(status);
            RecalculateStats();
            return status;
        }

        public bool HasStatus(string id)
        {
            for (int i = 0; i < statuses.Count; i++)
                if (statuses[i].Data.id == id)
                    return true;
            return false;
        }

        public void RemoveStatus(string id)
        {
            for (int i = statuses.Count - 1; i >= 0; i--)
                if (statuses[i].Data.id == id)
                    RemoveAt(i);
            RecalculateStats();
        }

        private void TickStatuses(float dt)
        {
            bool changed = false;
            for (int i = statuses.Count - 1; i >= 0; i--)
            {
                ActiveStatus s = statuses[i];
                if (s.Data.damagePerSecond > 0f)
                {
                    float tick = s.Data.damagePerSecond * Mathf.Min(dt, s.Remaining);
                    ApplyDamage(new DamageInfo(tick, s.Data.damageType, s.Source, false, s.Data.element));
                    if (!IsAlive)
                        return;
                }

                s.Remaining -= dt;
                if (s.Remaining <= 0f)
                {
                    RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
                RecalculateStats();
        }

        private void RemoveAt(int index)
        {
            ActiveStatus s = statuses[index];
            if (s.Vfx != null)
                VfxPool.Release(s.Vfx);
            statuses.RemoveAt(index);
        }

        private void ClearStatuses()
        {
            for (int i = statuses.Count - 1; i >= 0; i--)
                RemoveAt(i);
            RecalculateStats();
        }

        private void RecalculateStats()
        {
            float armor = baseStats.armor;
            float speedPercent = 0f;
            float attackSpeed = baseStats.attackSpeed;
            float bonusDamage = 0f;
            float regen = baseStats.healthRegen;
            UnitStates states = UnitStates.None;
            float slowest = 0f;

            for (int i = 0; i < statuses.Count; i++)
            {
                StatusEffectData d = statuses[i].Data;
                armor += d.armorBonus;
                attackSpeed += d.attackSpeedBonus;
                bonusDamage += d.damageBonus;
                regen += d.healthRegenBonus;
                states |= d.states;
                // Замедления не складываются (как в Dota) — берётся сильнейшее; ускорения суммируются.
                if (d.moveSpeedPercent < 0f) slowest = Mathf.Min(slowest, d.moveSpeedPercent);
                else speedPercent += d.moveSpeedPercent;
            }

            Armor = armor;
            AttackSpeed = Mathf.Clamp(attackSpeed, 20f, 700f);
            BonusDamage = bonusDamage;
            HealthRegen = regen;
            States = states;
            MoveSpeed = Mathf.Max(1f, baseStats.moveSpeed * (1f + (speedPercent + slowest) / 100f));
        }

        /// <summary>Случайный урон атаки с учётом бонусов.</summary>
        public float RollAttackDamage() =>
            UnityEngine.Random.Range(baseStats.damageMin, baseStats.damageMax) + BonusDamage;

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, radius);
            Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, radius + baseStats.attackRange);
        }
#endif
    }
}
