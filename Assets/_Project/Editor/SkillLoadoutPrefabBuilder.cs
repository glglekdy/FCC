#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 스킬 정비 화면 프리팹(SkillLoadout)과 목록 한 줄(SkillLoadoutRow)을 찍어내는 에디터 도구.
//
// 기획 「스킬 정비·강화」의 화면 시안을 1920×1080 기준으로 옮겼다. 왼쪽은 장착 슬롯 3칸과 보유 스킬 목록,
// 오른쪽은 고른 스킬의 설명 · 레벨 · 현재와 다음 수치 · 강화 비용과 버튼이다. 예전 창(920×720, 목록만 있고
// 강화 패널은 빌더에만 있던 것)을 갈아엎었다.
//
// 색은 UiTheme 토큰만 쓴다. **여기서 색을 새로 만들지 않는다** — 빌더와 프리팹의 색이 갈라지면
// Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 으로도 맞출 수 없게 된다.
//
// **다시 실행하면 두 프리팹을 덮어씁니다.** SkillLoadout 은 InGameUi 프리팹 안에 중첩돼 있는데, 정비 화면은
// 싱글턴(Instance)으로만 찾으므로 밖에서 물린 참조는 없다. 다만 InGameUi 쪽에서 손으로 덮어쓴 배치값은 풀린다.
//
// 사용법: Tools ▸ FCC ▸ Build Skill Loadout Prefab
public static class SkillLoadoutPrefabBuilder {
    #region 경로 · 치수

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/UI";
    const string WindowPrefabPath = PrefabDir + "/SkillLoadout.prefab";
    const string RowPrefabPath = PrefabDir + "/SkillLoadoutRow.prefab";

    const float WindowW = 1360f, WindowH = 820f;
    const float Pad = 56f; // 창 안쪽 좌우 여백.
    const float BodyY = 128f; // 머리 아래 본문이 시작하는 높이.
    const float LeftW = 456f; // 왼쪽 열 폭 = 슬롯 144 × 3 + 간격 12 × 2.
    const float DetailX = 568f, DetailW = 736f; // 오른쪽 열.
    const float SlotW = 144f, SlotH = 112f, SlotGap = 12f;
    const float RowH = 48f;

    // 일시정지 메뉴보다 위. InGameUi 캔버스 안에 중첩돼도 이 순서를 지키도록 overrideSorting 을 켠다.
    const int SortingOrder = 200;

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
        if (rowPrefab == null) {
            Debug.LogError("[SkillLoadout] 목록 줄 프리팹을 저장하지 못해 창 프리팹을 만들지 않았습니다 — " + RowPrefabPath);
            return;
        }

        BuildWindowPrefab(font, rowPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[SkillLoadout] 프리팹을 만들었습니다.\n· {WindowPrefabPath}\n· {RowPrefabPath}\n" +
            "InGameUi 안의 SkillLoadout 은 **켜진 상태**여야 합니다(꺼져 있으면 거울이 정비 화면을 열지 못합니다).");
    }

    // 씬에 프리팹을 올린다. 이미 InGameUi 등으로 들어가 있으면 새로 놓지 않는다.
    [MenuItem("Tools/FCC/Place Skill Loadout In Scene")]
    public static void PlaceInScene() {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(WindowPrefabPath);
        if (prefab == null) {
            Debug.LogError($"[SkillLoadout] 프리팹이 없습니다({WindowPrefabPath}). 먼저 Tools ▸ FCC ▸ Build Skill Loadout Prefab 을 실행하세요.");
            return;
        }

        foreach (SkillLoadoutView existing in Object.FindObjectsByType<SkillLoadoutView>(FindObjectsInactive.Include)) {
            Debug.Log($"[SkillLoadout] 씬에 이미 '{existing.name}' 이 있어 새로 놓지 않았습니다." +
                (existing.gameObject.activeInHierarchy ? "" : " **꺼져 있으니 켜 주세요.**"), existing.gameObject);
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

    // "보유 스킬" 목록의 한 줄. 이름은 왼쪽, "장착 중 · Lv 1 / 2" 는 오른쪽에 붙는다.
    static SkillRowView BuildRowPrefab(TMP_FontAsset font) {
        GameObject root = new GameObject("SkillLoadoutRow", typeof(RectTransform));
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(LeftW, RowH);

        Image background = root.AddComponent<Image>();
        background.color = UiTheme.Transparent;
        background.raycastTarget = true; // 투명해도 마우스를 받아야 줄 위에 커서를 올려 고를 수 있다.

        LayoutElement element = root.AddComponent<LayoutElement>();
        element.preferredHeight = RowH;
        element.flexibleHeight = 0f;

        Image marker = Box(rootRect, "FocusMarker", UiTheme.Accent);
        Place(marker.rectTransform, 0f, 0f, 3f, RowH);
        marker.gameObject.SetActive(false); // 커서가 올라왔을 때만 켜진다.

        TextMeshProUGUI nameLabel = Label(rootRect, font, "Name", "Skill Name", 19f, UiTheme.TextBody, 18f, 12f, 260f, 24f);
        TextMeshProUGUI stateLabel = Label(rootRect, font, "State", "Lv 0 / 2", 14f, UiTheme.TextMuted, 200f, 15f, LeftW - 216f, 20f);
        stateLabel.alignment = TextAlignmentOptions.TopRight;
        stateLabel.richText = true; // "장착 중" 만 포인트 컬러로 칠한다.

        Button button = root.AddComponent<Button>();
        button.transition = Selectable.Transition.None; // 줄의 색은 SkillRowView 가 칠한다. 버튼 틴트가 겹치면 잠긴 줄이 더 흐려진다.

        SkillRowView view = root.AddComponent<SkillRowView>();
        view.background = background;
        view.focusMarker = marker.gameObject;
        view.nameLabel = nameLabel;
        view.stateLabel = stateLabel;
        view.button = button;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
        Object.DestroyImmediate(root);

        return saved != null ? saved.GetComponent<SkillRowView>() : null;
    }

    #endregion
    #region 창 프리팹

    static void BuildWindowPrefab(TMP_FontAsset font, SkillRowView rowPrefab) {
        GameObject root = new GameObject("SkillLoadout", typeof(RectTransform));

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        // 화면 전체를 덮는 막. 열고 닫을 때 이것만 켜고 끈다(windowRoot). 막이 클릭을 받아 뒤쪽 게임 화면으로 새지 않는다.
        Image dim = Box(root.transform, "Dim", UiTheme.Dim);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        Image window = Box(dim.rectTransform, "Window", UiTheme.Panel);
        RectTransform windowRect = window.rectTransform;
        windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(WindowW, WindowH);
        Border(windowRect, "Border", UiTheme.Line, 1f);

        SkillLoadoutView view = root.AddComponent<SkillLoadoutView>();
        view.windowRoot = dim.gameObject;
        view.rowPrefab = rowPrefab;

        BuildHeader(windowRect, font, view);
        BuildLeft(windowRect, font, view);
        BuildDetail(windowRect, font, view);
        BuildFooter(windowRect, font);

        PrefabUtility.SaveAsPrefabAsset(root, WindowPrefabPath);
        Object.DestroyImmediate(root);
    }

    static void BuildHeader(RectTransform window, TMP_FontAsset font, SkillLoadoutView view) {
        RectTransform header = Empty(window, "Header", Pad, 40f, WindowW - Pad * 2f, 65f);

        Label(header, font, "Title", "정비하기", 40f, UiTheme.TextHigh, 0f, 0f, 400f, 48f);

        // 보유 조각 수는 오른쪽 끝에, 무엇을 세는 값인지 알려주는 문구는 그 왼쪽에 둔다.
        // 강화 비용을 판단할 근거라 화면이 열려 있는 내내 보여야 한다.
        TextMeshProUGUI shardLabel = Label(header, font, "ShardLabel", "기억 조각", 16f, UiTheme.TextMuted, header.sizeDelta.x - 260f, 20f, 120f, 22f);
        shardLabel.alignment = TextAlignmentOptions.TopRight;

        TextMeshProUGUI shardValue = Label(header, font, "ShardCount", "0", 32f, UiTheme.TextHigh, header.sizeDelta.x - 130f, 6f, 130f, 40f);
        shardValue.alignment = TextAlignmentOptions.TopRight;
        view.shardCountLabel = shardValue;

        Image divider = Box(header, "Divider", UiTheme.Line);
        Place(divider.rectTransform, 0f, 64f, header.sizeDelta.x, 1f);
    }

    static void BuildLeft(RectTransform window, TMP_FontAsset font, SkillLoadoutView view) {
        Label(window, font, "SlotsCaption", "장착 슬롯", 16f, UiTheme.TextMuted, Pad, BodyY, 300f, 20f);

        view.slotViews = new SkillSlotView[SkillManager.SlotCount];
        for (int i = 0; i < SkillManager.SlotCount; i++) {
            view.slotViews[i] = BuildSlot(window, font, i, Pad + i * (SlotW + SlotGap), BodyY + 32f);
        }

        Label(window, font, "ListCaption", "보유 스킬", 16f, UiTheme.TextMuted, Pad, BodyY + 172f, 300f, 20f);

        // 줄은 실행 중에 SkillLoadoutView 가 게임에 있는 스킬 수만큼 찍어 넣는다. 여기서는 자리만 잡는다.
        RectTransform list = Empty(window, "RowContainer", Pad, BodyY + 204f, LeftW, 390f);
        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        view.rowContainer = list;

        Image column = Box(window, "ColumnDivider", UiTheme.Line);
        Place(column.rectTransform, DetailX - 32f, BodyY, 1f, 594f);
    }

    static SkillSlotView BuildSlot(RectTransform window, TMP_FontAsset font, int index, float x, float y) {
        Image slot = Box(window, "Slot" + index, UiTheme.PanelRaised);
        slot.raycastTarget = true; // 슬롯을 눌러 장착 · 해제가 향할 칸을 고른다.
        Place(slot.rectTransform, x, y, SlotW, SlotH);
        Border(slot.rectTransform, "Border", UiTheme.Line, 1f);

        GameObject focus = Border(slot.rectTransform, "FocusBorder", UiTheme.Accent, 2f).gameObject;
        focus.SetActive(false);

        TextMeshProUGUI slotLabel = Label(slot.rectTransform, font, "SlotLabel", "슬롯 " + (index + 1), 14f, UiTheme.TextMuted, 0f, 12f, SlotW, 18f);
        slotLabel.alignment = TextAlignmentOptions.Top;

        // 이름이 긴 스킬(Invisible Reality)은 두 줄로 접힌다. 칸 높이를 두 줄 몫으로 잡아둔다.
        TextMeshProUGUI skillLabel = Label(slot.rectTransform, font, "SkillLabel", "비어 있음", 17f, UiTheme.TextHigh, 8f, 36f, SlotW - 16f, 44f);
        skillLabel.alignment = TextAlignmentOptions.Center;

        TextMeshProUGUI levelLabel = Label(slot.rectTransform, font, "LevelLabel", "", 13f, UiTheme.TextMuted, 0f, 86f, SlotW, 18f);
        levelLabel.alignment = TextAlignmentOptions.Top;

        Button button = slot.gameObject.AddComponent<Button>();
        button.transition = Selectable.Transition.None;

        SkillSlotView view = slot.gameObject.AddComponent<SkillSlotView>();
        view.background = slot;
        view.focusBorder = focus;
        view.skillLabel = skillLabel;
        view.levelLabel = levelLabel;
        view.button = button;
        return view;
    }

    static void BuildDetail(RectTransform window, TMP_FontAsset font, SkillLoadoutView view) {
        RectTransform detail = Empty(window, "Detail", DetailX, BodyY, DetailW, 594f);
        view.detailRoot = detail.gameObject;

        view.nameLabel = Label(detail, font, "Name", "Skill Name", 32f, UiTheme.TextHigh, 0f, 0f, DetailW, 40f);
        view.roleLabel = Label(detail, font, "Role", "", 16f, UiTheme.TextMuted, 0f, 46f, DetailW, 22f);
        view.descriptionLabel = Label(detail, font, "Description", "", 18f, UiTheme.TextBody, 0f, 82f, DetailW, 76f);

        // 레벨 칸부터 아래는 강화 단계가 있는 스킬에서만 보인다.
        RectTransform level = Empty(detail, "Level", 0f, 170f, DetailW, 424f);
        view.levelRoot = level.gameObject;

        view.levelPips = new Image[2];
        for (int i = 0; i < view.levelPips.Length; i++) {
            Image pip = Box(level, "Pip" + i, UiTheme.PanelRaised);
            Place(pip.rectTransform, i * 48f, 8f, 40f, 8f);
            view.levelPips[i] = pip;
        }
        view.levelLabel = Label(level, font, "LevelLabel", "Lv 0 / 2", 16f, UiTheme.TextMuted, 104f, 0f, 300f, 24f);

        // 머리글의 <pos> 는 SkillLoadoutView.BuildStatTable 의 열 위치와 같아야 한다.
        TextMeshProUGUI header = Label(level, font, "StatsHeader", "항목<pos=46%>현재<pos=76%>다음", 14f, UiTheme.TextMuted, 0f, 44f, DetailW, 20f);
        header.richText = true;

        Image headerLine = Box(level, "StatsDivider", UiTheme.Line);
        Place(headerLine.rectTransform, 0f, 70f, DetailW, 1f);

        TextMeshProUGUI stats = Label(level, font, "Stats", "", 18f, UiTheme.TextBody, 0f, 82f, DetailW, 190f);
        stats.richText = true;
        stats.lineSpacing = 28f; // 다섯 줄(Cycle of Fate)이 칸 안에 들어가면서 줄마다 행이 구분돼 보이는 간격.
        view.statsLabel = stats;

        Image actionLine = Box(level, "ActionDivider", UiTheme.Line);
        Place(actionLine.rectTransform, 0f, 290f, DetailW, 1f);

        TextMeshProUGUI cost = Label(level, font, "Cost", "필요 3", 18f, UiTheme.TextHigh, 0f, 316f, 360f, 26f);
        cost.richText = true;
        view.costLabel = cost;

        view.upgradeButton = ActionButton(level, font, "Btn_Upgrade", "Lv 1 로 강화", DetailW - 210f, 304f, 210f, UiTheme.Accent, UiTheme.TextHigh, false, out view.upgradeButtonLabel);
        view.refundButton = ActionButton(level, font, "Btn_Refund", "되돌리기 · 조각 3 반환", DetailW - 210f - 12f - 250f, 304f, 250f, UiTheme.Panel, UiTheme.TextBody, true, out view.refundButtonLabel);

        // 장착 상태는 강화와 무관하게 늘 보여야 하므로 레벨 영역 밖(상세 아래쪽)에 둔다.
        view.equipStatusLabel = Label(detail, font, "EquipStatus", "", 15f, UiTheme.TextMuted, 0f, 554f, DetailW, 24f);
    }

    static Button ActionButton(RectTransform parent, TMP_FontAsset font, string name, string text, float x, float y, float width,
        Color fill, Color textColor, bool outlined, out TMP_Text label) {

        Image bg = Box(parent, name, fill);
        bg.raycastTarget = true; // 버튼은 자기 이미지가 클릭을 받아야 눌린다.
        Place(bg.rectTransform, x, y, width, 48f);
        if (outlined) Border(bg.rectTransform, "Border", UiTheme.Line, 1f);

        TextMeshProUGUI labelText = Text(bg.rectTransform, "Label", font, 17f, TextAlignmentOptions.Center, textColor);
        labelText.text = text;
        Stretch(labelText.rectTransform);
        label = labelText;

        Button button = bg.gameObject.AddComponent<Button>();
        // 조각이 모자라 누를 수 없을 때는 버튼 전체를 흐리게 한다. 기본 틴트를 쓰되 비활성만 뚜렷하게 낮춘다.
        ColorBlock colors = button.colors;
        colors.disabledColor = new Color(1f, 1f, 1f, 0.35f);
        button.colors = colors;
        return button;
    }

    static void BuildFooter(RectTransform window, TMP_FontAsset font) {
        Image divider = Box(window, "FooterDivider", UiTheme.Line);
        Place(divider.rectTransform, Pad, 742f, WindowW - Pad * 2f, 1f);

        Label(window, font, "Help", "↑↓ 스킬     1 2 3 슬롯     Enter 장착 · 해제     E 강화     R 되돌리기     ESC 닫기",
            15f, UiTheme.TextMuted, Pad, 760f, WindowW - Pad * 2f, 22f);
    }

    #endregion
    #region 생성 도우미

    // 이 화면은 전부 부모의 좌상단 기준으로 자리를 잡는다. 시안 좌표를 그대로 옮겨 적을 수 있어 대조가 쉽다.
    static void Place(RectTransform rect, float x, float y, float w, float h) {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }

    static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    static RectTransform Empty(Transform parent, string name, float x, float y, float w, float h) {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        RectTransform rect = (RectTransform)obj.transform;
        Place(rect, x, y, w, h);
        return rect;
    }

    static Image Box(Transform parent, string name, Color color) {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        Image image = obj.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false; // 기본은 끈다. 클릭을 받아야 하는 면(막 · 슬롯 · 줄 · 버튼)만 만든 쪽에서 켠다.
        return image;
    }

    static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size, TextAlignmentOptions align, Color color) {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = align;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, string name, string content, float size, Color color,
        float x, float y, float w, float h) {

        TextMeshProUGUI label = Text(parent, name, font, size, TextAlignmentOptions.TopLeft, color);
        label.text = content;
        Place(label.rectTransform, x, y, w, h);
        return label;
    }

    // Unity UI 의 Image 는 테두리를 그리지 못해 얇은 이미지 4개를 변에 붙인다.
    static RectTransform Border(RectTransform parent, string name, Color color, float thickness) {
        RectTransform border = Empty(parent, name, 0f, 0f, 0f, 0f);
        Stretch(border);

        Edge(border, "Top", color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness));
        Edge(border, "Bottom", color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness));
        Edge(border, "Left", color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f));
        Edge(border, "Right", color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f));

        return border;
    }

    static void Edge(RectTransform parent, string name, Color color, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size) {
        Image edge = Box(parent, name, color);

        RectTransform rect = edge.rectTransform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
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
