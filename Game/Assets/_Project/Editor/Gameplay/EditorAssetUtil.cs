using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastStand.EditorTools.Gameplay
{
    /// <summary>Общие утилиты редакторской сборки ассетов геймплея.</summary>
    public static class EditorAssetUtil
    {
        public const string Root = "Assets/_Project";
        public const string DataDir = Root + "/Data/Gameplay";
        public const string AbilitiesDir = DataDir + "/Abilities";
        public const string MaterialsDir = Root + "/Art/Generated/Gameplay/Materials";
        public const string MeshesDir = Root + "/Art/Generated/Gameplay/Meshes";
        public const string FontsDir = Root + "/Art/Fonts";
        public const string UnitsPrefabDir = Root + "/Prefabs/Units";
        public const string VfxPrefabDir = Root + "/Prefabs/VFX";
        public const string UiPrefabDir = Root + "/Prefabs/UI";

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        /// <summary>Загружает ScriptableObject или создаёт новый (GUID сохраняется между сборками).</summary>
        public static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null)
                return asset;
            EnsureFolder(Path.GetDirectoryName(path)?.Replace('\\', '/'));
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        public static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null)
                Debug.LogWarning($"[Last Stand] Не найден ассет: {path}");
            return a;
        }

        public static Sprite UiSprite(string relativeToUi) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtImportSettings.UiDir}/{relativeToUi}");

        public static Texture2D FxTexture(string name) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtImportSettings.FxDir}/{name}");

        /// <summary>Создаёт или обновляет материал на месте (ссылки из префабов не теряются).</summary>
        public static Material SaveMaterial(Material m, string name)
        {
            EnsureFolder(MaterialsDir);
            string path = $"{MaterialsDir}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.shader = m.shader;
                existing.CopyPropertiesFromMaterial(m);
                existing.shaderKeywords = m.shaderKeywords;
                existing.renderQueue = m.renderQueue;
                existing.enableInstancing = m.enableInstancing;
                existing.SetOverrideTag("RenderType", m.GetTag("RenderType", false));
                Object.DestroyImmediate(m);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            m.name = name;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        public static Mesh SaveMesh(Mesh mesh)
        {
            EnsureFolder(MeshesDir);
            string path = $"{MeshesDir}/{mesh.name}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(mesh, existing);
                existing.name = mesh.name;
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ---------- Материалы ----------

        private static Shader Find(string name)
        {
            var s = Shader.Find(name);
            if (s == null)
                throw new System.InvalidOperationException($"Шейдер не найден: {name}");
            return s;
        }

        public static Material Lit(string name, Color color, float smoothness = 0.25f, float metallic = 0f,
            Color? emission = null, float emissionIntensity = 0f)
        {
            var m = new Material(Find("Universal Render Pipeline/Lit")) { enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value * emissionIntensity);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            return SaveMaterial(m, name);
        }

        /// <summary>Прозрачный неосвещённый материал с текстурой (индикаторы на земле).</summary>
        public static Material UnlitTransparent(string name, Texture2D tex, Color color, bool additive = false)
        {
            var m = new Material(Find("Universal Render Pipeline/Unlit"));
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            SetupTransparent(m, additive);
            m.SetFloat("_Cull", (float)CullMode.Off);
            return SaveMaterial(m, name);
        }

        /// <summary>
        /// Материал индикатора на земле (шейдер LastStand/GroundIndicator).
        /// onTop — рисовать поверх рельефа и построек (прицеливание), иначе обычная метка с глубиной.
        /// </summary>
        public static Material Indicator(string name, Texture2D tex, Color color, bool onTop, bool additive = false)
        {
            var m = new Material(Find("LastStand/GroundIndicator"));
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_ZTest", onTop ? (float)CompareFunction.Always : (float)CompareFunction.LessEqual);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.renderQueue = (int)RenderQueue.Transparent + (onTop ? 60 : 10);
            return SaveMaterial(m, name);
        }

        /// <summary>Материал частиц URP (аддитивный по умолчанию — свечение).</summary>
        public static Material Particle(string name, Texture2D tex, bool additive = true)
        {
            var m = new Material(Find("Universal Render Pipeline/Particles/Unlit"));
            m.SetTexture("_BaseMap", tex);
            m.SetColor("_BaseColor", Color.white);
            SetupTransparent(m, additive);
            m.SetFloat("_Cull", (float)CullMode.Off);
            return SaveMaterial(m, name);
        }

        private static void SetupTransparent(Material m, bool additive)
        {
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        // ---------- Сериализованные поля ----------

        /// <summary>Назначает приватное [SerializeField] поле компонента.</summary>
        public static void Set(Object target, string field, Object value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[Last Stand] Поле {field} не найдено в {target.GetType().Name}");
                return;
            }
            p.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void Set(Object target, string field, float value)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[Last Stand] Поле {field} не найдено в {target.GetType().Name}");
                return;
            }
            p.floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void SetArray(Object target, string field, Object[] values)
        {
            var so = new SerializedObject(target);
            SerializedProperty p = so.FindProperty(field);
            if (p == null)
            {
                Debug.LogError($"[Last Stand] Поле {field} не найдено в {target.GetType().Name}");
                return;
            }
            p.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
                p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static GameObject SavePrefab(GameObject go, string dir)
        {
            EnsureFolder(dir);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/{go.name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }
    }
}
