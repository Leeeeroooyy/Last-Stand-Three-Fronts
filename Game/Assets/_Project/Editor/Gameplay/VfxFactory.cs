using System;
using LastStand.Core;
using LastStand.Map;
using LastStand.Units;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using U = LastStand.EditorTools.Gameplay.EditorAssetUtil;

namespace LastStand.EditorTools.Gameplay
{
    /// <summary>
    /// Собирает префабы визуальных эффектов из систем частиц (Prefabs/VFX).
    /// Художник может заменить любой префаб своим — ссылки в способностях останутся.
    /// </summary>
    public sealed class VfxFactory
    {
        public GameObject HitSpark, FireBurst, LanceProjectile, LeapTakeoff, LeapLand, AegisAura, SunfallTelegraph,
            SunfallImpact, Burn, Stun, StructureDestroyed, UnitDeath;
        public GameObject CreepProjectileDawn, CreepProjectileDusk, TowerProjectileDawn, TowerProjectileDusk;
        public Mesh GroundQuad;

        private Material softAdd, softAlpha, streakAdd, ringAdd, starAdd, glyphQuad, auraQuad, trailAdd;
        private Mesh crystal, rock;
        private Material rockMat;

        // Палитра эффектов
        private static readonly Color Sun = new Color(1f, 0.82f, 0.45f);
        private static readonly Color SunHot = new Color(1f, 0.95f, 0.8f);
        private static readonly Color Fire = new Color(1f, 0.48f, 0.16f);
        private static readonly Color Ember = new Color(0.9f, 0.22f, 0.08f);
        private static readonly Color Dust = new Color(0.55f, 0.5f, 0.43f, 0.55f);
        private static readonly Color DuskGlow = new Color(0.72f, 0.38f, 1f);

        public static VfxFactory Build()
        {
            var f = new VfxFactory();
            f.LoadShared();
            f.BuildAll();
            return f;
        }

        private void LoadShared()
        {
            softAdd = U.Particle("M_FX_Soft_Add", U.FxTexture("fx_soft.png"));
            softAlpha = U.Particle("M_FX_Soft_Alpha", U.FxTexture("fx_soft.png"), additive: false);
            streakAdd = U.Particle("M_FX_Streak_Add", U.FxTexture("fx_streak.png"));
            ringAdd = U.Particle("M_FX_Ring_Add", U.FxTexture("fx_ring.png"));
            starAdd = U.Particle("M_FX_Star_Add", U.FxTexture("fx_star.png"));
            trailAdd = U.Particle("M_FX_Trail_Add", U.FxTexture("fx_soft.png"));
            glyphQuad = U.Indicator("M_FX_SunGlyph", U.FxTexture("fx_glyph.png"), new Color(1f, 0.72f, 0.3f, 0.95f), onTop: true, additive: true);
            auraQuad = U.Indicator("M_FX_AegisRing", U.FxTexture("sel_ring.png"), new Color(1f, 0.82f, 0.45f, 0.8f), onTop: false, additive: true);

            crystal = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Generated/Meshes/Crystal_Small.asset");
            rock = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Generated/Meshes/Rock_A.asset");
            rockMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Generated/Materials/M_Rock.mat");

            GroundQuad = new Mesh { name = "GroundQuad" };
            GroundQuad.SetVertices(new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f)
            });
            GroundQuad.SetUVs(0, new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            GroundQuad.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            GroundQuad.RecalculateNormals();
            GroundQuad.RecalculateBounds();
            GroundQuad = U.SaveMesh(GroundQuad);
        }

        private void BuildAll()
        {
            HitSpark = Save(BuildHitSpark());
            FireBurst = Save(BuildFireBurst());
            LanceProjectile = Save(BuildLance());
            LeapTakeoff = Save(BuildTakeoff());
            LeapLand = Save(BuildLand());
            AegisAura = Save(BuildAegis());
            SunfallTelegraph = Save(BuildTelegraph());
            SunfallImpact = Save(BuildSunfall());
            Burn = Save(BuildBurn());
            Stun = Save(BuildStun());
            StructureDestroyed = Save(BuildStructureDestroyed());
            UnitDeath = Save(BuildUnitDeath());
            CreepProjectileDawn = Save(BuildOrb("VFX_Projectile_Creep_Dawn", Sun, 0.22f, 0.22f, false));
            CreepProjectileDusk = Save(BuildOrb("VFX_Projectile_Creep_Dusk", DuskGlow, 0.22f, 0.22f, false));
            TowerProjectileDawn = Save(BuildOrb("VFX_Projectile_Tower_Dawn", Sun, 0.42f, 0.4f, true));
            TowerProjectileDusk = Save(BuildOrb("VFX_Projectile_Tower_Dusk", DuskGlow, 0.42f, 0.4f, true));
        }

        private static GameObject Save(GameObject go) => U.SavePrefab(go, U.VfxPrefabDir);

        // ================= Помощники частиц =================

        private static GameObject Root(string name, float lifetime)
        {
            var go = new GameObject(name);
            if (lifetime >= 0f)
                go.AddComponent<VfxLifetime>().Lifetime = lifetime;
            return go;
        }

        private static ParticleSystem PS(GameObject parent, string name, Material mat, Action<ParticleSystem> setup,
            ParticleSystemRenderMode mode = ParticleSystemRenderMode.Billboard)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = mode;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.Distance;
            if (mode == ParticleSystemRenderMode.Stretch)
            {
                r.velocityScale = 0.06f;
                r.lengthScale = 1.6f;
            }

            var main = ps.main;
            main.playOnAwake = true;
            main.loop = false;
            main.duration = 1f;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = 1f;
            main.maxParticles = 200;
            var em = ps.emission;
            em.rateOverTime = 0f;
            var shape = ps.shape;
            shape.enabled = false;
            setup(ps);
            return ps;
        }

        private static void Burst(ParticleSystem ps, int count, float time = 0f)
        {
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(time, (short)count) });
        }

        private static ParticleSystem.MinMaxGradient Fade(Color a, Color b) => new ParticleSystem.MinMaxGradient(Grad(a, b));

        private static Gradient Grad(Color a, Color b, float fadeIn = 0.05f)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(a, 0f), new GradientColorKey(b, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(a.a, fadeIn), new GradientAlphaKey(b.a * 0.6f, 0.6f), new GradientAlphaKey(0f, 1f) });
            return g;
        }

        private static void ColorOverLife(ParticleSystem ps, Color a, Color b, float fadeIn = 0.05f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(Grad(a, b, fadeIn));
        }

        private static void SizeOverLife(ParticleSystem ps, float start, float end, float peak = -1f)
        {
            var sol = ps.sizeOverLifetime;
            sol.enabled = true;
            AnimationCurve c = peak > 0f
                ? new AnimationCurve(new Keyframe(0f, start), new Keyframe(peak, 1f), new Keyframe(1f, end))
                : AnimationCurve.EaseInOut(0f, start, 1f, end);
            sol.size = new ParticleSystem.MinMaxCurve(1f, c);
        }

        private static ParticleSystem.MinMaxCurve Range(float a, float b) => new ParticleSystem.MinMaxCurve(a, b);

        private static void Sphere(ParticleSystem ps, float radius)
        {
            var s = ps.shape;
            s.enabled = true;
            s.shapeType = ParticleSystemShapeType.Sphere;
            s.radius = radius;
        }

        private static void Circle(ParticleSystem ps, float radius, float thickness = 1f)
        {
            var s = ps.shape;
            s.enabled = true;
            s.shapeType = ParticleSystemShapeType.Circle;
            s.radius = radius;
            s.radiusThickness = thickness;
            s.rotation = new Vector3(-90f, 0f, 0f); // плоскость XZ, частицы разлетаются горизонтально
        }

        private static void Cone(ParticleSystem ps, float angle, float radius)
        {
            var s = ps.shape;
            s.enabled = true;
            s.shapeType = ParticleSystemShapeType.Cone;
            s.angle = angle;
            s.radius = radius;
            s.rotation = new Vector3(-90f, 0f, 0f); // вверх
        }

        private ParticleSystem Flash(GameObject root, float size, Color color, float life = 0.18f)
        {
            return PS(root, "Flash", softAdd, ps =>
            {
                var m = ps.main;
                m.startLifetime = life;
                m.startSize = size;
                m.startColor = color;
                Burst(ps, 1);
                SizeOverLife(ps, 0.6f, 1.3f);
                ColorOverLife(ps, color, color, 0.01f);
            });
        }

        private ParticleSystem Sparks(GameObject root, int count, float speedMin, float speedMax, Color color, float life = 0.35f, float gravity = 0.6f)
        {
            return PS(root, "Sparks", streakAdd, ps =>
            {
                var m = ps.main;
                m.startLifetime = Range(life * 0.6f, life);
                m.startSpeed = Range(speedMin, speedMax);
                m.startSize = Range(0.12f, 0.24f);
                m.startColor = color;
                m.gravityModifier = gravity;
                Burst(ps, count);
                Sphere(ps, 0.15f);
                ColorOverLife(ps, SunHot, color, 0.01f);
                SizeOverLife(ps, 1f, 0.2f);
            }, ParticleSystemRenderMode.Stretch);
        }

        private ParticleSystem GroundRing(GameObject root, float endSize, Color color, float life = 0.45f)
        {
            return PS(root, "Shockwave", ringAdd, ps =>
            {
                var m = ps.main;
                m.startLifetime = life;
                m.startSize = endSize;
                m.startColor = color;
                Burst(ps, 1);
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.08f, 0f, 4f), new Keyframe(1f, 1f, 0f, 0f)));
                ColorOverLife(ps, color, color, 0.01f);
            }, ParticleSystemRenderMode.HorizontalBillboard);
        }

        private ParticleSystem DustPuff(GameObject root, int count, float radius, float speed, float size, float life = 0.9f)
        {
            return PS(root, "Dust", softAlpha, ps =>
            {
                var m = ps.main;
                m.startLifetime = Range(life * 0.7f, life);
                m.startSpeed = Range(speed * 0.5f, speed);
                m.startSize = Range(size * 0.6f, size);
                m.startColor = Dust;
                m.startRotation = Range(0f, Mathf.PI * 2f);
                Burst(ps, count);
                Circle(ps, radius);
                var lim = ps.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.dampen = 0.25f;
                lim.limit = 0.5f;
                SizeOverLife(ps, 0.6f, 1.5f);
                ColorOverLife(ps, Dust, Dust * new Color(1f, 1f, 1f, 0.6f), 0.1f);
            });
        }

        // ================= Эффекты =================

        private GameObject BuildHitSpark()
        {
            var root = Root("VFX_HitSpark", 0.7f);
            Flash(root, 0.9f, SunHot * new Color(1f, 1f, 1f, 0.8f), 0.12f);
            Sparks(root, 8, 3f, 7f, Sun, 0.3f, 0.3f);
            return root;
        }

        private GameObject BuildFireBurst()
        {
            var root = Root("VFX_FireBurst", 1.2f);
            Flash(root, 2.2f, new Color(1f, 0.75f, 0.4f, 0.9f));
            PS(root, "Flames", softAdd, ps =>
            {
                var m = ps.main;
                m.startLifetime = Range(0.3f, 0.55f);
                m.startSpeed = Range(1.5f, 3.5f);
                m.startSize = Range(0.5f, 0.9f);
                m.startColor = Fire;
                Burst(ps, 14);
                Sphere(ps, 0.3f);
                SizeOverLife(ps, 1f, 0.1f);
                ColorOverLife(ps, new Color(1f, 0.85f, 0.5f, 1f), Ember);
            });
            Sparks(root, 16, 4f, 9f, Fire, 0.55f, 0.4f);
            return root;
        }

        private GameObject BuildLance()
        {
            var root = Root("VFX_SolarLance", -1f);
            // кристаллическое остриё вдоль +Z
            if (crystal != null)
            {
                var tip = new GameObject("Spear");
                tip.transform.SetParent(root.transform, false);
                tip.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                tip.transform.localScale = new Vector3(0.32f, 1.5f, 0.32f);
                tip.AddComponent<MeshFilter>().sharedMesh = crystal;
                var mr = tip.AddComponent<MeshRenderer>();
                mr.sharedMaterial = U.Lit("M_FX_LanceCore", SunHot, 0.8f, 0f, new Color(1f, 0.7f, 0.3f), 3.5f);
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            PS(root, "Core", softAdd, ps =>
            {
                var m = ps.main;
                m.loop = true;
                m.simulationSpace = ParticleSystemSimulationSpace.Local;
                m.startLifetime = 0.12f;
                m.startSize = 1.8f;
                m.startColor = new Color(1f, 0.78f, 0.4f, 0.7f);
                var em = ps.emission;
                em.rateOverTime = 40f;
            });
            PS(root, "FireTrail", softAdd, ps =>
            {
                var m = ps.main;
                m.loop = true;
                m.startLifetime = Range(0.25f, 0.45f);
                m.startSize = Range(0.5f, 0.85f);
                m.startColor = Fire;
                var em = ps.emission;
                em.rateOverTime = 90f;
                Sphere(ps, 0.2f);
                SizeOverLife(ps, 1f, 0f);
                ColorOverLife(ps, new Color(1f, 0.9f, 0.6f, 1f), Ember);
            });
            PS(root, "Embers", streakAdd, ps =>
            {
                var m = ps.main;
                m.loop = true;
                m.startLifetime = Range(0.3f, 0.6f);
                m.startSpeed = Range(0.5f, 2f);
                m.startSize = Range(0.06f, 0.14f);
                m.startColor = Sun;
                var em = ps.emission;
                em.rateOverTime = 50f;
                Sphere(ps, 0.35f);
                ColorOverLife(ps, SunHot, Fire);
            }, ParticleSystemRenderMode.Stretch);
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailAdd;
            trail.time = 0.22f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.9f), new Keyframe(1f, 0f));
            trail.colorGradient = Grad(new Color(1f, 0.85f, 0.5f, 0.9f), new Color(1f, 0.4f, 0.1f, 0f), 0.001f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.minVertexDistance = 0.2f;
            return root;
        }

        private GameObject BuildTakeoff()
        {
            var root = Root("VFX_LeapTakeoff", 1.2f);
            DustPuff(root, 10, 0.5f, 3f, 1f, 0.7f);
            GroundRing(root, 3.2f, new Color(1f, 0.8f, 0.45f, 0.6f), 0.35f);
            return root;
        }

        private GameObject BuildLand()
        {
            var root = Root("VFX_LeapLand", 1.6f);
            Flash(root, 3.5f, new Color(1f, 0.8f, 0.45f, 0.75f), 0.2f);
            GroundRing(root, 7f, new Color(1f, 0.82f, 0.45f, 0.95f), 0.5f);
            DustPuff(root, 16, 1.2f, 5f, 1.4f);
            Sparks(root, 22, 5f, 10f, Sun, 0.5f, 1f);
            return root;
        }

        private GameObject BuildAegis()
        {
            var root = Root("VFX_SunAegis", -1f);
            var ring = new GameObject("Ring");
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = Vector3.up * 0.06f;
            ring.transform.localScale = new Vector3(2.4f, 1f, 2.4f);
            ring.AddComponent<MeshFilter>().sharedMesh = GroundQuad;
            var mr = ring.AddComponent<MeshRenderer>();
            mr.sharedMaterial = auraQuad;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            var spin = ring.AddComponent<CrystalFloat>();
            SetSpin(spin, 50f, 0f);

            PS(root, "Motes", softAdd, ps =>
            {
                var m = ps.main;
                m.loop = true;
                m.startLifetime = Range(0.9f, 1.4f);
                m.startSpeed = 0f;
                m.startSize = Range(0.1f, 0.2f);
                m.startColor = Sun;
                var em = ps.emission;
                em.rateOverTime = 16f;
                Circle(ps, 0.8f, 0f);
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.y = Range(0.8f, 1.8f);
                vel.x = Range(0f, 0f);
                vel.z = Range(0f, 0f);
                ColorOverLife(ps, SunHot, Sun, 0.2f);
            });
            PS(root, "Shimmer", softAdd, ps =>
            {
                var m = ps.main;
                m.loop = true;
                m.simulationSpace = ParticleSystemSimulationSpace.Local;
                m.startLifetime = 0.8f;
                m.startSize = Range(0.9f, 1.4f);
                m.startColor = new Color(1f, 0.75f, 0.35f, 0.18f);
                var em = ps.emission;
                em.rateOverTime = 7f;
                Sphere(ps, 0.4f);
                ps.transform.localPosition = Vector3.up * 1.1f;
                ColorOverLife(ps, new Color(1f, 0.8f, 0.4f, 0.22f), new Color(1f, 0.6f, 0.2f, 0.1f), 0.3f);
            });
            return root;
        }

        private GameObject BuildTelegraph()
        {
            // Диаметр 2 м при масштабе 1 — ability масштабирует по радиусу.
            var root = Root("VFX_SunfallTelegraph", -1f);
            var glyph = new GameObject("Glyph");
            glyph.transform.SetParent(root.transform, false);
            glyph.transform.localPosition = Vector3.up * 0.05f;
            glyph.transform.localScale = new Vector3(2f, 1f, 2f);
            glyph.AddComponent<MeshFilter>().sharedMesh = GroundQuad;
            var mr = glyph.AddComponent<MeshRenderer>();
            mr.sharedMaterial = glyphQuad;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            SetSpin(glyph.AddComponent<CrystalFloat>(), 45f, 0f);

            PS(root, "Gather", softAdd, ps =>
            {
                var m = ps.main;
                m.loop = true;
                m.simulationSpace = ParticleSystemSimulationSpace.Local;
                m.startLifetime = 0.6f;
                m.startSpeed = -1.6f;
                m.startSize = Range(0.08f, 0.16f);
                m.startColor = Sun;
                var em = ps.emission;
                em.rateOverTime = 50f;
                Circle(ps, 1f, 0f);
                ColorOverLife(ps, Sun, SunHot, 0.2f);
            });
            PS(root, "Beam", softAdd, ps =>
            {
                var m = ps.main;
                m.loop = true;
                m.simulationSpace = ParticleSystemSimulationSpace.Local;
                m.startLifetime = 0.5f;
                m.startSize3D = true;
                m.startSizeX = 0.35f;
                m.startSizeY = 9f;
                m.startSizeZ = 1f;
                m.startColor = new Color(1f, 0.8f, 0.45f, 0.25f);
                var em = ps.emission;
                em.rateOverTime = 6f;
                ps.transform.localPosition = Vector3.up * 4.5f;
                ColorOverLife(ps, new Color(1f, 0.85f, 0.5f, 0.3f), new Color(1f, 0.6f, 0.2f, 0.1f), 0.3f);
            }, ParticleSystemRenderMode.VerticalBillboard);
            return root;
        }

        private GameObject BuildSunfall()
        {
            // Рассчитан на радиус 4 м при масштабе 1.
            var root = Root("VFX_SunfallImpact", 2.6f);
            PS(root, "Pillar", softAdd, ps =>
            {
                var m = ps.main;
                m.startLifetime = 0.55f;
                m.startSize3D = true;
                m.startSizeX = 3.2f;
                m.startSizeY = 26f;
                m.startSizeZ = 1f;
                m.startColor = new Color(1f, 0.88f, 0.6f, 1f);
                Burst(ps, 1);
                ps.transform.localPosition = Vector3.up * 11f;
                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                sol.separateAxes = true;
                sol.x = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0.1f)));
                sol.y = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
                sol.z = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Constant(0f, 1f, 1f));
                ColorOverLife(ps, SunHot, Sun, 0.01f);
            }, ParticleSystemRenderMode.VerticalBillboard);
            Flash(root, 9f, new Color(1f, 0.82f, 0.5f, 0.85f), 0.3f);
            GroundRing(root, 10f, new Color(1f, 0.75f, 0.35f, 1f), 0.6f);
            PS(root, "Flames", softAdd, ps =>
            {
                var m = ps.main;
                m.startLifetime = Range(0.5f, 0.9f);
                m.startSpeed = Range(1f, 4f);
                m.startSize = Range(1f, 1.8f);
                m.startColor = Fire;
                Burst(ps, 30);
                Circle(ps, 3f);
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = Range(0f, 0f);
                vel.z = Range(0f, 0f);
                vel.y = Range(1.5f, 3.5f);
                SizeOverLife(ps, 1f, 0.2f);
                ColorOverLife(ps, new Color(1f, 0.9f, 0.6f, 1f), Ember);
            });
            Sparks(root, 50, 6f, 14f, Sun, 0.9f, 1.2f);
            DustPuff(root, 20, 3f, 6f, 2.4f, 1.4f);
            return root;
        }

        private GameObject BuildBurn()
        {
            var root = Root("VFX_Burn", -1f);
            PS(root, "Flames", softAdd, ps =>
            {
                var m = ps.main;
                m.loop = true;
                m.startLifetime = Range(0.35f, 0.6f);
                m.startSpeed = 0f;
                m.startSize = Range(0.25f, 0.45f);
                m.startColor = Fire;
                var em = ps.emission;
                em.rateOverTime = 22f;
                Sphere(ps, 0.35f);
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.World;
                vel.x = Range(0f, 0f);
                vel.z = Range(0f, 0f);
                vel.y = Range(0.8f, 1.6f);
                SizeOverLife(ps, 1f, 0f);
                ColorOverLife(ps, new Color(1f, 0.85f, 0.5f, 1f), Ember);
            });
            return root;
        }

        private GameObject BuildStun()
        {
            var root = Root("VFX_Stun", -1f);
            PS(root, "Stars", starAdd, ps =>
            {
                var m = ps.main;
                m.loop = true;
                m.duration = 1f;
                m.simulationSpace = ParticleSystemSimulationSpace.Local;
                m.startLifetime = 1.02f;
                m.startSize = 0.32f;
                m.startColor = new Color(1f, 0.88f, 0.55f, 1f);
                Burst(ps, 3);
                Circle(ps, 0.45f, 0f);
                var shape = ps.shape;
                shape.arcMode = ParticleSystemShapeMultiModeValue.BurstSpread;
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space = ParticleSystemSimulationSpace.Local;
                vel.orbitalY = new ParticleSystem.MinMaxCurve(5f);
                vel.x = new ParticleSystem.MinMaxCurve(0f);
                vel.y = new ParticleSystem.MinMaxCurve(0f);
                vel.z = new ParticleSystem.MinMaxCurve(0f);
            });
            return root;
        }

        private GameObject BuildStructureDestroyed()
        {
            var root = Root("VFX_StructureDestroyed", 3.5f);
            Flash(root, 6f, new Color(1f, 0.8f, 0.5f, 0.8f), 0.3f);
            DustPuff(root, 34, 2f, 6f, 3.5f, 2.4f);
            Sparks(root, 30, 6f, 12f, Sun, 0.8f, 1.2f);
            if (rock != null && rockMat != null)
            {
                PS(root, "Debris", rockMat, ps =>
                {
                    var m = ps.main;
                    m.startLifetime = Range(0.9f, 1.4f);
                    m.startSpeed = Range(5f, 10f);
                    m.startSize = Range(0.25f, 0.6f);
                    m.startRotation3D = true;
                    m.startRotationX = Range(0f, Mathf.PI * 2f);
                    m.startRotationY = Range(0f, Mathf.PI * 2f);
                    m.startRotationZ = Range(0f, Mathf.PI * 2f);
                    m.gravityModifier = 2f;
                    Burst(ps, 16);
                    Cone(ps, 40f, 1.5f);
                    var rot = ps.rotationOverLifetime;
                    rot.enabled = true;
                    rot.z = Range(-6f, 6f);
                    var r = ps.GetComponent<ParticleSystemRenderer>();
                    r.mesh = rock;
                    r.shadowCastingMode = ShadowCastingMode.On;
                }, ParticleSystemRenderMode.Mesh);
            }
            return root;
        }

        private GameObject BuildUnitDeath()
        {
            var root = Root("VFX_UnitDeath", 1.6f);
            DustPuff(root, 8, 0.3f, 1.5f, 0.9f, 0.8f);
            PS(root, "Motes", softAdd, ps =>
            {
                var m = ps.main;
                m.startLifetime = Range(0.8f, 1.3f);
                m.startSpeed = Range(0.6f, 1.6f);
                m.startSize = Range(0.08f, 0.16f);
                m.startColor = Sun;
                Burst(ps, 12);
                Cone(ps, 25f, 0.4f);
                ColorOverLife(ps, SunHot, Sun, 0.1f);
            });
            return root;
        }

        /// <summary>Снаряд автоатаки: светящийся кристалл, шлейф и искры.</summary>
        private GameObject BuildOrb(string name, Color color, float size, float trailWidth, bool tower)
        {
            var root = Root(name, -1f);
            root.AddComponent<Projectile>();
            if (crystal != null)
            {
                var core = new GameObject("Core");
                core.transform.SetParent(root.transform, false);
                core.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                core.transform.localScale = new Vector3(size, size * 1.8f, size);
                core.AddComponent<MeshFilter>().sharedMesh = crystal;
                var mr = core.AddComponent<MeshRenderer>();
                string matName = color == Sun ? "M_FX_Orb_Dawn" : "M_FX_Orb_Dusk";
                mr.sharedMaterial = U.Lit(matName, Color.Lerp(color, Color.white, 0.4f), 0.8f, 0f, color, 3f);
                mr.shadowCastingMode = ShadowCastingMode.Off;
            }
            PS(root, "Glow", softAdd, ps =>
            {
                var m = ps.main;
                m.loop = true;
                m.simulationSpace = ParticleSystemSimulationSpace.Local;
                m.startLifetime = 0.15f;
                m.startSize = size * 4.5f;
                m.startColor = color * new Color(1f, 1f, 1f, 0.6f);
                var em = ps.emission;
                em.rateOverTime = 30f;
            });
            if (tower)
            {
                PS(root, "Sparks", softAdd, ps =>
                {
                    var m = ps.main;
                    m.loop = true;
                    m.startLifetime = Range(0.2f, 0.4f);
                    m.startSize = Range(0.08f, 0.16f);
                    m.startColor = color;
                    var em = ps.emission;
                    em.rateOverTime = 40f;
                    Sphere(ps, 0.2f);
                });
            }
            var trail = root.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailAdd;
            trail.time = tower ? 0.28f : 0.18f;
            trail.widthCurve = new AnimationCurve(new Keyframe(0f, trailWidth), new Keyframe(1f, 0f));
            trail.colorGradient = Grad(Color.Lerp(color, Color.white, 0.3f), color * new Color(1f, 1f, 1f, 0f), 0.001f);
            trail.shadowCastingMode = ShadowCastingMode.Off;
            trail.minVertexDistance = 0.15f;
            return root;
        }

        private static void SetSpin(CrystalFloat spin, float speed, float bob)
        {
            var so = new SerializedObject(spin);
            so.FindProperty("spinSpeed").floatValue = speed;
            so.FindProperty("bobAmplitude").floatValue = bob;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public GameObject ProjectileFor(TeamSide side, bool tower) =>
            tower ? (side == TeamSide.Dusk ? TowerProjectileDusk : TowerProjectileDawn)
                  : (side == TeamSide.Dusk ? CreepProjectileDusk : CreepProjectileDawn);
    }
}
