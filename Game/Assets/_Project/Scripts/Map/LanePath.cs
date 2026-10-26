using System.Collections.Generic;
using UnityEngine;

namespace LastStand.Map
{
    /// <summary>
    /// Маршрут линии для крипов. Точки идут от базы Dawn к базе Dusk;
    /// крипы Dusk проходят тот же список в обратном порядке.
    /// </summary>
    public sealed class LanePath : MonoBehaviour
    {
        [SerializeField] private Lane lane;
        [SerializeField] private List<Transform> waypoints = new List<Transform>();

        public Lane Lane => lane;
        public int Count => waypoints.Count;

        public void Setup(Lane pathLane, List<Transform> points)
        {
            lane = pathLane;
            waypoints = points;
        }

        /// <summary>Точка маршрута с учётом стороны (для Dusk порядок обратный).</summary>
        public Vector3 GetPoint(int index, TeamSide side)
        {
            int i = side == TeamSide.Dusk ? waypoints.Count - 1 - index : index;
            return waypoints[i].position;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (waypoints == null || waypoints.Count < 2)
                return;

            Gizmos.color = lane switch
            {
                Lane.Top => new Color(1f, 0.55f, 0.2f),
                Lane.Mid => new Color(1f, 0.9f, 0.3f),
                _ => new Color(0.4f, 0.9f, 1f)
            };

            for (int i = 0; i < waypoints.Count - 1; i++)
            {
                if (waypoints[i] == null || waypoints[i + 1] == null)
                    continue;
                Vector3 a = waypoints[i].position + Vector3.up * 0.4f;
                Vector3 b = waypoints[i + 1].position + Vector3.up * 0.4f;
                Gizmos.DrawLine(a, b);
                Gizmos.DrawSphere(a, 0.35f);
            }
        }
#endif
    }
}
