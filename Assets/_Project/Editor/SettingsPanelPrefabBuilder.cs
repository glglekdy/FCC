#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 설정창 프리팹을 한 번 찍어내는 에디터 도구.
//
// Figma 파일 「FCC_UI」의 설정_일반 · 설정_컨트롤 · 설정_그래픽 세 안을 1920×1080 기준으로 그대로 옮겼다.
// 좌표·크기는 전부 그 안에서 읽은 값이고, 색은 Figma 쪽이 이미 UiTheme 토큰과 같은 값이라 토큰을 그대로 쓴다
// (그래야 나중에 Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 로 다른 화면과 함께 관리된다).
//
// 만들고 나면 문구·간격은 프리팹 인스펙터에서 고치면 된다.
// **다시 실행하면 프리팹을 덮어씁니다** — 손으로 고친 내용이 날아가므로 디자인을 갈아엎을 때만 실행한다.
//
// 사용법: Tools ▸ FCC ▸ Build Settings Panel Prefab
public static class SettingsPanelPrefabBuilder {
    #region 경로 · 치수

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/UI";
    const string PrefabPath = PrefabDir + "/SettingsPanel.prefab";

    // Figma 안의 수치. 이름을 붙여두면 어디서 온 값인지 나중에 대조할 수 있다.
    const float CurtainWidth = 120f;
    const float PanelX = 280f, PanelY = 110f, PanelW = 1360f, PanelH = 860f;
    const float HeaderX = 56f, HeaderY = 40f, HeaderW = 1248f;
    const float TabBarY = 128f, TabW = 160f, TabH = 52f;
    const float ContentX = 32f, ContentY = 212f, ContentW = 1296f;
    const float FooterY = 750f, FooterW = 1248f;
    const float RowH = 48f;
    const float LabelX = 24f, ValueX = 792f, ValueW = 400f, NumberX = 1212f;

    const int SortingOrder = 300; // 스킬 정비창(200)보다 위. 설정은 그 위에서 열려야 한다.

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Build Settings Panel Prefab")]
    public static void BuildPrefab() {
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[SettingsPanel] 프리팹 편집 모드를 닫고 다시 실행하세요. 임시 오브젝트가 섞여 들어갑니다.");
            return;
        }

        TMP_FontAsset font = PrefabBuilderFont.LoadProjectFont("SettingsPanel");

        GameObject root = new GameObject("SettingsPanel", typeof(RectTransform));

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        // 바탕과 커튼. 설정창은 게임 위에 겹쳐 뜨지만 무대를 완전히 덮는다.
        Image screen = Box(root.transform, "Screen", UiTheme.Stage);
        Stretch(screen.rectTransform);
        screen.raycastTarget = true; // 뒤쪽 화면이 클릭을 받지 않도록 막아준다.

        Image curtainL = Box(root.transform, "Curtain_L", UiTheme.Curtain);
        Place(curtainL.rectTransform, 0f, 0f, CurtainWidth, 1080f);
        Image curtainR = Box(root.transform, "Curtain_R", UiTheme.Curtain);
        Place(curtainR.rectTransform, 1920f - CurtainWidth, 0f, CurtainWidth, 1080f);

        RectTransform panel = Panel(root.transform);

        SettingsPanelView view = root.AddComponent<SettingsPanelView>();
        List<SettingsPanelView.Tab> tabs = new List<SettingsPanelView.Tab>();

        BuildHeader(panel, font);
        BuildTabBar(panel, font, tabs);
        RectTransform content = Empty(panel, "Content", ContentX, ContentY, ContentW, 514f);

        BuildGeneralTab(content, font, tabs[0]);
        BuildControlTab(content, font, tabs[1]);
        BuildGraphicTab(content, font, tabs[2]);

        BuildFooter(panel, font, view);
        BuildHints(root.transform, font, tabs);

        view.tabs = tabs.ToArray();

        if (!AssetDatabase.IsValidFolder(PrefabDir)) {
            Debug.LogError("[SettingsPanel] 폴더가 없습니다 — " + PrefabDir);
            Object.DestroyImmediate(root);
            return;
        }

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[SettingsPanel] 프리팹을 만들었습니다.\n· " + PrefabPath + "\n" +
            "씬에 올리려면 프리팹을 드래그한 뒤 평소에는 꺼두고, 여는 쪽에서 SetActive(true) 하세요.");
    }

    #endregion
    #region 껍데기

    static RectTransform Panel(Transform parent) {
        Image panel = Box(parent, "Panel", UiTheme.Panel);
        Place(panel.rectTransform, PanelX, PanelY, PanelW, PanelH);
        Border(panel.rectTransform, "Border", UiTheme.Line, 1f);
        return panel.rectTransform;
    }

    static void BuildHeader(RectTransform panel, TMP_FontAsset font) {
        RectTransform header = Empty(panel, "Header", HeaderX, HeaderY, HeaderW, 65f);

        TextMeshProUGUI title = Text(header, "Title", font, 40f, TextAlignmentOptions.TopLeft, UiTheme.TextHigh);
        title.text = "설정";
        Place(title.rectTransform, 0f, 0f, 300f, 48f);

        TextMeshProUGUI close = Text(header, "CloseHint", font, 18f, TextAlignmentOptions.TopRight, UiTheme.TextMuted);
        close.text = "[ESC] 닫기";
        Place(close.rectTransform, HeaderW - 300f, 16f, 300f, 22f);

        Image divider = Box(header, "Divider", UiTheme.Line);
        Place(divider.rectTransform, 0f, 64f, HeaderW, 1f);
    }

    static void BuildTabBar(RectTransform panel, TMP_FontAsset font, List<SettingsPanelView.Tab> tabs) {
        RectTransform bar = Empty(panel, "TabBar", HeaderX, TabBarY, HeaderW, 53f);

        // Figma 안의 탭 문구 x 위치. 탭 칸은 164px 간격으로 놓여 있고 문구는 칸 안에서 가운데에 가깝다.
        string[] names = { "일반", "컨트롤", "그래픽" };
        float[] textX = { 58f, 211f, 375f };

        for (int i = 0; i < names.Length; i++) {
            float slotX = i * 164f;

            Image bg = Box(bar, "Tab" + i + "_SelectedBg", UiTheme.PanelRaised);
            Place(bg.rectTransform, slotX, 0f, TabW, TabH);

            Image marker = Box(bar, "Tab" + i + "_SelectedMarker", UiTheme.Accent);
            Place(marker.rectTransform, slotX, 49f, TabW, 3f);

            TextMeshProUGUI label = Text(bar, "Tab" + i + "_Label", font, 22f, TextAlignmentOptions.TopLeft, UiTheme.TextBody);
            label.text = names[i];
            Place(label.rectTransform, textX[i], 12f, 120f, 27f);

            tabs.Add(new SettingsPanelView.Tab {
                name = names[i],
                label = label,
                selectedBackground = bg.rectTransform,
                selectedMarker = marker.rectTransform,
            });
        }

        Image divider = Box(bar, "Divider", UiTheme.Line);
        Place(divider.rectTransform, 0f, 52f, HeaderW, 1f);


    }

    static void BuildFooter(RectTransform panel, TMP_FontAsset font, SettingsPanelView view) {
        RectTransform footer = Empty(panel, "Footer", HeaderX, FooterY, FooterW, 80f);

        Image divider = Box(footer, "Divider", UiTheme.Line);
        Place(divider.rectTransform, 0f, 10f, FooterW, 1f);

        view.resetButton = FooterButton(footer, font, "Btn_Reset", "기본값 복원", 0f, 170f, UiTheme.Panel, UiTheme.TextMuted, true);
        view.cancelButton = FooterButton(footer, font, "Btn_Cancel", "취소", 948f, 140f, UiTheme.Panel, UiTheme.TextBody, true);
        // 「적용」만 면을 포인트 컬러로 채운다. 이 화면에서 가장 중요한 한 번의 확정이기 때문이다.
        view.applyButton = FooterButton(footer, font, "Btn_Apply", "적용", 1108f, 140f, UiTheme.Accent, UiTheme.TextHigh, false);
    }

    static Button FooterButton(RectTransform parent, TMP_FontAsset font, string name, string text,
        float x, float width, Color fill, Color textColor, bool outlined) {

        Image bg = Box(parent, name, fill);
        Place(bg.rectTransform, x, 36f, width, 44f);
        if (outlined) Border(bg.rectTransform, "Border", UiTheme.Line, 1f);

        TextMeshProUGUI label = Text(bg.rectTransform, "Label", font, 17f, TextAlignmentOptions.Center, textColor);
        label.text = text;
        Stretch(label.rectTransform);

        return bg.gameObject.AddComponent<Button>();
    }

    // 화면 맨 아래 안내 문구. 탭마다 내용이 달라 탭 정보에 함께 담는다.
    static void BuildHints(Transform root, TMP_FontAsset font, List<SettingsPanelView.Tab> tabs) {
        string[] texts = {
            "상하 방향키로 항목 이동 / 좌우 방향키로 값 변경 / Enter 확정",
            "Enter로 다시 지정 / 새 키를 누르면 등록 / Backspace로 해제",
            "해상도와 화면 모드는 적용을 눌러야 반영됩니다 / 15초 안에 확인하지 않으면 되돌립니다",
        };

        for (int i = 0; i < tabs.Count; i++) {
            TextMeshProUGUI hint = Text(root, "Hint" + i, font, 16f, TextAlignmentOptions.Top, UiTheme.TextMuted);
            hint.text = texts[i];
            Place(hint.rectTransform, 336f, 1004f, 1248f, 19f);
            tabs[i].hint = hint;
        }
    }

    #endregion
    #region 탭 내용 — 일반

    // y 값은 전부 Figma 안에서 읽은 그대로다. 섹션마다 줄 간격이 다르므로(소리만 52px, 나머지 48px)
    // 규칙으로 계산하지 않고 그 값을 그대로 적는다.
    static void BuildGeneralTab(RectTransform content, TMP_FontAsset font, SettingsPanelView.Tab tab) {
        RectTransform page = Empty(content, "Page_General", 0f, 0f, ContentW, 514f);
        tab.content = page.gameObject;

        RectTransform display = Section(page, font, "표시", 0f, 82f, 0f);
        Selector(display, font, "Row_Language", "언어", SettingsField.Language, 34f, false);

        RectTransform sound = Section(page, font, "소리", 86f, 186f, 0f);
        Slider(sound, font, "Row_MasterVolume", "마스터 볼륨", SettingsField.MasterVolume, 34f, "");
        Slider(sound, font, "Row_BgmVolume", "배경음 볼륨", SettingsField.BgmVolume, 86f, "");
        Slider(sound, font, "Row_SfxVolume", "효과음 볼륨", SettingsField.SfxVolume, 138f, "");

        RectTransform presentation = Section(page, font, "대사와 연출", 276f, 238f, 30f);
        Selector(presentation, font, "Row_DialogueSpeed", "대사 속도", SettingsField.DialogueSpeed, 64f, true);
        Slider(presentation, font, "Row_ScreenShake", "화면 흔들림", SettingsField.ScreenShake, 112f, "%");
        Toggle(presentation, font, "Row_DamageNumbers", "데미지 수치 표시", SettingsField.DamageNumbers, 160f);
    }

    #endregion
    #region 탭 내용 — 컨트롤

    static void BuildControlTab(RectTransform content, TMP_FontAsset font, SettingsPanelView.Tab tab) {
        RectTransform page = Empty(content, "Page_Control", 0f, 0f, ContentW, 514f);
        tab.content = page.gameObject;
        page.gameObject.SetActive(false);

        RectTransform head = Empty(page, "ColumnHeader", 0f, 0f, ContentW, 30f);
        Caption(head, font, "동작", LabelX, 0f);
        Caption(head, font, "키보드", 768f, 0f);
        Caption(head, font, "게임패드", 1028f, 0f);

        // 동작 이름과 초기 키는 GameSettings 가 들고 있다. 여기서 또 적어두면 실행 중 표시와 어긋난다.
        string[] actions = GameSettings.ActionNames;
        string[] keys = GameSettings.DefaultKeyboard;
        string[] pads = GameSettings.DefaultPad;

        for (int i = 0; i < actions.Length; i++) {
            float y = 34f + i * 48f;

            Image bg = Box(page, "Row_" + i + "_" + actions[i].Replace(" ", ""), UiTheme.Transparent);
            Place(bg.rectTransform, 0f, y, ContentW, RowH);

            GameObject marker = FocusMarker(bg.rectTransform);
            TextMeshProUGUI label = RowLabel(bg.rectTransform, font, actions[i]);

            Image keyChip = Chip(bg.rectTransform, font, "Chip_Key", keys[i], 768f, out TextMeshProUGUI keyText);
            Image padChip = Chip(bg.rectTransform, font, "Chip_Pad", pads[i], 1028f, out TextMeshProUGUI padText);

            SettingsKeybindRow row = bg.gameObject.AddComponent<SettingsKeybindRow>();
            row.field = SettingsField.Keybind;
            row.actionIndex = i;
            row.background = bg;
            row.focusMarker = marker;
            row.label = label;
            row.keyChip = keyChip;
            row.keyLabel = keyText;
            row.padChip = padChip;
            row.padLabel = padText;
        }
    }

    static Image Chip(RectTransform parent, TMP_FontAsset font, string name, string text, float x, out TextMeshProUGUI label) {
        Image chip = Box(parent, name, UiTheme.PanelRaised);
        Place(chip.rectTransform, x, 7f, 220f, 34f);
        Border(chip.rectTransform, "Border", UiTheme.Line, 1f);

        label = Text(chip.rectTransform, "Label", font, 17f, TextAlignmentOptions.Center, UiTheme.TextBody);
        label.text = text;
        Stretch(label.rectTransform);

        return chip;
    }

    #endregion
    #region 탭 내용 — 그래픽

    static void BuildGraphicTab(RectTransform content, TMP_FontAsset font, SettingsPanelView.Tab tab) {
        RectTransform page = Empty(content, "Page_Graphic", 0f, 0f, ContentW, 514f);
        tab.content = page.gameObject;
        page.gameObject.SetActive(false);

        RectTransform screen = Section(page, font, "화면", 0f, 226f, 0f);
        Selector(screen, font, "Row_ScreenMode", "화면 모드", SettingsField.ScreenMode, 34f, true);
        Selector(screen, font, "Row_Resolution", "해상도", SettingsField.Resolution, 82f, false);
        Selector(screen, font, "Row_FrameLimit", "프레임 제한", SettingsField.FrameLimit, 130f, true);
        Toggle(screen, font, "Row_VSync", "수직 동기화", SettingsField.VSync, 178f);
    }

    #endregion
    #region 줄 만들기

    // captionY 를 따로 받는 이유: Figma 안에서 「대사와 연출」 섹션만 제목이 30px 내려와 있다.
    static RectTransform Section(RectTransform page, TMP_FontAsset font, string title, float y, float height, float captionY) {
        RectTransform body = Empty(page, "Section_" + title.Replace(" ", ""), 0f, y, ContentW, height);
        Caption(body, font, title, LabelX, captionY);
        return body;
    }

    static void Caption(RectTransform parent, TMP_FontAsset font, string text, float x, float y) {
        TextMeshProUGUI caption = Text(parent, "Caption_" + text.Replace(" ", ""), font, 16f, TextAlignmentOptions.TopLeft, UiTheme.TextMuted);
        caption.text = text;
        Place(caption.rectTransform, x, y, 300f, 19f);
    }

    static Image RowBase(RectTransform parent, string name, float y, out GameObject marker) {
        Image bg = Box(parent, name, UiTheme.Transparent);
        Place(bg.rectTransform, 0f, y, ContentW, RowH);
        marker = FocusMarker(bg.rectTransform);
        return bg;
    }

    static GameObject FocusMarker(RectTransform parent) {
        Image marker = Box(parent, "FocusMarker", UiTheme.Accent);
        Place(marker.rectTransform, 0f, 0f, 3f, RowH);
        marker.gameObject.SetActive(false); // 포커스가 왔을 때만 켜진다.
        return marker.gameObject;
    }

    static TextMeshProUGUI RowLabel(RectTransform parent, TMP_FontAsset font, string text) {
        TextMeshProUGUI label = Text(parent, "Label", font, 20f, TextAlignmentOptions.Left, UiTheme.TextBody);
        label.text = text;
        Place(label.rectTransform, LabelX, 12f, 500f, 24f);
        return label;
    }

    static void Slider(RectTransform parent, TMP_FontAsset font, string name, string text, SettingsField field, float y, string suffix) {
        Image bg = RowBase(parent, name, y, out GameObject marker);
        TextMeshProUGUI label = RowLabel(bg.rectTransform, font, text);

        Image track = Box(bg.rectTransform, "Slider_Track", UiTheme.TextDim);
        Place(track.rectTransform, ValueX, 22f, ValueW, 4f);

        // 채움과 손잡이는 막대의 왼쪽 끝에 붙여둔다. 폭·x 만 바꿔서 움직이므로 앵커가 왼쪽이어야 한다.
        Image fill = Box(track.rectTransform, "Slider_Fill", UiTheme.AccentBright);
        AnchorLeft(fill.rectTransform, 0f, ValueW, 4f);

        Image knob = Box(track.rectTransform, "Slider_Knob", UiTheme.TextHigh);
        AnchorLeft(knob.rectTransform, 0f, 6f, 20f);

        TextMeshProUGUI value = Text(bg.rectTransform, "Value", font, 18f, TextAlignmentOptions.Left, UiTheme.TextBody);
        Place(value.rectTransform, NumberX, 14f, 100f, 22f);

        SettingsSliderRow row = bg.gameObject.AddComponent<SettingsSliderRow>();
        row.field = field;
        row.background = bg;
        row.focusMarker = marker;
        row.label = label;
        row.track = track.rectTransform;
        row.fill = fill.rectTransform;
        row.knob = knob.rectTransform;
        row.valueLabel = value;
        row.suffix = suffix;
    }

    static void Selector(RectTransform parent, TMP_FontAsset font, string name, string text, SettingsField field, float y, bool wrap) {
        Image bg = RowBase(parent, name, y, out GameObject marker);
        TextMeshProUGUI label = RowLabel(bg.rectTransform, font, text);

        TextMeshProUGUI prev = Text(bg.rectTransform, "Prev", font, 18f, TextAlignmentOptions.Center, UiTheme.TextMuted);
        prev.text = "<";
        Place(prev.rectTransform, ValueX, 14f, 20f, 22f);

        TextMeshProUGUI value = Text(bg.rectTransform, "Value", font, 18f, TextAlignmentOptions.Center, UiTheme.TextBody);
        Place(value.rectTransform, ValueX + 28f, 14f, ValueW - 56f, 22f);

        TextMeshProUGUI next = Text(bg.rectTransform, "Next", font, 18f, TextAlignmentOptions.Center, UiTheme.TextMuted);
        next.text = ">";
        Place(next.rectTransform, ValueX + ValueW - 12f, 14f, 20f, 22f);

        SettingsSelectorRow row = bg.gameObject.AddComponent<SettingsSelectorRow>();
        row.field = field;
        row.background = bg;
        row.focusMarker = marker;
        row.label = label;
        row.valueLabel = value;
        row.prevArrow = prev;
        row.nextArrow = next;
        row.wrap = wrap;
    }

    static void Toggle(RectTransform parent, TMP_FontAsset font, string name, string text, SettingsField field, float y) {
        Image bg = RowBase(parent, name, y, out GameObject marker);
        TextMeshProUGUI label = RowLabel(bg.rectTransform, font, text);

        Image track = Box(bg.rectTransform, "Toggle_Track", UiTheme.PanelRaised);
        Place(track.rectTransform, ValueX, 10f, 64f, 28f);
        Border(track.rectTransform, "Border", UiTheme.Line, 1f);

        Image knob = Box(track.rectTransform, "Toggle_Knob", UiTheme.AccentBright);
        AnchorLeft(knob.rectTransform, 4f, 28f, 20f);

        TextMeshProUGUI state = Text(bg.rectTransform, "State", font, 18f, TextAlignmentOptions.Left, UiTheme.TextBody);
        Place(state.rectTransform, ValueX + 80f, 12f, 120f, 22f);

        SettingsToggleRow row = bg.gameObject.AddComponent<SettingsToggleRow>();
        row.field = field;
        row.background = bg;
        row.focusMarker = marker;
        row.label = label;
        row.toggleTrack = track.rectTransform;
        row.toggleKnob = knob.rectTransform;
        row.toggleKnobImage = knob;
        row.stateLabel = state;
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

    // 세로 가운데에 붙이고 왼쪽 끝을 기준으로 자라는 요소(슬라이더 채움·토글 손잡이)에 쓴다.
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
        image.raycastTarget = false; // 줄 배경이 클릭을 가로채면 푸터 버튼이 안 눌린다.
        return image;
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

    // Unity UI 의 Image 는 테두리를 그리지 못해 얇은 이미지 4개를 변에 붙인다.
    static void Border(RectTransform parent, string name, Color color, float thickness) {
        RectTransform border = Empty(parent, name, 0f, 0f, 0f, 0f);
        Stretch(border);

        Edge(border, "Top", color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness));
        Edge(border, "Bottom", color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness));
        Edge(border, "Left", color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f));
        Edge(border, "Right", color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f));
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
