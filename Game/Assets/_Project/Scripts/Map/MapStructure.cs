using UnityEngine;

namespace LastStand.Map
{
    /// <summary>
    /// Метка объекта карты: башня, казарма, Ancient, святыня, сундук, точка спавна и т.д.
    /// Игровая логика (HP, атака, открытие сундука) будет навешиваться отдельными компонентами.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MapStructure : MonoBehaviour
    {
        [SerializeField] private StructureType type;
        [SerializeField] private TeamSide team;
        [SerializeField] private Lane lane;
        [Tooltip("Уровень башни: 1 — внешняя, 4 — у Ancient. Для остальных объектов 0.")]
        [SerializeField, Range(0, 4)] private int tier;

        public StructureType Type => type;
        public TeamSide Team => team;
        public Lane Lane => lane;
        public int Tier => tier;

        public void Setup(StructureType structureType, TeamSide side, Lane structureLane = Lane.None, int structureTier = 0)
        {
            type = structureType;
            team = side;
            lane = structureLane;
            tier = structureTier;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (type != StructureType.HeroSpawn && type != StructureType.CreepSpawn)
                return;

            Gizmos.color = team == TeamSide.Dawn ? new Color(1f, 0.8f, 0.3f) :
                           team == TeamSide.Dusk ? new Color(0.8f, 0.3f, 1f) : Color.cyan;
            Gizmos.DrawWireSphere(transform.position + Vector3.up * 0.5f, type == StructureType.HeroSpawn ? 1.5f : 0.8f);
            Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * 2f);
        }
#endif
    }
}
