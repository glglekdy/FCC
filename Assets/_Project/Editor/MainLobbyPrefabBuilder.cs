#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

// 메인 로비(커튼콜 정면 · 메뉴 5개) 프리팹을 한 번 찍어내는 에디터 도구.
//
// 와이어프레임 1d 안(960×540)을 기준 해상도 1920×1080 에 2배로 옮겨 담았고, 색은 UiTheme 토큰을 쓴다.
// 점선 테두리는 Unity UI가 기본으로 그리지 못해 같은 색의 실선으로 대신했다(자리표시 프레임이라는 뜻은 같다).
//
// 만들고 나면 간격·문구는 프리팹 인스펙터에서 고치면 되고, 색만 다시 맞추고 싶을 때는 이 메뉴가 아니라
// Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 을 쓴다.
//
// 디자인을 갈아엎어 처음부터 다시 뽑고 싶을 때만 이 메뉴를 다시 실행한다. **다시 실행하면 프리팹을 덮어씁니다.**
// 지금 프리팹은 생성 뒤 손으로 고쳐 둔 상태라(자리표시 주석과 거울 소품을 지웠다) 다시 실행하면 그것들이 되살아난다.
//
// 사용법: Tools ▸ FCC ▸ Build Main Lobby Prefab → Tools ▸ FCC ▸ Place Main Lobby In Scene
public static class MainLobbyPrefabBuilder {
    #region 경로 · 색 · 크기

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/UI";
    const string PrefabPath = PrefabDir + "/MainLobby.prefab";

    // 처음에는 와이어프레임의 회색 토큰(styles.css 의 --color-neutral-*)을 그대로 옮겨 썼다. 배치와 정보량을
    // 먼저 확정하려던 단계라 밝은 종이 톤이었는데, 아트 디렉션이 「퇴락한 빈티지 극장」으로 확정되면서
    // 전부 UiTheme 토큰으로 갈아끼웠다. **여기서 색을 새로 만들지 않는다** — 빌더와 프리팹의 색이 갈라지면
    // Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 으로도 맞출 수 없게 된다.
    //
    // 이름을 Screen 으로 두면 UnityEngine.Screen 을 가려버려서 뒤에 해상도를 읽는 코드가 들어올 때 헷갈린다.
    static readonly Color ScreenColor = UiTheme.Stage; // 무대 바탕.
    static readonly Color Ink = UiTheme.TextBody; // 메뉴 줄의 글자.
    static readonly Color InkStrong = UiTheme.TextHigh; // 골라진 줄의 글자.
    static readonly Color FrameColor = UiTheme.Accent; // 골라진 줄을 감싸는 굵은 테두리. 이 화면의 유일한 포인트 컬러다.
    static readonly Color Line = UiTheme.Line; // 구분선 · 자리표시 프레임.
    static readonly Color Muted = UiTheme.TextMuted; // 보조 표기 · 주석 · 버전.
    static readonly Color LockedColor = UiTheme.TextDim; // 아직 못 여는 항목.

    static readonly Color CurtainFill = UiTheme.Curtain; // 커튼은 벨벳이라 살짝 덮는 게 아니라 완전히 덮는다.
    static readonly Color SelectedFill = UiTheme.With(UiTheme.PanelRaised, 0.92f);
    static readonly Color Transparent = UiTheme.Transparent;

    const float CurtainWidth = 300f; // 다 열렸을 때 커튼 한 짝의 폭.
    const float MenuWidth = 580f;
    const float ItemHeight = 88f;
    const float ItemGap = 4f;
    const float ItemPadding = 36f; // 메뉴 줄의 좌우 여백.
    const float SuffixWidth = 240f; // "CH1 · 3일차" 같은 오른쪽 보조 표기 칸.

    const float TitleFontSize = 24f;
    const float SubtitleFontSize = 23f;
    const float ItemFontSize = 30f;
    const float SuffixFontSize = 23f;
    const float NoteFontSize = 22f;

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

        // 커튼이 다 닫히면 화면을 덮어야 하므로 알맹이를 먼저 만든다(=커튼보다 뒤에 그려진다).
        RectTransform content = CreateRect("Content", rootObj.transform);
        Stretch(content);
        CanvasGroup contentGroup = content.gameObject.AddComponent<CanvasGroup>();

        BuildTitle(content, font);
        MainLobbyItemView[] items = BuildMenu(content, font);
        BuildMirror(content, font);
        TextMeshProUGUI version = BuildFooter(content, font);

        RectTransform curtainLeft = BuildCurtain("CurtainLeft", rootObj.transform, true);
        RectTransform curtainRight = BuildCurtain("CurtainRight", rootObj.transform, false);

        // 와이어프레임의 주석 문구. 커튼 위에 떠야 하므로 커튼의 자식으로 넣었다.
        // 실제 아트가 들어가면 이 오브젝트는 지우면 된다.
        CreateNote("CurtainNote", curtainLeft, font, "커튼 (좌우 애니메이션)",
            new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(32f, -28f), TextAlignmentOptions.TopLeft);

        MainLobbyView view = rootObj.AddComponent<MainLobbyView>();
        view.contentGroup = contentGroup;
        view.curtainLeft = curtainLeft;
        view.curtainRight = curtainRight;
        view.items = items;
        view.versionLabel = version;

        PrefabUtility.SaveAsPrefabAsset(rootObj, PrefabPath);
        Object.DestroyImmediate(rootObj);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[MainLobby] 프리팹을 만들었습니다.\n· {PrefabPath}\n" +
            "씬에 올리려면 Tools ▸ FCC ▸ Place Main Lobby In Scene 을 실행하거나 프리팹을 그냥 드래그하세요.");
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
            "· [이어하기]는 씬에 SaveManager 가 있어야 켜집니다. 없으면 '저장된 기록 없음' 으로 잠깁니다.", instance);
    }

    #endregion
    #region 커튼

    // 좌우 커튼 한 짝. 폭만 줄이면 열리도록 화면 가장자리에 앵커를 붙여둔다
    // (MainLobbyView가 SetSizeWithCurrentAnchors 로 폭을 애니메이션한다).
    static RectTransform BuildCurtain(string name, Transform parent, bool isLeft) {
        Image curtain = CreateImage(name, parent, CurtainFill);
        curtain.raycastTarget = false; // 커튼이 열린 뒤 가장자리에서 클릭을 먹지 않도록.

        RectTransform rect = curtain.rectTransform;
        rect.anchorMin = new Vector2(isLeft ? 0f : 1f, 0f);
        rect.anchorMax = new Vector2(isLeft ? 0f : 1f, 1f);
        rect.pivot = new Vector2(isLeft ? 0f : 1f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(CurtainWidth, 0f);

        // 무대와 맞닿는 안쪽 모서리 선.
        CreateEdge("Edge", rect, Line,
            new Vector2(isLeft ? 1f : 0f, 0f), new Vector2(isLeft ? 1f : 0f, 1f),
            new Vector2(isLeft ? 1f : 0f, 0.5f), new Vector2(2f, 0f));

        return rect;
    }

    #endregion
    #region 타이틀

    static void BuildTitle(Transform parent, TMP_FontAsset font) {
        // 로고 자리표시 프레임. 실제 로고 이미지가 나오면 이 오브젝트를 Image 로 바꾸면 된다.
        RectTransform logo = CreateRect("LogoFrame", parent);
        Place(logo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -176f), new Vector2(840f, 192f));
        CreateBorder("Border", logo, Line, 2f);

        TextMeshProUGUI logoText = CreateText("LogoText", logo, font, TitleFontSize, TextAlignmentOptions.Center, Muted);
        logoText.text = "TITLE LOGO — Final Curtain Call";
        logoText.characterSpacing = 6f;
        Stretch(logoText.rectTransform);

        TextMeshProUGUI subtitle = CreateText("Subtitle", parent, font, SubtitleFontSize, TextAlignmentOptions.Top, Muted);
        subtitle.text = "잊혀진 자들의 서커스";
        subtitle.characterSpacing = 22f; // 와이어프레임의 letter-spacing .22em.
        Place(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -390f), new Vector2(840f, 40f));
    }

    #endregion
    #region 메뉴

    // 로비 메뉴는 5줄로 고정이라 목록 프리팹을 따로 두지 않고 여기서 5개를 직접 만든다.
    static MainLobbyItemView[] BuildMenu(Transform parent, TMP_FontAsset font) {
        RectTransform menu = CreateRect("Menu", parent);
        Place(menu, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -524f),
            new Vector2(MenuWidth, ItemHeight * 5f + ItemGap * 4f));

        VerticalLayoutGroup layout = menu.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = ItemGap;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        return new[] {
            CreateItem(menu, font, 0, MainLobbyAction.Continue, "이어하기", string.Empty, true),
            CreateItem(menu, font, 1, MainLobbyAction.NewGame, "새로 시작", string.Empty, true),
            CreateItem(menu, font, 2, MainLobbyAction.MemoryRoom, "기억의 방", "— 수집 갤러리", true),
            CreateItem(menu, font, 3, MainLobbyAction.Settings, "설정", string.Empty, true),
            // 마지막 줄 밑에는 구분선이 없다. 목록의 끝이라 선을 그으면 메뉴가 잘린 것처럼 보인다.
            CreateItem(menu, font, 4, MainLobbyAction.Quit, "종료", string.Empty, false),
        };
    }

    static MainLobbyItemView CreateItem(Transform parent, TMP_FontAsset font, int index, MainLobbyAction action,
        string labelText, string suffixText, bool showUnderline) {

        // 평소에는 투명하지만 raycastTarget 은 켜둔다 — 마우스 커서를 이 이미지가 받는다.
        Image background = CreateImage($"Item{index}_{action}", parent, Transparent);
        SetHeight(background.rectTransform, ItemHeight);

        GameObject frame = CreateBorder("Frame", background.rectTransform, FrameColor, 4f);
        frame.SetActive(false); // 골라졌을 때만 켜진다.

        Image underline = CreateEdge("Underline", background.rectTransform, Line,
            new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f));
        underline.gameObject.SetActive(showUnderline);

        TextMeshProUGUI label = CreateText("Label", background.rectTransform, font, ItemFontSize, TextAlignmentOptions.Left, Ink);
        label.text = labelText;
        Stretch(label.rectTransform);
        label.rectTransform.offsetMin = new Vector2(ItemPadding, 0f);
        label.rectTransform.offsetMax = new Vector2(-(ItemPadding + SuffixWidth), 0f);

        TextMeshProUGUI suffix = CreateText("Suffix", background.rectTransform, font, SuffixFontSize, TextAlignmentOptions.Right, Muted);
        suffix.text = suffixText;
        Place(suffix.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-ItemPadding, 0f),
            new Vector2(SuffixWidth, ItemHeight));
        suffix.gameObject.SetActive(!string.IsNullOrEmpty(suffixText));

        MainLobbyItemView view = background.gameObject.AddComponent<MainLobbyItemView>();
        view.action = action;
        view.background = background;
        view.frame = frame;
        view.underline = underline.gameObject;
        view.label = label;
        view.suffixLabel = suffix;
        view.normalBackground = Transparent;
        view.selectedBackground = SelectedFill;
        view.labelColor = Ink;
        view.selectedLabelColor = InkStrong;
        view.suffixColor = Muted;
        view.lockedColor = LockedColor;

        return view;
    }

    #endregion
    #region 무대 소품 · 하단

    // 무대 좌측의 세이브 거울. 지금은 자리표시 프레임이고, 아트가 나오면 Image 로 갈아끼우면 된다.
    static void BuildMirror(Transform parent, TMP_FontAsset font) {
        RectTransform mirror = CreateRect("Mirror", parent);
        Place(mirror, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(372f, 128f), new Vector2(156f, 264f));
        CreateBorder("Border", mirror, Line, 2f);

        TextMeshProUGUI label = CreateText("Label", mirror, font, NoteFontSize, TextAlignmentOptions.Bottom, Muted);
        label.text = "거울";
        Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 20f), new Vector2(156f, 30f));

        CreateNote("MirrorNote", parent, font, "↑ 세이브 거울 (무대 소품)",
            new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(372f, 80f), TextAlignmentOptions.Left);
    }

    static TextMeshProUGUI BuildFooter(Transform parent, TMP_FontAsset font) {
        TextMeshProUGUI version = CreateText("Version", parent, font, SuffixFontSize, TextAlignmentOptions.Left, Muted);
        version.text = "v0.1.0"; // MainLobbyView 가 Application.version 으로 갈아끼운다.
        Place(version.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(44f, 36f), new Vector2(300f, 32f));

        TextMeshProUGUI hint = CreateText("Hint", parent, font, SuffixFontSize, TextAlignmentOptions.Right, Muted);
        hint.text = "↑↓ 선택 · Enter 확인";
        Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-44f, 36f), new Vector2(500f, 32f));

        return version;
    }

    // 와이어프레임의 설명 주석. 아트가 들어가면 지우면 되는 오브젝트라 이름을 Note로 통일해둔다.
    static void CreateNote(string name, Transform parent, TMP_FontAsset font, string text,
        Vector2 anchor, Vector2 pivot, Vector2 position, TextAlignmentOptions alignment) {

        TextMeshProUGUI note = CreateText(name, parent, font, NoteFontSize, alignment, Muted);
        note.text = text;
        Place(note.rectTransform, anchor, pivot, position, new Vector2(400f, 28f));
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

    // 사각형 테두리. Unity UI의 Image 는 테두리를 그리지 못해서 얇은 이미지 4개를 변에 붙인다
    // (스프라이트를 따로 만들지 않아 두께·색을 인스펙터에서 그대로 고칠 수 있다).
    static GameObject CreateBorder(string name, Transform parent, Color color, float thickness) {
        RectTransform border = CreateRect(name, parent);
        Stretch(border);

        CreateEdge("Top", border, color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness));
        CreateEdge("Bottom", border, color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness));
        CreateEdge("Left", border, color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f));
        CreateEdge("Right", border, color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f));

        return border.gameObject;
    }

    // 한 변만 채우는 얇은 선. 늘어나는 축은 앵커가, 두께는 sizeDelta 가 정한다.
    static Image CreateEdge(string name, Transform parent, Color color, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 size) {
        Image edge = CreateImage(name, parent, color);
        edge.raycastTarget = false;

        RectTransform rect = edge.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;

        return edge;
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
