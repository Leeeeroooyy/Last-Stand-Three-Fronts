using LastStand.Map;
using LastStand.Match;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LastStand.UI
{
    /// <summary>Экран итогов: VICTORY / DEFEAT в стиле ар-деко, кнопка новой игры.</summary>
    public sealed class EndScreenView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private RectTransform panel;
        [SerializeField] private TMP_Text title;
        [SerializeField] private TMP_Text subtitle;
        [SerializeField] private TMP_Text details;
        [SerializeField] private Image crest;
        [SerializeField] private Button playAgain;
        [SerializeField] private UiTheme theme;

        private MatchManager match;
        private float shownAt = -1f;

        private void Awake()
        {
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            panel.gameObject.SetActive(false);
            if (playAgain != null)
                playAgain.onClick.AddListener(OnPlayAgain);
        }

        private void Update()
        {
            if (match == null && MatchManager.Instance != null)
            {
                match = MatchManager.Instance;
                match.MatchEnded += Show;
            }

            if (shownAt < 0f)
                return;

            float t = Mathf.Clamp01((Time.unscaledTime - shownAt) / 0.6f);
            float e = 1f - (1f - t) * (1f - t) * (1f - t);
            group.alpha = e;
            panel.localScale = Vector3.one * Mathf.Lerp(0.92f, 1f, e);
            if (crest != null)
                crest.rectTransform.localRotation = Quaternion.identity;
        }

        private void OnDestroy()
        {
            if (match != null) match.MatchEnded -= Show;
        }

        private void Show(TeamSide winner)
        {
            bool won = match != null && winner == match.PlayerTeam;
            title.SetText(won ? "VICTORY" : "DEFEAT");
            title.color = won ? theme.goldLight : new Color(0.93f, 0.45f, 0.42f);
            if (crest != null) crest.color = won ? theme.gold : new Color(0.75f, 0.35f, 0.33f);
            string side = winner == TeamSide.Dawn ? "DAWN" : "DUSK";
            subtitle.SetText(won ? $"THE {side} STANDS UNBROKEN" : $"THE ANCIENT HAS FALLEN — {side} PREVAILS");
            details.SetText($"Match time  {MatchManager.FormatTime(match != null ? match.MatchTime : 0f)}");

            panel.gameObject.SetActive(true);
            group.blocksRaycasts = true;
            group.interactable = true;
            shownAt = Time.unscaledTime;
        }

        private void OnPlayAgain()
        {
            if (match != null) match.Restart();
        }
    }
}
