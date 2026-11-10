using LastStand.Abilities;
using LastStand.Controls;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LastStand.UI
{
    /// <summary>Слот способности в HUD: иконка, перезарядка, стоимость маны, уровень, клавиша, подсказка.</summary>
    public sealed class AbilitySlotView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private Image cooldownOverlay;
        [SerializeField] private TMP_Text cooldownText;
        [SerializeField] private TMP_Text hotkeyText;
        [SerializeField] private TMP_Text manaText;
        [SerializeField] private Image glow;
        [SerializeField] private Image frame;
        [SerializeField] private Image[] levelPips;
        [SerializeField] private UiTheme theme;

        private static readonly Color NoManaTint = new Color(0.45f, 0.55f, 1f, 1f);
        private static readonly Color DisabledTint = new Color(0.4f, 0.4f, 0.45f, 1f);

        private HeroAbilities abilities;
        private int index;
        private bool hovered;
        private float flash;

        public void Bind(HeroAbilities heroAbilities, int slotIndex)
        {
            abilities = heroAbilities;
            index = slotIndex;
            hotkeyText.SetText(HeroAbilities.Hotkeys[slotIndex]);
            AbilitySlot slot = abilities != null ? abilities[slotIndex] : null;
            icon.sprite = slot?.Definition != null ? slot.Definition.icon : null;
            icon.enabled = icon.sprite != null;
            for (int i = 0; i < levelPips.Length; i++)
                levelPips[i].gameObject.SetActive(slot?.Definition != null && i < slot.Definition.maxLevel);
        }

        public void FlashCast() => flash = 1f;

        private void Update()
        {
            if (abilities == null)
                return;

            AbilitySlot slot = abilities[index];
            bool learned = slot.IsLearned;
            AbilityCastError error = abilities.CanCast(index);

            float cd = slot.CooldownRemaining;
            cooldownOverlay.fillAmount = cd > 0f ? slot.CooldownFraction : 0f;
            if (cd > 0f)
            {
                if (!cooldownText.gameObject.activeSelf) cooldownText.gameObject.SetActive(true);
                if (cd >= 1f) cooldownText.SetText("{0:0}", Mathf.Ceil(cd));
                else cooldownText.SetText("{0:1}", cd);
            }
            else if (cooldownText.gameObject.activeSelf)
                cooldownText.gameObject.SetActive(false);

            float mana = slot.ManaCost;
            manaText.gameObject.SetActive(learned && mana > 0f);
            if (mana > 0f) manaText.SetText("{0:0}", mana);

            Color tint = Color.white;
            if (!learned) tint = DisabledTint;
            else if (error == AbilityCastError.NotEnoughMana) tint = NoManaTint;
            else if (error == AbilityCastError.Silenced || error == AbilityCastError.Stunned || error == AbilityCastError.Dead) tint = DisabledTint;
            icon.color = Color.Lerp(icon.color, tint, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));

            for (int i = 0; i < levelPips.Length; i++)
                levelPips[i].color = i < slot.Level ? theme.goldLight : new Color(0.2f, 0.22f, 0.32f, 1f);

            // Подсветка: прицеливание этой способностью, наведение мышью, вспышка при касте.
            PlayerCommander commander = PlayerCommander.Instance;
            bool targeting = commander != null && commander.TargetingSlot == index;
            flash = Mathf.Max(0f, flash - Time.unscaledDeltaTime * 2.5f);
            float glowAlpha = Mathf.Max(targeting ? 0.95f : hovered && error == AbilityCastError.None ? 0.45f : 0f, flash);
            Color g = theme.goldLight;
            g.a = Mathf.Lerp(glow.color.a, glowAlpha, 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
            glow.color = g;
            frame.color = targeting ? Color.white : new Color(0.9f, 0.9f, 0.9f, 1f);
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovered = true;
            if (abilities != null)
            {
                AbilitySlot slot = abilities[index];
                if (slot.Definition != null)
                    TooltipView.Show(slot.Definition, Mathf.Max(1, slot.Level), HeroAbilities.Hotkeys[index], (RectTransform)transform);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovered = false;
            TooltipView.Hide();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && PlayerCommander.Instance != null)
                PlayerCommander.Instance.BeginAbility(index);
        }
    }
}
