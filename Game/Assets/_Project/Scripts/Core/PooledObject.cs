using UnityEngine;

namespace LastStand.Core
{
    /// <summary>Метка объекта из пула (какой префаб его породил).</summary>
    [DisallowMultipleComponent]
    public sealed class PooledObject : MonoBehaviour
    {
        [System.NonSerialized] public GameObject Prefab;
    }
}
