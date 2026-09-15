#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 기록 선택 화면(SaveSlotSelect)과 자리 한 줄(SaveSlotRow) 프리팹을 찍어내는 에디터 도구.
//
// Figma 파일 「FCC_UI」의 기록선택 · 기록선택_덮어쓰기확인 두 안을 1920×1080 기준으로 옮겼다.
// 좌표·크기는 그 안에서 읽은 값이고, 색은 Figma 쪽이 이미 UiTheme 토큰과 같은 값이라 토큰을 그대로 쓴다
// (SettingsPanelPrefabBuilder 와 같은 방식).
//
// 줄 프리팹을 먼저 저장하고, 화면 프리팹이 그 에셋을 rowPrefab 으로 문다. 순서가 뒤바뀌면 화면이 옛 줄이나 빈 참조를 문다.
// **다시 실행하면 두 프리팹을 덮어씁니다** — 손으로 고친 배치가 날아가므로 디자인을 갈아엎을 때만 실행한다.
// 색만 다시 맞출 때는 Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 를 쓴다.
//
// 사용법: Tools ▸ FCC ▸ Build Save Slot Prefabs
public static class SaveSlotPrefabBuilder {
    #region 경로 · 치수

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/UI";
    const string ScreenPrefabPath = PrefabDir + "/SaveSlotSelect.prefab";
    const string RowPrefabPath = PrefabDir + "/SaveSlotRow.prefab";

    // Figma 안의 수치. 이름을 붙여두면 어디서 온 값인지 나중에 대조할 수 있다.
    const float CurtainWidth = 120f;
    const float ContentX = 240f, ContentW = 1440f;
    const float HeaderY = 112f;
    const float ListY = 272f, ListH = 648f, RowGap = 24f;
    const float FooterY = 952f;
    const float RowW = 1440f, RowH = 200f;
    const float ConfirmW = 720f, ConfirmH = 340f;

    const int SortingOrder = 300; // 메인 로비(10) 위. 설정창과 같은 층이지만 둘이 동시에 열리지는 않는다.

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Build Save Slot Prefabs")]
    public static void BuildPrefabs() {
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[SaveSlot] 프리팹 편집 모드를 닫고 다시 실행하세요. 임시 오브젝트가 섞여 들어갑니다.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(PrefabDir)) {
            Debug.LogError("[SaveSlot] 폴더가 없습니다 — " + PrefabDir);
            return;
        }

        TMP_FontAsset font = PrefabBuilderFont.LoadProjectFont("SaveSlot");

        SaveSlotRowView rowPrefab = BuildRow(font);
        if (rowPrefab == null) {
            Debug.LogError("[SaveSlot] 줄 프리팹을 저장하지 못해 화면 프리팹을 만들지 않았습니다 — " + RowPrefabPath);
            return;
        }

        BuildScreen(font, rowPrefab);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SaveSlot] 프리팹을 만들었습니다.\n· " + ScreenPrefabPath + "\n· " + RowPrefabPath + "\n" +
            "메인 메뉴 씬에 SaveSlotSelect 를 올려 꺼두고, MainLobbyView 의 saveSlotPanel 칸에 연결하세요.");
    }

    #endregion
    #region 자리 한 줄

    static SaveSlotRowView BuildRow(TMP_FontAsset font) {
        // 줄은 목록의 VerticalLayoutGroup 안에 들어가므로 루트는 크기만 정해두면 자리는 목록이 잡는다.
        GameObject root = new GameObject("SaveSlotRow", typeof(RectTransform));
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(RowW, RowH);

        Image background = root.AddComponent<Image>();
        background.color = UiTheme.Panel;
        background.raycastTarget = true; // 줄의 마우스 입력(SaveSlotRowView)을 이 바탕이 받는다.

        Border(rootRect, "Border", UiTheme.Line, 1f);
        GameObject focusBorder = Border(rootRect, "FocusBorder", UiTheme.Accent, 2f).gameObject;
        focusBorder.SetActive(false); // 골라졌을 때만 켜진다.

        TextMeshProUGUI number = Label(rootRect, font, "Number", "01", 56f, UiTheme.TextMuted, 40f, 64f, 96f, 68f);

        Image divider = Box(rootRect, "VDivider", UiTheme.Line);
        Place(divider.rectTransform, 140f, 32f, 1f, 136f);

        // ── 기록 있음
        RectTransform filled = Empty(rootRect, "Filled", 0f, 0f, RowW, RowH);

        TextMeshProUGUI region = Label(filled, font, "Region", "서커스 극장", 30f, UiTheme.TextHigh, 180f, 36f, 680f, 36f);
        TextMeshProUGUI checkpoint = Label(filled, font, "Checkpoint", "무대 뒤편 거울에서 저장", 18f, UiTheme.TextMuted, 180f, 82f, 680f, 22f);

        Label(filled, font, "Ego_Label", "자아", 16f, UiTheme.TextMuted, 180f, 136f, 40f, 20f);

        Image track = Box(filled, "Ego_Track", UiTheme.TextDim);
        Place(track.rectTransform, 226f, 142f, 220f, 8f);

        // 채움은 막대의 왼쪽 끝에 붙여 폭만 바꾼다. 앵커가 왼쪽이어야 줄어들 때 오른쪽부터 깎인다.
        Image fill = Box(track.rectTransform, "Ego_Fill", UiTheme.AccentBright);
        AnchorLeft(fill.rectTransform, 0f, 165f, 8f);

        TextMeshProUGUI egoValue = Label(filled, font, "Ego_Value", "75 / 100", 16f, UiTheme.TextBody, 462f, 136f, 120f, 20f);

        Label(filled, font, "Shard_Label", "기억 조각", 16f, UiTheme.TextMuted, 600f, 136f, 80f, 20f);
        TextMeshProUGUI shardValue = Label(filled, font, "Shard_Value", "7", 16f, UiTheme.TextBody, 684f, 136f, 80f, 20f);

        Label(filled, font, "Skills_Label", "장착 스킬", 16f, UiTheme.TextMuted, 900f, 36f, 200f, 20f);

        Image[] skills = new Image[3];
        for (int i = 0; i < skills.Length; i++) {
            skills[i] = Box(filled, "Skill_" + i, UiTheme.PanelRaised);
            Place(skills[i].rectTransform, 900f + i * 68f, 72f, 56f, 56f);
            Border(skills[i].rectTransform, "Border", UiTheme.Line, 1f);
        }

        // Figma 에서는 글자 왼쪽 끝(1250)이 기준이었지만, 날짜 길이가 달라져도 오른쪽 여백이 유지되도록 오른쪽 정렬로 둔다.
        TextMeshProUGUI savedAt = Label(filled, font, "SavedAt", "2026.09.14  23:41", 16f, UiTheme.TextMuted, 1100f, 36f, 300f, 20f);
        savedAt.alignment = TextAlignmentOptions.TopRight;

        // ── 빈자리
        RectTransform empty = Empty(rootRect, "Empty", 0f, 0f, RowW, RowH);

        Label(empty, font, "Title", "비어 있는 자리", 30f, UiTheme.TextBody, 180f, 60f, 680f, 36f);
        TextMeshProUGUI emptySub = Label(empty, font, "Sub", "이 자리에 새 기억을 만들고 프롤로그부터 시작합니다", 18f, UiTheme.TextMuted, 180f, 106f, 900f, 22f);

        // 누르는 면은 줄 바탕 하나로 모았다. 버튼이 따로 클릭을 받으면 줄을 누른 것과 결과가 갈라질 여지가 생긴다.
        Image create = Box(empty, "Btn_Create", UiTheme.Accent);
        Place(create.rectTransform, 1160f, 76f, 240f, 48f);

        TextMeshProUGUI createLabel = Text(create.rectTransform, "Label", font, 18f, TextAlignmentOptions.Center, UiTheme.TextHigh);
        createLabel.text = "새 기억 만들기";
        Stretch(createLabel.rectTransform);

        create.gameObject.SetActive(false);
        empty.gameObject.SetActive(false); // 프리팹을 열었을 때 기록 있음 쪽이 보이도록 둔다. 실행 중에는 뷰가 갈아끼운다.

        SaveSlotRowView view = root.AddComponent<SaveSlotRowView>();
        view.background = background;
        view.focusBorder = focusBorder;
        view.numberLabel = number;
        view.filledGroup = filled.gameObject;
        view.regionLabel = region;
        view.checkpointLabel = checkpoint;
        view.egoTrack = track.rectTransform;
        view.egoFill = fill.rectTransform;
        view.egoValueLabel = egoValue;
        view.shardValueLabel = shardValue;
        view.skillSlots = skills;
        view.savedAtLabel = savedAt;
        view.emptyGroup = empty.gameObject;
        view.emptySubLabel = emptySub;
        view.createButton = create.gameObject;

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, RowPrefabPath);
        Object.DestroyImmediate(root);

        return saved != null ? saved.GetComponent<SaveSlotRowView>() : null;
    }

    #endregion
    #region 화면

    static void BuildScreen(TMP_FontAsset font, SaveSlotRowView rowPrefab) {
        GameObject root = new GameObject("SaveSlotSelect", typeof(RectTransform));

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        // 바탕과 커튼. 로비 위에 겹쳐 뜨지만 무대를 완전히 덮는다.
        Image screen = Box(root.transform, "Screen", UiTheme.Stage);
        Stretch(screen.rectTransform);
        screen.raycastTarget = true; // 뒤에 깔린 로비 메뉴가 클릭을 받지 않도록 막는다.

        Image curtainL = Box(root.transform, "Curtain_L", UiTheme.Curtain);
        Place(curtainL.rectTransform, 0f, 0f, CurtainWidth, 1080f);
        Image curtainR = Box(root.transform, "Curtain_R", UiTheme.Curtain);
        Place(curtainR.rectTransform, 1920f - CurtainWidth, 0f, CurtainWidth, 1080f);

        BuildHeader(root.transform, font, out TextMeshProUGUI title, out TextMeshProUGUI subtitle);
        RectTransform list = BuildList(root.transform);
        Button back = BuildFooter(root.transform, font);

        SaveSlotSelectView view = root.AddComponent<SaveSlotSelectView>();
        view.titleLabel = title;
        view.subtitleLabel = subtitle;
        view.rowPrefab = rowPrefab;
        view.slotList = list;
        view.backButton = back;

        BuildConfirm(root.transform, font, view); // 맨 마지막에 만들어야 목록 위에 그려진다.

        PrefabUtility.SaveAsPrefabAsset(root, ScreenPrefabPath);
        Object.DestroyImmediate(root);
    }

    // 제목과 부제는 새로 시작 / 불러오기에 따라 뷰가 갈아끼운다. 여기 적는 문구는 프리팹을 열었을 때 보이는 자리표시다.
    static void BuildHeader(Transform root, TMP_FontAsset font, out TextMeshProUGUI title, out TextMeshProUGUI subtitle) {
        RectTransform header = Empty(root, "Header", ContentX, HeaderY, ContentW, 125f);

        title = Label(header, font, "Title", "기억 선택", 44f, UiTheme.TextHigh, 0f, 0f, 600f, 53f);
        subtitle = Label(header, font, "Subtitle", "새로 시작할 자리를 고르세요. 기억이 있는 자리를 고르면 덮어씁니다.", 20f, UiTheme.TextMuted, 0f, 68f, ContentW, 24f);

        Image divider = Box(header, "Divider", UiTheme.Line);
        Place(divider.rectTransform, 0f, 124f, ContentW, 1f);
    }

    // 줄은 실행 중에 SaveSlotSelectView 가 자리 수만큼 찍어 넣는다. 여기서는 들어갈 자리와 간격만 잡는다.
    static RectTransform BuildList(Transform root) {
        RectTransform list = Empty(root, "SlotList", ContentX, ListY, ContentW, ListH);

        VerticalLayoutGroup layout = list.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = RowGap;
        layout.childAlignment = TextAnchor.UpperLeft;
        // 줄 크기는 줄 프리팹이 정한다. 목록이 늘리거나 줄이면 Figma 의 1440×200 이 깨진다.
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        return list;
    }

    static Button BuildFooter(Transform root, TMP_FontAsset font) {
        RectTransform footer = Empty(root, "Footer", ContentX, FooterY, ContentW, 56f);

        Image divider = Box(footer, "Divider", UiTheme.Line);
        Place(divider.rectTransform, 0f, 0f, ContentW, 1f);

        Label(footer, font, "Hint", "↑↓ 이동     Enter 선택     Delete 기억 지우기", 18f, UiTheme.TextMuted, 0f, 24f, 1000f, 22f);

        Image back = ButtonBase(footer, "Btn_Back", 1300f, 12f, 140f, 44f, UiTheme.Panel);
        Border(back.rectTransform, "Border", UiTheme.Line, 1f);

        TextMeshProUGUI label = Text(back.rectTransform, "Label", font, 18f, TextAlignmentOptions.Center, UiTheme.TextBody);
        label.text = "뒤로";
        Stretch(label.rectTransform);

        return back.GetComponent<Button>();
    }

    // 덮어쓰기 · 지우기 확인 창. 제목과 버튼 문구는 뷰가 상황에 따라 갈아끼우고, 경고 문구만 여기 고정으로 적는다.
    static void BuildConfirm(Transform root, TMP_FontAsset font, SaveSlotSelectView view) {
        // 막이 클릭을 받아야 창 뒤의 줄이 눌리지 않는다.
        Image dim = Box(root, "ConfirmDim", UiTheme.Dim);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // 창은 가운데에 붙인다. 화면 비율이 16:9 가 아니어도 한가운데에 뜨게 하기 위함이다.
        Image window = Box(dim.rectTransform, "ConfirmWindow", UiTheme.Panel);
        RectTransform windowRect = window.rectTransform;
        windowRect.anchorMin = windowRect.anchorMax = windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(ConfirmW, ConfirmH);
        Border(windowRect, "Border", UiTheme.Line, 1f);

        TextMeshProUGUI title = Label(windowRect, font, "Title", "기억 01을 덮어쓸까요?", 30f, UiTheme.TextHigh, 48f, 44f, 624f, 36f);
        TextMeshProUGUI summary = Label(windowRect, font, "Summary", "서커스 극장   |   자아 75 / 100   |   기억 조각 7", 18f, UiTheme.TextMuted, 48f, 104f, 624f, 22f);
        Label(windowRect, font, "Warning", "지금까지의 진행이 사라지며 되돌릴 수 없습니다.", 20f, UiTheme.TextBody, 48f, 150f, 624f, 24f);

        Image divider = Box(windowRect, "Divider", UiTheme.Line);
        Place(divider.rectTransform, 48f, 220f, 624f, 1f);

        // 「취소」가 기본 포커스다. 되돌릴 수 없는 쪽은 면을 채우지 않고 적색 글자로만 위험을 알린다.
        Image cancel = ConfirmButton(windowRect, font, "Btn_Cancel", "취소", 392f, UiTheme.PanelRaised, UiTheme.TextHigh,
            out GameObject cancelFocus, out TextMeshProUGUI cancelLabel);
        cancelFocus.SetActive(true);

        Image accept = ConfirmButton(windowRect, font, "Btn_Accept", "덮어쓰기", 540f, UiTheme.Panel, UiTheme.AccentBright,
            out GameObject acceptFocus, out TextMeshProUGUI acceptLabel);

        dim.gameObject.SetActive(false); // 평소에는 꺼져 있다. 뷰가 필요할 때 켠다.

        view.confirmRoot = dim.gameObject;
        view.confirmTitle = title;
        view.confirmSummary = summary;
        view.confirmCancelButton = cancel.GetComponent<Button>();
        view.confirmAcceptButton = accept.GetComponent<Button>();
        view.confirmCancelBackground = cancel;
        view.confirmAcceptBackground = accept;
        view.confirmCancelFocus = cancelFocus;
        view.confirmAcceptFocus = acceptFocus;
        view.confirmCancelLabel = cancelLabel;
        view.confirmAcceptLabel = acceptLabel;
    }

    static Image ConfirmButton(RectTransform parent, TMP_FontAsset font, string name, string text, float x,
        Color fill, Color textColor, out GameObject focus, out TextMeshProUGUI label) {

        Image bg = ButtonBase(parent, name, x, 252f, 132f, 48f, fill);
        Border(bg.rectTransform, "Border", UiTheme.Line, 1f);

        // 포커스 테두리는 평소 테두리와 따로 둔다. 두께가 1px → 2px 로 바뀌어서 색만 바꿔서는 Figma 와 같아지지 않는다.
        focus = Border(bg.rectTransform, "FocusBorder", UiTheme.Accent, 2f).gameObject;
        focus.SetActive(false);

        label = Text(bg.rectTransform, "Label", font, 18f, TextAlignmentOptions.Center, textColor);
        label.text = text;
        Stretch(label.rectTransform);

        return bg;
    }

    #endregion
    #region 생성 도우미

    // 이 화면은 전부 좌상단 기준으로 자리를 잡는다. Figma 좌표를 그대로 쓸 수 있어 대조가 쉽다.
    static void Place(RectTransform rect, float x, float y, float w, float h) {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
    }

    // 세로 가운데에 붙이고 왼쪽 끝을 기준으로 자라는 요소(게이지 채움)에 쓴다.
    static void AnchorLeft(RectTransform rect, float x, float w, float h) {
        rect.anchorMin = new Vector2(0f, 0.5f);
        rect.anchorMax = new Vector2(0f, 0.5f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchoredPosition = new Vector2(x, 0f);
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
        image.raycastTarget = false; // 기본은 끈다. 클릭을 받아야 하는 면(바탕 · 줄 · 버튼 · 막)만 만든 쪽에서 켠다.
        return image;
    }

    static Image ButtonBase(Transform parent, string name, float x, float y, float w, float h, Color fill) {
        Image bg = Box(parent, name, fill);
        bg.raycastTarget = true; // 버튼은 자기 이미지가 클릭을 받아야 눌린다.
        Place(bg.rectTransform, x, y, w, h);
        bg.gameObject.AddComponent<Button>();
        return bg;
    }

    static TextMeshProUGUI Text(Transform parent, string name, TMP_FontAsset font, float size,
        TextAlignmentOptions align, Color color) {

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

    #endregion
}
#endif
