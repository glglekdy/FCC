#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;

// Figma 정비 시안을 에디터에서만 생성해 디자이너가 모든 요소를 프리팹에서 수정할 수 있게 합니다.
public static class SkillMaintenancePrefabBuilder {
    #region 경로와 디자인

    public const string WindowPath = "Assets/_Project/Assets/Prefabs/UI/SkillMaintenance.prefab";
    public const string SlotPath = "Assets/_Project/Assets/Prefabs/UI/SkillMaintenanceSlot.prefab";
    public const string RowPath = "Assets/_Project/Assets/Prefabs/UI/SkillMaintenanceRow.prefab";
    static readonly Color Background = ColorOf("0e0b0c");
    static readonly Color Panel = ColorOf("171113");
    static readonly Color Selected = ColorOf("2b1e21");
    static readonly Color High = ColorOf("f0e6d8");
    static readonly Color Body = ColorOf("d0c3b9");
    static readonly Color Muted = ColorOf("b4a59d");
    static readonly Color Accent = ColorOf("a66b70");

    #endregion
    #region 생성 메뉴

    [MenuItem("Tools/FCC/UI/Create Skill Maintenance Prefabs")]
    public static void Build() {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("플레이 모드를 종료한 후 생성하세요.");
        // 재실행으로 인스펙터에서 작업한 디자인이 덮이지 않도록 기존 에셋은 보존합니다.
        foreach (string path in new[] { WindowPath, SlotPath, RowPath }) {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) {
                throw new InvalidOperationException("이미 존재하는 프리팹은 덮어쓰지 않습니다: " + path);
            }
        }
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrefabBuilderFont.ProjectFontPath);
        if (font == null || !font.HasCharacter('정', true, false)) {
            font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrefabBuilderFont.FallbackFontPath);
        }
        if (font == null || !font.HasCharacter('정', true, false)) {
            throw new InvalidOperationException("한글을 지원하는 프로젝트 SDF 폰트를 확인하세요.");
        }

        // 별도 프리뷰 씬에서 만들어 현재 열려 있는 씬이나 프리팹 편집 내용을 건드리지 않습니다.
        var scene = EditorSceneManager.NewPreviewScene();
        GameObject staging = new GameObject("SkillMaintenanceBuild", typeof(RectTransform));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(staging, scene);
        try {
            GameObject slot = BuildSlot(staging.transform, font);
            GameObject row = BuildRow(staging.transform, font);
            BuildWindow(staging.transform, font, slot, row);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(WindowPath);
            EditorGUIUtility.PingObject(Selection.activeObject);
            Debug.Log("[SkillMaintenance] 정비 화면·장착 슬롯·목록 항목 프리팹을 생성했습니다. 수치는 디자인 예시입니다.");
        }
        finally {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    #endregion
    #region 재사용 항목

    static GameObject BuildSlot(Transform parent, TMP_FontAsset font) {
        RectTransform slot = Box("SkillMaintenanceSlot", parent, 0, 0, 552, 126, Panel);
        MakeButton(slot);
        Label("SlotLabel", slot, font, 28, 18, 496, 28, "슬롯 1", 18, Muted);
        Label("SkillLabel", slot, font, 28, 57, 496, 42, "비어 있음", 28, High);
        Box("SelectionLine", slot, 0, 122, 552, 4, Accent).gameObject.SetActive(false);
        var saved = PrefabUtility.SaveAsPrefabAsset(slot.gameObject, SlotPath);
        UnityEngine.Object.DestroyImmediate(slot.gameObject);
        return saved;
    }

    static GameObject BuildRow(Transform parent, TMP_FontAsset font) {
        RectTransform row = Box("SkillMaintenanceRow", parent, 0, 0, 550, 66, Background);
        MakeButton(row);
        Label("SkillLabel", row, font, 24, 14, 408, 38, "스킬 이름", 24, High);
        var state = Label("StateLabel", row, font, 438, 18, 94, 30, "미장착", 18, Muted);
        state.alignment = TextAlignmentOptions.MidlineRight;
        var layout = row.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
        layout.preferredWidth = 550;
        layout.preferredHeight = 66;
        var saved = PrefabUtility.SaveAsPrefabAsset(row.gameObject, RowPath);
        UnityEngine.Object.DestroyImmediate(row.gameObject);
        return saved;
    }

    #endregion
    #region 전체 창

    static void BuildWindow(Transform parent, TMP_FontAsset font, GameObject slotPrefab, GameObject rowPrefab) {
        RectTransform root = Rect("SkillMaintenance", parent, 0, 0, 1920, 1080);
        Canvas canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        var scaler = root.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        // 16:10·울트라와이드에서도 원본 비율을 유지하며 창 전체가 화면 안에 들어오게 합니다.
        scaler.screenMatchMode = UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0.5f;
        root.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
        var backdrop = Box("Backdrop", root, 0, 0, 1920, 1080, Background);
        backdrop.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
        Stretch(backdrop);
        RectTransform window = Rect("Window", root, 0, 0, 1920, 1080);
        window.anchorMin = window.anchorMax = window.pivot = new Vector2(0.5f, 0.5f);
        window.anchoredPosition = Vector2.zero;

        Box("CurtainLeft", window, 0, 0, 32, 1080, UiTheme.Curtain);
        Box("CurtainRight", window, 1888, 0, 32, 1080, UiTheme.Curtain);
        RectTransform header = Rect("Header", window, 96, 64, 1728, 86);
        Label("Title", header, font, 0, 0, 650, 64, "정비하기", 44, High);
        Label("ShardBalance", header, font, 1439, 18, 289, 38, "기억 조각    12", 24, High);
        Box("Divider", header, 0, 84, 1728, 1, UiTheme.Line);
        Label("EquippedHelp", window, font, 96, 172, 1728, 30, "장착 스킬     교체할 자리를 선택하세요.", 20, Muted);

        RectTransform slots = Rect("EquippedSlots", window, 96, 214, 1728, 126);
        var slotLayout = slots.gameObject.AddComponent<UnityEngine.UI.HorizontalLayoutGroup>();
        slotLayout.spacing = 36;
        slotLayout.childControlWidth = slotLayout.childControlHeight = false;
        slotLayout.childForceExpandWidth = slotLayout.childForceExpandHeight = false;
        string[] skills = { "Pure Dream", "Broken Phantasm", "Cycle of Fate" };
        string[] captions = { "슬롯 1     정화", "슬롯 2     교체할 자리", "슬롯 3     스택 폭발" };
        for (int i = 0; i < 3; i++) {
            GameObject slot = (GameObject)PrefabUtility.InstantiatePrefab(slotPrefab, slots);
            slot.name = "Slot" + (i + 1);
            slot.transform.Find("SlotLabel").GetComponent<TMP_Text>().text = captions[i];
            slot.transform.Find("SkillLabel").GetComponent<TMP_Text>().text = skills[i];
            if (i == 1) {
                slot.GetComponent<UnityEngine.UI.Image>().color = Selected;
                slot.transform.Find("SlotLabel").GetComponent<TMP_Text>().color = ColorOf("d9a4a6");
                slot.transform.Find("SelectionLine").gameObject.SetActive(true);
            }
            RecordOverrides(slot);
        }

        RectTransform list = Rect("SkillList", window, 96, 402, 550, 548);
        Label("Title", list, font, 0, 0, 550, 36, "보유 스킬", 24, High);
        Label("Help", list, font, 0, 39, 550, 30, "스킬을 선택해 교체하거나 강화하세요.", 18, Muted);
        RectTransform rows = Rect("Rows", list, 0, 86, 550, 444);
        var rowLayout = rows.gameObject.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
        rowLayout.spacing = 6;
        rowLayout.childControlWidth = rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = rowLayout.childForceExpandHeight = false;
        string[] names = { "Pure Dream", "Broken Phantasm", "Cycle of Fate", "Invisible Reality", "Bent Spirit", "Close Call" };
        for (int i = 0; i < names.Length; i++) {
            GameObject row = (GameObject)PrefabUtility.InstantiatePrefab(rowPrefab, rows);
            row.name = "Skill_" + names[i].Replace(" ", "");
            row.transform.Find("SkillLabel").GetComponent<TMP_Text>().text = names[i];
            TMP_Text state = row.transform.Find("StateLabel").GetComponent<TMP_Text>();
            state.text = i < 3 ? "장착 중" : i == 3 ? "Lv. 1" : "미획득";
            if (i == 3) {
                row.GetComponent<UnityEngine.UI.Image>().color = Selected;
                state.color = ColorOf("d9a4a6");
                Border(row.transform, 550, 66, ColorOf("9e666d"));
            }
            if (i > 3) {
                row.GetComponent<UnityEngine.UI.Button>().interactable = false;
                row.transform.Find("SkillLabel").GetComponent<TMP_Text>().color = ColorOf("8f8581");
            }
            RecordOverrides(row);
        }

        RectTransform detail = Box("SkillDetail", window, 714, 404, 1110, 546, Panel);
        Label("SkillName", detail, font, 48, 40, 1014, 56, "Invisible Reality", 42, High);
        Label("SkillMeta", detail, font, 48, 100, 1014, 34, "마임의 스킬     투명 벽     Lv. 1", 20, ColorOf("c3aaa8"));
        Label("Description", detail, font, 48, 154, 1014, 60, "보이지 않는 벽을 세워 적의 접근을 막습니다.", 24, Body);
        Box("Divider", detail, 48, 222, 1014, 1, UiTheme.Line);
        Label("UpgradeLevel", detail, font, 48, 252, 1014, 40, "업그레이드     Lv. 1 → Lv. 2", 26, High);
        Label("UpgradeEffect", detail, font, 48, 299, 1014, 72, "투명 벽의 유지 시간이 늘어납니다.", 22, Body);
        Label("UpgradeCost", detail, font, 48, 382, 1014, 42, "필요한 기억 조각  3       업그레이드 후 잔여  9", 20, ColorOf("c3aaa8"));
        RectTransform equip = Box("ReplaceButton", detail, 48, 444, 460, 64, ColorOf("241b1e"));
        MakeButton(equip);
        Border(equip, 460, 64, ColorOf("85666b"));
        CenterLabel(equip, font, "2번 슬롯에 교체", 22);
        RectTransform upgrade = Box("UpgradeButton", detail, 534, 444, 528, 64, ColorOf("76474e"));
        MakeButton(upgrade);
        CenterLabel(upgrade, font, "업그레이드", 22);
        RectTransform footer = Rect("Footer", window, 96, 984, 1728, 64);
        Box("Divider", footer, 0, 0, 1728, 1, UiTheme.Line);
        Label("InputHint", footer, font, 0, 24, 1450, 32, "스킬과 장착 슬롯을 선택하세요.", 18, Muted);
        RectTransform close = Box("CloseButton", footer, 1590, 12, 138, 52, Background);
        var closeButton = MakeButton(close);
        CenterLabel(close, font, "닫기", 20);
        // 키보드 입력이나 소비 로직을 가장하지 않고 닫기만 직렬화된 이벤트로 연결합니다.
        UnityEventTools.AddBoolPersistentListener(closeButton.onClick, root.gameObject.SetActive, false);

        Canvas.ForceUpdateCanvases();
        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(window);
        PrefabUtility.SaveAsPrefabAsset(root.gameObject, WindowPath);
    }

    #endregion
    #region 생성 도우미

    static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height) {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.layer = 5;
        var rect = (RectTransform)obj.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
        return rect;
    }

    static RectTransform Box(string name, Transform parent, float x, float y, float width, float height, Color color) {
        RectTransform rect = Rect(name, parent, x, y, width, height);
        var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return rect;
    }

    static TextMeshProUGUI Label(string name, Transform parent, TMP_FontAsset font, float x, float y, float width, float height, string value, float size, Color color) {
        var label = Rect(name, parent, x, y, width, height).gameObject.AddComponent<TextMeshProUGUI>();
        label.font = font;
        label.text = value;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.TopLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }

    static void CenterLabel(RectTransform parent, TMP_FontAsset font, string text, float size) {
        var label = Label("Label", parent, font, 0, 0, parent.sizeDelta.x, parent.sizeDelta.y, text, size, High);
        Stretch(label.rectTransform);
        label.alignment = TextAlignmentOptions.Center;
    }

    static UnityEngine.UI.Button MakeButton(RectTransform rect) {
        var image = rect.GetComponent<UnityEngine.UI.Image>();
        image.raycastTarget = true;
        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        var colors = button.colors;
        colors.highlightedColor = new Color(1.2f, 1.2f, 1.2f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f);
        colors.selectedColor = Color.white;
        colors.disabledColor = Color.white;
        colors.fadeDuration = 0.1f;
        button.colors = colors;
        return button;
    }

    static void Border(Transform parent, float width, float height, Color color) {
        Box("BorderTop", parent, 0, 0, width, 1, color);
        Box("BorderBottom", parent, 0, height - 1, width, 1, color);
        Box("BorderLeft", parent, 0, 0, 1, height, color);
        Box("BorderRight", parent, width - 1, 0, 1, height, color);
    }

    static void RecordOverrides(GameObject root) {
        foreach (Component component in root.GetComponentsInChildren<Component>(true)) {
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true)) {
            PrefabUtility.RecordPrefabInstancePropertyModifications(child.gameObject);
        }
    }

    static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    static Color ColorOf(string hex) {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }

    #endregion
}
#endif
