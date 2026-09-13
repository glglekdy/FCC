#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 스킬 정비 화면 프리팹을 한 번 찍어내는 에디터 도구.
//
// 예전에는 SkillLoadoutView가 런타임에 Canvas를 직접 지었다. 그 배치 값(창 크기·여백·글자 크기)을 버리지 않고
// 여기로 옮겨왔다 — 메뉴를 한 번 누르면 같은 화면이 프리팹 에셋으로 남고, 그 뒤로는 인스펙터에서 꾸미면 된다.
// 디자인을 갈아엎어 처음부터 다시 뽑고 싶을 때만 다시 실행하면 된다. **다시 실행하면 프리팹을 덮어씁니다.**
//
// 사용법: Tools ▸ FCC ▸ Build Skill Loadout Prefab → Tools ▸ FCC ▸ Place Skill Loadout In Scene
public static class SkillLoadoutPrefabBuilder {
    #region 경로 · 색 · 크기

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/UI";
    const string WindowPrefabPath = PrefabDir + "/SkillLoadout.prefab";
    const string RowPrefabPath = PrefabDir + "/SkillLoadoutRow.prefab";

    // 원래는 보랏빛 다크 + 금색 포인트였는데, 아트 디렉션이 「퇴락한 빈티지 극장」으로 확정되면서
    // UiTheme 토큰으로 갈아끼웠다. **여기서 색을 새로 만들지 않는다** — 빌더와 프리팹의 색이 갈라지면
    // Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 으로도 맞출 수 없게 된다.
    static readonly Color DimColor = UiTheme.Dim; // 뒤쪽 게임 화면을 덮는 어두운 막.
    static readonly Color WindowColor = UiTheme.With(UiTheme.Panel, 0.98f);
    static readonly Color SlotColor = UiTheme.PanelRaised;
    static readonly Color SlotSelectedColor = UiTheme.Accent;
    static readonly Color RowColor = UiTheme.PanelRaised;
    static readonly Color RowHighlightColor = UiTheme.Line; // 커서가 올라간 줄. 면으로 쓰기엔 Accent 가 세서 경계선 색을 면으로 돌려 쓴다.
    static readonly Color TextColor = UiTheme.TextHigh;
    static readonly Color DimTextColor = UiTheme.TextMuted;
    static readonly Color AccentColor = UiTheme.Accent; // "장착 중" · 레벨 · 강화 비용. 이 화면의 유일한 포인트 컬러다.

    const float TitleFontSize = 44f;
    const float SlotFontSize = 24f;
    const float RowFontSize = 26f;
    const float BodyFontSize = 21f;

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Build Skill Loadout Prefab")]
    public static void BuildPrefabs() {
        // 프리팹 편집 모드에서 실행하면 임시로 만든 오브젝트가 그 프리팹 안으로 들어가 버린다.
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[SkillLoadout] 프리팹 편집 모드를 닫고 다시 실행하세요. 임시 오브젝트가 편집 중인 프리팹에 섞여 들어갑니다.");
            return;
        }

        EnsureFolder(PrefabDir);

        TMP_FontAsset font = PrefabBuilderFont.LoadProjectFont("SkillLoadout");

        SkillRowView rowPrefab = BuildRowPrefab(font);
        BuildWindowPrefab(font, rowPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SkillLoadout] 프리팹을 만들었습니다.\n· {WindowPrefabPath}\n· {RowPrefabPath}\n" +
            "씬에 올리려면 Tools ▸ FCC ▸ Place Skill Loadout In Scene 을 실행하거나 프리팹을 그냥 드래그하세요.");
    }

    // 씬에 프리팹을 올린다. 코드로 UI를 짓던 시절의 SkillLoadoutView 컴포넌트가 남아 있으면 같이 걷어낸다
    // (그대로 두면 프리팹 쪽이 싱글턴 자리를 먼저 잡거나 잃어서 정비 화면이 안 뜬다).
    [MenuItem("Tools/FCC/Place Skill Loadout In Scene")]
    public static void PlaceInScene() {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WindowPrefabPath);
        if (prefab == null) {
            Debug.LogError($"[SkillLoadout] 프리팹이 없습니다({WindowPrefabPath}). 먼저 Tools ▸ FCC ▸ Build Skill Loadout Prefab 을 실행하세요.");
            return;
        }

        bool alreadyPlaced = false;

        // 꺼둔 상태로 씬에 남아 있는 것도 찾아야 하므로 Include.
        foreach (SkillLoadoutView existing in UnityEngine.Object.FindObjectsByType<SkillLoadoutView>(FindObjectsInactive.Include)) {
            // 이미 프리팹 인스턴스라면 정상적으로 올라간 것이므로 손대지 않는다.
            if (PrefabUtility.GetCorrespondingObjectFromSource(existing) != null) {
                alreadyPlaced = true;
                continue;
            }

            Debug.Log($"[SkillLoadout] '{existing.name}' 에 붙어 있던 구버전 SkillLoadoutView 컴포넌트를 제거했습니다.", existing.gameObject);
            Undo.DestroyObjectImmediate(existing);
        }

        if (alreadyPlaced) {
            Debug.Log("[SkillLoadout] 씬에 이미 SkillLoadout 프리팹이 있어 새로 놓지 않았습니다.");
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place Skill Loadout");

        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);

        Debug.Log("[SkillLoadout] 씬에 SkillLoadout 프리팹을 놓았습니다. 씬을 저장하세요. " +
            "클릭 조작을 쓰려면 씬에 EventSystem이 있어야 합니다.", instance);
    }

    #endregion
    #region 목록 줄 프리팹

    // "보유 스킬" 목록의 한 줄. 아이콘 · 이름 · 상태 표기가 가로로 놓인다.
    static SkillRowView BuildRowPrefab(TMP_FontAsset font) {
        Image background = CreateImage("SkillLoadoutRow", null, RowColor);
        background.rectTransform.sizeDelta = new Vector2(760f, 48f);

        HorizontalLayoutGroup layout = background.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 0, 0);
        layout.spacing = 12f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        // 강제 확장을 끄고 이름 라벨에만 flexibleWidth를 준다. 켜두면 아이콘 칸까지 같이 늘어나 정사각형이 깨진다.
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        layout.childAlignment = TextAnchor.MiddleLeft;

        SetSize(background.rectTransform, -1f, 48f);

        Button button = background.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None; // 색은 SkillRowView가 상태에 맞춰 직접 칠한다.
        button.targetGraphic = background;

        Image icon = CreateIcon("Icon", background.transform, 34f, 34f);

        TextMeshProUGUI nameLabel = CreateText("Name", background.transform, font, RowFontSize, TextAlignmentOptions.Left, TextColor);
        LayoutElement nameElement = nameLabel.gameObject.AddComponent<LayoutElement>();
        nameElement.flexibleWidth = 1f; // 남는 폭은 이름이 전부 가져간다.

        TextMeshProUGUI stateLabel = CreateText("State", background.transform, font, BodyFontSize, TextAlignmentOptions.Right, DimTextColor);
        SetSize(stateLabel.rectTransform, 200f, -1f);

        SkillRowView view = background.gameObject.AddComponent<SkillRowView>();
        view.background = background;
        view.nameLabel = nameLabel;
        view.stateLabel = stateLabel;
        view.iconImage = icon;
        view.button = button;
        view.normalColor = RowColor;
        view.highlightColor = RowHighlightColor;
        view.nameColor = TextColor;
        view.equippedColor = AccentColor;
        view.cooldownColor = DimTextColor;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(background.gameObject, RowPrefabPath);
        UnityEngine.Object.DestroyImmediate(background.gameObject);

        return saved != null ? saved.GetComponent<SkillRowView>() : null;
    }

    #endregion
    #region 정비 창 프리팹

    static void BuildWindowPrefab(TMP_FontAsset font, SkillRowView rowPrefab) {
        GameObject rootObj = new GameObject("SkillLoadout", typeof(RectTransform));

        Canvas canvas = rootObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200; // 일시정지 메뉴보다 위에 뜨도록 넉넉히 높게.

        CanvasScaler scaler = rootObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f; // 가로세로 비율이 달라져도 창이 화면 밖으로 나가지 않도록 절충.

        rootObj.AddComponent<GraphicRaycaster>(); // 마우스 클릭을 받으려면 필요하다.

        // 뒤쪽 게임 화면을 덮는 막. raycastTarget이 켜져 있어 뒤의 UI가 눌리는 것도 막아준다.
        Image dim = CreateImage("Dim", rootObj.transform, DimColor);
        Stretch(dim.rectTransform);

        Image window = CreateImage("Window", dim.transform, WindowColor);
        window.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        window.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        window.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        window.rectTransform.anchoredPosition = Vector2.zero;
        window.rectTransform.sizeDelta = new Vector2(920f, 900f); // 강화 패널이 들어가면서 세로를 늘렸다.

        VerticalLayoutGroup layout = window.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(44, 44, 36, 36);
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TextMeshProUGUI title = CreateText("Title", window.transform, font, TitleFontSize, TextAlignmentOptions.Center, TextColor);
        title.text = "정비하기";
        title.fontStyle = FontStyles.Bold;
        SetSize(title.rectTransform, -1f, 58f);

        SkillSlotView[] slots = BuildSlotRow(window.transform, font);

        TextMeshProUGUI listTitle = CreateText("ListTitle", window.transform, font, BodyFontSize + 4f, TextAlignmentOptions.Left, DimTextColor);
        listTitle.text = "보유 스킬";
        SetSize(listTitle.rectTransform, -1f, 32f);

        RectTransform listArea = BuildListArea(window.transform);

        TextMeshProUGUI description = CreateText("Description", window.transform, font, BodyFontSize, TextAlignmentOptions.TopLeft, DimTextColor);
        description.textWrappingMode = TextWrappingModes.Normal;
        SetSize(description.rectTransform, -1f, 86f);

        SkillLoadoutView view = rootObj.AddComponent<SkillLoadoutView>();
        BuildUpgradePanel(window.transform, font, view);

        TextMeshProUGUI help = CreateText("Help", window.transform, font, BodyFontSize - 2f, TextAlignmentOptions.Center, DimTextColor);
        help.text = "[↑↓] 스킬 선택   [1·2·3] 슬롯   [Enter] 장착   [Del] 해제   [E] 강화   [R] 되돌리기   [ESC] 닫기";
        SetSize(help.rectTransform, -1f, 30f);

        view.windowRoot = dim.gameObject;
        view.slotViews = slots;
        view.rowContainer = listArea;
        view.rowPrefab = rowPrefab;
        view.listTitleLabel = listTitle;
        view.descriptionLabel = description;

        PrefabUtility.SaveAsPrefabAsset(rootObj, WindowPrefabPath);
        UnityEngine.Object.DestroyImmediate(rootObj);
    }

    // 강화 패널 — 레벨 표시 · 현재→다음 수치 비교표 · 비용 · 강화/되돌리기 버튼.
    // 내용은 전부 SkillLoadoutView가 채우므로 여기서는 빈 칸과 자리만 잡아 둔다.
    static void BuildUpgradePanel(Transform parent, TMP_FontAsset font, SkillLoadoutView view) {
        RectTransform panel = CreateRect("UpgradePanel", parent);

        VerticalLayoutGroup layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        SetSize(panel, -1f, 196f);

        TextMeshProUGUI level = CreateText("LevelLabel", panel, font, BodyFontSize + 3f, TextAlignmentOptions.Left, AccentColor);
        level.text = "Lv 0 / 2";
        level.fontStyle = FontStyles.Bold;
        SetSize(level.rectTransform, -1f, 30f);

        // 비교표는 줄바꿈되면 열이 어긋나 읽기 어려워지므로 접지 않는다.
        TextMeshProUGUI stats = CreateText("StatsLabel", panel, font, BodyFontSize, TextAlignmentOptions.TopLeft, TextColor);
        stats.textWrappingMode = TextWrappingModes.NoWrap;
        SetSize(stats.rectTransform, -1f, 86f);

        TextMeshProUGUI cost = CreateText("CostLabel", panel, font, BodyFontSize - 1f, TextAlignmentOptions.Left, AccentColor);
        cost.text = "필요 3 (최소 3)   ·   보유 0";
        SetSize(cost.rectTransform, -1f, 26f);

        RectTransform buttonRow = CreateRect("ButtonRow", panel);
        HorizontalLayoutGroup rowLayout = buttonRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = 10f;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = true;
        rowLayout.childAlignment = TextAnchor.MiddleLeft;
        SetSize(buttonRow, -1f, 44f);

        // 되돌리기를 왼쪽·수수하게, 강화를 오른쪽·강조색으로 둔다. 조각을 쓰는 쪽이 주된 행동이기 때문.
        Button refund = CreateButton("RefundButton", buttonRow, font, "되돌리기", RowColor, DimTextColor, 240f, out TextMeshProUGUI refundLabel);
        Button upgrade = CreateButton("UpgradeButton", buttonRow, font, "강화", AccentColor, TextColor, 200f, out TextMeshProUGUI upgradeLabel);

        view.upgradePanelRoot = panel.gameObject;
        view.levelLabel = level;
        view.statsLabel = stats;
        view.costLabel = cost;
        view.upgradeButton = upgrade;
        view.upgradeButtonLabel = upgradeLabel;
        view.refundButton = refund;
        view.refundButtonLabel = refundLabel;
    }

    static Button CreateButton(string name, Transform parent, TMP_FontAsset font, string text, Color background, Color textColor, float width, out TextMeshProUGUI label) {
        Image image = CreateImage(name, parent, background);
        SetSize(image.rectTransform, width, 44f);

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image; // 색 전환은 Button 기본 동작에 맡긴다 — 슬롯·줄과 달리 상태가 단순하다.

        label = CreateText("Label", image.transform, font, BodyFontSize, TextAlignmentOptions.Center, textColor);
        Stretch(label.rectTransform);
        label.text = text;

        return button;
    }

    // 상단 장착 슬롯 3칸.
    static SkillSlotView[] BuildSlotRow(Transform parent, TMP_FontAsset font) {
        RectTransform slotRow = CreateRect("SlotRow", parent);

        HorizontalLayoutGroup layout = slotRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 18f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        SetSize(slotRow, -1f, 128f);

        SkillSlotView[] slots = new SkillSlotView[SkillManager.SlotCount];
        for (int i = 0; i < slots.Length; i++) {
            slots[i] = CreateSlotView(slotRow, font, i);
        }

        return slots;
    }

    static SkillSlotView CreateSlotView(Transform parent, TMP_FontAsset font, int slotIndex) {
        Image background = CreateImage($"Slot{slotIndex}", parent, SlotColor);

        VerticalLayoutGroup layout = background.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 14, 14);
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        // 아이콘 칸이 세로로 늘어나지 않게 끄고, 남는 높이는 스킬 이름이 먹는다.
        layout.childForceExpandHeight = false;
        layout.childAlignment = TextAnchor.MiddleCenter;

        Button button = background.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None; // 색은 SkillSlotView가 선택 상태에 맞춰 직접 칠한다.
        button.targetGraphic = background;

        Image icon = CreateIcon("Icon", background.transform, 44f, 44f);

        // "슬롯 1" 고정 문구. 코드가 건드리지 않으니 문구를 바꾸고 싶으면 프리팹에서 고치면 된다.
        TextMeshProUGUI slotLabel = CreateText("SlotLabel", background.transform, font, SlotFontSize - 4f, TextAlignmentOptions.Center, DimTextColor);
        slotLabel.text = $"슬롯 {slotIndex + 1}";
        SetSize(slotLabel.rectTransform, -1f, 24f);

        TextMeshProUGUI skillLabel = CreateText("SkillLabel", background.transform, font, SlotFontSize, TextAlignmentOptions.Center, TextColor);
        skillLabel.text = "비어 있음";
        skillLabel.fontStyle = FontStyles.Bold;
        LayoutElement skillElement = skillLabel.gameObject.AddComponent<LayoutElement>();
        skillElement.flexibleHeight = 1f;

        SkillSlotView view = background.gameObject.AddComponent<SkillSlotView>();
        view.background = background;
        view.skillLabel = skillLabel;
        view.iconImage = icon;
        view.button = button;
        view.normalColor = SlotColor;
        view.selectedColor = SlotSelectedColor;
        view.skillTextColor = TextColor;
        view.emptyTextColor = DimTextColor;

        return view;
    }

    static RectTransform BuildListArea(Transform parent) {
        RectTransform listArea = CreateRect("ListArea", parent);

        VerticalLayoutGroup layout = listArea.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 남는 세로 공간을 목록이 가져간다. 스킬이 늘어나도 창 크기만 키우면 된다.
        LayoutElement element = listArea.gameObject.AddComponent<LayoutElement>();
        element.flexibleHeight = 1f;

        return listArea;
    }

    #endregion
    #region 생성 도우미

    static RectTransform CreateRect(string name, Transform parent) {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        if (parent != null) obj.transform.SetParent(parent, false);
        return (RectTransform)obj.transform;
    }

    static Image CreateImage(string name, Transform parent, Color color) {
        Image image = CreateRect(name, parent).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    // 스킬 아이콘 칸. 스프라이트가 없으면 흰 사각형이 되므로 Image를 꺼둔 상태로 만든다
    // (SkillSlotView·SkillRowView가 스킬을 물릴 때 켠다).
    static Image CreateIcon(string name, Transform parent, float width, float height) {
        Image icon = CreateImage(name, parent, Color.white);
        icon.preserveAspect = true;
        icon.raycastTarget = false; // 아이콘이 아래 버튼의 클릭을 가로채지 않도록.
        icon.enabled = false;
        SetSize(icon.rectTransform, width, height);
        return icon;
    }

    static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size, TextAlignmentOptions alignment, Color color) {
        TextMeshProUGUI text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();

        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false; // 글자가 아래 버튼의 클릭을 가로채지 않도록.

        return text;
    }

    static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 레이아웃 그룹 안에서 크기를 고정시킨다. LayoutElement가 없으면 내용에 따라 제멋대로 늘어난다.
    // 음수를 넘기면 그 축은 건드리지 않는다 (한쪽만 고정하고 싶을 때).
    static void SetSize(RectTransform rect, float width, float height) {
        LayoutElement element = rect.GetComponent<LayoutElement>();
        if (element == null) element = rect.gameObject.AddComponent<LayoutElement>();

        if (width >= 0f) {
            element.preferredWidth = width;
            element.flexibleWidth = 0f;
        }
        if (height >= 0f) {
            element.preferredHeight = height;
            element.flexibleHeight = 0f;
        }
    }

    static void EnsureFolder(string path) {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    #endregion
}
#endif
