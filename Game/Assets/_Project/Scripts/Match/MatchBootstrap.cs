using System.Collections.Generic;
using LastStand.CameraControl;
using LastStand.Core;
using LastStand.Creeps;
using LastStand.Heroes;
using LastStand.Map;
using LastStand.Structures;
using LastStand.Units;
using UnityEngine;

namespace LastStand.Match
{
    /// <summary>
    /// Превращает размеченную карту в игровой матч: навешивает боевые компоненты на постройки
    /// (по MapStructure), настраивает защиту по ярусам, фонтаны, спавнер крипов и героя игрока.
    /// Карта может перегенерироваться — вся логика берётся из MatchConfig, а не из сцены.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(MatchManager))]
    public sealed class MatchBootstrap : MonoBehaviour
    {
        [SerializeField] private CreepWaveSpawner creepSpawner;
        [SerializeField] private MobaCameraController cameraController;

        private readonly Dictionary<(TeamSide, Lane, int), Unit> towers = new Dictionary<(TeamSide, Lane, int), Unit>();
        private readonly Dictionary<TeamSide, List<Unit>> tier4 = new Dictionary<TeamSide, List<Unit>>();
        private readonly Dictionary<(TeamSide, Lane), List<Unit>> barracks = new Dictionary<(TeamSide, Lane), List<Unit>>();
        private readonly Dictionary<TeamSide, Unit> ancients = new Dictionary<TeamSide, Unit>();

        private MatchManager match;
        private MatchConfig config;

        private void Start()
        {
            match = GetComponent<MatchManager>();
            config = match.Config;
            if (config == null)
            {
                Debug.LogError("[Match] Не назначен MatchConfig. Запустите Last Stand ▸ Gameplay ▸ Build All.", this);
                return;
            }

            SetupStructures();
            SetupProtection();

            if (creepSpawner == null)
                creepSpawner = GetComponent<CreepWaveSpawner>();
            if (creepSpawner != null)
                creepSpawner.Initialize(config);

            SpawnPlayerHero();
        }

        // ---------- Постройки ----------

        private void SetupStructures()
        {
            foreach (MapStructure s in FindObjectsByType<MapStructure>())
            {
                switch (s.Type)
                {
                    case StructureType.Tower:
                    {
                        Unit u = MakeStructure(s, UnitKind.Tower, config.TowerStats(s.Tier), $"Tower T{s.Tier}");
                        var attack = s.gameObject.AddComponent<UnitAttack>();
                        Transform crystal = s.transform.Find("Crystal");
                        attack.Configure(config.TowerProjectile(s.Team), crystal != null ? crystal : s.transform);
                        s.gameObject.AddComponent<TowerAI>();
                        if (s.Tier >= 4)
                        {
                            if (!tier4.TryGetValue(s.Team, out var list)) tier4[s.Team] = list = new List<Unit>();
                            list.Add(u);
                        }
                        else
                            towers[(s.Team, s.Lane, s.Tier)] = u;
                        break;
                    }
                    case StructureType.BarracksMelee:
                    case StructureType.BarracksRanged:
                    {
                        bool melee = s.Type == StructureType.BarracksMelee;
                        Unit u = MakeStructure(s, UnitKind.Barracks,
                            melee ? config.barracksMeleeStats : config.barracksRangedStats,
                            melee ? "Melee Barracks" : "Ranged Barracks");
                        if (!barracks.TryGetValue((s.Team, s.Lane), out var list))
                            barracks[(s.Team, s.Lane)] = list = new List<Unit>();
                        list.Add(u);
                        break;
                    }
                    case StructureType.Ancient:
                        ancients[s.Team] = MakeStructure(s, UnitKind.Ancient, config.ancientStats, "Ancient");
                        break;
                    case StructureType.Fountain:
                    {
                        var aura = s.gameObject.AddComponent<FountainAura>();
                        aura.Configure(s.Team, config.fountainRadius, config.fountainHealPercent,
                            config.fountainManaPercent, config.fountainDamagePerSecond);
                        break;
                    }
                    case StructureType.HeroSpawn:
                        match.RegisterHeroSpawn(s.Team, s.transform);
                        break;
                }
            }
        }

        private Unit MakeStructure(MapStructure s, UnitKind kind, UnitStats stats, string title)
        {
            GameObject go = s.gameObject;
            var unit = go.GetComponent<Unit>();
            if (unit == null) unit = go.AddComponent<Unit>();

            // Радиус и высота полоски — из коллайдера модели.
            float radius = 2f, height = 6f;
            var col = go.GetComponent<Collider>();
            if (col != null)
            {
                Bounds b = col.bounds;
                radius = Mathf.Max(b.extents.x, b.extents.z);
                height = b.max.y - go.transform.position.y;
            }

            string side = s.Team == TeamSide.Dawn ? "Dawn" : "Dusk";
            string lane = s.Lane != Lane.None ? $" ({s.Lane})" : string.Empty;
            unit.Setup($"{side} {title}{lane}", kind, s.Team, stats, radius, height + 1.4f);

            go.AddComponent<StructureGuard>();
            var destruction = go.AddComponent<StructureDestruction>();
            destruction.DestroyedVfx = config.structureDestroyedVfx;
            return unit;
        }

        /// <summary>Правила неуязвимости: T2←T1, T3←T2, казармы←T3, T4←любая T3, Ancient←обе T4.</summary>
        private void SetupProtection()
        {
            foreach (var pair in towers)
            {
                (TeamSide side, Lane lane, int tier) = pair.Key;
                if (tier > 1 && towers.TryGetValue((side, lane, tier - 1), out Unit outer))
                    pair.Value.GetComponent<StructureGuard>().SetProtectors(GuardRule.AllDestroyed, new[] { outer });
            }

            foreach (var pair in barracks)
            {
                (TeamSide side, Lane lane) = pair.Key;
                if (!towers.TryGetValue((side, lane, 3), out Unit t3))
                    continue;
                foreach (Unit b in pair.Value)
                    b.GetComponent<StructureGuard>().SetProtectors(GuardRule.AllDestroyed, new[] { t3 });
            }

            foreach (var pair in tier4)
            {
                var t3s = new List<Unit>();
                foreach (Lane lane in new[] { Lane.Top, Lane.Mid, Lane.Bot })
                    if (towers.TryGetValue((pair.Key, lane, 3), out Unit t3))
                        t3s.Add(t3);
                foreach (Unit t4 in pair.Value)
                    t4.GetComponent<StructureGuard>().SetProtectors(GuardRule.AnyDestroyed, t3s);
            }

            foreach (var pair in ancients)
                if (tier4.TryGetValue(pair.Key, out var t4s))
                    pair.Value.GetComponent<StructureGuard>().SetProtectors(GuardRule.AllDestroyed, t4s);
        }

        // ---------- Герой игрока ----------

        private void SpawnPlayerHero()
        {
            HeroDefinition def = config.playerHero;
            if (def == null || def.prefab == null)
            {
                Debug.LogWarning("[Match] В MatchConfig не задан герой игрока.", this);
                return;
            }

            Transform spawn = match.GetHeroSpawn(config.playerTeam);
            Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
            Quaternion rot = spawn != null ? spawn.rotation : Quaternion.identity;

            GameObject go = Instantiate(def.prefab, pos, rot);
            go.name = $"Hero {def.heroName} (Player)";
            var hero = go.GetComponent<Hero>();
            hero.Initialize(def, config.playerTeam);
            go.GetComponent<UnitMotor>().EnsureOnNavMesh();

            match.SetPlayerHero(hero.Unit);

            if (cameraController == null)
                cameraController = FindAnyObjectByType<MobaCameraController>();
            if (cameraController != null)
            {
                cameraController.FollowTarget = go.transform;
                cameraController.CenterOn(go.transform.position);
            }
        }
    }
}
