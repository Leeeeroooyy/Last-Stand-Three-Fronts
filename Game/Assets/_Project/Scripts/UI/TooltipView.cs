using LastStand.Abilities;
using LastStand.Core;
using TMPro;
using UnityEngine;

namespace LastStand.UI
{
    /// <summary>Подсказка способности над панелью героя.</summary>
    public sealed class TooltipView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text meta;
        [SerializeField] private TMP_Text body;

        private static TooltipView instance;
        private float target;

        private void Awake()
        {
            instance = this;
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Show(AbilityDefinition def, int level, string hotkey, RectTransform anchor)
        {
            if (instance == null || def == null)
                return;
            TooltipView t = instance;
            t.title.SetText($"{def.displayName.ToUpperInvariant()}  <size=70%><color=#B9A27A>[{hotkey}]</color></size>");

            string kind = def.isUltimate ? "Ultimate" : def.targeting switch
            {
                AbilityTargeting.NoTarget => "No target",
                AbilityTargeting.UnitTarget => "Unit target",
                AbilityTargeting.Point => "Point target",
                AbilityTargeting.Direction => "Direction",
                _ => "Passive"
            };
            string element = def.element != Element.None ? $"  ·  {def.element}" : string.Empty;
            t.meta.SetText($"{kind}{element}  ·  Mana {def.ManaCost(level):0}  ·  Cooldown {def.Cooldown(level):0.#}s");
            t.body.SetText(def.GetTooltip(level));

            Vector3 p = t.panel.position;
            p.x = anchor.position.x;
            t.panel.position = p;
            t.target = 1f;
        }

        public static void Hide()
        {
            if (instance != null)
                instance.target = 0f;
        }

        private void Update()
        {
            group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 8f);
        }
    }
}
