using System;
using System.Collections;
using System.Collections.Generic;
using LastStand.Core;
using LastStand.Map;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LastStand.Match
{
    public enum MatchPhase
    {
        PreGame = 0,
        Playing = 1,
        Ended = 2
    }

    /// <summary>
    /// Состояние матча: таймер, фазы, победа при разрушении Ancient, точки возрождения героев.
    /// </summary>
    [DefaultExecutionOrder(-600)]
    public sealed class MatchManager : MonoBehaviour
    {
        [SerializeField] private MatchConfig config;

        private readonly Dictionary<TeamSide, Transform> heroSpawns = new Dictionary<TeamSide, Transform>();

        public static MatchManager Instance { get; private set; }

        public MatchConfig Config => config;
        public MatchPhase Phase { get; private set; } = MatchPhase.PreGame;
        /// <summary>Время матча, с. Во время подготовки отрицательное.</summary>
        public float MatchTime { get; private set; }
        public TeamSide PlayerTeam => config != null ? config.playerTeam : TeamSide.Dawn;
        public TeamSide Winner { get; private set; } = TeamSide.Neutral;
        public Unit PlayerHero { get; private set; }

        public event Action<MatchPhase> PhaseChanged;
        public event Action<TeamSide> MatchEnded;
        public event Action<Unit> PlayerHeroChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[Match] На сцене два MatchManager — лишний отключён.", this);
                enabled = false;
                return;
            }
            Instance = this;
            Time.timeScale = 1f;
            MatchTime = config != null ? -config.prepTime : 0f;
            if (MatchTime >= 0f)
                Phase = MatchPhase.Playing;
        }

        private void OnEnable() => Unit.AnyDied += OnUnitDied;
        private void OnDisable() => Unit.AnyDied -= OnUnitDied;

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        private void Update()
        {
            if (Phase == MatchPhase.Ended)
                return;

            MatchTime += Time.deltaTime;
            if (Phase == MatchPhase.PreGame && MatchTime >= 0f)
                SetPhase(MatchPhase.Playing);
        }

        private void SetPhase(MatchPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }

        // ---------- Герои ----------

        public void RegisterHeroSpawn(TeamSide side, Transform spawn) => heroSpawns[side] = spawn;

        public Transform GetHeroSpawn(TeamSide side) =>
            heroSpawns.TryGetValue(side, out Transform t) ? t : null;

        public void SetPlayerHero(Unit hero)
        {
            PlayerHero = hero;
            PlayerHeroChanged?.Invoke(hero);
        }

        // ---------- Победа ----------

        private void OnUnitDied(Unit unit, Unit killer)
        {
            if (unit.Kind == UnitKind.Ancient && Phase != MatchPhase.Ended)
                EndMatch(CombatMath.Opponent(unit.Team));
        }

        public void EndMatch(TeamSide winner)
        {
            if (Phase == MatchPhase.Ended)
                return;
            Winner = winner;
            SetPhase(MatchPhase.Ended);
            StartCoroutine(EndSequence(winner));
        }

        private IEnumerator EndSequence(TeamSide winner)
        {
            // Короткое замедление на разрушении Ancient, затем мир замирает под экраном итогов.
            Time.timeScale = 0.25f;
            yield return new WaitForSecondsRealtime(1.6f);
            Time.timeScale = 0f;
            MatchEnded?.Invoke(winner);
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            Scene scene = SceneManager.GetActiveScene();
#if UNITY_EDITOR
            // В редакторе сцена может отсутствовать в Build Settings — грузим по пути.
            if (scene.buildIndex < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    scene.path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scene.buildIndex);
        }

        public static string FormatTime(float seconds)
        {
            bool negative = seconds < 0f;
            int total = Mathf.FloorToInt(Mathf.Abs(seconds));
            if (negative && Mathf.Abs(seconds) - total > 0f) total += 1;
            string s = $"{total / 60:00}:{total % 60:00}";
            return negative ? "-" + s : s;
        }
    }
}
