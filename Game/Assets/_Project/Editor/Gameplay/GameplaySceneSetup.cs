using LastStand.CameraControl;
using LastStand.Controls;
using LastStand.Creeps;
using LastStand.EditorTools.MapGeneration;
using LastStand.Match;
using LastStand.Prototype;
using LastStand.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using U = LastStand.EditorTools.Gameplay.EditorAssetUtil;

namespace LastStand.EditorTools.Gameplay
{
    /// <summary>
    /// Добавляет в сцену карты игровые системы: матч, управление игрока, индикаторы, HUD, EventSystem.
    /// Идемпотентно: повторный запуск пересоздаёт только объекты геймплея, карту не трогает.
    /// </summary>
    public static class GameplaySceneSetup
    {
        private const string GameplayRootName = "--- Gameplay ---";

        public struct Refs
        {
            public MatchConfig Config;
            public GameObject Hud;
            public CursorSet Cursors;
            public Material RangeMat, AreaMat, LineMat, TargetMat, MarkerMat;
        }

        /// <summary>Ссылки от только что выполненной сборки (иначе загружаются существующие ассеты).</summary>
        public static Refs? Assets;

        public static void Apply(bool openMapScene)
        {
            Refs refs = Assets ?? LoadExisting();
            Assets = null;
            if (refs.Config == null || refs.Hud == null)
            {
                Debug.LogError("[Last Stand] Ассеты геймплея не собраны — запустите Last Stand ▸ Gameplay ▸ Build All.");
                return;
            }

            if (openMapScene && EditorSceneManager.GetActiveScene().path != ThreeFrontsMapGenerator.ScenePath)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ThreeFrontsMapGenerator.ScenePath) == null)
                {
                    Debug.LogError("[Last Stand] Нет сцены карты — сначала Last Stand ▸ Map ▸ Generate Three Fronts Map.");
                    return;
                }
                EditorSceneManager.OpenScene(ThreeFrontsMapGenerator.ScenePath, OpenSceneMode.Single);
            }

            ApplyToActiveScene(refs);
        }

        /// <summary>Вызывается генератором карты: если геймплей уже собран — сразу ставит его в новую сцену.</summary>
        public static void TryApplyAfterGeneration()
        {
            if (!GameplayAssetBuilder.IsBuilt)
                return;
            GameplayAssetBuilder.MakeStructurePrefabsDynamic();
            ApplyToActiveScene(LoadExisting());
        }

        private static Refs LoadExisting() => new Refs
        {
            Config = GameplayAssetBuilder.ExistingConfig,
            Hud = AssetDatabase.LoadAssetAtPath<GameObject>($"{U.UiPrefabDir}/HUD.prefab"),
            Cursors = GameplayAssetBuilder.ExistingCursors,
            RangeMat = GameplayAssetBuilder.IndicatorMaterial("M_Indicator_Range"),
            AreaMat = GameplayAssetBuilder.IndicatorMaterial("M_Indicator_Area"),
            LineMat = GameplayAssetBuilder.IndicatorMaterial("M_Indicator_Line"),
            TargetMat = GameplayAssetBuilder.IndicatorMaterial("M_Indicator_Target"),
            MarkerMat = GameplayAssetBuilder.IndicatorMaterial("M_Indicator_Marker")
        };

        private static void ApplyToActiveScene(Refs refs)
        {
            var scene = EditorSceneManager.GetActiveScene();

            // Убираем прототип и предыдущую установку геймплея.
            foreach (var mover in Object.FindObjectsByType<TestHeroMover>(FindObjectsInactive.Include))
                Object.DestroyImmediate(mover.gameObject);
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == GameplayRootName || root.GetComponent<HealthBarLayer>() != null ||
                    root.name == "HUD" || root.GetComponent<EventSystem>() != null)
                    Object.DestroyImmediate(root);
            }

            var cameraController = Object.FindAnyObjectByType<MobaCameraController>();
            Camera cam = cameraController != null ? cameraController.GetComponent<Camera>() : Camera.main;

            var gameplay = new GameObject(GameplayRootName);

            var matchGo = new GameObject("Match");
            matchGo.transform.SetParent(gameplay.transform, false);
            var match = matchGo.AddComponent<MatchManager>();
            U.Set(match, "config", refs.Config);
            var spawner = matchGo.AddComponent<CreepWaveSpawner>();
            var bootstrap = matchGo.AddComponent<MatchBootstrap>();
            U.Set(bootstrap, "creepSpawner", spawner);
            U.Set(bootstrap, "cameraController", cameraController);

            var indicatorGo = new GameObject("Ability Indicator");
            indicatorGo.transform.SetParent(gameplay.transform, false);
            var indicator = indicatorGo.AddComponent<AbilityIndicator>();
            indicator.Configure(refs.RangeMat, refs.AreaMat, refs.LineMat, refs.TargetMat);

            var markerGo = new GameObject("Click Marker");
            markerGo.transform.SetParent(gameplay.transform, false);
            var marker = markerGo.AddComponent<ClickMarker>();
            marker.Configure(refs.MarkerMat);

            var inputGo = new GameObject("Player Commander");
            inputGo.transform.SetParent(gameplay.transform, false);
            var commander = inputGo.AddComponent<PlayerCommander>();
            U.Set(commander, "viewCamera", cam);
            U.Set(commander, "indicator", indicator);
            U.Set(commander, "clickMarker", marker);
            U.Set(commander, "cursors", refs.Cursors);
            inputGo.AddComponent<DevCheats>();

            var hud = (GameObject)PrefabUtility.InstantiatePrefab(refs.Hud, scene);
            hud.name = "HUD";
            var bars = hud.GetComponentInChildren<HealthBarLayer>(true);
            if (bars != null)
            {
                U.Set(bars, "viewCamera", cam);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bars);
            }

            var eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Selection.activeGameObject = gameplay;
        }
    }
}
