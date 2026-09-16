#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 일시정지 메뉴(PauseMenu) 프리팹을 찍어내는 에디터 도구.
//
// Figma 파일 「FCC_UI」의 `일시중단 메뉴`(1088×1080 가운데 기둥) · `메인메뉴로 나가기`(720×340 확인 창) 두 안을
// 1920×1080 기준으로 옮겼다. 좌표 · 크기는 그 안에서 읽은 값이고, 색은 Figma 쪽이 이미 UiTheme 토큰과 같은 값이라
// 토큰을 그대로 쓴다 (SaveSlotPrefabBuilder 와 같은 방식). 토큰에 없는 줄 글자색(#F5EBD9)만 가장 가까운 TextHigh 로 맞췄다.
//
// 기둥은 화면 높이를 따라 늘어나게 두고, 머리는 위에 · 꼬리는 아래에 · 메뉴는 한가운데에 붙인다.
// Figma 에서 세 줄의 중심이 정확히 화면 가운데와 그 위아래 같은 간격이라, 16:9 가 아닌 화면에서도 같은 모양이 나온다.
//
// 기둥 안쪽은 Figma 수치에 UiScale 을 곱해 쓴다(확인 창은 제외). 아래 상수는 Figma 원래 값으로 남겨 대조할 수 있게 둔다.
//
// **다시 실행하면 프리팹을 덮어씁니다** — 손으로 고친 배치가 날아가고, 씬 인스턴스의 settingsPanel 연결은 남지만
// 안쪽 오브젝트에 준 오버라이드는 엉뚱한 곳에 붙을 수 있다. 디자인을 갈아엎을 때만 실행한다.
// 색만 다시 맞출 때는 Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 를 쓴다.
//
// 사용법: Tools ▸ FCC ▸ Build Pause Menu Prefab
public static class PauseMenuPrefabBuilder {
    #region 경로 · 치수

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/UI";
    const string PrefabPath = PrefabDir + "/PauseMenu.prefab";

    // Figma 안의 수치. 이름을 붙여두면 어디서 온 값인지 나중에 대조할 수 있다.
    const float ColumnW = 1088f;
    const float ColumnAlpha = 0.75f; // 기둥 바탕. 뒤의 게임 화면이 흐리게 비쳐 "멈춰 있다" 는 것이 보이게 한다.
    const float ContentX = 78f, ContentW = 932f;
    const float HeaderY = 65f, HeaderH = 125f;
    const float FooterBottom = 84f, FooterH = 56f; // 꼬리 윗변이 1080 기준 940.
    const float ItemW = 725f, ItemH = 150f, ItemPitch = 194f;
    const float ItemFontSize = 60f;
    const float FocusMarkerW = 3f;
    const float ConfirmW = 720f, ConfirmH = 340f;

    // 기둥 안쪽(머리 · 메뉴 · 꼬리)에 곱하는 배율. Figma 안을 그대로 옮기니 1920×1080 에서 글자와 줄이 화면을 꽉 채워
    // 답답해 보였다(2026-09-17). 배치 비율은 그대로 두고 크기만 한꺼번에 줄이려고 값마다 따로 고치지 않고 배율로 둔다.
    // 확인 창은 곱하지 않는다 — 기억 선택 화면의 확인 창(SaveSlotPrefabBuilder)과 크기가 같아야 같은 종류의 창으로 읽힌다.
    const float UiScale = 0.8f;
    const float MinFontSize = 16f; // 줄였을 때 이보다 작아지면 1080p 에서 조작 안내가 읽히지 않는다.

    // 한글이 섞인 줄은 TMP 가 받침 폰트(Hahmlet)의 ascender 로 줄 높이를 잡아 Figma 보다 0.19em 처진다.
    // MainLobbyPrefabBuilder 에서 잰 값을 그대로 쓴다. **대표 폰트나 받침 폰트를 바꾸면 이 값도 다시 재야 합니다.**
    const float HangulBaselineShift = 0.19f;

    // 가운데 정렬 글자는 줄 상자 전체를 가운데에 두므로 처짐도 절반만 나타난다. 60px 줄에서 글자 중심이
    // 5.4px 아래에 있었고(0.09em), 위쪽 정렬 값을 그대로 걸었더니 반대로 6px 떠올랐다.
    const float HangulCenterShift = HangulBaselineShift * 0.5f;

    // 정비 화면(200)보다 위, 설정창(300)보다 아래. 설정은 이 메뉴 위에서 열려야 한다.
    // 정비 화면과는 동시에 열리지 않는다(정비 화면이 떠 있는 동안 PauseMenuView 를 꺼 둔다).
    const int SortingOrder = 250;

    // 기둥 안쪽의 Figma 좌표 · 크기를 화면 값으로. 소수 픽셀에 걸치면 1px 선이 흐려져서 반올림한다.
    static float S(float figma) => Mathf.Round(figma * UiScale);

    // 기둥 안쪽의 Figma 글자 크기를 화면 값으로.
    static float F(float figmaSize) => Mathf.Max(MinFontSize, Mathf.Round(figmaSize * UiScale));

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Build Pause Menu Prefab")]
    public static void BuildPrefab() {
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[PauseMenu] 프리팹 편집 모드를 닫고 다시 실행하세요. 임시 오브젝트가 섞여 들어갑니다.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(PrefabDir)) {
            Debug.LogError("[PauseMenu] 폴더가 없습니다 — " + PrefabDir);
            return;
        }

        TMP_FontAsset font = PrefabBuilderFont.LoadProjectFont("PauseMenu");

        GameObject root = new GameObject("PauseMenu", typeof(RectTransform));

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        PauseMenuView view = root.AddComponent<PauseMenuView>();

        // 열고 닫을 때는 이 묶음만 켜고 끈다. 루트를 끄면 PauseMenuView.Update 가 돌지 않아 ESC 로 열 수 없다.
        RectTransform window = Empty(root.transform, "Window", 0f, 0f, 0f, 0f);
        Stretch(window);

        // 기둥 바깥의 게임 화면은 가리지 않되, 클릭은 막는다. 뒤에 깔린 HUD 나 대화창 버튼이 눌리면 안 된다.
        Image blocker = Box(window, "Blocker", UiTheme.Transparent);
        Stretch(blocker.rectTransform);
        blocker.raycastTarget = true;

        Image column = Box(window, "Column", UiTheme.With(UiTheme.Stage, ColumnAlpha));
        RectTransform columnRect = column.rectTransform;
        columnRect.anchorMin = new Vector2(0.5f, 0f);
        columnRect.anchorMax = new Vector2(0.5f, 1f);
        columnRect.pivot = new Vector2(0.5f, 0.5f);
        columnRect.anchoredPosition = Vector2.zero;
        columnRect.sizeDelta = new Vector2(S(ColumnW), 0f);

        BuildHeader(columnRect, font);
        view.items = BuildMenu(columnRect, font);
        BuildFooter(columnRect, font);

        BuildConfirm(window, font, view); // 맨 마지막에 만들어야 기둥 위에 그려진다.

        view.windowRoot = window.gameObject;
        window.gameObject.SetActive(false); // 평소에는 닫혀 있다. 프리팹을 열어 볼 때는 Window 를 켜서 확인한다.

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (saved == null) {
            Debug.LogError("[PauseMenu] 프리팹을 저장하지 못했습니다 — " + PrefabPath);
            return;
        }

        Debug.Log("[PauseMenu] 프리팹을 만들었습니다 — " + PrefabPath + "\n" +
            "게임 씬에 올려 루트를 켜 둔 채로 두고, PauseMenuView 의 settingsPanel 칸에 씬의 SettingsPanel 을 연결하세요.", saved);
    }

    #endregion
    #region 기둥

    static void BuildHeader(RectTransform column, TMP_FontAsset font) {
        RectTransform header = Empty(column, "Header", S(ContentX), S(HeaderY), S(ContentW), S(HeaderH));

        Label(header, font, "Title", "일시중지", F(44f), UiTheme.TextHigh, 0f, 0f, S(ContentW), S(53f));
        Label(header, font, "Subtitle", "가끔은 쉬는것도 중요하긴 하죠", F(20f), UiTheme.TextMuted, 0f, S(68f), S(ContentW), S(24f));

        Image divider = Box(header, "Divider", UiTheme.Line);
        Place(divider.rectTransform, 0f, S(HeaderH) - 1f, S(ContentW), 1f);
    }

    // 줄은 3개로 고정이라 목록 프리팹 없이 여기서 직접 만든다 (MainLobbyPrefabBuilder 와 같은 이유).
    static PauseMenuItemView[] BuildMenu(RectTransform column, TMP_FontAsset font) {
        (string name, string text, PauseMenuAction action)[] rows = {
            ("Item_Resume", "계속하기", PauseMenuAction.Resume),
            ("Item_Settings", "설정", PauseMenuAction.Settings),
            ("Item_MainMenu", "메뉴로 돌아가기", PauseMenuAction.MainMenu),
        };

        RectTransform menu = Empty(column, "Menu", 0f, 0f, S(ItemW), S(ItemH) + S(ItemPitch) * (rows.Length - 1));
        menu.anchorMin = menu.anchorMax = menu.pivot = new Vector2(0.5f, 0.5f);
        menu.anchoredPosition = Vector2.zero;

        PauseMenuItemView[] items = new PauseMenuItemView[rows.Length];

        for (int i = 0; i < rows.Length; i++) {
            Image background = Box(menu, rows[i].name, i == 0 ? UiTheme.With(UiTheme.PanelRaised, 0.75f) : UiTheme.Transparent);
            background.raycastTarget = true; // 줄의 마우스 입력(PauseMenuItemView)을 이 면이 받는다.
            Place(background.rectTransform, 0f, i * S(ItemPitch), S(ItemW), S(ItemH));

            // 줄 왼쪽 끝의 포인트 막대. 줄 높이를 따라가도록 세로로 늘린다.
            Image marker = Box(background.rectTransform, "FocusMarker", UiTheme.Accent);
            RectTransform markerRect = marker.rectTransform;
            markerRect.anchorMin = new Vector2(0f, 0f);
            markerRect.anchorMax = new Vector2(0f, 1f);
            markerRect.pivot = new Vector2(0f, 0.5f);
            markerRect.anchoredPosition = Vector2.zero;
            markerRect.sizeDelta = new Vector2(FocusMarkerW, 0f);
            marker.gameObject.SetActive(i == 0); // 프리팹을 열었을 때 첫 줄이 골라진 모습으로 보이게 둔다.

            TextMeshProUGUI label = Text(background.rectTransform, "Label", font, F(ItemFontSize), TextAlignmentOptions.Center,
                i == 0 ? UiTheme.TextHigh : UiTheme.TextBody);
            label.text = rows[i].text;
            Stretch(label.rectTransform);
            ShiftUp(label.rectTransform, F(ItemFontSize) * HangulCenterShift);

            PauseMenuItemView item = background.gameObject.AddComponent<PauseMenuItemView>();
            item.action = rows[i].action;
            item.background = background;
            item.focusMarker = marker.gameObject;
            item.label = label;

            items[i] = item;
        }

        return items;
    }

    static void BuildFooter(RectTransform column, TMP_FontAsset font) {
        RectTransform footer = Empty(column, "Footer", 0f, 0f, S(ContentW), S(FooterH));
        footer.anchorMin = footer.anchorMax = footer.pivot = Vector2.zero;
        footer.anchoredPosition = new Vector2(S(ContentX), S(FooterBottom));

        Image divider = Box(footer, "Divider", UiTheme.Line);
        Place(divider.rectTransform, 0f, 0f, S(ContentW), 1f);

        Label(footer, font, "Hint", "↑↓ 이동     Enter 선택     Esc 종료", F(18f), UiTheme.TextMuted, 0f, S(24f), S(ContentW), S(22f));
    }

    #endregion
    #region 확인 창

    // 메인메뉴로 나가기 확인 창. 제목과 안내 문구는 저장할 수 있는지 · 어느 자리인지에 따라 뷰가 갈아끼운다.
    static void BuildConfirm(RectTransform window, TMP_FontAsset font, PauseMenuView view) {
        // 막이 클릭을 받아야 창 뒤의 메뉴 줄이 눌리지 않는다.
        Image dim = Box(window, "ConfirmDim", UiTheme.Dim);
        Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // 창은 가운데에 붙인다. 화면 비율이 16:9 가 아니어도 한가운데에 뜨게 하기 위함이다.
        Image panel = Box(dim.rectTransform, "ConfirmWindow", UiTheme.Panel);
        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.anchoredPosition = Vector2.zero;
        panelRect.sizeDelta = new Vector2(ConfirmW, ConfirmH);
        Border(panelRect, "Border", UiTheme.Line, 1f);

        TextMeshProUGUI title = Label(panelRect, font, "Title", "저장하고 메인메뉴로 나가시겠습니까?", 30f, UiTheme.TextHigh, 48f, 44f, 624f, 36f);
        TextMeshProUGUI message = Label(panelRect, font, "Message", "해당 게임은 기억 01에 저장됩니다.", 20f, UiTheme.TextBody, 48f, 92f, 624f, 24f);

        Image divider = Box(panelRect, "Divider", UiTheme.Line);
        Place(divider.rectTransform, 48f, 220f, 624f, 1f);

        // 「취소」가 기본 포커스다. 되돌릴 수 없는 쪽은 면을 채우지 않고 적색 글자로만 위험을 알린다.
        Image cancel = ConfirmButton(panelRect, font, "Btn_Cancel", "취소", 392f, UiTheme.PanelRaised, UiTheme.TextHigh,
            out GameObject cancelFocus, out TextMeshProUGUI cancelLabel);
        cancelFocus.SetActive(true);

        Image accept = ConfirmButton(panelRect, font, "Btn_Exit", "나가기", 540f, UiTheme.Panel, UiTheme.AccentBright,
            out GameObject acceptFocus, out TextMeshProUGUI acceptLabel);

        dim.gameObject.SetActive(false); // 평소에는 꺼져 있다. 뷰가 필요할 때 켠다.

        view.confirmRoot = dim.gameObject;
        view.confirmTitle = title;
        view.confirmMessage = message;
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

        Image bg = Box(parent, name, fill);
        bg.raycastTarget = true; // 버튼은 자기 이미지가 클릭을 받아야 눌린다.
        Place(bg.rectTransform, x, 252f, 132f, 48f);
        bg.gameObject.AddComponent<Button>();
        Border(bg.rectTransform, "Border", UiTheme.Line, 1f);

        // 포커스 테두리는 평소 테두리와 따로 둔다. 두께가 1px → 2px 로 바뀌어서 색만 바꿔서는 같아지지 않는다.
        focus = Border(bg.rectTransform, "FocusBorder", UiTheme.Accent, 2f).gameObject;
        focus.SetActive(false);

        label = Text(bg.rectTransform, "Label", font, 18f, TextAlignmentOptions.Center, textColor);
        label.text = text;
        Stretch(label.rectTransform);
        ShiftUp(label.rectTransform, 18f * HangulCenterShift);

        return bg;
    }

    #endregion
    #region 생성 도우미

    // 좌상단 기준으로 자리를 잡는다. Figma 좌표를 그대로 쓸 수 있어 대조가 쉽다.
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

    // 한글 줄의 처짐(HangulBaselineShift)을 되돌린다. 크기는 그대로 두고 자리만 올린다.
    static void ShiftUp(RectTransform rect, float amount) {
        rect.anchoredPosition += new Vector2(0f, amount);
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
        image.raycastTarget = false; // 기본은 끈다. 클릭을 받아야 하는 면(막 · 줄 · 버튼)만 만든 쪽에서 켠다.
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

    // 좌상단 정렬 문구. 전부 한글이 섞여 있어 처짐 보정을 함께 건다.
    static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, string name, string content, float size, Color color,
        float x, float y, float w, float h) {

        TextMeshProUGUI label = Text(parent, name, font, size, TextAlignmentOptions.TopLeft, color);
        label.text = content;
        Place(label.rectTransform, x, y, w, h);
        ShiftUp(label.rectTransform, size * HangulBaselineShift);
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
