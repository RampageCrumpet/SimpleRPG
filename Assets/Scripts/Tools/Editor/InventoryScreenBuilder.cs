using SimpleRPG.UI;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace SimpleRPG.Tools
{
    /// <summary>
    /// Builds the HUD additions and the inventory screens of the player UI in the open scene, from the wireframes in 1920x1080 reference units.
    /// Re-running it replaces what it built, so layout changes are made here and rebuilt rather than hand-edited.
    /// </summary>
    public static class InventoryScreenBuilder
    {
        private const string InventoryPanelPrefab = "Assets/Prefabs/UI/Inventory/InventoryPanel.prefab";
        private const string RigPrefab = "Assets/Prefabs/Characters/Human/HumanMale_Rig.prefab";

        private static readonly Color ScreenColor = new Color(0.082f, 0.098f, 0.118f, 0.97f);
        private static readonly Color PanelColor = new Color(0.11f, 0.133f, 0.153f, 1f);
        private static readonly Color SubpanelColor = new Color(0.09f, 0.106f, 0.125f, 1f);
        private static readonly Color CharacterColor = new Color(0.125f, 0.149f, 0.173f, 1f);
        private static readonly Color TitleColor = new Color(0.89f, 0.91f, 0.92f, 1f);
        private static readonly Color DividerColor = new Color(0.42f, 0.46f, 0.51f, 1f);

        [MenuItem("SimpleRPG/UI/Build HUD And Inventory Screens")]
        public static void Build()
        {
            GameObject playerUI = GameObject.FindGameObjectWithTag("UI");
            while (playerUI != null && playerUI.transform.parent != null)
            {
                playerUI = playerUI.transform.parent.gameObject;
            }

            if (playerUI == null || playerUI.GetComponent<PlayerUIController>() == null)
            {
                Debug.LogError("Open the scene that contains the PlayerUI canvas first.");
                return;
            }

            LootMenuUI lootMenu = playerUI.GetComponentInChildren<LootMenuUI>(true);
            if (lootMenu == null)
            {
                Debug.LogError("The PlayerUI has no LootMenuUI.");
                return;
            }

            ConfigureScaler(playerUI);
            BuildScreens(lootMenu);
            BuildHud(playerUI);

            EditorSceneManager.MarkSceneDirty(playerUI.scene);
            Debug.Log("HUD and inventory screens built. Save the scene.");
        }

        private static void ConfigureScaler(GameObject playerUI)
        {
            // The nested loot canvas carries its own scaler, which must agree or it keeps drawing at pixel size.
            foreach (CanvasScaler scaler in playerUI.GetComponentsInChildren<CanvasScaler>(true))
            {
                SerializedObject so = new SerializedObject(scaler);
                so.FindProperty("m_UiScaleMode").intValue = (int)CanvasScaler.ScaleMode.ScaleWithScreenSize;
                so.FindProperty("m_ReferenceResolution").vector2Value = new Vector2(1920f, 1080f);
                so.FindProperty("m_ScreenMatchMode").intValue = (int)CanvasScaler.ScreenMatchMode.Expand;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        // ---------- inventory screens ----------

        private static void BuildScreens(LootMenuUI lootMenu)
        {
            Transform parent = lootMenu.transform;

            // The screens draw above the HUD but below the death screen.
            lootMenu.transform.SetAsLastSibling();
            Transform deathScreen = lootMenu.transform.parent.Find("DeathScreen");
            if (deathScreen != null)
            {
                deathScreen.SetAsLastSibling();
            }

            // Replace what an earlier build, or the old single-panel layout, left.
            Remove(parent, "InventoryScreen");
            Remove(parent, "InventoryPanel");
            Remove(parent, "InventoryPanel (1)");

            GameObject screen = NewUI("InventoryScreen", parent);
            Stretch((RectTransform)screen.transform);
            Image dim = screen.AddComponent<Image>();
            dim.color = ScreenColor;

            GameObject frame = NewUI("Frame", screen.transform);
            RectTransform frameRect = (RectTransform)frame.transform;
            frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0.5f, 0.5f);
            frameRect.sizeDelta = new Vector2(1920f, 1080f);
            frameRect.anchoredPosition = Vector2.zero;

            // Stat sheet.
            RectTransform statPanel = Panel("StatPanel", frameRect, new Rect(40f, 100f, 620f, 940f), "Player Stat Sheet");
            RectTransform statContent = SubPanel("Content", statPanel, new Rect(24f, 68f, 572f, 840f));
            VerticalLayoutGroup statLayout = statContent.gameObject.AddComponent<VerticalLayoutGroup>();
            statLayout.padding = new RectOffset(24, 24, 8, 8);
            statLayout.childAlignment = TextAnchor.UpperLeft;
            statLayout.childControlWidth = true;
            statLayout.childControlHeight = true;
            statLayout.childForceExpandWidth = true;
            statLayout.childForceExpandHeight = false;
            StatSheetUI statSheet = statPanel.gameObject.AddComponent<StatSheetUI>();
            SetRef(statSheet, "content", statContent);

            // The player's panel: its rectangle is chosen by the open screen.
            RectTransform playerPanel = Panel("PlayerPanel", frameRect, new Rect(700f, 100f, 1180f, 940f), "Player");
            PaperDollUI playerDoll = BuildDoll(playerPanel, "DollRegion", 68f, 500f, "Player Equipment / Paper Doll", PaperDollLayout.Full, out CharacterPreviewUI preview);
            InventoryUI playerInventory = BuildInventoryRegion(playerPanel, "InventoryRegion", 592f, 316f, "Player Inventory", "PlayerInventory");

            // The interacted object's panel.
            RectTransform targetPanel = Panel("TargetPanel", frameRect, new Rect(980f, 100f, 900f, 940f), "Interacted Object");
            PaperDollUI targetDoll = BuildDoll(targetPanel, "DollRegion", 68f, 260f, "Equipment", PaperDollLayout.Compact, out _);
            InventoryUI targetInventory = BuildInventoryRegion(targetPanel, "InventoryRegion", 352f, 556f, "Interacted Object Inventory", "TargetInventory");

            SerializedObject loot = new SerializedObject(lootMenu);
            loot.FindProperty("<PlayerInventoryUI>k__BackingField").objectReferenceValue = playerInventory;
            loot.FindProperty("<LootInventoryUI>k__BackingField").objectReferenceValue = targetInventory;
            loot.FindProperty("screenRoot").objectReferenceValue = screen;
            loot.FindProperty("statPanel").objectReferenceValue = statPanel.gameObject;
            loot.FindProperty("playerPanel").objectReferenceValue = playerPanel;
            loot.FindProperty("targetPanel").objectReferenceValue = targetPanel.gameObject;
            loot.FindProperty("playerPaperDoll").objectReferenceValue = playerDoll;
            loot.FindProperty("playerPreview").objectReferenceValue = preview;
            loot.FindProperty("statSheet").objectReferenceValue = statSheet;
            loot.FindProperty("targetPaperDoll").objectReferenceValue = targetDoll;
            loot.FindProperty("targetInventoryRegion").objectReferenceValue = targetInventory.transform.parent;
            loot.ApplyModifiedPropertiesWithoutUndo();
        }

        private static PaperDollUI BuildDoll(RectTransform panel, string name, float y, float height, string title, PaperDollLayout layout, out CharacterPreviewUI preview)
        {
            RectTransform region = SubPanel(name, panel, new Rect(24f, y, 0f, height));
            StretchWidth(region, 24f, 24f);
            Text(region, title, 18f, new Vector2(16f, 8f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            preview = null;
            if (layout == PaperDollLayout.Full)
            {
                GameObject frame = NewUI("PreviewFrame", region);
                RectTransform frameRect = (RectTransform)frame.transform;
                frameRect.anchorMin = frameRect.anchorMax = frameRect.pivot = new Vector2(0.5f, 0.5f);
                frameRect.sizeDelta = new Vector2(260f, 352f);
                frameRect.anchoredPosition = new Vector2(0f, -2f);
                frame.AddComponent<Image>().color = CharacterColor;

                GameObject previewObject = NewUI("CharacterPreview", frame.transform);
                Stretch((RectTransform)previewObject.transform);
                RawImage raw = previewObject.AddComponent<RawImage>();
                raw.raycastTarget = false;
                raw.color = new Color(1f, 1f, 1f, 0f);
                preview = previewObject.AddComponent<CharacterPreviewUI>();
                SetRef(preview, "rigPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(RigPrefab));
            }

            GameObject doll = NewUI("PaperDoll", region);
            Stretch((RectTransform)doll.transform);
            PaperDollUI paperDoll = doll.AddComponent<PaperDollUI>();
            SerializedObject so = new SerializedObject(paperDoll);
            so.FindProperty("layout").enumValueIndex = (int)layout;
            so.ApplyModifiedPropertiesWithoutUndo();
            return paperDoll;
        }

        private static InventoryUI BuildInventoryRegion(RectTransform panel, string name, float y, float height, string title, string viewName)
        {
            RectTransform region = SubPanel(name, panel, new Rect(24f, y, 0f, height));
            StretchWidth(region, 24f, 24f);
            Text(region, title, 18f, new Vector2(16f, 8f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            GameObject view = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPanelPrefab), region);
            view.name = viewName;
            view.SetActive(true);
            RectTransform rect = (RectTransform)view.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -48f);
            rect.sizeDelta = new Vector2(768f, 256f);
            rect.localScale = Vector3.one;
            return view.GetComponent<InventoryUI>();
        }

        /// <summary>
        /// A titled panel with a divider, positioned in reference units from the top left of the frame.
        /// </summary>
        private static RectTransform Panel(string name, RectTransform parent, Rect rect, string title)
        {
            GameObject go = NewUI(name, parent);
            RectTransform rt = (RectTransform)go.transform;
            SetRect(rt, rect);
            go.AddComponent<Image>().color = PanelColor;
            Text(rt, title, 20f, new Vector2(24f, 12f), TextAlignmentOptions.TopLeft, FontStyles.Bold);

            GameObject divider = NewUI("Divider", rt);
            RectTransform dr = (RectTransform)divider.transform;
            dr.anchorMin = new Vector2(0f, 1f);
            dr.anchorMax = new Vector2(1f, 1f);
            dr.pivot = new Vector2(0.5f, 1f);
            dr.offsetMin = new Vector2(16f, -50f);
            dr.offsetMax = new Vector2(-16f, -48f);
            divider.AddComponent<Image>().color = DividerColor;
            return rt;
        }

        private static RectTransform SubPanel(string name, RectTransform parent, Rect rect)
        {
            GameObject go = NewUI(name, parent);
            RectTransform rt = (RectTransform)go.transform;
            SetRect(rt, rect);
            go.AddComponent<Image>().color = SubpanelColor;
            return rt;
        }

        // ---------- HUD ----------

        private static void BuildHud(GameObject playerUI)
        {
            Transform root = playerUI.transform;

            // Health bar: centred along the bottom. The frame keeps its art; the inner bar is 600x36 as in the wireframe.
            Transform health = root.Find("HealthBar");
            RectTransform healthRect = (RectTransform)health.transform;
            healthRect.anchorMin = healthRect.anchorMax = new Vector2(0.5f, 0f);
            healthRect.pivot = new Vector2(0.5f, 0f);
            healthRect.sizeDelta = new Vector2(716f, 80f);
            healthRect.anchoredPosition = new Vector2(0f, 80f);

            Remove(health, "ValueText");
            TextMeshProUGUI value = Text((RectTransform)health, string.Empty, 22f, Vector2.zero, TextAlignmentOptions.Center, FontStyles.Bold);
            value.name = "ValueText";
            RectTransform valueRect = value.rectTransform;
            valueRect.pivot = new Vector2(0.5f, 0.5f);
            Stretch(valueRect);
            valueRect.sizeDelta = new Vector2(-116f, -44f);
            SetRef(health.GetComponent<HealthBarUI>(), "valueText", value);

            // Buff bar: directly above the health bar, empty until an effect is active.
            Remove(root, "BuffBar");
            GameObject bar = NewUI("BuffBar", root);
            RectTransform barRect = (RectTransform)bar.transform;
            barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(528f, 64f);
            barRect.anchoredPosition = new Vector2(0f, 166f);

            GameObject strip = NewUI("Strip", barRect);
            Stretch((RectTransform)strip.transform);
            Image stripImage = strip.AddComponent<Image>();
            stripImage.color = new Color(0.09f, 0.106f, 0.125f, 0.85f);
            stripImage.raycastTarget = false;

            GameObject slots = NewUI("Slots", barRect);
            Stretch((RectTransform)slots.transform);
            HorizontalLayoutGroup layout = slots.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.padding = new RectOffset(16, 16, 8, 8);
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            BuffBarUI buffBar = bar.AddComponent<BuffBarUI>();
            SetRef(buffBar, "strip", strip);
            SetRef(buffBar, "slotContainer", (RectTransform)slots.transform);
        }

        // ---------- helpers ----------

        private static GameObject NewUI(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void Remove(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }
        }

        /// <summary>
        /// Positions a rectangle in reference units measured from the parent's top left, y down.
        /// </summary>
        private static void SetRect(RectTransform rt, Rect rect)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(rect.x, -rect.y);
            rt.sizeDelta = rect.size;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Makes a top-anchored rectangle follow its parent's width with the given margins, keeping its y and height.
        /// </summary>
        private static void StretchWidth(RectTransform rt, float left, float right)
        {
            float y = rt.anchoredPosition.y;
            float height = rt.sizeDelta.y;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.offsetMin = new Vector2(left, y - height);
            rt.offsetMax = new Vector2(-right, y);
        }

        private static TextMeshProUGUI Text(RectTransform parent, string text, float size, Vector2 offset, TextAlignmentOptions alignment, FontStyles style)
        {
            GameObject go = NewUI("Text", parent);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(offset.x, -offset.y);
            rt.sizeDelta = new Vector2(-offset.x * 2f, size * 1.6f);
            TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.color = TitleColor;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static void SetRef(Object target, string field, Object value)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty(field).objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
