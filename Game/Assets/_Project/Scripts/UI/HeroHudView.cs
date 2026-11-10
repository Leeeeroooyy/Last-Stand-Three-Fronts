using LastStand.Abilities;
using LastStand.Core;
using LastStand.Heroes;
using LastStand.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastStand.UI
{
    /// <summary>Нижняя панель: портрет героя, здоровье/мана с регенерацией, четыре способности, таймер возрождения.</summary>
    public sealed class HeroHudView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text heroName;
        [SerializeField] private TMP_Text heroTitle;
        [SerializeField] private Image healthFill;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text healthRegenText;
        [SerializeField] private Image manaFill;
        [SerializeField] private TMP_Text manaText;
        [SerializeField] private TMP_Text manaRegenText;
        [SerializeField] private TMP_Text damageText;
        [SerializeField] private TMP_Text armorText;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private AbilitySlotView[] slots;
        [SerializeField] private GameObject respawnOverlay;
        [SerializeField] private TMP_Text respawnText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private UiTheme theme;

        private Unit unit;
        private Hero hero;
        private HeroAbilities abilities;
        private int shownLevel = -1;

        private void Start()
        {
            if (group != null) group.alpha = 0f;
            if (respawnOverlay != null) respawnOverlay.SetActive(false);
        }

        private void OnDisable() => Bind(null);

        private void Bind(Unit u)
        {
            if (abilities != null)
                abilities.AbilityCast -= OnAbilityCast;

            unit = u;
            hero = u != null ? u.GetComponent<Hero>() : null;
            abilities = u != null ? u.GetComponent<HeroAbilities>() : null;

            if (abilities != null)
                abilities.AbilityCast += OnAbilityCast;

            if (u == null)
                return;

            HeroDefinition def = hero != null ? hero.Definition : null;
            if (def != null)
            {
                if (def.portrait != null) portrait.sprite = def.portrait;
                heroName.SetText(def.heroName);
                heroTitle.SetText(def.title);
            }
            else
            {
                heroName.SetText(u.DisplayName);
                heroTitle.SetText(string.Empty);
            }

            for (int i = 0; i < slots.Length; i++)
                slots[i].Bind(abilities, i);
        }

        private void OnAbilityCast(int index)
        {
            if (index >= 0 && index < slots.Length)
                slots[index].FlashCast();
        }

        private void Update()
        {
            MatchManager match = MatchManager.Instance;
            Unit player = match != null ? match.PlayerHero : null;
            if (player != unit)
                Bind(player);

            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, unit != null ? 1f : 0f, Time.unscaledDeltaTime * 3f);
            if (unit == null)
                return;

            healthFill.fillAmount = unit.HealthFraction;
            healthText.SetText("{0:0}/{1:0}", Mathf.Ceil(unit.Health), unit.MaxHealth);
            healthRegenText.SetText("+{0:1}", unit.HealthRegen);

            bool hasMana = unit.MaxMana > 0f;
            manaFill.fillAmount = hasMana ? unit.ManaFraction : 0f;
            manaText.SetText("{0:0}/{1:0}", Mathf.Floor(unit.Mana), unit.MaxMana);
            manaRegenText.SetText("+{0:1}", unit.BaseStats.manaRegen);

            // Характеристики: урон (с бонусами), броня, скорость бега.
            if (damageText != null)
                damageText.SetText("{0:0}~{1:0}", unit.BaseStats.damageMin + unit.BonusDamage, unit.BaseStats.damageMax + unit.BonusDamage);
            if (armorText != null)
                armorText.SetText("{0:0}", unit.Armor);
            if (speedText != null)
                speedText.SetText("{0:1}", unit.MoveSpeed);

            if (levelText != null && hero != null && hero.Level != shownLevel)
            {
                shownLevel = hero.Level;
                levelText.SetText("Lv. " + shownLevel);
            }

            bool dead = hero != null && hero.IsRespawning;
            if (respawnOverlay != null && respawnOverlay.activeSelf != dead)
                respawnOverlay.SetActive(dead);
            if (dead && respawnText != null)
                respawnText.SetText("{0:0}", Mathf.Ceil(hero.RespawnRemaining));
            portrait.color = dead ? new Color(0.35f, 0.35f, 0.4f) : Color.white;
        }
    }
}
