using UnityEngine;
using UnityEngine.AI;

namespace LastStand.Map
{
    /// <summary>
    /// Подключает заранее запечённый NavMesh карты (печётся генератором карты в редакторе).
    /// Работает на встроенном модуле AI, без пакета AI Navigation.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class MapNavMesh : MonoBehaviour
    {
        [SerializeField] private NavMeshData navMeshData;

        private NavMeshDataInstance instance;

        public NavMeshData Data
        {
            get => navMeshData;
            set
            {
                navMeshData = value;
                if (Application.isPlaying && isActiveAndEnabled)
                {
                    Remove();
                    Add();
                }
            }
        }

        private void OnEnable() => Add();
        private void OnDisable() => Remove();

        private void Add()
        {
            if (navMeshData == null || instance.valid)
                return;
            instance = NavMesh.AddNavMeshData(navMeshData, transform.position, transform.rotation);
            instance.owner = this;
        }

        private void Remove()
        {
            if (instance.valid)
                instance.Remove();
            instance = default;
        }
    }
}
