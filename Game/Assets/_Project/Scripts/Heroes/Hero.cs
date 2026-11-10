using System;
using System.Collections;
using LastStand.Abilities;
using LastStand.Core;
using LastStand.Match;
using LastStand.Units;
using UnityEngine;

namespace LastStand.Heroes
{
    /// <summary>
    /// Герой: связывает определение (HeroDefinition) с компонентами и отвечает за смерть/возрождение.
    /// </summary>
    [RequireComponent(typeof(Unit), typeof(HeroController), typeof(HeroAbilities))]
    [DisallowMultipleComponent]
    public sealed class Hero : MonoBehaviour
    {
        [SerializeField] private HeroDefinition definition;

        private Unit unit;
        private UnitMotor motor;
        private Renderer[] renderers;
        private Coroutine respawnRoutine;

        public HeroDefinition Definition => definition;
        public Unit Unit => unit;
        public int Level { get; private set; } = 1;
        public bool IsRespawning => respawnRoutine != null;
        public float RespawnRemaining { get; private set; }

        public event Action<Hero> Respawned;

        private void Awake()
        {
            unit = GetComponent<Unit>();
            motor = GetComponent<UnitMotor>();
            renderers = GetComponentsInChildren<Renderer>(true);
        }

        private void OnEnable() => unit.Died += OnDied;
        private void OnDisable() => unit.Died -= OnDied;

        /// <summary>Применяет определение героя: характеристики, способности, команда.</summary>
        public void Initialize(HeroDefinition def, Map.TeamSide team)
        {
            definition = def;
            unit.Setup(def.heroName, UnitKind.Hero, team, def.stats, def.bodyRadius, def.barHeight);
            GetComponent<HeroAbilities>().Setup(def.abilities);
        }

        private void OnDied(Unit u, Unit killer)
        {
            if (respawnRoutine != null)
                StopCoroutine(respawnRoutine);
            respawnRoutine = StartCoroutine(RespawnRoutine());
        }

        private IEnumerator RespawnRoutine()
        {
            MatchManager match = MatchManager.Instance;
            float time = match != null && match.Config != null ? match.Config.heroRespawnTime : 6f;
            RespawnRemaining = time;

            bool hidden = false;
            while (RespawnRemaining > 0f)
            {
                RespawnRemaining -= Time.deltaTime;
                if (!hidden && time - RespawnRemaining > 2.4f)
                {
                    SetVisible(false);
                    hidden = true;
                }
                yield return null;
            }

            RespawnRemaining = 0f;
            Transform spawn = match != null ? match.GetHeroSpawn(unit.Team) : null;
            Vector3 pos = spawn != null ? spawn.position : transform.position;
            Quaternion rot = spawn != null ? spawn.rotation : transform.rotation;
            motor.Teleport(pos, rot);
            unit.Revive();
            SetVisible(true);
            respawnRoutine = null;
            Respawned?.Invoke(this);
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].enabled = visible;
        }
    }
}
