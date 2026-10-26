using System.Collections.Generic;
using System.IO;
using LastStand.Map;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace LastStand.EditorTools.MapGeneration
{
    /// <summary>
    /// Создаёт (или пересоздаёт на месте, сохраняя GUID) все ассеты карты:
    /// текстуры и слои террейна, материалы, low-poly меши и префабы построек/деревьев.
    /// </summary>
    public sealed class MapAssetFactory
    {
        public const string Root = "Assets/_Project";
        public const string GeneratedDir = Root + "/Art/Generated";
        public const string TexturesDir = GeneratedDir + "/Textures";
        public const string MaterialsDir = GeneratedDir + "/Materials";
        public const string MeshesDir = GeneratedDir + "/Meshes";
        public const string TerrainLayersDir = GeneratedDir + "/TerrainLayers";
        public const string PrefabsDir = Root + "/Prefabs/Map";
        public const string DataDir = Root + "/Data/Map";
        public const string ScenesDir = Root + "/Scenes";

        // ---------- Палитра ----------
        public static readonly Color DawnGlow = new Color(1f, 0.72f, 0.28f);
        public static readonly Color DuskGlow = new Color(0.72f, 0.3f, 1f);
        public static readonly Color ShrineGlow = new Color(0.3f, 0.9f, 1f);

        // ---------- Результаты ----------
        public TerrainLayer[] TerrainLayers;
        public Material Water;
        public Material HeroDawn;

        public readonly Dictionary<TeamSide, GameObject> Tower = new Dictionary<TeamSide, GameObject>();
        public readonly Dictionary<TeamSide, GameObject> BarracksMelee = new Dictionary<TeamSide, GameObject>();
        public readonly Dictionary<TeamSide, GameObject> BarracksRanged = new Dictionary<TeamSide, GameObject>();
        public readonly Dictionary<TeamSide, GameObject> Ancient = new Dictionary<TeamSide, GameObject>();
        public readonly Dictionary<TeamSide, GameObject> Fountain = new Dictionary<TeamSide, GameObject>();
        public readonly Dictionary<TeamSide, GameObject> Shop = new Dictionary<TeamSide, GameObject>();
        public GameObject Shrine;
        public GameObject Chest;
        public readonly Dictionary<TeamSide, GameObject[]> Trees = new Dictionary<TeamSide, GameObject[]>();
        public GameObject[] Rocks;

        private readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        // =====================================================================

        public static MapAssetFactory BuildAll()
        {
            foreach (var dir in new[] { TexturesDir, MaterialsDir, MeshesDir, TerrainLayersDir, PrefabsDir, DataDir, ScenesDir })
                EnsureFolder(dir);

            var f = new MapAssetFactory();
            f.BuildTerrainLayers();
            f.BuildMaterials();
            f.BuildPrefabs();
            AssetDatabase.SaveAssets();
            return f;
        }

        // ---------- Террейн ----------

        private void BuildTerrainLayers()
        {
            TerrainLayers = new TerrainLayer[ThreeFrontsLayout.LayerCount];
            TerrainLayers[ThreeFrontsLayout.LayerGrass] = Layer("Grass", new Color(0.33f, 0.45f, 0.23f), new Color(0.41f, 0.52f, 0.27f), 11, 0.35f, false, 9f);
            TerrainLayers[ThreeFrontsLayout.LayerJungle] = Layer("JungleGrass", new Color(0.22f, 0.33f, 0.18f), new Color(0.29f, 0.4f, 0.21f), 23, 0.4f, false, 9f);
            TerrainLayers[ThreeFrontsLayout.LayerDuskGrass] = Layer("DuskGrass", new Color(0.24f, 0.27f, 0.26f), new Color(0.33f, 0.31f, 0.34f), 37, 0.4f, false, 9f);
            TerrainLayers[ThreeFrontsLayout.LayerDirt] = Layer("Dirt", new Color(0.52f, 0.41f, 0.29f), new Color(0.63f, 0.5f, 0.36f), 41, 0.5f, false, 7f);
            TerrainLayers[ThreeFrontsLayout.LayerSand] = Layer("RiverSand", new Color(0.55f, 0.52f, 0.42f), new Color(0.68f, 0.63f, 0.5f), 53, 0.35f, false, 7f);
            TerrainLayers[ThreeFrontsLayout.LayerStone] = Layer("BaseStone", new Color(0.56f, 0.54f, 0.5f), new Color(0.7f, 0.68f, 0.63f), 67, 0.25f, true, 8f);
            TerrainLayers[ThreeFrontsLayout.LayerRock] = Layer("Cliff", new Color(0.36f, 0.35f, 0.34f), new Color(0.5f, 0.48f, 0.45f), 79, 0.6f, false, 10f);
        }

        private TerrainLayer Layer(string name, Color a, Color b, int seed, float speckle, bool paving, float tile)
        {
            Texture2D tex = SaveTexture("T_" + name, NoiseTexture(a, b, seed, speckle, paving));
            var layer = new TerrainLayer
            {
                name = "TL_" + name,
                diffuseTexture = tex,
                tileSize = new Vector2(tile, tile),
                smoothness = 0f,
                metallic = 0f
            };
            return SaveOrReplace(layer, $"{TerrainLayersDir}/TL_{name}.terrainlayer");
        }

        /// <summary>Бесшовная текстура: фрактальный value-noise, крапинки и (опционально) плитка.</summary>
        private static Texture2D NoiseTexture(Color a, Color b, int seed, float speckle, bool paving)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGB24, true);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = 0f, amp = 0.5f, norm = 0f;
                for (int o = 0; o < 4; o++)
                {
                    int period = 4 << o;
                    n += ValueNoise(u * period, v * period, period, seed + o * 31) * amp;
                    norm += amp;
                    amp *= 0.5f;
                }
                n /= norm;
                float s = Hash(x, y, seed + 999);
                float t = Mathf.Clamp01(n + (s - 0.5f) * speckle * 0.35f);
                Color c = Color.Lerp(a, b, t);

                if (paving)
                {
                    const int cells = 4;
                    float cu = u * cells, cv = v * cells;
                    int row = Mathf.FloorToInt(cv);
                    if ((row & 1) == 1) cu += 0.5f;
                    float fu = cu - Mathf.Floor(cu), fv = cv - Mathf.Floor(cv);
                    float edge = Mathf.Min(Mathf.Min(fu, 1f - fu), Mathf.Min(fv, 1f - fv));
                    int cellId = Mathf.FloorToInt(cu) % cells + row * 7;
                    c *= 0.9f + Hash(cellId, row, seed) * 0.18f;
                    if (edge < 0.035f) c *= 0.62f;
                }

                c.a = 1f;
                pixels[y * size + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply(true);
            return tex;
        }

        private static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int Wrap(int i) => ((i % period) + period) % period;
            float h00 = Hash(Wrap(x0), Wrap(y0), seed), h10 = Hash(Wrap(x0 + 1), Wrap(y0), seed);
            float h01 = Hash(Wrap(x0), Wrap(y0 + 1), seed), h11 = Hash(Wrap(x0 + 1), Wrap(y0 + 1), seed);
            return Mathf.Lerp(Mathf.Lerp(h00, h10, fx), Mathf.Lerp(h01, h11, fx), fy);
        }

        private static Texture2D SaveTexture(string name, Texture2D tex)
        {
            string path = $"{TexturesDir}/{name}.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.anisoLevel = 4;
                importer.mipmapEnabled = true;
                importer.sRGBTexture = true;
                importer.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ---------- Материалы ----------

        private void BuildMaterials()
        {
            Mat("Stone_Light", new Color(0.78f, 0.74f, 0.66f), 0.15f);
            Mat("Stone_Dark", new Color(0.33f, 0.31f, 0.37f), 0.2f);
            Mat("Stone_Neutral", new Color(0.55f, 0.56f, 0.55f), 0.12f);
            Mat("Trim_Dawn", new Color(0.86f, 0.64f, 0.24f), 0.45f, 0.6f);
            Mat("Trim_Dusk", new Color(0.46f, 0.13f, 0.24f), 0.45f, 0.5f);
            Mat("Roof_Dawn", new Color(0.2f, 0.42f, 0.62f), 0.2f);
            Mat("Roof_Dusk", new Color(0.36f, 0.12f, 0.2f), 0.2f);
            Mat("Glow_Dawn", DawnGlow, 0.6f, 0f, DawnGlow, 1.6f);
            Mat("Glow_Dusk", DuskGlow, 0.6f, 0f, DuskGlow, 1.8f);
            Mat("Pool_Dawn", new Color(0.78f, 0.55f, 0.22f), 0.6f, 0f, DawnGlow, 0.22f);
            Mat("Pool_Dusk", new Color(0.45f, 0.25f, 0.62f), 0.6f, 0f, DuskGlow, 0.3f);
            Mat("Glow_Shrine", ShrineGlow, 0.6f, 0f, ShrineGlow, 1.6f);
            Mat("Glow_Gold", new Color(1f, 0.85f, 0.35f), 0.7f, 0f, new Color(1f, 0.8f, 0.3f), 1.4f);
            Mat("Wood", new Color(0.45f, 0.3f, 0.19f), 0.1f);
            Mat("Gold", new Color(0.93f, 0.74f, 0.32f), 0.6f, 0.85f);
            Mat("Bark", new Color(0.38f, 0.26f, 0.17f), 0.05f);
            Mat("Leaves_Dawn", new Color(0.2f, 0.38f, 0.2f), 0.08f);
            Mat("Leaves_DawnAlt", new Color(0.32f, 0.44f, 0.19f), 0.08f);
            Mat("Leaves_Dusk", new Color(0.15f, 0.27f, 0.27f), 0.08f);
            Mat("Leaves_DuskAlt", new Color(0.29f, 0.22f, 0.34f), 0.08f);
            Mat("Rock", new Color(0.5f, 0.49f, 0.47f), 0.1f);
            HeroDawn = Mat("Hero_Dawn", new Color(0.95f, 0.78f, 0.4f), 0.4f, 0.3f);
            Water = WaterMaterial();
        }

        private Material Mat(string name, Color color, float smoothness, float metallic = 0f,
            Color? emission = null, float emissionIntensity = 0f)
        {
            var m = new Material(LitShader()) { name = "M_" + name, enableInstancing = true };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value * emissionIntensity);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            m = SaveOrReplace(m, $"{MaterialsDir}/M_{name}.mat");
            materials[name] = m;
            return m;
        }

        private Material WaterMaterial()
        {
            var m = new Material(LitShader()) { name = "M_Water" };
            m.SetColor("_BaseColor", new Color(0.16f, 0.46f, 0.6f, 0.7f));
            m.SetFloat("_Smoothness", 0.93f);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_ReceiveShadows", 1f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
            return SaveOrReplace(m, $"{MaterialsDir}/M_Water.mat");
        }

        private static Shader LitShader()
        {
            var s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null)
                throw new System.InvalidOperationException("Не найден шейдер URP Lit — проект должен использовать URP.");
            return s;
        }

        private Material M(string name) => materials[name];
        private Material Team(string prefix, TeamSide side) => materials[$"{prefix}_{side}"];

        // ---------- Меши и префабы ----------

        private void BuildPrefabs()
        {
            // Общие меши
            Mesh crystalSmall = SaveMesh(new LowPolyMeshBuilder().Crystal(0, Matrix4x4.identity, 5, 0.5f, 0.95f, 0.65f).Build("Crystal_Small"));
            Mesh crystalLarge = SaveMesh(new LowPolyMeshBuilder().Crystal(0, Matrix4x4.identity, 6, 1.7f, 4f, 1.8f).Build("Crystal_Large"));
            Mesh towerBody = SaveMesh(TowerMesh());
            Mesh barracksMelee = SaveMesh(BarracksMeleeMesh());
            Mesh barracksRanged = SaveMesh(BarracksRangedMesh());
            Mesh ancientBody = SaveMesh(AncientMesh());
            Mesh fountainBody = SaveMesh(FountainMesh());
            Mesh shopBody = SaveMesh(ShopMesh());
            Mesh shrineBody = SaveMesh(ShrineMesh());
            Mesh chestBody = SaveMesh(ChestMesh());

            foreach (TeamSide side in new[] { TeamSide.Dawn, TeamSide.Dusk })
            {
                Material stone = side == TeamSide.Dawn ? M("Stone_Light") : M("Stone_Dark");
                Material trim = Team("Trim", side);
                Material roof = Team("Roof", side);
                Material glow = Team("Glow", side);
                Color glowColor = side == TeamSide.Dawn ? DawnGlow : DuskGlow;

                // Башня
                var go = new GameObject($"Tower_{side}");
                Part(go, "Body", towerBody, stone, trim);
                Floating(go, "Crystal", crystalSmall, glow, new Vector3(0f, 7.1f, 0f), 1f, 40f);
                Capsule(go, new Vector3(0f, 3.5f, 0f), 1.9f, 7f);
                Tag(go, StructureType.Tower, side);
                Tower[side] = SavePrefab(go);

                // Казармы
                go = new GameObject($"Barracks_Melee_{side}");
                Part(go, "Body", barracksMelee, stone, roof, trim);
                Floating(go, "Crystal", crystalSmall, glow, new Vector3(0f, 6.2f, 0f), 0.7f, 30f);
                Box(go, new Vector3(0f, 2f, 0f), new Vector3(5.4f, 4f, 5.4f));
                Tag(go, StructureType.BarracksMelee, side);
                BarracksMelee[side] = SavePrefab(go);

                go = new GameObject($"Barracks_Ranged_{side}");
                Part(go, "Body", barracksRanged, stone, roof, trim);
                Floating(go, "Crystal", crystalSmall, glow, new Vector3(0f, 7.3f, 0f), 0.7f, 30f);
                Capsule(go, new Vector3(0f, 2.5f, 0f), 2.7f, 5f);
                Tag(go, StructureType.BarracksRanged, side);
                BarracksRanged[side] = SavePrefab(go);

                // Ancient
                go = new GameObject($"Ancient_{side}");
                Part(go, "Body", ancientBody, stone, trim);
                Floating(go, "Core", crystalLarge, glow, new Vector3(0f, 6.4f, 0f), 1f, 14f);
                AddLight(go, new Vector3(0f, 6.5f, 0f), glowColor, 20f, 2f);
                Capsule(go, new Vector3(0f, 3f, 0f), 5.6f, 6f);
                Tag(go, StructureType.Ancient, side);
                Ancient[side] = SavePrefab(go);

                // Фонтан (без коллайдера — по нему ходят)
                go = new GameObject($"Fountain_{side}");
                Part(go, "Body", fountainBody, stone, Team("Pool", side));
                Floating(go, "Crystal", crystalSmall, glow, new Vector3(0f, 3.4f, 0f), 1.6f, 25f);
                AddLight(go, new Vector3(0f, 5f, 0f), glowColor, 14f, 0.8f);
                Tag(go, StructureType.Fountain, side);
                Fountain[side] = SavePrefab(go);

                // Лавка
                go = new GameObject($"Shop_{side}");
                Part(go, "Body", shopBody, M("Wood"), roof, M("Stone_Neutral"), glow);
                Capsule(go, new Vector3(0f, 2f, 0f), 2.4f, 4f);
                Tag(go, StructureType.Shop, side);
                Shop[side] = SavePrefab(go);
            }

            // Святыня (Cesta duše)
            {
                var go = new GameObject("Shrine");
                Part(go, "Body", shrineBody, M("Stone_Neutral"), M("Glow_Shrine"));
                Floating(go, "Crystal", crystalSmall, M("Glow_Shrine"), new Vector3(0f, 3.9f, 0f), 0.9f, 35f);
                AddLight(go, new Vector3(0f, 3.5f, 0f), ShrineGlow, 11f, 1.5f);
                for (int i = 0; i < 5; i++)
                {
                    float a = (i / 5f) * Mathf.PI * 2f + 0.3f;
                    var c = go.AddComponent<CapsuleCollider>();
                    c.center = new Vector3(Mathf.Cos(a) * 2.9f, 1.2f, Mathf.Sin(a) * 2.9f);
                    c.radius = 0.45f;
                    c.height = 2.4f;
                }
                Tag(go, StructureType.Shrine, TeamSide.Neutral);
                Shrine = SavePrefab(go);
            }

            // Сундук (Klíče a truhly)
            {
                var go = new GameObject("Chest");
                Part(go, "Body", chestBody, M("Wood"), M("Gold"), M("Stone_Neutral"));
                Floating(go, "Gem", crystalSmall, M("Glow_Gold"), new Vector3(0f, 1.9f, 0f), 0.35f, 60f);
                Box(go, new Vector3(0f, 0.6f, 0f), new Vector3(2f, 1.2f, 1.6f));
                Tag(go, StructureType.Chest, TeamSide.Neutral);
                Chest = SavePrefab(go);
            }

            // Деревья
            var treeMeshes = new[]
            {
                SaveMesh(PineMesh("Tree_Pine_A", 3, 1.55f, 101)),
                SaveMesh(PineMesh("Tree_Pine_B", 4, 1.45f, 202)),
                SaveMesh(PineMesh("Tree_Pine_C", 3, 1.8f, 303)),
                SaveMesh(RoundTreeMesh("Tree_Round_A", 404))
            };
            foreach (TeamSide side in new[] { TeamSide.Dawn, TeamSide.Dusk })
            {
                var leaves = Team("Leaves", side);
                var leavesAlt = materials[$"Leaves_{side}Alt"];
                var list = new List<GameObject>();
                for (int i = 0; i < treeMeshes.Length; i++)
                {
                    var go = new GameObject($"{treeMeshes[i].name}_{side}");
                    Part(go, "Mesh", treeMeshes[i], M("Bark"), i == 3 || i == 1 ? leavesAlt : leaves);
                    Capsule(go, new Vector3(0f, 2f, 0f), 0.6f, 4f);
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                    list.Add(SavePrefab(go, "Trees"));
                }
                Trees[side] = list.ToArray();
            }

            // Камни
            var rocks = new List<GameObject>();
            for (int i = 0; i < 3; i++)
            {
                var rng = new System.Random(500 + i * 17);
                var mesh = SaveMesh(new LowPolyMeshBuilder()
                    .Blob(0, LowPolyMeshBuilder.TRS(new Vector3(0f, 0.25f, 0f)), 1f, 0.28f, rng, 0.62f)
                    .Build($"Rock_{(char)('A' + i)}"));
                var go = new GameObject(mesh.name);
                Part(go, "Mesh", mesh, M("Rock"));
                var col = go.AddComponent<SphereCollider>();
                col.center = new Vector3(0f, 0.25f, 0f);
                col.radius = 0.8f;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                rocks.Add(SavePrefab(go, "Rocks"));
            }
            Rocks = rocks.ToArray();
        }

        // ---------- Геометрия построек ----------

        private static Matrix4x4 At(float x, float y, float z, float yaw = 0f) =>
            LowPolyMeshBuilder.TRS(new Vector3(x, y, z), yaw);

        private static Mesh TowerMesh()
        {
            var b = new LowPolyMeshBuilder(2);
            b.Frustum(0, At(0, 0, 0), 8, 2.1f, 1.85f, 0.7f, capBottom: false);
            b.Frustum(0, At(0, 0.7f, 0), 6, 1.25f, 0.95f, 4.6f);
            b.Frustum(0, At(0, 5.3f, 0), 6, 1.2f, 1.55f, 0.5f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                b.Frustum(0, At(Mathf.Cos(a) * 1.3f, 5.8f, Mathf.Sin(a) * 1.3f, -i * 60f), 4, 0.28f, 0.22f, 0.55f, angleOffsetDeg: 45f);
            }
            // Командная отделка
            b.Frustum(1, At(0, 0.68f, 0), 8, 1.95f, 1.6f, 0.14f);
            b.Frustum(1, At(0, 2.9f, 0), 6, 1.15f, 1.12f, 0.3f);
            b.Frustum(1, At(0, 5.25f, 0), 6, 1.6f, 1.6f, 0.12f);
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                b.Frustum(1, At(Mathf.Cos(a) * 1.6f, 0f, Mathf.Sin(a) * 1.6f), 4, 0.22f, 0f, 1.3f, angleOffsetDeg: 45f);
            }
            return b.Build("Tower_Body");
        }

        private static Mesh BarracksMeleeMesh()
        {
            var b = new LowPolyMeshBuilder(3);
            b.Frustum(0, At(0, 0, 0), 4, 3.9f, 3.7f, 0.4f, angleOffsetDeg: 45f);
            b.Frustum(0, At(0, 0.4f, 0), 4, 3.35f, 3.05f, 2.5f, angleOffsetDeg: 45f);
            b.Frustum(1, At(0, 2.9f, 0), 4, 3.75f, 0f, 2.4f, angleOffsetDeg: 45f);
            b.Frustum(2, At(0, 2.6f, 0), 4, 3.2f, 3.15f, 0.3f, angleOffsetDeg: 45f);
            b.Frustum(2, At(0, 1.2f, 0), 4, 3.25f, 3.2f, 0.18f, angleOffsetDeg: 45f);
            // угловые колонны
            for (int i = 0; i < 4; i++)
            {
                float a = (i * 90f + 45f) * Mathf.Deg2Rad;
                b.Frustum(0, At(Mathf.Cos(a) * 3.2f, 0.4f, Mathf.Sin(a) * 3.2f), 4, 0.42f, 0.36f, 2.9f, angleOffsetDeg: 45f);
            }
            return b.Build("Barracks_Melee");
        }

        private static Mesh BarracksRangedMesh()
        {
            var b = new LowPolyMeshBuilder(3);
            b.Frustum(0, At(0, 0, 0), 8, 3.1f, 2.9f, 0.4f);
            b.Frustum(0, At(0, 0.4f, 0), 8, 2.55f, 2.25f, 3.3f);
            b.Frustum(1, At(0, 3.7f, 0), 8, 2.95f, 0f, 3.1f);
            b.Frustum(2, At(0, 3.45f, 0), 8, 2.4f, 2.35f, 0.28f);
            b.Frustum(2, At(0, 1.6f, 0), 8, 2.45f, 2.42f, 0.16f);
            return b.Build("Barracks_Ranged");
        }

        private static Mesh AncientMesh()
        {
            var b = new LowPolyMeshBuilder(2);
            b.Frustum(0, At(0, 0, 0), 8, 6f, 5.6f, 0.8f, angleOffsetDeg: 22.5f);
            b.Frustum(0, At(0, 0.8f, 0), 8, 4.9f, 4.5f, 0.8f, angleOffsetDeg: 22.5f);
            b.Frustum(0, At(0, 1.6f, 0), 8, 3.6f, 3.2f, 0.7f, angleOffsetDeg: 22.5f);
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f + 30f) * Mathf.Deg2Rad;
                float x = Mathf.Cos(a) * 4.1f, z = Mathf.Sin(a) * 4.1f;
                b.Frustum(0, At(x, 1.6f, z), 4, 0.45f, 0.32f, 4.6f, angleOffsetDeg: 45f);
                b.Frustum(1, At(x, 6.2f, z), 4, 0.55f, 0f, 0.9f, angleOffsetDeg: 45f);
            }
            b.Frustum(1, At(0, 0.78f, 0), 8, 5.65f, 5.6f, 0.12f, angleOffsetDeg: 22.5f);
            b.Frustum(1, At(0, 1.58f, 0), 8, 4.55f, 4.5f, 0.12f, angleOffsetDeg: 22.5f);
            b.Frustum(1, At(0, 2.3f, 0), 8, 3.25f, 2.2f, 0.4f, angleOffsetDeg: 22.5f);
            return b.Build("Ancient_Body");
        }

        private static Mesh FountainMesh()
        {
            var b = new LowPolyMeshBuilder(2);
            b.Frustum(0, At(0, -0.2f, 0), 12, 6.6f, 6.3f, 0.5f);
            b.Frustum(1, At(0, -0.2f, 0), 12, 4.6f, 4.6f, 0.58f);
            for (int i = 0; i < 6; i++)
            {
                float a = (i * 60f) * Mathf.Deg2Rad;
                b.Frustum(0, At(Mathf.Cos(a) * 5.6f, 0.2f, Mathf.Sin(a) * 5.6f, -i * 60f), 4, 0.5f, 0.28f, 2.6f, angleOffsetDeg: 45f);
            }
            return b.Build("Fountain_Body");
        }

        private static Mesh ShopMesh()
        {
            var b = new LowPolyMeshBuilder(4);
            b.Frustum(2, At(0, 0, 0), 6, 2.7f, 2.55f, 0.3f);
            b.Frustum(0, At(0, 0.3f, 0), 6, 2.15f, 1.95f, 2.1f);
            b.Frustum(1, At(0, 2.35f, 0), 6, 2.85f, 0f, 1.9f);
            b.Frustum(0, At(0, 0.3f, 2.5f), 4, 1.05f, 1.0f, 0.95f, angleOffsetDeg: 45f); // прилавок
            b.Crystal(3, At(1.6f, 2.0f, 1.6f), 4, 0.22f, 0.4f, 0.3f);                  // фонарь
            return b.Build("Shop_Body");
        }

        private static Mesh ShrineMesh()
        {
            var b = new LowPolyMeshBuilder(2);
            b.Frustum(0, At(0, -0.1f, 0), 10, 3.6f, 3.35f, 0.38f);
            b.Frustum(1, At(0, -0.1f, 0), 10, 2.3f, 2.3f, 0.43f);
            b.Frustum(0, At(0, 0.28f, 0), 4, 0.55f, 0.18f, 2.7f, angleOffsetDeg: 45f);
            for (int i = 0; i < 5; i++)
            {
                float a = (i / 5f) * Mathf.PI * 2f + 0.3f;
                b.Frustum(0, At(Mathf.Cos(a) * 2.9f, 0.1f, Mathf.Sin(a) * 2.9f, -a * Mathf.Rad2Deg), 4, 0.42f, 0.26f, 2.3f + (i % 2) * 0.4f, angleOffsetDeg: 45f);
            }
            return b.Build("Shrine_Body");
        }

        private static Mesh ChestMesh()
        {
            var b = new LowPolyMeshBuilder(3);
            var boxScale = new Vector3(1.25f, 1f, 0.85f);
            b.Frustum(2, At(0, 0, 0), 6, 1.35f, 1.2f, 0.25f);
            b.Frustum(0, LowPolyMeshBuilder.TRS(new Vector3(0, 0.25f, 0), Quaternion.identity, boxScale), 4, 0.75f, 0.75f, 0.6f, angleOffsetDeg: 45f);
            b.Frustum(0, LowPolyMeshBuilder.TRS(new Vector3(0, 0.85f, 0), Quaternion.identity, boxScale), 4, 0.78f, 0.55f, 0.32f, angleOffsetDeg: 45f);
            b.Frustum(1, LowPolyMeshBuilder.TRS(new Vector3(0, 0.78f, 0), Quaternion.identity, boxScale * 1.02f), 4, 0.78f, 0.78f, 0.09f, angleOffsetDeg: 45f);
            b.Frustum(1, LowPolyMeshBuilder.TRS(new Vector3(0, 0.35f, 0), Quaternion.identity, boxScale * 1.02f), 4, 0.76f, 0.76f, 0.07f, angleOffsetDeg: 45f);
            b.Frustum(1, At(0, 0.62f, 0.66f), 4, 0.13f, 0.13f, 0.24f, angleOffsetDeg: 45f); // замок
            return b.Build("Chest_Body");
        }

        private static Mesh PineMesh(string name, int tiers, float radius, int seed)
        {
            var rng = new System.Random(seed);
            var b = new LowPolyMeshBuilder(2);
            b.Frustum(0, At(0, 0, 0), 5, 0.3f, 0.2f, 1.5f);
            for (int t = 0; t < tiers; t++)
            {
                float r = radius * (1f - t * 0.24f);
                float y = 1.0f + t * (3.2f / tiers);
                b.Frustum(1, At(0, y, 0, (float)rng.NextDouble() * 360f), 7, r, 0f, 1.9f - t * 0.1f, capBottom: true);
            }
            return b.Build(name);
        }

        private static Mesh RoundTreeMesh(string name, int seed)
        {
            var rng = new System.Random(seed);
            var b = new LowPolyMeshBuilder(2);
            b.Frustum(0, At(0, 0, 0), 5, 0.32f, 0.22f, 2.0f);
            b.Blob(1, LowPolyMeshBuilder.TRS(new Vector3(0f, 2.9f, 0f)), 1.5f, 0.18f, rng, 0.85f);
            b.Blob(1, LowPolyMeshBuilder.TRS(new Vector3(0.7f, 3.6f, 0.3f)), 1.0f, 0.2f, rng, 0.9f);
            b.Blob(1, LowPolyMeshBuilder.TRS(new Vector3(-0.6f, 3.4f, -0.4f)), 0.9f, 0.2f, rng, 0.9f);
            return b.Build(name);
        }

        // ---------- Сборка префабов ----------

        private static void Part(GameObject root, string name, Mesh mesh, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        private static GameObject Floating(GameObject root, string name, Mesh mesh, Material mat, Vector3 pos, float scale, float spin)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.one * scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            var f = go.AddComponent<CrystalFloat>();
            var so = new SerializedObject(f);
            so.FindProperty("spinSpeed").floatValue = spin;
            so.FindProperty("bobAmplitude").floatValue = 0.12f * Mathf.Max(1f, scale);
            so.ApplyModifiedPropertiesWithoutUndo();
            return go;
        }

        private static void AddLight(GameObject root, Vector3 pos, Color color, float range, float intensity)
        {
            var go = new GameObject("Glow Light");
            go.transform.SetParent(root.transform, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }

        private static void Capsule(GameObject go, Vector3 center, float radius, float height)
        {
            var c = go.AddComponent<CapsuleCollider>();
            c.center = center;
            c.radius = radius;
            c.height = height;
        }

        private static void Box(GameObject go, Vector3 center, Vector3 size)
        {
            var c = go.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        private static void Tag(GameObject go, StructureType type, TeamSide side) =>
            go.AddComponent<MapStructure>().Setup(type, side);

        private static GameObject SavePrefab(GameObject go, string subFolder = null)
        {
            string dir = subFolder == null ? PrefabsDir : $"{PrefabsDir}/{subFolder}";
            EnsureFolder(dir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{dir}/{go.name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static Mesh SaveMesh(Mesh mesh) => SaveOrReplace(mesh, $"{MeshesDir}/{mesh.name}.asset");

        // ---------- Общие утилиты ----------

        public static T SaveOrReplace<T>(T obj, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
            {
                EditorUtility.CopySerialized(obj, existing);
                existing.name = Path.GetFileNameWithoutExtension(path);
                Object.DestroyImmediate(obj);
                EditorUtility.SetDirty(existing);
                return existing;
            }
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
                EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
