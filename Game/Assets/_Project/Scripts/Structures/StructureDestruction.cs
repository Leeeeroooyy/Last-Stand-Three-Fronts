using System.Collections;
using LastStand.Core;
using UnityEngine;

namespace LastStand.Structures
{
    /// <summary>Разрушение постройки: облако пыли, тряска, оседание в землю и потемнение.</summary>
    [RequireComponent(typeof(Unit))]
    [DisallowMultipleComponent]
    public sealed class StructureDestruction : MonoBehaviour
    {
        [SerializeField] private GameObject destroyedVfx;
        [SerializeField] private float sinkDepth = 0.65f;
        [Tooltip("Доля высоты, на которую оседают руины.")]
        [SerializeField] private float flatten = 0.45f;

        private Unit unit;

        public GameObject DestroyedVfx
        {
            get => destroyedVfx;
            set => destroyedVfx = value;
        }

        private void Awake() => unit = GetComponent<Unit>();
        private void OnEnable() => unit.Died += OnDied;
        private void OnDisable() => unit.Died -= OnDied;

        private void OnDied(Unit u, Unit killer)
        {
            if (destroyedVfx != null)
                VfxPool.Spawn(destroyedVfx, transform.position, Quaternion.identity, Mathf.Max(1f, unit.Radius / 2f));

            // Летающие кристаллы и свет гаснут.
            foreach (var light in GetComponentsInChildren<Light>())
                light.enabled = false;
            foreach (var mono in GetComponentsInChildren<Map.CrystalFloat>())
            {
                mono.enabled = false;
                mono.gameObject.SetActive(false);
            }

            StartCoroutine(Collapse());
        }

        private IEnumerator Collapse()
        {
            Transform t = transform;
            Vector3 startPos = t.position;
            Vector3 startScale = t.localScale;
            Vector3 endScale = new Vector3(startScale.x * 1.05f, startScale.y * (1f - flatten), startScale.z * 1.05f);

            float time = 0f;
            const float duration = 1.4f;
            while (time < duration)
            {
                time += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, time / duration);
                Vector3 shake = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * (0.12f * (1f - k));
                t.position = startPos + Vector3.down * (sinkDepth * k) + shake;
                t.localScale = Vector3.Lerp(startScale, endScale, k);
                yield return null;
            }

            t.position = startPos + Vector3.down * sinkDepth;
            Darken();
        }

        private void Darken()
        {
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", new Color(0.32f, 0.3f, 0.3f));
            foreach (var r in GetComponentsInChildren<MeshRenderer>())
                r.SetPropertyBlock(block);
        }
    }
}
