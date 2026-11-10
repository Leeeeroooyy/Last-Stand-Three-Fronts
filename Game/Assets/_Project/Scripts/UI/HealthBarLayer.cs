using System.Collections.Generic;
using LastStand.Core;
using LastStand.Heroes;
using LastStand.Map;
using LastStand.Match;
using UnityEngine;

namespace LastStand.UI
{
    /// <summary>
    /// Слой полосок над юнитами на экранном Canvas. Полоски берутся из пула,
    /// позиция пересчитывается каждый кадр (без отдельного Canvas на каждого юнита).
    /// </summary>
    public sealed class HealthBarLayer : MonoBehaviour
    {
        [SerializeField] private WorldBarView barPrefab;
        [SerializeField] private UiTheme theme;
        [SerializeField] private Camera viewCamera;
        [Tooltip("Смещение над точкой опоры (в пикселях экрана).")]
        [SerializeField] private float screenOffset = 6f;

        private readonly Dictionary<Unit, WorldBarView> active = new Dictionary<Unit, WorldBarView>();
        private readonly Stack<WorldBarView> pool = new Stack<WorldBarView>();
        private readonly List<Unit> removeBuffer = new List<Unit>();
        private RectTransform rect;
        private Canvas canvas;

        private void Awake()
        {
            rect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            if (viewCamera == null) viewCamera = Camera.main;
        }

        private void LateUpdate()
        {
            if (viewCamera == null || barPrefab == null)
                return;

            MatchManager match = MatchManager.Instance;
            Unit player = match != null ? match.PlayerHero : null;
            TeamSide playerTeam = match != null ? match.PlayerTeam : TeamSide.Dawn;
            float dt = Time.unscaledDeltaTime;
            float scale = canvas != null ? canvas.scaleFactor : 1f;

            // Убираем полоски исчезнувших или погибших юнитов.
            removeBuffer.Clear();
            foreach (var pair in active)
                if (pair.Key == null || !pair.Key.IsAlive || !pair.Key.isActiveAndEnabled)
                    removeBuffer.Add(pair.Key);
            foreach (Unit u in removeBuffer)
                Release(u);

            var units = UnitRegistry.All;
            for (int i = 0; i < units.Count; i++)
            {
                Unit u = units[i];
                if (!u.IsAlive)
                    continue;

                Vector3 world = u.Position + Vector3.up * u.BarHeight;
                Vector3 screen = viewCamera.WorldToScreenPoint(world);
                bool visible = screen.z > 0f && screen.x > -150f && screen.x < Screen.width + 150f &&
                               screen.y > -60f && screen.y < Screen.height + 60f;

                active.TryGetValue(u, out WorldBarView bar);
                if (!visible)
                {
                    if (bar != null) Release(u);
                    continue;
                }

                bool isPlayer = u == player;
                bool enemy = CombatMath.IsEnemy(playerTeam, u.Team);
                if (bar == null)
                {
                    bar = Acquire();
                    active[u] = bar;
                    bar.Bind(u, theme, isPlayer, enemy);
                    var hero = u.GetComponent<Hero>();
                    bar.SetLevel(hero != null ? hero.Level : 1);
                }

                bar.Refresh(theme, isPlayer, enemy, dt);
                bar.Root.position = new Vector3(Mathf.Round(screen.x), Mathf.Round(screen.y + screenOffset * scale), 0f);
            }
        }

        private WorldBarView Acquire()
        {
            WorldBarView bar = pool.Count > 0 ? pool.Pop() : Instantiate(barPrefab, rect);
            bar.gameObject.SetActive(true);
            return bar;
        }

        private void Release(Unit u)
        {
            if (!active.TryGetValue(u, out WorldBarView bar))
            {
                active.Remove(u);
                return;
            }
            active.Remove(u);
            bar.gameObject.SetActive(false);
            pool.Push(bar);
        }
    }
}
