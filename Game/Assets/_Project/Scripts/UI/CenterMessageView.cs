using LastStand.Controls;
using LastStand.Match;
using TMPro;
using UnityEngine;

namespace LastStand.UI
{
    /// <summary>
    /// Сообщения по центру: обратный отсчёт до боя, «битва началась», ошибки приказов (нет маны и т.п.).
    /// </summary>
    public sealed class CenterMessageView : MonoBehaviour
    {
        [SerializeField] private TMP_Text headline;
        [SerializeField] private TMP_Text kicker;
        [SerializeField] private CanvasGroup headlineGroup;
        [SerializeField] private TMP_Text errorText;
        [SerializeField] private CanvasGroup errorGroup;

        private PlayerCommander commander;
        private float errorUntil;
        private float headlineUntil;
        private int lastCountdown = int.MinValue;
        private bool announcedStart;

        private void Start()
        {
            errorGroup.alpha = 0f;
            headlineGroup.alpha = 0f;
        }

        private void OnDisable()
        {
            if (commander != null) commander.Notify -= ShowError;
            commander = null;
        }

        public void ShowError(string message)
        {
            errorText.SetText(message);
            errorUntil = Time.unscaledTime + 1.6f;
            errorGroup.alpha = 1f;
        }

        private void ShowHeadline(string top, string message, float duration)
        {
            if (kicker != null) kicker.SetText(top);
            headline.SetText(message);
            headlineUntil = Time.unscaledTime + duration;
            headlineGroup.alpha = 1f;
            headline.rectTransform.localScale = Vector3.one * 1.15f;
        }

        private void Update()
        {
            if (commander == null && PlayerCommander.Instance != null)
            {
                commander = PlayerCommander.Instance;
                commander.Notify += ShowError;
            }

            MatchManager match = MatchManager.Instance;
            if (match != null)
            {
                if (match.Phase == MatchPhase.PreGame)
                {
                    int left = Mathf.CeilToInt(-match.MatchTime);
                    if (left != lastCountdown && left <= 5 && left > 0)
                    {
                        lastCountdown = left;
                        ShowHeadline("THE BATTLE BEGINS IN", left.ToString(), 1.1f);
                    }
                }
                else if (match.Phase == MatchPhase.Playing && !announcedStart)
                {
                    announcedStart = true;
                    ShowHeadline("THE CREEPS MARCH", "DEFEND THE THREE FRONTS", 2.4f);
                }
            }

            float dt = Time.unscaledDeltaTime;
            if (Time.unscaledTime > errorUntil)
                errorGroup.alpha = Mathf.MoveTowards(errorGroup.alpha, 0f, dt * 3f);
            if (Time.unscaledTime > headlineUntil)
                headlineGroup.alpha = Mathf.MoveTowards(headlineGroup.alpha, 0f, dt * 2.5f);
            headline.rectTransform.localScale = Vector3.Lerp(headline.rectTransform.localScale, Vector3.one, 1f - Mathf.Exp(-10f * dt));
        }
    }
}
