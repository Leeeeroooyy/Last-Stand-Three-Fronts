using LastStand.Core;
using LastStand.Creeps;
using LastStand.Heroes;
using LastStand.Map;
using UnityEngine;

namespace LastStand.Match
{
    /// <summary>
    /// Все числа матча в одном ассете: время, волны крипов, характеристики построек, фонтан.
    /// Удобно для балансировки — дизайнер правит один файл.
    /// </summary>
    [CreateAssetMenu(menuName = "Last Stand/Match Config", fileName = "MatchConfig")]
    public sealed class MatchConfig : ScriptableObject
    {
        [Header("Время")]
        [Tooltip("Подготовка до начала матча, с (таймер идёт с минусом).")]
        public float prepTime = 5f;
        [Tooltip("Время первой волны крипов (время матча, с).")]
        public float firstWaveTime;
        public float waveInterval = 30f;

        [Header("Игрок")]
        public TeamSide playerTeam = TeamSide.Dawn;
        public HeroDefinition playerHero;
        [Tooltip("Время возрождения героя, с (этап 2: будет расти по ходу матча).")]
        public float heroRespawnTime = 6f;

        [Header("Крипы")]
        public CreepDefinition meleeCreep;
        public CreepDefinition rangedCreep;
        [Min(0)] public int meleePerWave = 3;
        [Min(0)] public int rangedPerWave = 1;
        [Tooltip("Усиление крипов, если у врага на линии разрушены соответствующие казармы.")]
        public float megaHealthMultiplier = 1.6f;
        public float megaDamageMultiplier = 1.4f;

        [Header("Башни (T1–T4)")]
        public UnitStats[] towerStats = new UnitStats[4];
        public GameObject towerProjectileDawn;
        public GameObject towerProjectileDusk;

        [Header("Казармы и Ancient")]
        public UnitStats barracksMeleeStats = new UnitStats();
        public UnitStats barracksRangedStats = new UnitStats();
        public UnitStats ancientStats = new UnitStats();

        [Header("Фонтан")]
        public float fountainRadius = 8f;
        [Tooltip("Лечение союзников, % от максимума в секунду.")]
        public float fountainHealPercent = 6f;
        public float fountainManaPercent = 6f;
        [Tooltip("Урон врагам в радиусе фонтана, ед/с.")]
        public float fountainDamagePerSecond = 300f;

        [Header("Эффекты")]
        public GameObject structureDestroyedVfx;
        public GameObject unitDeathVfx;

        public UnitStats TowerStats(int tier) =>
            towerStats[Mathf.Clamp(tier - 1, 0, towerStats.Length - 1)];

        public GameObject TowerProjectile(TeamSide side) =>
            side == TeamSide.Dusk ? towerProjectileDusk : towerProjectileDawn;
    }
}
