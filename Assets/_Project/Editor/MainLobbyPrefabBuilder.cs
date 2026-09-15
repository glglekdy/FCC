#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 메인 로비(커튼콜 정면 · 메뉴 5개) 프리팹을 한 번 찍어내는 에디터 도구.
//
// Figma 파일 「FCC_UI」의 MainLobby_new 안을 1920×1080 기준으로 옮겼다. 좌표·크기·글자 크기는 그 안에서 읽은 값이다.
// 옛 안(MainLobby_old — 가운데 로고 틀 · 줄 테두리 · 구분선 · 보조 표기 · 거울 소품)에서 좌측 정렬 타이틀과
// 글자 밝기만으로 고른 줄을 보여주는 구성으로 바뀌었다.
//
// 색은 UiTheme 토큰만 쓴다. Figma 안의 타이틀(#DED6C7)과 골라진 줄(#F5EBD9)은 토큰에 없는 값이라
// 가장 가까운 TextHigh(#F0E6D8)로 맞췄다 — 화면마다 흰색 계열이 조금씩 달라지면 같은 게임으로 보이지 않는다.
//
// 만들고 나면 간격·문구는 프리팹 인스펙터에서 고치면 되고, 색만 다시 맞추고 싶을 때는 이 메뉴가 아니라
// Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 을 쓴다.
//
// **다시 실행하면 프리팹을 덮어씁니다.** 프리팹 안 오브젝트의 ID 가 새로 발급되어, 씬에 올라간 인스턴스에서
// MainLobbyView 에 물려둔 settingsPanel · saveSlotPanel 이 풀린다. 다시 실행했다면 씬에서 두 칸을 다시 이어주세요.
//
// 사용법: Tools ▸ FCC ▸ Build Main Lobby Prefab → Tools ▸ FCC ▸ Place Main Lobby In Scene
public static class MainLobbyPrefabBuilder {
    #region 경로 · 색 · 크기

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/UI";
    const string PrefabPath = PrefabDir + "/MainLobby.prefab";

    // **여기서 색을 새로 만들지 않는다** — 빌더와 프리팹의 색이 갈라지면 Apply Theme Colors 로도 맞출 수 없게 된다.
    // 이름을 Screen 으로 두면 UnityEngine.Screen 을 가려버려서 뒤에 해상도를 읽는 코드가 들어올 때 헷갈린다.
    static readonly Color ScreenColor = UiTheme.Stage; // 무대 바탕.
    static readonly Color Ink = UiTheme.TextBody; // 메뉴 줄의 글자.
    static readonly Color InkStrong = UiTheme.TextHigh; // 타이틀 · 골라진 줄의 글자.
    static readonly Color Muted = UiTheme.TextMuted; // 부제 · 버전 · 조작 안내.
    static readonly Color LockedColor = UiTheme.TextDim; // 아직 못 여는 항목.
    static readonly Color CurtainFill = UiTheme.Curtain; // 커튼은 벨벳이라 살짝 덮는 게 아니라 완전히 덮는다.
    static readonly Color Transparent = UiTheme.Transparent;

    // 커튼 한 짝의 폭. Figma 안에서 오른쪽만 77.6 으로 조금 넓다(손으로 끌어 맞춘 값으로 보인다). 그대로 옮긴다.
    const float CurtainLeftWidth = 64f;
    const float CurtainRightWidth = 78f;

    const float LeftMargin = 208f; // 부제 · 메뉴 · 버전이 맞춰 선 왼쪽 기준선.

    const float TitleX = 202f, TitleY = 168f, TitleW = 558f, TitleH = 184f; // 대문자 F 의 곁이 둥글지 않아 기준선보다 6px 당겨져 있다.
    const float SubtitleY = 380f, SubtitleW = 840f, SubtitleH = 40f;
    const float MenuY = 502f, MenuWidth = 268f;
    const float ItemHeight = 82f; // Figma 에서 줄 간격(82)과 글자 칸 높이(88)가 달라 칸끼리 겹친다. 마우스 판정이 겹치지 않게 간격으로 맞췄다.
    const float FooterBottom = 48f; // 버전 · 조작 안내의 바닥 여백.

    // 조작 안내 글자의 오른쪽 끝(1770)까지의 여백. Figma 의 글자 칸은 1802 까지지만, 그 폭은 Figma 가 한글을 다른
    // 글꼴로 잰 값이라 게임 글꼴로는 30px 가량 남는다. 칸 끝이 아니라 Figma 화면에 보이는 글자 끝에 맞췄다.
    const float HintRight = 150f;

    // 한글이 섞인 줄은 TMP 가 받침 폰트(Hahmlet)의 더 높은 ascender 로 기준선을 잡아 글자 크기의 0.19배만큼 아래로 처진다.
    // (Inter 만 쓰는 타이틀 · 버전은 Figma 와 기준선이 정확히 같다.) 그래서 한글 줄만 그만큼 올려 Figma 와 기준선을 맞춘다.
    // **대표 폰트나 받침 폰트를 바꾸면 이 값도 다시 재야 합니다.**
    const float HangulBaselineShift = 0.19f;

    const float TitleFontSize = 76f;
    const float SubtitleFontSize = 23f;
    const float ItemFontSize = 30f;
    const float FooterFontSize = 23f;

    // 일시정지 메뉴(SkillLoadout=200)보다 아래, 기존 Main_menu 캔버스(0)보다는 위에 그린다.
    const int SortingOrder = 10;

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Build Main Lobby Prefab")]
    public static void BuildPrefab() {
        // 프리팹 편집 모드에서 실행하면 임시로 만든 오브젝트가 그 프리팹 안으로 들어가 버린다.
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[MainLobby] 프리팹 편집 모드를 닫고 다시 실행하세요. 임시 오브젝트가 편집 중인 프리팹에 섞여 들어갑니다.");
            return;
        }

        EnsureFolder(PrefabDir);

        TMP_FontAsset font = PrefabBuilderFont.LoadProjectFont("MainLobby");

        GameObject rootObj = new GameObject("MainLobby", typeof(RectTransform));

        Canvas canvas = rootObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = rootObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f; // 가로세로 비율이 달라져도 메뉴가 화면 밖으로 나가지 않도록 절충.

        rootObj.AddComponent<GraphicRaycaster>(); // 마우스로 메뉴를 고르려면 필요하다.

        Image screen = CreateImage("Screen", rootObj.transform, ScreenColor);
        screen.raycastTarget = false; // 바탕은 클릭을 받지 않는다. 메뉴 줄만 받아야 커서가 엉키지 않는다.
        Stretch(screen.rectTransform);

        // 커튼은 화면 가장자리를 덮는 무대 막이라 알맹이를 먼저 만들고 그 위에 그린다.
        RectTransform content = CreateRect("Content", rootObj.transform);
        Stretch(content);

        BuildTitle(content, font);
        MainLobbyItemView[] items = BuildMenu(content, font);
        TextMeshProUGUI version = BuildFooter(content, font);

        BuildCurtain("CurtainLeft", rootObj.transform, true, CurtainLeftWidth);
        BuildCurtain("CurtainRight", rootObj.transform, false, CurtainRightWidth);

        MainLobbyView view = rootObj.AddComponent<MainLobbyView>();
        view.items = items;
        view.versionLabel = version;

        PrefabUtility.SaveAsPrefabAsset(rootObj, PrefabPath);
        Object.DestroyImmediate(rootObj);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MainLobby] 프리팹을 만들었습니다.\n· {PrefabPath}\n" +
            "씬에 올리려면 Tools ▸ FCC ▸ Place Main Lobby In Scene 을 실행하거나 프리팹을 그냥 드래그하세요.\n" +
            "· 이미 씬에 있던 로비라면 MainLobbyView 의 settingsPanel · saveSlotPanel 을 다시 이어주세요.");
    }

    [MenuItem("Tools/FCC/Place Main Lobby In Scene")]
    public static void PlaceInScene() {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) {
            Debug.LogError($"[MainLobby] 프리팹이 없습니다({PrefabPath}). 먼저 Tools ▸ FCC ▸ Build Main Lobby Prefab 을 실행하세요.");
            return;
        }

        // 꺼둔 상태로 씬에 남아 있는 것도 찾아야 하므로 Include.
        foreach (MainLobbyView existing in Object.FindObjectsByType<MainLobbyView>(FindObjectsInactive.Include)) {
            Debug.Log($"[MainLobby] 씬에 이미 '{existing.name}' 이 있어 새로 놓지 않았습니다.", existing.gameObject);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place Main Lobby");

        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);

        // 구버전 메뉴가 그대로 켜져 있으면 두 화면이 겹쳐 보인다. 지우는 것은 되돌리기 어려우니 알려만 준다.
        foreach (MainMenuController old in Object.FindObjectsByType<MainMenuController>(FindObjectsInactive.Include)) {
            Debug.LogWarning($"[MainLobby] 구버전 메인 메뉴('{old.name}')가 씬에 남아 있습니다. " +
                "겹쳐 보이므로 기존 Canvas 를 끄거나 지우고, 설정 패널은 MainLobbyView 의 settingsPanel 에 다시 물려주세요.", old.gameObject);
        }

        Debug.Log("[MainLobby] 씬에 MainLobby 프리팹을 놓았습니다. 씬을 저장하세요.\n" +
            "· 마우스 조작을 쓰려면 씬에 EventSystem 이 있어야 합니다.\n" +
            "· [이어하기]는 씬에 SaveManager 가 있어야 켜집니다. 없으면 기억이 하나도 없는 것으로 보고 잠깁니다.", instance);
    }

    #endregion
    #region 커튼

    // 좌우 커튼 한 짝. 화면 가장자리에 앵커를 붙여 화면 비율이 달라져도 가장자리를 따라간다.
    // 예전에는 MainLobbyView 가 이 폭을 줄이며 열었지만 여는 연출을 빼서 이제 고정된 막이다. 옛 안의 안쪽 경계선도 빠졌다.
    static void BuildCurtain(string name, Transform parent, bool isLeft, float width) {
        Image curtain = CreateImage(name, parent, CurtainFill);
        curtain.raycastTarget = false; // 커튼이 열린 뒤 가장자리에서 클릭을 먹지 않도록.

        RectTransform rect = curtain.rectTransform;
        rect.anchorMin = new Vector2(isLeft ? 0f : 1f, 0f);
        rect.anchorMax = new Vector2(isLeft ? 0f : 1f, 1f);
        rect.pivot = new Vector2(isLeft ? 0f : 1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(width, 0f);
    }

    #endregion
    #region 타이틀

    // 전부 좌상단 기준으로 붙인다. 화면이 16:9 보다 넓어져도 타이틀과 메뉴가 왼쪽 기준선에 함께 남는다.
    static void BuildTitle(Transform parent, TMP_FontAsset font) {
        TextMeshProUGUI title = CreateText("Title", parent, font, TitleFontSize, TextAlignmentOptions.TopLeft, InkStrong);
        title.text = "FINAL\nCURTAIN CALL";
        title.textWrappingMode = TextWrappingModes.NoWrap; // 칸 폭이 글자 폭과 거의 같아, 줄바꿈을 켜두면 CURTAIN CALL 이 한 번 더 꺾인다.
        Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(TitleX, -TitleY), new Vector2(TitleW, TitleH));

        TextMeshProUGUI subtitle = CreateText("Subtitle", parent, font, SubtitleFontSize, TextAlignmentOptions.TopLeft, Muted);
        subtitle.text = "잊혀진 자들의 서커스";
        Place(subtitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(LeftMargin, -(SubtitleY - SubtitleFontSize * HangulBaselineShift)), new Vector2(SubtitleW, SubtitleH));
    }

    #endregion
    #region 메뉴

    // 로비 메뉴는 5줄로 고정이라 목록 프리팹을 따로 두지 않고 여기서 5개를 직접 만든다.
    static MainLobbyItemView[] BuildMenu(Transform parent, TMP_FontAsset font) {
        RectTransform menu = CreateRect("Menu", parent);
        Place(menu, new Vector2(0f, 1f), new Vector2(0f, 1f),
            new Vector2(LeftMargin, -(MenuY - ItemFontSize * HangulBaselineShift)), new Vector2(MenuWidth, ItemHeight * 5f));

        VerticalLayoutGroup layout = menu.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 0f;
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return new[] {
            CreateItem(menu, font, 0, MainLobbyAction.Continue, "이어하기"),
            CreateItem(menu, font, 1, MainLobbyAction.NewGame, "새로 시작"),
            CreateItem(menu, font, 2, MainLobbyAction.MemoryRoom, "기억의 방"),
            CreateItem(menu, font, 3, MainLobbyAction.Settings, "설정"),
            CreateItem(menu, font, 4, MainLobbyAction.Quit, "종료"),
        };
    }

    static MainLobbyItemView CreateItem(Transform parent, TMP_FontAsset font, int index, MainLobbyAction action, string labelText) {
        // 평소에는 투명하지만 raycastTarget 은 켜둔다 — 마우스 커서를 이 이미지가 받는다.
        Image background = CreateImage($"Item{index}_{action}", parent, Transparent);
        SetHeight(background.rectTransform, ItemHeight);

        // Figma 안의 글자 칸은 줄 맨 위에서 시작한다. 가운데 정렬로 두면 줄마다 13px 씩 아래로 처진다.
        TextMeshProUGUI label = CreateText("Label", background.rectTransform, font, ItemFontSize, TextAlignmentOptions.TopLeft, Ink);
        label.text = labelText;
        Stretch(label.rectTransform);

        MainLobbyItemView view = background.gameObject.AddComponent<MainLobbyItemView>();
        view.action = action;
        view.background = background;
        view.label = label;
        view.normalBackground = Transparent;
        view.selectedBackground = Transparent; // 새 안은 면을 칠하지 않고 글자 밝기만으로 고른 줄을 보여준다.
        view.labelColor = Ink;
        view.selectedLabelColor = InkStrong;
        view.suffixColor = Muted;
        view.lockedColor = LockedColor;
        view.boldWhenSelected = false;

        return view;
    }

    #endregion
    #region 하단

    // 버전과 조작 안내는 화면 아래에 붙인다. 세로가 긴 비율에서도 바닥 여백이 유지된다.
    static TextMeshProUGUI BuildFooter(Transform parent, TMP_FontAsset font) {
        TextMeshProUGUI version = CreateText("Version", parent, font, FooterFontSize, TextAlignmentOptions.TopLeft, Muted);
        version.text = "v0.1.0"; // MainLobbyView 가 Application.version 으로 갈아끼운다.
        Place(version.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(LeftMargin, FooterBottom), new Vector2(300f, 32f));

        TextMeshProUGUI hint = CreateText("Hint", parent, font, FooterFontSize, TextAlignmentOptions.TopRight, Muted);
        hint.text = "↑↓ 선택 · Enter 확인";
        Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(-HintRight, FooterBottom + FooterFontSize * HangulBaselineShift), new Vector2(500f, 32f));

        return version;
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

    static TextMeshProUGUI CreateText(string name, Transform parent, TMP_FontAsset font, float size,
        TextAlignmentOptions alignment, Color color) {

        TextMeshProUGUI text = CreateRect(name, parent).gameObject.AddComponent<TextMeshProUGUI>();

        if (font != null) text.font = font;
        text.fontSize = size;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false; // 글자가 메뉴 줄의 클릭을 가로채지 않도록.

        return text;
    }

    static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size) {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    static void Stretch(RectTransform rect) {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    // 레이아웃 그룹 안에서 높이를 고정시킨다. LayoutElement가 없으면 내용에 따라 제멋대로 늘어난다.
    static void SetHeight(RectTransform rect, float height) {
        LayoutElement element = rect.gameObject.AddComponent<LayoutElement>();
        element.preferredHeight = height;
        element.flexibleHeight = 0f;
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
