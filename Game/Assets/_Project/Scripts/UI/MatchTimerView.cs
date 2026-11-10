using LastStand.Match;
using TMPro;
using UnityEngine;

namespace LastStand.UI
{
    /// <summary>Верхний баннер: время матча и подпись фазы.</summary>
    public sealed class MatchTimerView : MonoBehaviour
    {
        [SerializeField] private TMP_Text timeText;
        [SerializeField] private TMP_Text phaseText;
        [SerializeField] private UiTheme theme;

        private int lastSecond = int.MinValue;
        private MatchPhase lastPhase = (MatchPhase)(-1);

        private void Update()
        {
            MatchManager match = MatchManager.Instance;
            if (match == null)
                return;

            int second = Mathf.FloorToInt(match.MatchTime);
            if (second != lastSecond)
            {
                lastSecond = second;
                timeText.SetText(MatchManager.FormatTime(match.MatchTime));
            }

            if (match.Phase != lastPhase)
            {
                lastPhase = match.Phase;
                phaseText.SetText(match.Phase switch
                {
                    MatchPhase.PreGame => "PREPARATION",
                    MatchPhase.Playing => "THREE FRONTS",
                    _ => "MATCH OVER"
                });
            }
        }
    }
}
