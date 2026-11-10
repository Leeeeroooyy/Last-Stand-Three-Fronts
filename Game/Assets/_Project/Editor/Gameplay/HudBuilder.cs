using System.IO;
using LastStand.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.UI;
using U = LastStand.EditorTools.Gameplay.EditorAssetUtil;

namespace LastStand.EditorTools.Gameplay
{
    /// <summary>
    /// Шрифты TextMeshPro, тема UI и префабы интерфейса (HUD, полоска над юнитом) в стиле Art Deco Fantasy.
    /// </summary>
    public sealed class HudBuilder
    {
        public const string EssentialsSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string EssentialsPackage = "Packages/com.unity.ugui/Package Resources/TMP Essential Resources.unitypackage";

        public UiTheme Theme;
        public GameObject HudPrefab;
        public WorldBarView WorldBarPrefab;

        private TMP_FontAsset titleFont, bodyFont, boldFont, nameFont;
        private Material titleGlow, bodyShadow;

        // ================= TMP Essentials =================

        public static bool EssentialsReady => File.Exists(EssentialsSettingsPath);

        /// <summary>Импортирует TMP Essential Resources из пакета uGUI (локально, без сети).</summary>
        public static bool ImportEssentials()
        {
            string full = Path.GetFullPath(EssentialsPackage);
            if (!File.Exists(full))
            {
                Debug.LogError($"[Last Stand] Не найден пакет TMP Essentials: {full}");
                return false;
            }
#pragma warning disable CS0618 // событие importPackageCompleted работает с этим API
            AssetDatabase.ImportPackage(full, false);
#pragma warning restore CS0618
            return true;
        }

        // ================= Сборка =================

        public static HudBuilder Build()
        {
            var b = new HudBuilder();
            b.BuildFonts();
            b.BuildTheme();
            b.WorldBarPrefab = b.BuildWorldBar();
            b.HudPrefab = b.BuildHud();
            return b;
        }

        private void BuildFonts()
        {
            titleFont = FontAsset($"{U.FontsDir}/Pagella-Bold.otf", "Pagella Bold SDF");
            nameFont = FontAsset($"{U.FontsDir}/Pagella-Regular.otf", "Pagella Regular SDF");
            boldFont = FontAsset($"{U.FontsDir}/Poppins-Bold.ttf", "Poppins Bold SDF");
            bodyFont = FontAsset($"{U.FontsDir}/Poppins-Medium.ttf", "Poppins Medium SDF");

            titleGlow = Preset(titleFont, "Pagella Bold SDF - Glow", m =>
            {
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_UnderlayColor", new Color(1f, 0.68f, 0.25f, 0.42f));
                m.SetFloat("_UnderlaySoftness", 0.75f);
                m.SetFloat("_UnderlayDilate", 0.35f);
                m.SetFloat("_UnderlayOffsetX", 0f);
                m.SetFloat("_UnderlayOffsetY", 0f);
            });
            bodyShadow = Preset(bodyFont, "Poppins Medium SDF - Shadow", m =>
            {
                m.EnableKeyword("UNDERLAY_ON");
                m.SetColor("_UnderlayColor", new Color(0f, 0f, 0.03f, 0.85f));
                m.SetFloat("_UnderlaySoftness", 0.35f);
                m.SetFloat("_UnderlayDilate", 0.2f);
                m.SetFloat("_UnderlayOffsetX", 0.4f);
                m.SetFloat("_UnderlayOffsetY", -0.6f);
            });
        }

        private static TMP_FontAsset FontAsset(string fontPath, string assetName)
        {
            string path = $"{U.FontsDir}/{assetName}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (existing != null)
                return existing;

            var font = AssetDatabase.LoadAssetAtPath<Font>(fontPath);
            if (font == null)
            {
                Debug.LogError($"[Last Stand] Шрифт не найден: {fontPath}");
                return TMP_Settings.defaultFontAsset;
            }

            TMP_FontAsset fa = TMP_FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 1024, 1024,
                AtlasPopulationMode.Dynamic, true);
            fa.name = assetName;
            AssetDatabase.CreateAsset(fa, path);
            for (int i = 0; i < fa.atlasTextures.Length; i++)
            {
                fa.atlasTextures[i].name = $"{assetName} Atlas {i}";
                AssetDatabase.AddObjectToAsset(fa.atlasTextures[i], fa);
            }
            fa.material.name = $"{assetName} Material";
            AssetDatabase.AddObjectToAsset(fa.material, fa);

            fa.TryAddCharacters("ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?-+/()[]'\"%·—–×ÁČĎÉĚÍŇÓŘŠŤÚŮÝŽáčďéěíňóřšťúůýž");
            EditorUtility.SetDirty(fa);
            AssetDatabase.SaveAssets();
            return fa;
        }

        private static Material Preset(TMP_FontAsset font, string name, System.Action<Material> setup)
        {
            string path = $"{U.FontsDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = m == null;
            if (create)
                m = new Material(font.material) { name = name };
            else
                m.CopyPropertiesFromMaterial(font.material);
            setup(m);
            if (create) AssetDatabase.CreateAsset(m, path);
            else EditorUtility.SetDirty(m);
            return m;
        }

        private void BuildTheme()
        {
            Theme = U.LoadOrCreate<UiTheme>($"{U.DataDir}/UiTheme.asset");
            Theme.titleFont = titleFont;
            Theme.bodyFont = bodyFont;
            EditorUtility.SetDirty(Theme);
        }

        // ================= Примитивы UI =================

        private static RectTransform Rect(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        private static RectTransform Stretch(string name, Transform parent, float inset = 0f)
        {
            RectTransform rt = Rect(name, parent, Vector2.zero, Vector2.zero, Vector2.zero);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset);
            rt.offsetMax = new Vector2(-inset, -inset);
            return rt;
        }

        private static Image Img(Transform parent, string name, string sprite, Color color, Vector2 pos, Vector2 size,
            bool sliced = false, bool raycast = false, Vector2? anchor = null)
        {
            RectTransform rt = Rect(name, parent, anchor ?? new Vector2(0.5f, 0.5f), pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite != null ? U.UiSprite(sprite) : null;
            img.color = color;
            img.type = sliced ? Image.Type.Sliced : Image.Type.Simple;
            img.raycastTarget = raycast;
            return img;
        }

        private TextMeshProUGUI Txt(Transform parent, string name, string text, TMP_FontAsset font, Material mat, float size,
            Color color, TextAlignmentOptions align, Vector2 pos, Vector2 box, float spacing = 0f, Vector2? anchor = null)
        {
            RectTransform rt = Rect(name, parent, anchor ?? new Vector2(0.5f, 0.5f), pos, box);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            if (mat != null) t.fontSharedMaterial = mat;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.text = text;
            return t;
        }

        private static Image Filled(Transform parent, string name, string sprite, Color color, float insetX, float insetY,
            Image.FillMethod method = Image.FillMethod.Horizontal)
        {
            RectTransform rt = Stretch(name, parent);
            rt.offsetMin = new Vector2(insetX, insetY);
            rt.offsetMax = new Vector2(-insetX, -insetY);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = U.UiSprite(sprite);
            img.color = color;
            img.type = Image.Type.Filled;
            img.fillMethod = method;
            img.fillOrigin = (int)Image.OriginHorizontal.Left;
            img.fillAmount = 1f;
            img.raycastTarget = false;
            return img;
        }

        // ================= Полоска над юнитом =================

        private WorldBarView BuildWorldBar()
        {
            RectTransform root = Rect("WorldBar", null, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, new Vector2(0.5f, 0f));
            root.gameObject.layer = LayerMask.NameToLayer("UI");

            Image health = Img(root, "Health", "world_bar.png", Color.white, Vector2.zero, new Vector2(120f, 12f), sliced: true);
            health.rectTransform.pivot = new Vector2(0.5f, 0f);
            Image trail = Filled(health.transform, "Trail", "thin_bar_fill.png", Theme.damageTrail, 7f, 2.5f);
            Image fill = Filled(health.transform, "Fill", "thin_bar_fill.png", Theme.allyHealth, 7f, 2.5f);

            Image mana = Img(root, "Mana", "world_bar.png", Color.white, new Vector2(0f, 1f), new Vector2(120f, 9f), sliced: true);
            mana.rectTransform.pivot = new Vector2(0.5f, 1f);
            Image manaFill = Filled(mana.transform, "Fill", "thin_bar_fill.png", Theme.mana, 7f, 2f);

            Image badge = Img(health.transform, "LevelBadge", "level_badge.png", Color.white, new Vector2(-14f, -1f), new Vector2(22f, 22f),
                anchor: new Vector2(0f, 0.5f));
            TextMeshProUGUI level = Txt(badge.transform, "Level", "1", nameFont, null, 13f, Theme.cream,
                TextAlignmentOptions.Center, new Vector2(0f, 0.5f), new Vector2(22f, 18f));

            Image shield = Img(health.transform, "Protected", "diamond.png", Theme.goldLight, new Vector2(-9f, 0f), new Vector2(14f, 14f),
                anchor: new Vector2(0f, 0.5f));
            shield.gameObject.SetActive(false);

            var view = root.gameObject.AddComponent<WorldBarView>();
            U.Set(view, "root", root);
            U.Set(view, "healthFrame", health.rectTransform);
            U.Set(view, "healthFill", fill);
            U.Set(view, "healthTrail", trail);
            U.Set(view, "manaFrame", mana.rectTransform);
            U.Set(view, "manaFill", manaFill);
            U.Set(view, "levelBadge", badge.gameObject);
            U.Set(view, "levelText", level);
            U.Set(view, "protectedIcon", shield.gameObject);

            GameObject prefab = U.SavePrefab(root.gameObject, U.UiPrefabDir);
            return prefab.GetComponent<WorldBarView>();
        }

        // ================= HUD =================

        private GameObject BuildHud()
        {
            var go = new GameObject("HUD", typeof(RectTransform));
            go.layer = LayerMask.NameToLayer("UI");
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            canvas.pixelPerfect = false;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            Transform root = go.transform;

            BuildHealthLayer(root);
            BuildTopBanner(root);
            BuildCenterMessage(root);
            BuildHeroPanel(root);
            BuildTooltip(root);
            BuildEndScreen(root);

            return U.SavePrefab(go, U.UiPrefabDir);
        }

        private void BuildHealthLayer(Transform root)
        {
            RectTransform layer = Stretch("HealthBars", root);
            var view = layer.gameObject.AddComponent<HealthBarLayer>();
            U.Set(view, "barPrefab", WorldBarPrefab);
            U.Set(view, "theme", Theme);
        }

        private void BuildTopBanner(Transform root)
        {
            Image banner = Img(root, "TopBanner", "banner.png", Color.white, new Vector2(0f, -40f), new Vector2(420f, 66f),
                anchor: new Vector2(0.5f, 1f));
            TextMeshProUGUI time = Txt(banner.transform, "Time", "00:00", bodyFont, bodyShadow, 28f, Theme.cream,
                TextAlignmentOptions.Center, new Vector2(0f, 3f), new Vector2(180f, 40f), 2f);
            Txt(banner.transform, "Dawn", "DAWN", nameFont, null, 13f, Theme.dawn, TextAlignmentOptions.Center,
                new Vector2(-122f, 3f), new Vector2(90f, 22f), 6f);
            Txt(banner.transform, "Dusk", "DUSK", nameFont, null, 13f, Theme.dusk, TextAlignmentOptions.Center,
                new Vector2(122f, 3f), new Vector2(90f, 22f), 6f);
            TextMeshProUGUI phase = Txt(banner.transform, "Phase", "PREPARATION", nameFont, bodyShadow, 12f, Theme.gold,
                TextAlignmentOptions.Center, new Vector2(0f, -46f), new Vector2(400f, 22f), 10f);

            var view = banner.gameObject.AddComponent<MatchTimerView>();
            U.Set(view, "timeText", time);
            U.Set(view, "phaseText", phase);
            U.Set(view, "theme", Theme);
        }

        private void BuildCenterMessage(Transform root)
        {
            RectTransform area = Stretch("CenterMessage", root);

            RectTransform headlineRoot = Rect("Headline", area, new Vector2(0.5f, 0.5f), new Vector2(0f, 230f), new Vector2(1400f, 220f));
            var headlineGroup = headlineRoot.gameObject.AddComponent<CanvasGroup>();
            headlineGroup.blocksRaycasts = false;
            headlineGroup.alpha = 0f;
            Img(headlineRoot, "Divider", "divider.png", Color.white, new Vector2(0f, -62f), new Vector2(480f, 24f));
            TextMeshProUGUI kicker = Txt(headlineRoot, "Kicker", "", titleFont, titleGlow, 22f, Theme.gold,
                TextAlignmentOptions.Center, new Vector2(0f, 52f), new Vector2(1400f, 34f), 14f);
            TextMeshProUGUI headline = Txt(headlineRoot, "Text", "", titleFont, titleGlow, 60f, Theme.goldLight,
                TextAlignmentOptions.Center, new Vector2(0f, -10f), new Vector2(1400f, 80f), 10f);

            RectTransform errorRoot = Rect("Error", area, new Vector2(0.5f, 0f), new Vector2(0f, 300f), new Vector2(900f, 40f));
            var errorGroup = errorRoot.gameObject.AddComponent<CanvasGroup>();
            errorGroup.blocksRaycasts = false;
            errorGroup.alpha = 0f;
            TextMeshProUGUI error = Txt(errorRoot, "Text", "", bodyFont, bodyShadow, 21f, new Color(1f, 0.58f, 0.48f),
                TextAlignmentOptions.Center, Vector2.zero, new Vector2(900f, 40f), 1.5f);

            var view = area.gameObject.AddComponent<CenterMessageView>();
            U.Set(view, "headline", headline);
            U.Set(view, "kicker", kicker);
            U.Set(view, "headlineGroup", headlineGroup);
            U.Set(view, "errorText", error);
            U.Set(view, "errorGroup", errorGroup);
        }

        private void BuildHeroPanel(Transform root)
        {
            // Полупрозрачная полоса во всю ширину экрана — лёгкая, без тяжёлой рамки.
            RectTransform strip = Rect("HeroPanel", root, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, 176f), new Vector2(0.5f, 0f));
            strip.anchorMin = new Vector2(0f, 0f);
            strip.anchorMax = new Vector2(1f, 0f);
            strip.sizeDelta = new Vector2(0f, 176f);
            var bg = strip.gameObject.AddComponent<Image>();
            bg.sprite = U.UiSprite("hud_strip.png");
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = true;
            var group = strip.gameObject.AddComponent<CanvasGroup>();
            Img(strip, "Ornament", "hud_ornament.png", Color.white, new Vector2(0f, 0f), new Vector2(220f, 24f),
                anchor: new Vector2(0.5f, 1f));

            // Контент по центру фиксированной ширины
            RectTransform p = Rect("Content", strip, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(1240f, 176f), new Vector2(0.5f, 0f));
            Vector2 bottom = new Vector2(0.5f, 0f);

            // ---------- Портрет-карточка (выступает над полосой) ----------
            RectTransform card = Rect("Portrait", p, bottom, new Vector2(-532f, 100f), new Vector2(150f, 189f));
            Image mask = Img(card, "Mask", "portrait_card_mask.png", Color.white, Vector2.zero, new Vector2(150f, 189f));
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Image portrait = Img(mask.transform, "Image", "portrait_sun_warden.png", Color.white, Vector2.zero, new Vector2(150f, 189f));
            Img(mask.transform, "Shade", "portrait_shade.png", Color.white, new Vector2(0f, -71f), new Vector2(150f, 48f));
            GameObject respawn = Img(mask.transform, "Respawn", null, new Color(0.02f, 0.03f, 0.06f, 0.66f), Vector2.zero,
                new Vector2(150f, 189f)).gameObject;
            TextMeshProUGUI respawnText = Txt(respawn.transform, "Seconds", "0", nameFont, bodyShadow, 64f, Theme.cream,
                TextAlignmentOptions.Center, new Vector2(0f, 6f), new Vector2(140f, 90f));
            respawn.SetActive(false);
            Img(card, "Frame", "portrait_card.png", Color.white, Vector2.zero, new Vector2(150f, 189f));
            TextMeshProUGUI level = Txt(card, "Level", "1", nameFont, bodyShadow, 19f, Theme.cream,
                TextAlignmentOptions.Left, new Vector2(-62f, -76f), new Vector2(110f, 26f), 1f);
            level.rectTransform.pivot = new Vector2(0f, 0.5f);

            // ---------- Имя, полоски, характеристики ----------
            const float infoX = -440f;
            TextMeshProUGUI heroName = Txt(p, "Name", "Hero", nameFont, null, 36f, Theme.cream,
                TextAlignmentOptions.Left, new Vector2(infoX, 140f), new Vector2(420f, 44f), 1f, bottom);
            heroName.rectTransform.pivot = new Vector2(0f, 0.5f);
            heroName.rectTransform.anchoredPosition = new Vector2(infoX, 140f);
            TextMeshProUGUI heroTitle = Txt(p, "Title", "", nameFont, null, 16f, Theme.textMuted,
                TextAlignmentOptions.Left, new Vector2(infoX + 2f, 112f), new Vector2(420f, 22f), 1f, bottom);
            heroTitle.fontStyle = FontStyles.Italic;
            heroTitle.rectTransform.pivot = new Vector2(0f, 0.5f);
            heroTitle.rectTransform.anchoredPosition = new Vector2(infoX + 2f, 112f);

            StatRow(p, "Health", "Stats/stat_health.png", new Vector2(infoX, 80f), Theme.selfHealth,
                out Image hpFill, out TextMeshProUGUI hpText, out TextMeshProUGUI hpRegen, new Color(0.62f, 0.95f, 0.66f));
            StatRow(p, "Mana", "Stats/stat_mana.png", new Vector2(infoX, 50f), Theme.mana,
                out Image mpFill, out TextMeshProUGUI mpText, out TextMeshProUGUI mpRegen, new Color(0.62f, 0.76f, 1f));

            float sx = infoX;
            TextMeshProUGUI damage = StatChip(p, "Damage", "Stats/stat_damage.png", ref sx, 20f, 90f);
            Dot(p, ref sx, 20f);
            TextMeshProUGUI armor = StatChip(p, "Armor", "Stats/stat_armor.png", ref sx, 20f, 56f);
            Dot(p, ref sx, 20f);
            TextMeshProUGUI speed = StatChip(p, "Speed", "Stats/stat_speed.png", ref sx, 20f, 60f);

            // ---------- Способности ----------
            const float abilitiesX = 300f;
            Txt(p, "AbilitiesLabel", "ABILITIES", nameFont, null, 14f, Theme.gold, TextAlignmentOptions.Center,
                new Vector2(abilitiesX, 160f), new Vector2(200f, 20f), 8f, bottom);
            HairLine(p, new Vector2(abilitiesX - 150f, 160f), 150f);
            HairLine(p, new Vector2(abilitiesX + 150f, 160f), 150f);

            var slots = new AbilitySlotView[4];
            for (int i = 0; i < 4; i++)
                slots[i] = BuildSlot(p, i, new Vector2(abilitiesX + (i - 1.5f) * 116f, 92f));

            var view = strip.gameObject.AddComponent<HeroHudView>();
            U.Set(view, "group", group);
            U.Set(view, "portrait", portrait);
            U.Set(view, "heroName", heroName);
            U.Set(view, "heroTitle", heroTitle);
            U.Set(view, "healthFill", hpFill);
            U.Set(view, "healthText", hpText);
            U.Set(view, "healthRegenText", hpRegen);
            U.Set(view, "manaFill", mpFill);
            U.Set(view, "manaText", mpText);
            U.Set(view, "manaRegenText", mpRegen);
            U.Set(view, "damageText", damage);
            U.Set(view, "armorText", armor);
            U.Set(view, "speedText", speed);
            U.SetArray(view, "slots", slots);
            U.Set(view, "respawnOverlay", respawn);
            U.Set(view, "respawnText", respawnText);
            U.Set(view, "levelText", level);
            U.Set(view, "theme", Theme);
        }

        /// <summary>Строка «значок · число · тонкая полоска · реген».</summary>
        private void StatRow(Transform parent, string name, string icon, Vector2 pos, Color color,
            out Image fill, out TextMeshProUGUI text, out TextMeshProUGUI regen, Color regenColor)
        {
            Vector2 bottom = new Vector2(0.5f, 0f);
            Img(parent, name + "Icon", icon, Theme.cream, pos + new Vector2(10f, 0f), new Vector2(20f, 20f), anchor: bottom);
            text = Txt(parent, name + "Value", "0/0", bodyFont, bodyShadow, 16f, Theme.cream, TextAlignmentOptions.Left,
                pos + new Vector2(28f, 0f), new Vector2(110f, 24f), 0.5f, bottom);
            text.rectTransform.pivot = new Vector2(0f, 0.5f);
            text.rectTransform.anchoredPosition = pos + new Vector2(28f, 0f);

            Image frame = Img(parent, name + "Bar", "thin_bar.png", Color.white, pos + new Vector2(140f + 155f, 0f),
                new Vector2(310f, 20f), sliced: true, anchor: bottom);
            Filled(frame.transform, "Back", "thin_bar_fill.png", new Color(0.03f, 0.05f, 0.08f, 1f), 14f, 6f);
            fill = Filled(frame.transform, "Fill", "thin_bar_fill.png", color, 14f, 6f);
            regen = Txt(parent, name + "Regen", "+0.0", bodyFont, bodyShadow, 12.5f, regenColor, TextAlignmentOptions.Left,
                pos + new Vector2(458f, 0f), new Vector2(60f, 20f), 0f, bottom);
            regen.rectTransform.pivot = new Vector2(0f, 0.5f);
            regen.rectTransform.anchoredPosition = pos + new Vector2(458f, 0f);
        }

        private TextMeshProUGUI StatChip(Transform parent, string name, string icon, ref float x, float y, float width)
        {
            Vector2 bottom = new Vector2(0.5f, 0f);
            Img(parent, name + "Icon", icon, Theme.cream, new Vector2(x + 10f, y), new Vector2(20f, 20f), anchor: bottom);
            TextMeshProUGUI t = Txt(parent, name + "Value", "0", bodyFont, bodyShadow, 16f, Theme.cream, TextAlignmentOptions.Left,
                new Vector2(x + 28f, y), new Vector2(width, 24f), 0.5f, bottom);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.rectTransform.anchoredPosition = new Vector2(x + 28f, y);
            x += 28f + width;
            return t;
        }

        private void Dot(Transform parent, ref float x, float y)
        {
            Img(parent, "Dot", "diamond.png", new Color(0.86f, 0.8f, 0.68f, 0.5f), new Vector2(x + 4f, y), new Vector2(7f, 7f),
                anchor: new Vector2(0.5f, 0f));
            x += 22f;
        }

        private static void HairLine(Transform parent, Vector2 pos, float width)
        {
            RectTransform rt = Rect("Line", parent, new Vector2(0.5f, 0f), pos, new Vector2(width, 1f));
            var img = rt.gameObject.AddComponent<Image>();
            img.color = new Color(0.88f, 0.82f, 0.7f, 0.35f);
            img.raycastTarget = false;
        }

        private AbilitySlotView BuildSlot(Transform parent, int index, Vector2 pos)
        {
            const float size = 96f;
            Image root = Img(parent, $"Slot{HeroAbilitiesHotkey(index)}", "ability_mask.png", new Color(0.05f, 0.04f, 0.03f, 1f),
                pos, new Vector2(size, size), raycast: true, anchor: new Vector2(0.5f, 0f));
            Image glow = Img(root.transform, "Glow", "ability_glow.png", new Color(1f, 0.9f, 0.7f, 0f), Vector2.zero, new Vector2(132f, 132f));
            Image mask = Img(root.transform, "IconMask", "ability_mask.png", Color.white, Vector2.zero, new Vector2(size, size));
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            Image icon = Img(mask.transform, "Icon", null, Color.white, Vector2.zero, new Vector2(80f, 80f));
            Image cooldown = Img(root.transform, "Cooldown", "ability_mask.png", new Color(0.01f, 0.01f, 0.03f, 0.72f), Vector2.zero,
                new Vector2(size, size));
            cooldown.type = Image.Type.Filled;
            cooldown.fillMethod = Image.FillMethod.Radial360;
            cooldown.fillOrigin = (int)Image.Origin360.Top;
            cooldown.fillClockwise = false;
            cooldown.fillAmount = 0f;
            Image frame = Img(root.transform, "Ring", "ability_ring.png", new Color(0.92f, 0.92f, 0.92f, 1f), Vector2.zero, new Vector2(size, size));

            TextMeshProUGUI cdText = Txt(root.transform, "CooldownText", "", boldFont, bodyShadow, 26f, Color.white,
                TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(80f, 40f));
            cdText.gameObject.SetActive(false);

            TextMeshProUGUI hotkey = Txt(root.transform, "Key", HeroAbilitiesHotkey(index), nameFont, bodyShadow, 17f, Theme.cream,
                TextAlignmentOptions.Right, new Vector2(-6f, -60f), new Vector2(40f, 22f));
            hotkey.rectTransform.pivot = new Vector2(1f, 0.5f);
            hotkey.rectTransform.anchoredPosition = new Vector2(-5f, -60f);
            TextMeshProUGUI mana = Txt(root.transform, "Mana", "", bodyFont, bodyShadow, 13f, new Color(0.62f, 0.76f, 1f),
                TextAlignmentOptions.Left, new Vector2(6f, -60f), new Vector2(50f, 20f));
            mana.rectTransform.pivot = new Vector2(0f, 0.5f);
            mana.rectTransform.anchoredPosition = new Vector2(5f, -60f);

            var pips = new Image[4];
            for (int i = 0; i < 4; i++)
                pips[i] = Img(root.transform, $"Pip{i + 1}", "diamond.png", Theme.goldLight, new Vector2(-18f + i * 12f, -76f), new Vector2(8f, 8f));

            var view = root.gameObject.AddComponent<AbilitySlotView>();
            U.Set(view, "icon", icon);
            U.Set(view, "cooldownOverlay", cooldown);
            U.Set(view, "cooldownText", cdText);
            U.Set(view, "hotkeyText", hotkey);
            U.Set(view, "manaText", mana);
            U.Set(view, "glow", glow);
            U.Set(view, "frame", frame);
            U.SetArray(view, "levelPips", pips);
            U.Set(view, "theme", Theme);
            return view;
        }

        private static string HeroAbilitiesHotkey(int i) => LastStand.Abilities.HeroAbilities.Hotkeys[i];

        private void BuildTooltip(Transform root)
        {
            RectTransform rt = Rect("Tooltip", root, new Vector2(0.5f, 0f), new Vector2(0f, 280f), new Vector2(470f, 120f), new Vector2(0.5f, 0f));
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = U.UiSprite("plate.png");
            bg.type = Image.Type.Sliced;
            bg.raycastTarget = false;
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 18, 20);
            layout.spacing = 6f;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI title = Txt(rt, "Title", "ABILITY", titleFont, titleGlow, 21f, Theme.goldLight,
                TextAlignmentOptions.Left, Vector2.zero, new Vector2(420f, 28f), 4f);
            TextMeshProUGUI meta = Txt(rt, "Meta", "", bodyFont, null, 12.5f, Theme.gold, TextAlignmentOptions.Left,
                Vector2.zero, new Vector2(420f, 18f));
            Image divider = Img(rt, "Divider", "divider.png", Color.white, Vector2.zero, new Vector2(420f, 14f));
            divider.preserveAspect = false;
            divider.gameObject.AddComponent<LayoutElement>().preferredHeight = 14f;
            TextMeshProUGUI body = Txt(rt, "Body", "", bodyFont, null, 14.5f, new Color(0.88f, 0.89f, 0.95f),
                TextAlignmentOptions.TopLeft, Vector2.zero, new Vector2(420f, 40f));
            body.textWrappingMode = TextWrappingModes.Normal;
            body.lineSpacing = 4f;

            var view = rt.gameObject.AddComponent<TooltipView>();
            U.Set(view, "group", group);
            U.Set(view, "panel", rt);
            U.Set(view, "title", title);
            U.Set(view, "meta", meta);
            U.Set(view, "body", body);
        }

        private void BuildEndScreen(Transform root)
        {
            RectTransform screen = Stretch("EndScreen", root);
            var group = screen.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            var dim = screen.gameObject.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.03f, 0.09f, 0.8f);
            dim.raycastTarget = true;

            Image panel = Img(screen, "Panel", "panel.png", Color.white, Vector2.zero, new Vector2(780f, 480f), sliced: true);
            Transform p = panel.transform;
            Image crest = Img(p, "Crest", "crest.png", Theme.gold, new Vector2(0f, 222f), new Vector2(320f, 120f));
            TextMeshProUGUI title = Txt(p, "Title", "VICTORY", titleFont, titleGlow, 96f, Theme.goldLight,
                TextAlignmentOptions.Center, new Vector2(0f, 96f), new Vector2(740f, 120f), 16f);
            Img(p, "Divider", "divider.png", Color.white, new Vector2(0f, 26f), new Vector2(520f, 24f));
            TextMeshProUGUI subtitle = Txt(p, "Subtitle", "", titleFont, null, 20f, Theme.gold, TextAlignmentOptions.Center,
                new Vector2(0f, -16f), new Vector2(740f, 30f), 6f);
            TextMeshProUGUI details = Txt(p, "Details", "", bodyFont, null, 16f, Theme.textMuted, TextAlignmentOptions.Center,
                new Vector2(0f, -56f), new Vector2(700f, 26f), 2f);

            Image button = Img(p, "PlayAgain", "button.png", Color.white, new Vector2(0f, -150f), new Vector2(320f, 76f),
                sliced: true, raycast: true);
            var btn = button.gameObject.AddComponent<Button>();
            ColorBlock colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.15f, 0.95f, 1f);
            colors.pressedColor = new Color(0.8f, 0.75f, 0.65f, 1f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1.2f;
            colors.fadeDuration = 0.08f;
            btn.colors = colors;
            Txt(button.transform, "Label", "PLAY AGAIN", titleFont, titleGlow, 22f, Theme.goldLight,
                TextAlignmentOptions.Center, new Vector2(0f, 1f), new Vector2(300f, 60f), 8f);

            // Уголки арт-деко вокруг панели
            Vector2 half = new Vector2(390f + 16f, 240f + 16f);
            Corner(p, new Vector2(-half.x + 48f, half.y - 48f), new Vector3(1f, 1f, 1f));
            Corner(p, new Vector2(half.x - 48f, half.y - 48f), new Vector3(-1f, 1f, 1f));
            Corner(p, new Vector2(-half.x + 48f, -half.y + 48f), new Vector3(1f, -1f, 1f));
            Corner(p, new Vector2(half.x - 48f, -half.y + 48f), new Vector3(-1f, -1f, 1f));

            var view = screen.gameObject.AddComponent<EndScreenView>();
            U.Set(view, "group", group);
            U.Set(view, "panel", panel.rectTransform);
            U.Set(view, "title", title);
            U.Set(view, "subtitle", subtitle);
            U.Set(view, "details", details);
            U.Set(view, "crest", crest);
            U.Set(view, "playAgain", btn);
            U.Set(view, "theme", Theme);
        }

        private void Corner(Transform parent, Vector2 pos, Vector3 scale)
        {
            Image c = Img(parent, "Corner", "corner.png", Theme.gold, pos, new Vector2(96f, 96f));
            c.rectTransform.localScale = scale;
        }
    }
}
