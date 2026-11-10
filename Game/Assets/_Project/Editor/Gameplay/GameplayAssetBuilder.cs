using LastStand.Abilities;
using LastStand.Controls;
using LastStand.Core;
using LastStand.Creeps;
using LastStand.EditorTools.MapGeneration;
using LastStand.Heroes;
using LastStand.Map;
using LastStand.Match;
using UnityEditor;
using UnityEngine;
using U = LastStand.EditorTools.Gameplay.EditorAssetUtil;

namespace LastStand.EditorTools.Gameplay
{
    /// <summary>
    /// Last Stand ▸ Gameplay ▸ Build All — собирает все ассеты этапа 1 и настраивает сцену карты:
    /// шрифты и HUD, эффекты, модели юнитов, способности и герой, крипы, конфиг матча.
    /// Повторный запуск обновляет ассеты на месте (GUID и ручные ссылки сохраняются).
    /// </summary>
    public static class GameplayAssetBuilder
    {
        public const string MatchConfigPath = U.DataDir + "/MatchConfig.asset";

        [MenuItem("Last Stand/Gameplay/Build All (assets + scene)", priority = 10)]
        public static void BuildAllMenu()
        {
            if (!UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BuildAll();
        }

        [MenuItem("Last Stand/Gameplay/Setup Scene Only", priority = 11)]
        public static void SetupSceneMenu() => GameplaySceneSetup.Apply(openMapScene: true);

        public static void BuildAll()
        {
            if (!HudBuilder.EssentialsReady)
            {
                Debug.Log("[Last Stand] Импорт TMP Essential Resources…");
                AssetDatabase.importPackageCompleted += OnEssentialsImported;
                if (!HudBuilder.ImportEssentials())
                    AssetDatabase.importPackageCompleted -= OnEssentialsImported;
                return;
            }

            try
            {
                Progress("Импорт графики…", 0.05f);
                ArtImportSettings.ReloadSlices();
                AssetDatabase.ImportAsset(ArtImportSettings.UiDir, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
                AssetDatabase.ImportAsset(ArtImportSettings.FxDir, ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);

                foreach (string dir in new[] { U.DataDir, U.AbilitiesDir, U.MaterialsDir, U.MeshesDir, U.UnitsPrefabDir, U.VfxPrefabDir, U.UiPrefabDir })
                    U.EnsureFolder(dir);

                Progress("Постройки: подготовка к разрушению…", 0.1f);
                MakeStructurePrefabsDynamic();

                Progress("Визуальные эффекты…", 0.2f);
                VfxFactory vfx = VfxFactory.Build();

                Progress("Модели и префабы юнитов…", 0.4f);
                UnitPrefabFactory units = UnitPrefabFactory.Build(vfx);

                Progress("Способности и герой…", 0.55f);
                HeroDefinition hero = BuildSunWarden(units, vfx);

                Progress("Крипы и конфиг матча…", 0.65f);
                MatchConfig config = BuildMatchConfig(units, vfx, hero);

                Progress("Шрифты и интерфейс…", 0.75f);
                HudBuilder hud = HudBuilder.Build();

                Progress("Курсоры и индикаторы…", 0.85f);
                CursorSet cursors = BuildCursors();
                Material[] indicators = BuildIndicatorMaterials();

                AssetDatabase.SaveAssets();

                Progress("Сцена…", 0.92f);
                GameplaySceneSetup.Assets = new GameplaySceneSetup.Refs
                {
                    Config = config,
                    Hud = hud.HudPrefab,
                    Cursors = cursors,
                    RangeMat = indicators[0],
                    AreaMat = indicators[1],
                    LineMat = indicators[2],
                    TargetMat = indicators[3],
                    MarkerMat = indicators[4]
                };
                GameplaySceneSetup.Apply(openMapScene: true);
                Debug.Log("[Last Stand] Этап 1 собран: ассеты обновлены, сцена настроена. Нажмите Play.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static void OnEssentialsImported(string packageName)
        {
            AssetDatabase.importPackageCompleted -= OnEssentialsImported;
            EditorApplication.delayCall += () =>
            {
                if (HudBuilder.EssentialsReady) BuildAll();
                else Debug.LogError("[Last Stand] TMP Essential Resources не импортировались — импортируйте вручную: Window ▸ TextMeshPro ▸ Import TMP Essential Resources.");
            };
        }

        private static void Progress(string info, float p) =>
            EditorUtility.DisplayProgressBar("Last Stand — сборка геймплея", info, p);

        // ================= Постройки =================

        /// <summary>
        /// Башни, казармы и Ancient должны оседать при разрушении — снимаем Static batching с их частей
        /// (в префабах карты и в MapAssetFactory для будущих генераций).
        /// </summary>
        public static void MakeStructurePrefabsDynamic()
        {
            string[] names =
            {
                "Tower_Dawn", "Tower_Dusk", "Barracks_Melee_Dawn", "Barracks_Melee_Dusk",
                "Barracks_Ranged_Dawn", "Barracks_Ranged_Dusk", "Ancient_Dawn", "Ancient_Dusk"
            };
            foreach (string n in names)
            {
                string path = $"{MapAssetFactory.PrefabsDir}/{n}.prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
                    continue;
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                foreach (Transform t in contents.GetComponentsInChildren<Transform>(true))
                    GameObjectUtility.SetStaticEditorFlags(t.gameObject, (StaticEditorFlags)0);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        // ================= Герой =================

        private static HeroDefinition BuildSunWarden(UnitPrefabFactory units, VfxFactory vfx)
        {
            var burn = new StatusEffectData
            {
                id = "solar_burn", duration = 3f, isDebuff = true, damagePerSecond = 15f,
                damageType = DamageType.Magical, element = Element.Fire, attachedVfx = vfx.Burn, vfxHeight = 0.35f
            };

            var lance = U.LoadOrCreate<SkillshotAbility>($"{U.AbilitiesDir}/Ability_SolarLance.asset");
            lance.displayName = "Solar Lance";
            lance.description = "Hurls a lance of living sunlight in a line. It pierces every enemy on its path and sets them ablaze.";
            lance.icon = U.UiSprite("Icons/icon_solar_lance.png");
            lance.element = Element.Fire;
            lance.targeting = AbilityTargeting.Direction;
            lance.targetTeam = TeamFilter.Enemies;
            lance.maxLevel = 4;
            lance.castPoint = 0.25f;
            lance.manaCost = new[] { 90f, 100f, 110f, 120f };
            lance.cooldown = new[] { 9f, 8f, 7f, 6f };
            lance.castRange = new[] { 12f };
            lance.lineWidth = 1.5f;
            lance.areaRadius = new[] { 0f };
            lance.damage = new[] { 90f, 150f, 210f, 270f };
            lance.damageType = DamageType.Magical;
            lance.speed = 28f;
            lance.pierce = true;
            lance.flightHeight = 1.2f;
            lance.onHit = burn;
            lance.projectileVfx = vfx.LanceProjectile;
            lance.hitVfx = vfx.FireBurst;
            lance.castVfx = vfx.HitSpark;
            EditorUtility.SetDirty(lance);

            var leap = U.LoadOrCreate<LeapAbility>($"{U.AbilitiesDir}/Ability_RadiantLeap.asset");
            leap.displayName = "Radiant Leap";
            leap.description = "Leaps to the target point. On landing, a radiant shockwave damages and slows nearby enemies.";
            leap.icon = U.UiSprite("Icons/icon_radiant_leap.png");
            leap.element = Element.Fire;
            leap.targeting = AbilityTargeting.Point;
            leap.targetTeam = TeamFilter.Enemies;
            leap.maxLevel = 4;
            leap.castPoint = 0.15f;
            leap.manaCost = new[] { 80f };
            leap.cooldown = new[] { 14f, 12f, 10f, 8f };
            leap.castRange = new[] { 8f };
            leap.areaRadius = new[] { 3f };
            leap.leapSpeed = 20f;
            leap.arcHeight = 2.6f;
            leap.damage = new[] { 60f, 100f, 140f, 180f };
            leap.damageType = DamageType.Magical;
            leap.onLand = new StatusEffectData { id = "radiant_slow", duration = 2f, moveSpeedPercent = -30f, isDebuff = true };
            leap.takeoffVfx = vfx.LeapTakeoff;
            leap.landVfx = vfx.LeapLand;
            leap.hitVfx = vfx.HitSpark;
            EditorUtility.SetDirty(leap);

            var aegis = U.LoadOrCreate<SelfBuffAbility>($"{U.AbilitiesDir}/Ability_SunAegis.asset");
            aegis.displayName = "Sun Aegis";
            aegis.description = "Wreathes Aurel in a golden aegis, hardening his armor and quickening his strikes.";
            aegis.icon = U.UiSprite("Icons/icon_sun_aegis.png");
            aegis.element = Element.None;
            aegis.targeting = AbilityTargeting.NoTarget;
            aegis.maxLevel = 4;
            aegis.castPoint = 0f;
            aegis.manaCost = new[] { 60f };
            aegis.cooldown = new[] { 18f };
            aegis.castRange = new[] { 0f };
            aegis.areaRadius = new[] { 0f };
            aegis.buffId = "sun_aegis";
            aegis.duration = new[] { 6f };
            aegis.armorBonus = new[] { 4f, 6f, 8f, 10f };
            aegis.attackSpeedBonus = new[] { 30f, 45f, 60f, 75f };
            aegis.moveSpeedPercent = new[] { 10f };
            aegis.damageBonus = new[] { 0f };
            aegis.auraVfx = vfx.AegisAura;
            aegis.castVfx = vfx.LeapTakeoff;
            EditorUtility.SetDirty(aegis);

            var sunfall = U.LoadOrCreate<DelayedAreaAbility>($"{U.AbilitiesDir}/Ability_Sunfall.asset");
            sunfall.displayName = "Sunfall";
            sunfall.description = "Calls down a pillar of the sun. After a short delay it scorches the area and stuns every enemy caught inside.";
            sunfall.icon = U.UiSprite("Icons/icon_sunfall.png");
            sunfall.element = Element.Fire;
            sunfall.isUltimate = true;
            sunfall.targeting = AbilityTargeting.Point;
            sunfall.targetTeam = TeamFilter.Enemies;
            sunfall.maxLevel = 3;
            sunfall.castPoint = 0.3f;
            sunfall.manaCost = new[] { 150f, 200f, 250f };
            sunfall.cooldown = new[] { 70f, 60f, 50f };
            sunfall.castRange = new[] { 12f };
            sunfall.areaRadius = new[] { 4.5f };
            sunfall.delay = 1f;
            sunfall.damage = new[] { 200f, 325f, 450f };
            sunfall.damageType = DamageType.Magical;
            sunfall.stunDuration = new[] { 1.5f, 1.75f, 2f };
            sunfall.stun = new StatusEffectData
            {
                id = "stun", duration = 1.5f, isDebuff = true, states = UnitStates.Stunned, attachedVfx = vfx.Stun, vfxHeight = 1.02f
            };
            sunfall.telegraphVfx = vfx.SunfallTelegraph;
            sunfall.impactVfx = vfx.SunfallImpact;
            sunfall.hitVfx = vfx.FireBurst;
            EditorUtility.SetDirty(sunfall);

            var def = U.LoadOrCreate<HeroDefinition>($"{U.DataDir}/Hero_SunWarden.asset");
            def.heroName = "Aurel";
            def.title = "the Sun Warden";
            def.role = HeroRole.Carry;
            def.lore = "Last knight of the Dawn order, sworn to hold the three fronts until the sun rises again.";
            def.portrait = U.UiSprite("portrait_sun_warden.png");
            def.prefab = units.SunWarden;
            def.stats = new UnitStats
            {
                maxHealth = 640f, healthRegen = 2.2f, maxMana = 320f, manaRegen = 1.4f,
                armor = 3f, magicResist = 0.25f, damageMin = 48f, damageMax = 56f, attackRange = 1.4f,
                baseAttackTime = 1.7f, attackSpeed = 110f, attackPoint = 0.35f, attackBackswing = 0.35f,
                moveSpeed = 6.2f, turnRate = 1000f, acquisitionRange = 7f, visionRange = 14f
            };
            def.bodyRadius = 0.5f;
            def.barHeight = 3.2f;
            def.abilities = new AbilityDefinition[] { lance, leap, aegis, sunfall };
            EditorUtility.SetDirty(def);
            return def;
        }

        // ================= Крипы и матч =================

        private static MatchConfig BuildMatchConfig(UnitPrefabFactory units, VfxFactory vfx, HeroDefinition hero)
        {
            var melee = U.LoadOrCreate<CreepDefinition>($"{U.DataDir}/Creep_Melee.asset");
            melee.creepName = "Footman";
            melee.type = CreepType.Melee;
            melee.dawnPrefab = units.MeleeDawn;
            melee.duskPrefab = units.MeleeDusk;
            melee.stats = new UnitStats
            {
                maxHealth = 550f, healthRegen = 0.5f, armor = 2f, damageMin = 19f, damageMax = 23f,
                attackRange = 1.2f, baseAttackTime = 1f, attackPoint = 0.47f, attackBackswing = 0.53f,
                moveSpeed = 5f, turnRate = 720f, acquisitionRange = 7.5f, visionRange = 12f, goldBounty = 40, xpBounty = 57
            };
            melee.bodyRadius = 0.45f;
            melee.barHeight = 2.3f;
            EditorUtility.SetDirty(melee);

            var ranged = U.LoadOrCreate<CreepDefinition>($"{U.DataDir}/Creep_Ranged.asset");
            ranged.creepName = "Sunseer";
            ranged.type = CreepType.Ranged;
            ranged.dawnPrefab = units.RangedDawn;
            ranged.duskPrefab = units.RangedDusk;
            ranged.stats = new UnitStats
            {
                maxHealth = 300f, healthRegen = 0.5f, armor = 0f, damageMin = 21f, damageMax = 26f,
                attackRange = 7f, baseAttackTime = 1f, attackPoint = 0.5f, attackBackswing = 0.67f, projectileSpeed = 14f,
                moveSpeed = 5f, turnRate = 720f, acquisitionRange = 8f, visionRange = 12f, goldBounty = 50, xpBounty = 69
            };
            ranged.bodyRadius = 0.45f;
            ranged.barHeight = 2.3f;
            EditorUtility.SetDirty(ranged);

            var config = U.LoadOrCreate<MatchConfig>(MatchConfigPath);
            config.prepTime = 5f;
            config.firstWaveTime = 0f;
            config.waveInterval = 30f;
            config.playerTeam = TeamSide.Dawn;
            config.playerHero = hero;
            config.heroRespawnTime = 6f;
            config.meleeCreep = melee;
            config.rangedCreep = ranged;
            config.meleePerWave = 3;
            config.rangedPerWave = 1;
            config.megaHealthMultiplier = 1.6f;
            config.megaDamageMultiplier = 1.4f;
            config.towerStats = new[]
            {
                Tower(1800f, 12f, 100f, 120f),
                Tower(2400f, 15f, 120f, 140f),
                Tower(2700f, 17f, 130f, 150f),
                Tower(2900f, 20f, 140f, 160f)
            };
            config.towerProjectileDawn = vfx.TowerProjectileDawn;
            config.towerProjectileDusk = vfx.TowerProjectileDusk;
            config.barracksMeleeStats = new UnitStats { maxHealth = 2200f, healthRegen = 5f, armor = 15f, damageMin = 0f, damageMax = 0f, attackRange = 0f, moveSpeed = 0f };
            config.barracksRangedStats = new UnitStats { maxHealth = 1300f, healthRegen = 2.5f, armor = 9f, damageMin = 0f, damageMax = 0f, attackRange = 0f, moveSpeed = 0f };
            config.ancientStats = new UnitStats { maxHealth = 4500f, healthRegen = 3f, armor = 13f, damageMin = 0f, damageMax = 0f, attackRange = 0f, moveSpeed = 0f };
            config.fountainRadius = 8f;
            config.fountainHealPercent = 6f;
            config.fountainManaPercent = 6f;
            config.fountainDamagePerSecond = 300f;
            config.structureDestroyedVfx = vfx.StructureDestroyed;
            config.unitDeathVfx = vfx.UnitDeath;
            EditorUtility.SetDirty(config);
            return config;
        }

        private static UnitStats Tower(float hp, float armor, float dmgMin, float dmgMax) => new UnitStats
        {
            maxHealth = hp, healthRegen = 0f, armor = armor, damageMin = dmgMin, damageMax = dmgMax,
            attackRange = 10.5f, baseAttackTime = 1f, attackPoint = 0.2f, attackBackswing = 0.1f, projectileSpeed = 18f,
            moveSpeed = 0f, acquisitionRange = 10.5f, visionRange = 16f
        };

        // ================= Курсоры и индикаторы =================

        private static CursorSet BuildCursors()
        {
            var set = U.LoadOrCreate<CursorSet>($"{U.DataDir}/CursorSet.asset");
            set.defaultCursor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtImportSettings.UiDir}/Cursors/cursor_default.png");
            set.attackCursor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtImportSettings.UiDir}/Cursors/cursor_attack.png");
            set.targetCursor = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ArtImportSettings.UiDir}/Cursors/cursor_target.png");
            set.defaultHotspot = new Vector2(3f, 3f);
            set.attackHotspot = new Vector2(3f, 3f);
            set.targetHotspot = new Vector2(24f, 24f);
            EditorUtility.SetDirty(set);
            return set;
        }

        private static Material[] BuildIndicatorMaterials()
        {
            Color white = Color.white;
            return new[]
            {
                U.Indicator("M_Indicator_Range", U.FxTexture("ind_range.png"), white, onTop: true),
                U.Indicator("M_Indicator_Area", U.FxTexture("ind_area.png"), white, onTop: true),
                U.Indicator("M_Indicator_Line", U.FxTexture("ind_line.png"), white, onTop: true),
                U.Indicator("M_Indicator_Target", U.FxTexture("ind_target.png"), white, onTop: true),
                U.Indicator("M_Indicator_Marker", U.FxTexture("marker.png"), white, onTop: true, additive: true)
            };
        }

        /// <summary>Конфиг для сцены, если сборка уже выполнялась ранее.</summary>
        public static MatchConfig ExistingConfig => AssetDatabase.LoadAssetAtPath<MatchConfig>(MatchConfigPath);

        public static bool IsBuilt => ExistingConfig != null &&
            AssetDatabase.LoadAssetAtPath<GameObject>($"{U.UiPrefabDir}/HUD.prefab") != null;

        public static Material IndicatorMaterial(string name) =>
            AssetDatabase.LoadAssetAtPath<Material>($"{U.MaterialsDir}/{name}.mat");

        public static CursorSet ExistingCursors => AssetDatabase.LoadAssetAtPath<CursorSet>($"{U.DataDir}/CursorSet.asset");
    }
}
