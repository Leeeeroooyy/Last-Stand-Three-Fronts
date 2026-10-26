using System.Collections.Generic;
using LastStand.CameraControl;
using LastStand.Map;
using LastStand.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using L = LastStand.EditorTools.MapGeneration.ThreeFrontsLayout;

namespace LastStand.EditorTools.MapGeneration
{
    /// <summary>
    /// Генерирует сцену карты «Three Fronts» (квадратная карта в духе Dota 2, 3 линии, река, лес, две базы).
    /// Меню: Last Stand ▸ Map ▸ Generate Three Fronts Map. Повторный запуск полностью пересоздаёт сцену.
    /// </summary>
    public static class ThreeFrontsMapGenerator
    {
        public const string ScenePath = MapAssetFactory.ScenesDir + "/Map_ThreeFronts.unity";
        private const int Seed = 1337;

        private static Terrain terrain;
        private static readonly List<(Vector2 pos, float radius)> ClearZones = new List<(Vector2, float)>();

        [MenuItem("Last Stand/Map/Generate Three Fronts Map", priority = 0)]
        public static void GenerateMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            Generate();
        }

        public static void Generate()
        {
            try
            {
                Progress("Ассеты (текстуры, материалы, префабы)…", 0.05f);
                MapAssetFactory assets = MapAssetFactory.BuildAll();

                Progress("Новая сцена…", 0.2f);
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                ClearZones.Clear();

                Transform setup = Group("--- Setup ---");
                Transform world = Group("--- World ---");
                Transform teams = Group("--- Teams ---");
                Transform neutral = Group("--- Neutral ---");
                Transform lanes = Group("--- Lanes ---");

                Progress("Освещение и пост-обработка…", 0.25f);
                Light sun = BuildLighting(setup);
                BuildVolume(setup);

                Progress("Террейн…", 0.3f);
                terrain = BuildTerrain(world, assets);
                BuildWater(world, assets);

                Progress("Базы и постройки…", 0.5f);
                Transform heroSpawn = null;
                foreach (TeamSide side in new[] { TeamSide.Dawn, TeamSide.Dusk })
                {
                    Transform spawn = BuildTeam(teams, side, assets);
                    if (side == TeamSide.Dawn) heroSpawn = spawn;
                }
                BuildNeutral(neutral, assets);
                BuildLanePaths(lanes);

                Progress("Лес и камни…", 0.6f);
                BuildForest(world, assets);

                Progress("Запекание NavMesh…", 0.75f);
                NavMeshData nav = BakeNavMesh(terrain.transform, teams, neutral, world.Find("Forest"));
                var navGo = new GameObject("NavMesh");
                navGo.transform.SetParent(setup, false);
                navGo.AddComponent<MapNavMesh>().Data = nav;

                Progress("Камера и тестовый герой…", 0.9f);
                Transform hero = BuildTestHero(heroSpawn, assets);
                BuildCamera(setup, hero);

                RenderSettings.sun = sun;
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, ScenePath);
                AssetDatabase.SaveAssets();
                Debug.Log($"[Last Stand] Карта сгенерирована: {ScenePath}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                terrain = null;
            }
        }

        private static void Progress(string info, float p) =>
            EditorUtility.DisplayProgressBar("Last Stand — генерация карты", info, p);

        private static Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static Vector3 Ground(Vector2 p, float yOffset = 0f) =>
            new Vector3(p.x, terrain.SampleHeight(new Vector3(p.x, 0f, p.y)) + terrain.transform.position.y + yOffset, p.y);

        private static float Yaw(Vector2 dir) => Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

        // ================= Освещение =================

        private static Light BuildLighting(Transform parent)
        {
            var go = new GameObject("Sun");
            go.transform.SetParent(parent, false);
            go.transform.rotation = Quaternion.Euler(48f, 140f, 0f);
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.94f, 0.83f);
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.8f;

            RenderSettings.skybox = AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.56f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.38f, 0.4f, 0.4f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.18f, 0.16f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.63f, 0.7f, 0.78f);
            RenderSettings.fogStartDistance = 75f;
            RenderSettings.fogEndDistance = 190f;
            return light;
        }

        private static void BuildVolume(Transform parent)
        {
            string path = MapAssetFactory.DataDir + "/VP_ThreeFronts.asset";
            AssetDatabase.DeleteAsset(path);
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);

            var tone = Add<Tonemapping>(profile);
            tone.mode.Override(TonemappingMode.Neutral);

            var bloom = Add<Bloom>(profile);
            bloom.threshold.Override(1.05f);
            bloom.intensity.Override(0.45f);
            bloom.scatter.Override(0.65f);

            var color = Add<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(8f);
            color.saturation.Override(-4f);

            var vignette = Add<Vignette>(profile);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();

            var go = new GameObject("Global Volume");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static T Add<T>(VolumeProfile profile) where T : VolumeComponent
        {
            var c = profile.Add<T>(false);
            c.name = typeof(T).Name;
            c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
            AssetDatabase.AddObjectToAsset(c, profile);
            return c;
        }

        // ================= Террейн и вода =================

        private static Terrain BuildTerrain(Transform parent, MapAssetFactory assets)
        {
            const int hRes = 513;
            const int aRes = 512;
            float size = L.TerrainHalf * 2f;

            var data = new TerrainData
            {
                name = "TD_ThreeFronts",
                heightmapResolution = hRes,
                alphamapResolution = aRes,
                baseMapResolution = 1024
            };
            data.size = new Vector3(size, L.TerrainHeight, size);

            // Сначала сохраняем ассет — иначе текстуры сплат-карты не попадут в него как под-ассеты.
            string path = MapAssetFactory.DataDir + "/TD_ThreeFronts.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data, path);
            data.terrainLayers = assets.TerrainLayers;

            var heights = new float[hRes, hRes];
            for (int z = 0; z < hRes; z++)
            for (int x = 0; x < hRes; x++)
            {
                var p = new Vector2(-L.TerrainHalf + size * x / (hRes - 1), -L.TerrainHalf + size * z / (hRes - 1));
                heights[z, x] = Mathf.Clamp01(L.Height(p) / L.TerrainHeight);
            }
            data.SetHeights(0, 0, heights);

            var alpha = new float[aRes, aRes, L.LayerCount];
            var w = new float[L.LayerCount];
            for (int z = 0; z < aRes; z++)
            for (int x = 0; x < aRes; x++)
            {
                var p = new Vector2(-L.TerrainHalf + size * (x + 0.5f) / aRes, -L.TerrainHalf + size * (z + 0.5f) / aRes);
                L.SplatWeights(p, w);
                for (int i = 0; i < L.LayerCount; i++) alpha[z, x, i] = w[i];
            }
            data.SetAlphamaps(0, 0, alpha);
            data.SyncTexture(TerrainData.AlphamapTextureName);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-L.TerrainHalf, 0f, -L.TerrainHalf);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);

            var t = go.GetComponent<Terrain>();
            t.drawInstanced = true;
            t.heightmapPixelError = 3f;
            t.basemapDistance = 250f;
            return t;
        }

        private static void BuildWater(Transform parent, MapAssetFactory assets)
        {
            // Полоса воды вдоль реки (диагональ x + z = 0), концы уходят под скалы.
            const float halfLen = 135f, halfWidth = 10.5f;
            Vector3 along = new Vector3(1f, 0f, -1f).normalized;
            Vector3 across = new Vector3(1f, 0f, 1f).normalized;
            var mesh = new Mesh { name = "River_Water" };
            mesh.SetVertices(new[]
            {
                -along * halfLen - across * halfWidth, -along * halfLen + across * halfWidth,
                along * halfLen + across * halfWidth, along * halfLen - across * halfWidth
            });
            mesh.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            mesh.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            mesh.RecalculateNormals();
            if (mesh.normals[0].y < 0f) mesh.SetTriangles(new[] { 0, 2, 1, 0, 3, 2 }, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = MapAssetFactory.SaveOrReplace(mesh, MapAssetFactory.MeshesDir + "/River_Water.asset");

            var go = new GameObject("River Water");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(0f, L.WaterLevel, 0f);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = assets.Water;
            r.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ================= Базы =================

        private static GameObject Place(GameObject prefab, Transform parent, Vector2 p, float yaw, string name = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(Ground(p), Quaternion.Euler(0f, yaw, 0f));
            if (name != null) go.name = name;
            return go;
        }

        private static void SetupStructure(GameObject go, StructureType type, TeamSide side, Lane lane, int tier)
        {
            var s = go.GetComponent<MapStructure>();
            if (s == null) s = go.AddComponent<MapStructure>();
            s.Setup(type, side, lane, tier);
            PrefabUtility.RecordPrefabInstancePropertyModifications(s);
        }

        private static Transform Marker(Transform parent, string name, Vector2 p, float yaw, StructureType type, TeamSide side, Lane lane = Lane.None)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(Ground(p), Quaternion.Euler(0f, yaw, 0f));
            go.AddComponent<MapStructure>().Setup(type, side, lane);
            return go.transform;
        }

        /// <summary>Положение на линии с точки зрения команды: d — расстояние от своей базы.</summary>
        private static Vector2 Along(Vector2[] line, TeamSide side, float d, out Vector2 dir)
        {
            float len = L.Length(line);
            if (side == TeamSide.Dawn)
                return L.PointAt(line, d, out dir);
            Vector2 p = L.PointAt(line, len - d, out dir);
            dir = -dir;
            return p;
        }

        private static Transform BuildTeam(Transform teamsRoot, TeamSide side, MapAssetFactory a)
        {
            Transform root = Group(side == TeamSide.Dawn ? "Team Dawn" : "Team Dusk", teamsRoot);
            Transform towers = Group("Towers", root);
            Transform barracks = Group("Barracks", root);
            Transform spawns = Group("Spawns", root);
            Vector2 M(Vector2 p) => side == TeamSide.Dawn ? p : L.MirrorPoint(p);
            float toCenter(Vector2 p) => Yaw(-p);

            Vector2 ancient = M(L.AncientDawn);
            SetupStructure(Place(a.Ancient[side], root, ancient, toCenter(ancient), "Ancient"), StructureType.Ancient, side, Lane.None, 0);
            ClearZones.Add((ancient, 8f));

            Vector2 fountain = M(L.FountainDawn);
            Place(a.Fountain[side], root, fountain, toCenter(fountain), "Fountain");
            ClearZones.Add((fountain, 7f));
            Transform heroSpawn = Marker(spawns, "HeroSpawn", fountain, toCenter(fountain), StructureType.HeroSpawn, side);

            Vector2 shop = M(L.ShopDawn);
            Place(a.Shop[side], root, shop, toCenter(shop), "Shop");
            ClearZones.Add((shop, 4f));

            for (int i = 0; i < L.Tier4Dawn.Length; i++)
            {
                Vector2 p = M(L.Tier4Dawn[i]);
                SetupStructure(Place(a.Tower[side], towers, p, toCenter(p), $"Tower_T4_{(i == 0 ? "A" : "B")}"), StructureType.Tower, side, Lane.None, 4);
                ClearZones.Add((p, 3.5f));
            }

            var laneDefs = new[] { (Lane.Top, L.Top), (Lane.Mid, L.Mid), (Lane.Bot, L.Bot) };
            foreach (var (lane, line) in laneDefs)
            {
                float len = L.Length(line);
                bool mid = lane == Lane.Mid;
                (int tier, float d)[] tiers = { (3, 7f), (2, len * 0.22f), (1, len * (mid ? 0.36f : 0.40f)) };
                foreach (var (tier, d) in tiers)
                {
                    Vector2 onLane = Along(line, side, d, out Vector2 dir);
                    Vector2 p = onLane + L.Left(dir) * 5.6f;
                    var t = Place(a.Tower[side], towers, p, Yaw(dir), $"Tower_{lane}_T{tier}");
                    SetupStructure(t, StructureType.Tower, side, lane, tier);
                    ClearZones.Add((p, 3.5f));
                }

                Vector2 start = Along(line, side, 0f, out Vector2 d0);
                Vector2 back = start - d0 * 7f;
                Vector2 melee = back + L.Left(d0) * 4.6f;
                Vector2 ranged = back - L.Left(d0) * 4.6f;
                SetupStructure(Place(a.BarracksMelee[side], barracks, melee, Yaw(d0), $"Barracks_{lane}_Melee"), StructureType.BarracksMelee, side, lane, 0);
                SetupStructure(Place(a.BarracksRanged[side], barracks, ranged, Yaw(d0), $"Barracks_{lane}_Ranged"), StructureType.BarracksRanged, side, lane, 0);
                ClearZones.Add((melee, 4.5f));
                ClearZones.Add((ranged, 4.5f));

                Marker(spawns, $"CreepSpawn_{lane}", start - d0 * 2f, Yaw(d0), StructureType.CreepSpawn, side, lane);
            }

            return heroSpawn;
        }

        private static void BuildNeutral(Transform root, MapAssetFactory a)
        {
            var rng = new System.Random(Seed + 1);
            Transform shrines = Group("Shrines", root);
            for (int i = 0; i < L.Shrines.Length; i++)
            {
                var go = Place(a.Shrine, shrines, L.Shrines[i], (float)rng.NextDouble() * 360f, $"Shrine_{i + 1}");
                go.transform.position -= Vector3.up * 0.05f;
                ClearZones.Add((L.Shrines[i], 6f));
            }

            Transform chests = Group("Chests", root);
            for (int i = 0; i < L.Chests.Length; i++)
            {
                Vector2 p = L.Chests[i];
                Place(a.Chest, chests, p, Yaw(-p) + (float)rng.NextDouble() * 40f - 20f, $"Chest_{i + 1}");
                ClearZones.Add((p, 4.5f));
            }
        }

        private static void BuildLanePaths(Transform root)
        {
            var laneDefs = new[] { (Lane.Top, L.Top), (Lane.Mid, L.Mid), (Lane.Bot, L.Bot) };
            foreach (var (lane, line) in laneDefs)
            {
                var go = new GameObject($"Lane_{lane}");
                go.transform.SetParent(root, false);
                var points = new List<Transform>();

                Vector2 s = Along(line, TeamSide.Dawn, 0f, out Vector2 ds);
                Vector2 e = Along(line, TeamSide.Dusk, 0f, out Vector2 de);
                var samples = new List<Vector2> { s - ds * 2f };
                float len = L.Length(line);
                int count = Mathf.CeilToInt(len / 10f);
                for (int i = 0; i <= count; i++)
                    samples.Add(L.PointAt(line, len * i / count, out _));
                samples.Add(e - de * 2f);

                for (int i = 0; i < samples.Count; i++)
                {
                    var wp = new GameObject($"WP_{i:00}");
                    wp.transform.SetParent(go.transform, false);
                    wp.transform.position = Ground(samples[i]);
                    points.Add(wp.transform);
                }
                go.AddComponent<LanePath>().Setup(lane, points);
            }
        }

        // ================= Лес =================

        private static void BuildForest(Transform world, MapAssetFactory a)
        {
            var rng = new System.Random(Seed);
            float R() => (float)rng.NextDouble();
            Transform forest = Group("Forest", world);
            Transform trees = Group("Trees", forest);
            Transform rocks = Group("Rocks", forest);

            const float spacing = 3f, jitter = 1.1f, edge = 88f;
            for (float gx = -edge; gx < edge; gx += spacing)
            for (float gz = -edge; gz < edge; gz += spacing)
            {
                var p = new Vector2(gx + (R() * 2f - 1f) * jitter, gz + (R() * 2f - 1f) * jitter);
                float outer = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
                if (outer > edge) continue;
                if (p.y < -L.PlayHalf - 1f) continue; // южный край без деревьев — не закрывает обзор
                if (L.LaneDistance(p) < 7.5f || L.CorridorDistance(p) < 3.2f) continue;
                if (L.RiverDistance(p) < 9.5f && outer < 83f) continue;
                if (L.BaseFactor(p) > 0.05f && outer < L.PlayHalf) continue;
                if (InClearZone(p)) continue;
                if (R() > 0.9f) continue;

                TeamSide side = L.DuskFactor(p) + (R() - 0.5f) * 0.3f > 0.5f ? TeamSide.Dusk : TeamSide.Dawn;
                GameObject[] set = a.Trees[side];
                var prefab = set[rng.Next(set.Length)];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, trees);
                go.transform.SetPositionAndRotation(Ground(p, -0.15f), Quaternion.Euler(0f, R() * 360f, 0f));
                float s = Mathf.Lerp(0.9f, 1.35f, R()) * (outer > L.PlayHalf ? 1.15f : 1f) * (p.y < -L.PlayHalf + 6f ? 0.8f : 1f);
                go.transform.localScale = new Vector3(s, s * Mathf.Lerp(0.9f, 1.15f, R()), s);
            }

            int placed = 0;
            for (int attempt = 0; attempt < 4000 && placed < 170; attempt++)
            {
                var p = new Vector2((R() * 2f - 1f) * 80f, (R() * 2f - 1f) * 80f);
                float lane = L.LaneDistance(p);
                bool riverBank = L.RiverDistance(p) is > 6.5f and < 10.5f;
                if (lane < 6f || L.CorridorDistance(p) < 2.6f || InClearZone(p)) continue;
                if (L.BaseFactor(p) > 0.05f) continue;
                if (L.RiverDistance(p) < 6.5f) continue;
                if (!riverBank && lane > 12f && R() < 0.5f) continue; // больше камней по краям линий и у реки

                var go = (GameObject)PrefabUtility.InstantiatePrefab(a.Rocks[rng.Next(a.Rocks.Length)], rocks);
                go.transform.SetPositionAndRotation(Ground(p, -0.1f), Quaternion.Euler(R() * 20f - 10f, R() * 360f, R() * 20f - 10f));
                float s = Mathf.Lerp(0.45f, 1.5f, R() * R());
                go.transform.localScale = new Vector3(s * Mathf.Lerp(0.9f, 1.4f, R()), s, s * Mathf.Lerp(0.9f, 1.4f, R()));
                placed++;
            }
        }

        private static bool InClearZone(Vector2 p)
        {
            foreach (var (pos, radius) in ClearZones)
                if ((p - pos).sqrMagnitude < radius * radius)
                    return true;
            return false;
        }

        // ================= NavMesh =================

        private static NavMeshData BakeNavMesh(Transform walkable, params Transform[] obstacles)
        {
            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>();
            var tmp = new List<NavMeshBuildSource>();

            UnityEngine.AI.NavMeshBuilder.CollectSources(walkable, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, tmp);
            sources.AddRange(tmp);
            const int notWalkable = 1;
            foreach (Transform root in obstacles)
            {
                if (root == null) continue;
                tmp.Clear();
                UnityEngine.AI.NavMeshBuilder.CollectSources(root, ~0, NavMeshCollectGeometry.PhysicsColliders, notWalkable, markups, tmp);
                sources.AddRange(tmp);
            }

            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.overrideVoxelSize = true;
            settings.voxelSize = 0.2f;
            var bounds = new Bounds(new Vector3(0f, 6f, 0f), new Vector3(L.TerrainHalf * 2f - 4f, 30f, L.TerrainHalf * 2f - 4f));
            NavMeshData data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            data.name = "NavMesh_ThreeFronts";

            string path = MapAssetFactory.DataDir + "/NavMesh_ThreeFronts.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(data, path);
            return data;
        }

        // ================= Камера и тестовый герой =================

        private static Transform BuildTestHero(Transform spawn, MapAssetFactory a)
        {
            var hero = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            hero.name = "TestHero (prototype)";
            Vector3 pos = spawn != null ? spawn.position : Vector3.zero;
            hero.transform.SetPositionAndRotation(pos + Vector3.up * 1f, spawn != null ? spawn.rotation : Quaternion.identity);
            hero.GetComponent<MeshRenderer>().sharedMaterial = a.HeroDawn;

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(hero.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.45f, 0.45f);
            nose.transform.localScale = new Vector3(0.35f, 0.18f, 0.35f);
            nose.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MapAssetFactory.MaterialsDir + "/M_Glow_Dawn.mat");

            var agent = hero.AddComponent<NavMeshAgent>();
            agent.baseOffset = 1f;
            agent.radius = 0.5f;
            agent.height = 2f;
            agent.speed = 7f;
            agent.angularSpeed = 900f;
            agent.acceleration = 60f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = true;
            agent.enabled = false; // включится в Start после проверки NavMesh
            hero.AddComponent<TestHeroMover>();
            return hero.transform;
        }

        private static void BuildCamera(Transform parent, Transform hero)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 38f;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 400f;
            go.AddComponent<AudioListener>();

            var data = cam.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            data.antialiasingQuality = AntialiasingQuality.High;

            var controller = go.AddComponent<MobaCameraController>();
            var so = new SerializedObject(controller);
            so.FindProperty("followTarget").objectReferenceValue = hero;
            so.ApplyModifiedPropertiesWithoutUndo();

            Vector3 focus = hero != null ? hero.position : Vector3.zero;
            Quaternion rot = Quaternion.Euler(55f, 0f, 0f);
            go.transform.SetPositionAndRotation(focus - rot * Vector3.forward * 34f, rot);
        }
    }
}
