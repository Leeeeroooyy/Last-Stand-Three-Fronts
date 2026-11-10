using System;
using LastStand.Core;
using UnityEngine;

namespace LastStand.Abilities
{
    /// <summary>Способность героя в слоте: уровень и перезарядка.</summary>
    public sealed class AbilitySlot
    {
        public AbilityDefinition Definition { get; }
        public int Level { get; set; }
        public float CooldownRemaining { get; private set; }
        public float CooldownTotal { get; private set; }

        public AbilitySlot(AbilityDefinition definition, int level)
        {
            Definition = definition;
            Level = definition != null ? Mathf.Clamp(level, 0, definition.maxLevel) : 0;
        }

        public bool IsLearned => Definition != null && Level > 0;
        public float ManaCost => IsLearned ? Definition.ManaCost(Level) : 0f;
        public float CastRange => IsLearned ? Definition.CastRange(Level) : 0f;
        public float CooldownFraction => CooldownTotal > 0f ? CooldownRemaining / CooldownTotal : 0f;

        public void StartCooldown()
        {
            CooldownTotal = Definition.Cooldown(Level);
            CooldownRemaining = CooldownTotal;
        }

        public void Tick(float dt)
        {
            if (CooldownRemaining > 0f)
                CooldownRemaining = Mathf.Max(0f, CooldownRemaining - dt);
        }

        public void ResetCooldown() => CooldownRemaining = 0f;
    }

    /// <summary>Четыре способности героя (Q, W, E, R).</summary>
    [RequireComponent(typeof(Unit))]
    [DisallowMultipleComponent]
    public sealed class HeroAbilities : MonoBehaviour
    {
        public const int SlotCount = 4;
        public static readonly string[] Hotkeys = { "Q", "W", "E", "R" };

        [SerializeField] private AbilityDefinition[] definitions = new AbilityDefinition[SlotCount];
        [Tooltip("Стартовые уровни (этап 2: прокачка за опыт).")]
        [SerializeField] private int[] startLevels = { 1, 1, 1, 1 };

        private AbilitySlot[] slots;
        private Unit unit;

        public event Action<int> AbilityCast;

        public int Count => SlotCount;
        public AbilitySlot this[int index] => Slots[index];

        private AbilitySlot[] Slots
        {
            get
            {
                if (slots == null) Build();
                return slots;
            }
        }

        private void Awake() => unit = GetComponent<Unit>();

        public void Setup(AbilityDefinition[] defs, int[] levels = null)
        {
            definitions = new AbilityDefinition[SlotCount];
            if (defs != null)
                for (int i = 0; i < SlotCount && i < defs.Length; i++)
                    definitions[i] = defs[i];
            if (levels != null) startLevels = levels;
            Build();
        }

        private void Build()
        {
            slots = new AbilitySlot[SlotCount];
            for (int i = 0; i < SlotCount; i++)
            {
                AbilityDefinition def = definitions != null && i < definitions.Length ? definitions[i] : null;
                int level = startLevels != null && i < startLevels.Length ? startLevels[i] : 1;
                slots[i] = new AbilitySlot(def, level);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            AbilitySlot[] s = Slots;
            for (int i = 0; i < s.Length; i++)
                s[i].Tick(dt);
        }

        public AbilityCastError CanCast(int index)
        {
            if (index < 0 || index >= SlotCount)
                return AbilityCastError.NotLearned;
            AbilitySlot slot = Slots[index];
            if (!slot.IsLearned) return AbilityCastError.NotLearned;
            if (slot.Definition.targeting == AbilityTargeting.Passive) return AbilityCastError.Passive;
            if (!unit.IsAlive) return AbilityCastError.Dead;
            if (unit.IsStunned) return AbilityCastError.Stunned;
            if (unit.IsSilenced) return AbilityCastError.Silenced;
            if (slot.CooldownRemaining > 0f) return AbilityCastError.OnCooldown;
            if (unit.Mana + 0.001f < slot.ManaCost) return AbilityCastError.NotEnoughMana;
            return AbilityCastError.None;
        }

        /// <summary>Списывает ману, запускает перезарядку и применяет способность.</summary>
        public bool Commit(int index, Unit target, Vector3 point)
        {
            if (CanCast(index) != AbilityCastError.None)
                return false;
            AbilitySlot slot = Slots[index];
            if (!unit.TrySpendMana(slot.ManaCost))
                return false;
            slot.StartCooldown();
            slot.Definition.Execute(new AbilityCastContext(unit, slot.Level, target, point));
            AbilityCast?.Invoke(index);
            return true;
        }

        public static string ErrorText(AbilityCastError error) => error switch
        {
            AbilityCastError.NotLearned => "Ability not learned",
            AbilityCastError.Passive => "Passive ability",
            AbilityCastError.OnCooldown => "Ability on cooldown",
            AbilityCastError.NotEnoughMana => "Not enough mana",
            AbilityCastError.Silenced => "Silenced",
            AbilityCastError.Stunned => "Stunned",
            AbilityCastError.Dead => "Your hero is dead",
            AbilityCastError.InvalidTarget => "Invalid target",
            _ => string.Empty
        };
    }
}
