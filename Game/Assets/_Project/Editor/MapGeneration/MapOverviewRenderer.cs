using System.IO;
using UnityEditor;
using UnityEngine;

namespace LastStand.EditorTools.MapGeneration
{
    /// <summary>Рендер открытой карты сверху в PNG (проверка раскладки / заготовка для миникарты).</summary>
    public static class MapOverviewRenderer
    {
        public const string OutputPath = MapAssetFactory.GeneratedDir + "/Map_Overview.png";

        [MenuItem("Last Stand/Map/Render Overview Image", priority = 20)]
        public static void Render()
        {
            const int size = 1024;
            bool fog = RenderSettings.fog;
            var go = new GameObject("~OverviewCamera") { hideFlags = HideFlags.HideAndDontSave };
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            try
            {
                RenderSettings.fog = false;
                var cam = go.AddComponent<Camera>();
                cam.orthographic = true;
                cam.orthographicSize = ThreeFrontsLayout.PlayHalf + 4f;
                cam.nearClipPlane = 1f;
                cam.farClipPlane = 300f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.targetTexture = rt;
                go.transform.SetPositionAndRotation(new Vector3(0f, 150f, 0f), Quaternion.Euler(90f, 0f, 0f));
                cam.Render();

                var prev = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(size, size, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;

                MapAssetFactory.EnsureFolder(MapAssetFactory.GeneratedDir);
                File.WriteAllBytes(OutputPath, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);
                Debug.Log($"[Last Stand] Вид карты сверху сохранён: {OutputPath}");
            }
            finally
            {
                RenderSettings.fog = fog;
                Object.DestroyImmediate(go);
                rt.Release();
                Object.DestroyImmediate(rt);
            }
        }
    }
}
