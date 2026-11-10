using LastStand.Abilities;
using LastStand.Core;
using LastStand.Creeps;
using LastStand.EditorTools.MapGeneration;
using LastStand.Heroes;
using LastStand.Map;
using LastStand.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using B = LastStand.EditorTools.MapGeneration.LowPolyMeshBuilder;
using U = LastStand.EditorTools.Gameplay.EditorAssetUtil;

namespace LastStand.EditorTools.Gameplay
{
    /// <summary>
    /// Low-poly заглушки героя и крипов (без скелета, анимирует UnitAnimator) и их префабы с компонентами.
    /// Художник позже заменит дочерний объект Model своей моделью, сохранив префаб.
    /// </summary>
    public sealed class UnitPrefabFactory
    {
        public GameObject SunWarden;
        public GameObject MeleeDawn, MeleeDusk, RangedDawn, RangedDusk;

        private readonly VfxFactory vfx;

        private struct Palette
        {
            public Material Armor, Trim, Cloth, Glow, Steel, Skin;
        }

        private UnitPrefabFactory(VfxFactory vfx) => this.vfx = vfx;

        public static UnitPrefabFactory Build(VfxFactory vfx)
        {
            var f = new UnitPrefabFactory(vfx);
            Palette dawn = new Palette
            {
                Armor = U.Lit("M_Unit_Armor_Dawn", new Color(0.86f, 0.82f, 0.72f), 0.45f, 0.35f),
                Trim = U.Lit("M_Unit_Trim_Dawn", new Color(0.9f, 0.68f, 0.28f), 0.6f, 0.8f),
                Cloth = U.Lit("M_Unit_Cloth_Dawn", new Color(0.16f, 0.32f, 0.56f), 0.1f),
                Glow = U.Lit("M_Unit_Glow_Dawn", new Color(1f, 0.8f, 0.4f), 0.7f, 0f, new Color(1f, 0.72f, 0.28f), 2.2f),
                Steel = U.Lit("M_Unit_Steel", new Color(0.55f, 0.57f, 0.6f), 0.55f, 0.7f),
                Skin = U.Lit("M_Unit_Leather", new Color(0.36f, 0.25f, 0.17f), 0.15f)
            };
            Palette dusk = new Palette
            {
                Armor = U.Lit("M_Unit_Armor_Dusk", new Color(0.3f, 0.28f, 0.36f), 0.45f, 0.4f),
                Trim = U.Lit("M_Unit_Trim_Dusk", new Color(0.55f, 0.18f, 0.3f), 0.5f, 0.6f),
                Cloth = U.Lit("M_Unit_Cloth_Dusk", new Color(0.32f, 0.12f, 0.36f), 0.1f),
                Glow = U.Lit("M_Unit_Glow_Dusk", new Color(0.75f, 0.45f, 1f), 0.7f, 0f, new Color(0.72f, 0.3f, 1f), 2.4f),
                Steel = dawn.Steel,
                Skin = dawn.Skin
            };
            var selRing = U.Indicator("M_Unit_SelectionRing", U.FxTexture("sel_ring.png"), new Color(1f, 0.84f, 0.5f, 0.85f), onTop: false);

            f.SunWarden = f.BuildSunWarden(dawn, selRing);
            f.MeleeDawn = f.BuildMelee("Creep_Melee_Dawn", dawn, TeamSide.Dawn);
            f.MeleeDusk = f.BuildMelee("Creep_Melee_Dusk", dusk, TeamSide.Dusk);
            f.RangedDawn = f.BuildRanged("Creep_Ranged_Dawn", dawn, TeamSide.Dawn);
            f.RangedDusk = f.BuildRanged("Creep_Ranged_Dusk", dusk, TeamSide.Dusk);
            return f;
        }

        private static Matrix4x4 At(float x, float y, float z) => B.TRS(new Vector3(x, y, z));
        private static Matrix4x4 At(Vector3 p, Vector3 euler, Vector3 scale) => B.TRS(p, Quaternion.Euler(euler), scale);

        private static GameObject Part(Transform parent, string name, Mesh mesh, params Material[] mats)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = U.SaveMesh(mesh);
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            r.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        // ================= Герой: Aurel, the Sun Warden =================

        private GameObject BuildSunWarden(Palette p, Material selRing)
        {
            var root = new GameObject("Hero_SunWarden");
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localScale = Vector3.one * 1.18f;

            // submeshes: 0 броня, 1 золото, 2 ткань, 3 свечение, 4 сталь/кожа
            var b = new B(5);
            // ноги
            foreach (float x in new[] { -0.17f, 0.17f })
            {
                b.Frustum(4, At(x, 0f, 0f), 5, 0.15f, 0.13f, 0.5f);
                b.Frustum(0, At(x, 0.42f, 0f), 5, 0.15f, 0.17f, 0.5f);
                b.Frustum(1, At(x, 0.48f, 0.04f), 4, 0.1f, 0.05f, 0.14f);
            }
            // табард и пояс
            b.Frustum(2, At(0f, 0.55f, 0f), 6, 0.44f, 0.36f, 0.45f);
            b.Frustum(1, At(0f, 0.92f, 0f), 6, 0.38f, 0.38f, 0.1f);
            // торс (кираса)
            b.Frustum(0, At(0f, 1.0f, 0f), 6, 0.36f, 0.48f, 0.58f);
            b.Frustum(1, At(0f, 1.58f, 0f), 6, 0.48f, 0.3f, 0.1f);
            b.Crystal(3, At(0f, 1.28f, 0.42f), 4, 0.1f, 0.14f, 0.1f); // солнечная брошь
            // наплечники
            foreach (float x in new[] { -0.52f, 0.52f })
            {
                b.Frustum(1, At(new Vector3(x, 1.42f, 0f), new Vector3(0f, 0f, x > 0 ? -18f : 18f), Vector3.one), 6, 0.26f, 0.12f, 0.2f);
                b.Frustum(0, At(new Vector3(x, 1.3f, 0f), new Vector3(0f, 0f, x > 0 ? -18f : 18f), Vector3.one), 6, 0.2f, 0.27f, 0.14f);
            }
            // шлем с гребнем и забралом
            b.Frustum(4, At(0f, 1.62f, 0f), 5, 0.1f, 0.1f, 0.1f);
            b.Frustum(0, At(0f, 1.7f, 0f), 6, 0.2f, 0.22f, 0.24f);
            b.Frustum(0, At(0f, 1.94f, 0f), 6, 0.22f, 0.06f, 0.18f);
            b.Frustum(3, At(new Vector3(0f, 1.8f, 0.17f), new Vector3(0f, 0f, 0f), new Vector3(1f, 1f, 0.4f)), 4, 0.13f, 0.13f, 0.05f);
            b.Crystal(1, At(new Vector3(0f, 2.12f, -0.02f), Vector3.zero, new Vector3(0.35f, 1f, 1f)), 4, 0.16f, 0.26f, 0.08f);
            // плащ
            b.Frustum(2, At(new Vector3(0f, 0.35f, -0.36f), new Vector3(-8f, 0f, 0f), new Vector3(1f, 1f, 0.14f)), 4, 0.42f, 0.36f, 1.22f, angleOffsetDeg: 45f);
            // щит на левой руке
            b.Frustum(4, At(-0.56f, 0.95f, 0.05f), 5, 0.09f, 0.1f, 0.45f);
            b.Frustum(0, At(new Vector3(-0.66f, 1.05f, 0.22f), new Vector3(90f, -10f, 0f), Vector3.one), 6, 0.36f, 0.32f, 0.07f);
            b.Frustum(1, At(new Vector3(-0.66f, 1.05f, 0.29f), new Vector3(90f, -10f, 0f), Vector3.one), 6, 0.3f, 0.12f, 0.05f);
            b.Crystal(3, At(new Vector3(-0.67f, 1.05f, 0.34f), new Vector3(90f, 0f, 0f), Vector3.one), 6, 0.09f, 0.06f, 0.02f);
            Part(model, "Body", b.Build("Hero_SunWarden_Body"), p.Armor, p.Trim, p.Cloth, p.Glow, p.Skin);

            // рука с копьём на шарнире плеча
            var pivot = new GameObject("WeaponPivot").transform;
            pivot.SetParent(model, false);
            pivot.localPosition = new Vector3(0.55f, 1.38f, 0.02f);
            var w = new B(4);
            w.Frustum(0, At(new Vector3(0f, 0f, 0f), new Vector3(180f, 0f, 0f), Vector3.one), 5, 0.12f, 0.1f, 0.55f); // плечо → кисть
            w.Frustum(3, At(new Vector3(0f, -0.55f, -0.55f), new Vector3(80f, 0f, 0f), Vector3.one), 5, 0.035f, 0.035f, 2.0f); // древко
            w.Frustum(1, At(new Vector3(0f, -0.203f, 1.42f), new Vector3(80f, 0f, 0f), Vector3.one), 6, 0.09f, 0.06f, 0.12f); // гарда
            w.Crystal(2, At(new Vector3(0f, -0.168f, 1.617f), new Vector3(80f, 0f, 0f), Vector3.one), 4, 0.1f, 0.42f, 0.08f); // солнечный наконечник
            w.Frustum(1, At(new Vector3(0f, -0.62f, 0f), new Vector3(0f, 0f, 0f), Vector3.one), 5, 0.09f, 0.09f, 0.12f); // кулак
            Part(pivot, "Spear", w.Build("Hero_SunWarden_Spear"), p.Armor, p.Trim, p.Glow, p.Skin);

            AddSelectionRing(root.transform, selRing, 1.5f);

            // Компоненты героя
            var unit = root.AddComponent<Unit>();
            var agent = AddAgent(root, 0.45f, 2f, 6f, 30);
            root.AddComponent<UnitMotor>();
            var attack = root.AddComponent<UnitAttack>();
            U.Set(attack, "impactVfx", vfx.HitSpark);
            var anim = root.AddComponent<UnitAnimator>();
            anim.Configure(model, pivot);
            U.Set(anim, "model", model);
            U.Set(anim, "weaponPivot", pivot);
            root.AddComponent<HeroAbilities>();
            root.AddComponent<HeroController>();
            root.AddComponent<Hero>();
            var death = root.AddComponent<UnitDeathHandler>();
            death.DestroyDelay = 0f;
            death.DeathVfx = vfx.UnitDeath;

            var so = new SerializedObject(unit);
            so.FindProperty("displayName").stringValue = "Aurel";
            so.FindProperty("kind").enumValueIndex = (int)UnitKind.Hero;
            so.FindProperty("radius").floatValue = 0.5f;
            so.FindProperty("barHeight").floatValue = 3.2f;
            so.ApplyModifiedPropertiesWithoutUndo();
            agent.enabled = false;

            return U.SavePrefab(root, U.UnitsPrefabDir);
        }

        // ================= Крипы =================

        private GameObject BuildMelee(string name, Palette p, TeamSide side)
        {
            var root = new GameObject(name);
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localScale = Vector3.one * 1.05f;

            var b = new B(4);
            foreach (float x in new[] { -0.15f, 0.15f })
                b.Frustum(3, At(x, 0f, 0f), 4, 0.13f, 0.12f, 0.55f, angleOffsetDeg: 45f);
            b.Frustum(1, At(0f, 0.5f, 0f), 6, 0.4f, 0.33f, 0.4f);          // табард
            b.Frustum(0, At(0f, 0.85f, 0f), 6, 0.36f, 0.42f, 0.5f);         // кираса
            b.Frustum(0, At(0f, 1.35f, 0f), 6, 0.42f, 0.26f, 0.12f);
            b.Frustum(0, At(0f, 1.45f, 0f), 5, 0.19f, 0.2f, 0.2f);          // шлем
            b.Frustum(2, At(0f, 1.65f, 0f), 5, 0.2f, 0.02f, 0.16f);
            // круглый щит
            b.Frustum(0, At(new Vector3(-0.48f, 0.95f, 0.18f), new Vector3(90f, -15f, 0f), Vector3.one), 7, 0.32f, 0.3f, 0.07f);
            b.Frustum(2, At(new Vector3(-0.49f, 0.95f, 0.25f), new Vector3(90f, -15f, 0f), Vector3.one), 7, 0.12f, 0.05f, 0.05f);
            Part(model, "Body", b.Build($"{name}_Body"), p.Armor, p.Cloth, p.Trim, p.Steel);

            var pivot = new GameObject("WeaponPivot").transform;
            pivot.SetParent(model, false);
            pivot.localPosition = new Vector3(0.46f, 1.22f, 0f);
            var w = new B(2);
            w.Frustum(1, At(new Vector3(0f, 0f, 0f), new Vector3(180f, 0f, 0f), Vector3.one), 5, 0.1f, 0.09f, 0.45f);
            w.Frustum(0, At(new Vector3(0f, -0.48f, 0.05f), new Vector3(75f, 0f, 0f), new Vector3(1f, 1f, 0.35f)), 4, 0.09f, 0.03f, 0.95f, angleOffsetDeg: 45f);
            Part(pivot, "Sword", w.Build($"{name}_Sword"), p.Steel, p.Armor);

            SetupCreep(root, model, pivot, 0.45f, 5f, 2.3f);
            return U.SavePrefab(root, U.UnitsPrefabDir);
        }

        private GameObject BuildRanged(string name, Palette p, TeamSide side)
        {
            var root = new GameObject(name);
            var model = new GameObject("Model").transform;
            model.SetParent(root.transform, false);
            model.localScale = Vector3.one * 1.05f;

            var b = new B(4);
            b.Frustum(1, At(0f, 0f, 0f), 7, 0.42f, 0.24f, 1.15f);   // мантия
            b.Frustum(0, At(0f, 0.75f, 0f), 6, 0.3f, 0.36f, 0.5f);   // накидка
            b.Frustum(2, At(0f, 1.18f, 0f), 6, 0.36f, 0.2f, 0.12f);
            b.Frustum(1, At(0f, 1.25f, 0f), 6, 0.24f, 0.05f, 0.55f); // капюшон
            b.Frustum(3, At(new Vector3(0f, 1.42f, 0.14f), Vector3.zero, new Vector3(1f, 1f, 0.5f)), 5, 0.1f, 0.1f, 0.05f); // глаза
            Part(model, "Body", b.Build($"{name}_Body"), p.Armor, p.Cloth, p.Trim, p.Glow);

            var pivot = new GameObject("WeaponPivot").transform;
            pivot.SetParent(model, false);
            pivot.localPosition = new Vector3(0.42f, 1.05f, 0.05f);
            var w = new B(3);
            w.Frustum(0, At(new Vector3(0f, -0.9f, 0f), Vector3.zero, Vector3.one), 5, 0.04f, 0.035f, 1.9f); // посох
            w.Frustum(1, At(new Vector3(0f, 0.95f, 0f), Vector3.zero, Vector3.one), 6, 0.06f, 0.12f, 0.12f);
            w.Crystal(2, At(new Vector3(0f, 1.25f, 0f), Vector3.zero, Vector3.one), 5, 0.12f, 0.22f, 0.12f);
            Part(pivot, "Staff", w.Build($"{name}_Staff"), p.Skin, p.Trim, p.Glow);

            var origin = new GameObject("ProjectileOrigin").transform;
            origin.SetParent(pivot, false);
            origin.localPosition = new Vector3(0f, 1.28f, 0f);

            SetupCreep(root, model, pivot, 0.45f, 5f, 2.3f);
            var attack = root.GetComponent<UnitAttack>();
            U.Set(attack, "projectilePrefab", vfx.ProjectileFor(side, false));
            U.Set(attack, "projectileOrigin", origin);
            U.Set(attack, "impactVfx", null);
            var anim = root.GetComponent<UnitAnimator>();
            U.Set(anim, "swingBackAngle", -35f);
            U.Set(anim, "swingStrikeAngle", 30f);
            return U.SavePrefab(root, U.UnitsPrefabDir);
        }

        private UnitDeathHandler SetupCreep(GameObject root, Transform model, Transform pivot, float radius, float speed, float barHeight)
        {
            var unit = root.AddComponent<Unit>();
            var agent = AddAgent(root, radius, 1.8f, speed, 50);
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            root.AddComponent<UnitMotor>();
            var attack = root.AddComponent<UnitAttack>();
            U.Set(attack, "impactVfx", vfx.HitSpark);
            var anim = root.AddComponent<UnitAnimator>();
            U.Set(anim, "model", model);
            U.Set(anim, "weaponPivot", pivot);
            root.AddComponent<CreepAI>();
            var death = root.AddComponent<UnitDeathHandler>();
            death.DestroyDelay = 3.2f;
            death.DeathVfx = vfx.UnitDeath;

            var so = new SerializedObject(unit);
            so.FindProperty("kind").enumValueIndex = (int)UnitKind.Creep;
            so.FindProperty("radius").floatValue = radius;
            so.FindProperty("barHeight").floatValue = barHeight;
            so.ApplyModifiedPropertiesWithoutUndo();
            agent.enabled = false;
            return death;
        }

        private static NavMeshAgent AddAgent(GameObject root, float radius, float height, float speed, int priority)
        {
            var agent = root.AddComponent<NavMeshAgent>();
            agent.radius = radius;
            agent.height = height;
            agent.baseOffset = 0f;
            agent.speed = speed;
            agent.acceleration = 60f;
            agent.angularSpeed = 0f;
            agent.stoppingDistance = 0.1f;
            agent.autoBraking = true;
            agent.avoidancePriority = priority;
            agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
            return agent;
        }

        private void AddSelectionRing(Transform parent, Material mat, float size)
        {
            var ring = new GameObject("SelectionRing");
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = Vector3.up * 0.06f;
            ring.transform.localScale = new Vector3(size, 1f, size);
            ring.AddComponent<MeshFilter>().sharedMesh = vfx.GroundQuad;
            var r = ring.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
