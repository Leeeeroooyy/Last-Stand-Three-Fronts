using UnityEngine;

namespace LastStand.Controls
{
    /// <summary>
    /// Плоская метка на земле (круг дальности, область, линия, маркер клика).
    /// Квад с прозрачным материалом, цвет через MaterialPropertyBlock без копий материала.
    /// </summary>
    public sealed class GroundDecal
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static Mesh quad;

        private readonly GameObject go;
        private readonly MeshRenderer renderer;
        private readonly MaterialPropertyBlock block = new MaterialPropertyBlock();

        public Transform Transform => go.transform;
        public bool Visible => go.activeSelf;

        public GroundDecal(string name, Material material, Transform parent, int sortingOrder = 0)
        {
            go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = Quad;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = sortingOrder;
            go.SetActive(false);
        }

        /// <summary>Плоский квад 1×1 в плоскости XZ (смотрит вверх).</summary>
        public static Mesh Quad
        {
            get
            {
                if (quad != null)
                    return quad;
                quad = new Mesh { name = "GroundQuad" };
                quad.SetVertices(new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f),
                    new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f)
                });
                quad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
                quad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
                quad.RecalculateNormals();
                quad.RecalculateBounds();
                return quad;
            }
        }

        public static float GroundHeight(Vector3 p)
        {
            Terrain t = Terrain.activeTerrain;
            return t != null ? t.SampleHeight(p) + t.transform.position.y : p.y;
        }

        public void Show(Vector3 center, float sizeX, float sizeZ, Quaternion yaw, Color color, float lift = 0.25f)
        {
            center.y = GroundHeight(center) + lift;
            go.transform.SetPositionAndRotation(center, yaw);
            go.transform.localScale = new Vector3(sizeX, 1f, sizeZ);
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
            if (!go.activeSelf) go.SetActive(true);
        }

        public void SetColor(Color color)
        {
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }

        public void Hide()
        {
            if (go.activeSelf) go.SetActive(false);
        }
    }
}
