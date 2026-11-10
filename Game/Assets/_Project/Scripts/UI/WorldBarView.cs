using LastStand.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastStand.UI
{
    /// <summary>Полоска здоровья (и маны у героев) над юнитом. Хвост показывает только что полученный урон.</summary>
    public sealed class WorldBarView : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private RectTransform healthFrame;
        [SerializeField] private Image healthFill;
        [SerializeField] private Image healthTrail;
        [SerializeField] private RectTransform manaFrame;
        [SerializeField] private Image manaFill;
        [SerializeField] private GameObject levelBadge;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private GameObject protectedIcon;

        private float trail = 1f;
        private float trailHold;

        public Unit Unit { get; private set; }
        public RectTransform Root => root;

        public void Bind(Unit unit, UiTheme theme, bool isPlayer, bool enemy)
        {
            Unit = unit;
            trail = unit.HealthFraction;

            Vector2 size = unit.Kind switch
            {
                UnitKind.Hero => new Vector2(124f, 12f),
                UnitKind.Creep => new Vector2(66f, 10f),
                UnitKind.Tower => new Vector2(134f, 12f),
                UnitKind.Barracks => new Vector2(144f, 12f),
                _ => new Vector2(210f, 14f)
            };
            healthFrame.sizeDelta = size;

            bool hasMana = unit.IsHero && unit.MaxMana > 0f;
            manaFrame.gameObject.SetActive(hasMana);
            if (hasMana)
                manaFrame.sizeDelta = new Vector2(size.x, 9f);

            levelBadge.SetActive(unit.IsHero);
            healthFill.color = isPlayer ? theme.selfHealth : enemy ? theme.enemyHealth : theme.allyHealth;
            healthTrail.color = theme.damageTrail;
            manaFill.color = theme.mana;
            gameObject.SetActive(true);
            Refresh(theme, isPlayer, enemy, 1f);
        }

        public void Refresh(UiTheme theme, bool isPlayer, bool enemy, float dt)
        {
            float hp = Unit.HealthFraction;
            healthFill.fillAmount = hp;

            // Хвост урона: держится 0.35 с и плавно догоняет.
            if (hp < trail)
            {
                trailHold -= dt;
                if (trailHold <= 0f)
                    trail = Mathf.MoveTowards(trail, hp, dt * 0.9f);
            }
            else
            {
                trail = hp;
                trailHold = 0.35f;
            }
            healthTrail.fillAmount = trail;

            bool isProtected = Unit.IsInvulnerable && Unit.IsStructure;
            if (protectedIcon != null && protectedIcon.activeSelf != isProtected)
                protectedIcon.SetActive(isProtected);
            healthFill.color = isProtected ? theme.protectedHealth
                : isPlayer ? theme.selfHealth : enemy ? theme.enemyHealth : theme.allyHealth;

            if (manaFrame.gameObject.activeSelf)
                manaFill.fillAmount = Unit.ManaFraction;
        }

        public void SetLevel(int level)
        {
            if (levelText != null)
                levelText.SetText(level.ToString());
        }
    }
}
