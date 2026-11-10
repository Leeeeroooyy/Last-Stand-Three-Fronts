using TMPro;
using UnityEngine;

namespace LastStand.UI
{
    /// <summary>Палитра и шрифты интерфейса Art Deco Fantasy. Цвета можно менять в одном месте.</summary>
    [CreateAssetMenu(menuName = "Last Stand/UI Theme", fileName = "UiTheme")]
    public sealed class UiTheme : ScriptableObject
    {
        [Header("Шрифты")]
        public TMP_FontAsset titleFont;
        public TMP_FontAsset bodyFont;

        [Header("Золото")]
        public Color gold = new Color(0.914f, 0.773f, 0.51f);
        public Color goldLight = new Color(0.988f, 0.898f, 0.69f);
        public Color goldDim = new Color(0.66f, 0.5f, 0.27f);
        public Color textMuted = new Color(0.66f, 0.71f, 0.78f);
        [Tooltip("Основной светлый цвет текста (сливочный).")]
        public Color cream = new Color(0.933f, 0.878f, 0.761f);

        [Header("Полоски")]
        public Color selfHealth = new Color(0.45f, 0.9f, 0.5f);
        public Color allyHealth = new Color(0.33f, 0.76f, 0.44f);
        public Color enemyHealth = new Color(0.88f, 0.29f, 0.3f);
        public Color protectedHealth = new Color(0.55f, 0.55f, 0.6f);
        public Color mana = new Color(0.33f, 0.56f, 0.98f);
        public Color damageTrail = new Color(1f, 0.93f, 0.75f, 0.9f);

        [Header("Стороны")]
        public Color dawn = new Color(1f, 0.76f, 0.38f);
        public Color dusk = new Color(0.74f, 0.45f, 1f);
    }
}
