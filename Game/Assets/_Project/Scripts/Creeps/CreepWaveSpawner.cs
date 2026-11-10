using System.Collections.Generic;
using LastStand.Core;
using LastStand.Map;
using LastStand.Match;
using LastStand.Units;
using UnityEngine;

namespace LastStand.Creeps
{
    /// <summary>
    /// Выпускает волны крипов на все три линии за обе стороны каждые waveInterval секунд.
    /// Если у врага на линии разрушены казармы — крипы этого типа становятся усиленными.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CreepWaveSpawner : MonoBehaviour
    {
        private struct SpawnPoint
        {
            public TeamSide Side;
            public Lane Lane;
            public Transform Point;
        }

        private readonly List<SpawnPoint> spawnPoints = new List<SpawnPoint>();
        private readonly Dictionary<Lane, LanePath> lanes = new Dictionary<Lane, LanePath>();
        private readonly List<(TeamSide side, Lane lane, CreepType type, Unit barracks)> barracks =
            new List<(TeamSide, Lane, CreepType, Unit)>();

        private MatchConfig config;
        private float nextWaveTime;
        private Transform creepRoot;

        public int WaveNumber { get; private set; }
        public float NextWaveTime => nextWaveTime;

        /// <summary>Вызывается бутстрапом матча после разметки построек.</summary>
        public void Initialize(MatchConfig matchConfig)
        {
            config = matchConfig;
            nextWaveTime = config.firstWaveTime;
            creepRoot = new GameObject("--- Creeps ---").transform;

            foreach (LanePath path in FindObjectsByType<LanePath>())
                lanes[path.Lane] = path;

            foreach (MapStructure s in FindObjectsByType<MapStructure>())
            {
                if (s.Type == StructureType.CreepSpawn)
                    spawnPoints.Add(new SpawnPoint { Side = s.Team, Lane = s.Lane, Point = s.transform });
                else if (s.Type == StructureType.BarracksMelee || s.Type == StructureType.BarracksRanged)
                {
                    var u = s.GetComponent<Unit>();
                    if (u != null)
                        barracks.Add((s.Team, s.Lane, s.Type == StructureType.BarracksMelee ? CreepType.Melee : CreepType.Ranged, u));
                }
            }

            if (spawnPoints.Count == 0 || lanes.Count == 0)
                Debug.LogWarning("[Creeps] На карте нет точек спавна крипов или маршрутов линий.", this);
        }

        private void Update()
        {
            MatchManager match = MatchManager.Instance;
            if (config == null || match == null || match.Phase != MatchPhase.Playing)
                return;

            if (match.MatchTime >= nextWaveTime)
            {
                nextWaveTime += config.waveInterval;
                SpawnWave();
            }
        }

        private void SpawnWave()
        {
            WaveNumber++;
            foreach (SpawnPoint sp in spawnPoints)
            {
                if (!lanes.TryGetValue(sp.Lane, out LanePath path))
                    continue;

                int index = 0;
                for (int i = 0; i < config.meleePerWave; i++)
                    Spawn(config.meleeCreep, sp, path, index++);
                for (int i = 0; i < config.rangedPerWave; i++)
                    Spawn(config.rangedCreep, sp, path, index++);
            }
        }

        private void Spawn(CreepDefinition def, SpawnPoint sp, LanePath path, int index)
        {
            if (def == null)
                return;
            GameObject prefab = def.PrefabFor(sp.Side);
            if (prefab == null)
                return;

            // Строй: ряды по 3, ближний бой впереди.
            int row = index / 3, col = index % 3;
            Vector3 forward = sp.Point.forward;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            Vector3 pos = sp.Point.position + right * ((col - 1) * 1.3f) - forward * (row * 1.5f);

            GameObject go = Instantiate(prefab, pos, sp.Point.rotation, creepRoot);
            var unit = go.GetComponent<Unit>();
            bool mega = IsMega(sp.Side, sp.Lane, def.type);
            UnitStats stats = mega ? def.stats.Scaled(config.megaHealthMultiplier, config.megaDamageMultiplier) : def.stats;
            string label = mega ? "Mega " + def.creepName : def.creepName;
            unit.Setup(label, UnitKind.Creep, sp.Side, stats, def.bodyRadius, def.barHeight);
            if (mega)
                go.transform.localScale *= 1.15f;
            go.name = $"{label} ({sp.Side} {sp.Lane})";

            var motor = go.GetComponent<UnitMotor>();
            motor.EnsureOnNavMesh();
            var ai = go.GetComponent<CreepAI>();
            ai.AssignLane(path);

            var death = go.GetComponent<UnitDeathHandler>();
            if (death != null && config.unitDeathVfx != null && death.DeathVfx == null)
                death.DeathVfx = config.unitDeathVfx;
        }

        /// <summary>Крипы стороны усилены, если у противника на этой линии разрушены казармы того же типа.</summary>
        private bool IsMega(TeamSide side, Lane lane, CreepType type)
        {
            foreach (var b in barracks)
                if (b.side != side && b.lane == lane && b.type == type && b.barracks != null && !b.barracks.IsAlive)
                    return true;
            return false;
        }
    }
}
