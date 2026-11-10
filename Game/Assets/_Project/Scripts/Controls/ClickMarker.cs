using UnityEngine;

namespace LastStand.Controls
{
    /// <summary>Анимированная метка на месте приказа: золотой ромб — движение, красный — атака.</summary>
    public sealed class ClickMarker : MonoBehaviour
    {
        [SerializeField] private Material material;
        [SerializeField] private Color moveColor = new Color(0.55f, 1f, 0.7f, 1f);
        [SerializeField] private Color attackColor = new Color(1f, 0.38f, 0.32f, 1f);
        [SerializeField] private float duration = 0.45f;
        [SerializeField] private float size = 1.6f;
        [SerializeField] private int poolSize = 4;

        private GroundDecal[] decals;
        private float[] startTimes;
        private Color[] colors;
        private int next;

        public void Configure(Material mat) => material = mat;

        private void Awake()
        {
            decals = new GroundDecal[poolSize];
            startTimes = new float[poolSize];
            colors = new Color[poolSize];
            for (int i = 0; i < poolSize; i++)
            {
                decals[i] = new GroundDecal("ClickMarker", material, transform, 5);
                startTimes[i] = -10f;
            }
        }

        public void Show(Vector3 point, bool attack)
        {
            int i = next;
            next = (next + 1) % poolSize;
            startTimes[i] = Time.unscaledTime;
            colors[i] = attack ? attackColor : moveColor;
            decals[i].Show(point, size, size, Quaternion.Euler(0f, 45f, 0f), colors[i], 0.2f);
        }

        private void Update()
        {
            for (int i = 0; i < decals.Length; i++)
            {
                if (!decals[i].Visible)
                    continue;
                float t = (Time.unscaledTime - startTimes[i]) / duration;
                if (t >= 1f)
                {
                    decals[i].Hide();
                    continue;
                }
                float s = size * Mathf.Lerp(1.25f, 0.55f, 1f - (1f - t) * (1f - t));
                Transform tr = decals[i].Transform;
                tr.localScale = new Vector3(s, 1f, s);
                tr.rotation = Quaternion.Euler(0f, 45f + t * 90f, 0f);
                Color c = colors[i];
                c.a *= 1f - t * t;
                decals[i].SetColor(c);
            }
        }
    }
}
