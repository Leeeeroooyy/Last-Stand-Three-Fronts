using System.Collections.Generic;
using UnityEngine;

namespace LastStand.Core
{
    /// <summary>
    /// Пул визуальных эффектов и снарядов. Объекты не уничтожаются, а возвращаются в пул.
    /// Эффекты с компонентом <see cref="VfxLifetime"/> возвращаются сами по окончании.
    /// </summary>
    public static class VfxPool
    {
        private static readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();
        private static Transform root;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            pools.Clear();
            root = null;
        }

        private static Transform Root
        {
            get
            {
                if (root == null)
                {
                    var go = new GameObject("[VfxPool]");
                    root = go.transform;
                }
                return root;
            }
        }

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
                return null;

            GameObject go = null;
            if (pools.TryGetValue(prefab, out Stack<GameObject> stack))
            {
                while (stack.Count > 0 && go == null)
                    go = stack.Pop();
            }

            if (go == null)
            {
                go = Object.Instantiate(prefab, position, rotation, parent != null ? parent : Root);
                var tag = go.GetComponent<PooledObject>();
                if (tag == null) tag = go.AddComponent<PooledObject>();
                tag.Prefab = prefab;
            }
            else
            {
                go.transform.SetParent(parent != null ? parent : Root, false);
                go.transform.SetPositionAndRotation(position, rotation);
            }

            go.transform.localScale = prefab.transform.localScale;
            go.SetActive(true);

            // Перезапуск частиц и трейлов у переиспользованного объекта.
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>())
            {
                ps.Clear(true);
                if (ps.main.playOnAwake) ps.Play(true);
            }
            foreach (var tr in go.GetComponentsInChildren<TrailRenderer>())
                tr.Clear();

            return go;
        }

        /// <summary>Эффект с заданным масштабом (например, под радиус способности).</summary>
        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, float scale)
        {
            GameObject go = Spawn(prefab, position, rotation);
            if (go != null)
                go.transform.localScale = prefab.transform.localScale * scale;
            return go;
        }

        public static void Release(GameObject go)
        {
            if (go == null)
                return;
            var tag = go.GetComponent<PooledObject>();
            if (tag == null || tag.Prefab == null)
            {
                Object.Destroy(go);
                return;
            }

            go.SetActive(false);
            go.transform.SetParent(Root, false);
            if (!pools.TryGetValue(tag.Prefab, out Stack<GameObject> stack))
            {
                stack = new Stack<GameObject>();
                pools[tag.Prefab] = stack;
            }
            stack.Push(go);
        }
    }
}
