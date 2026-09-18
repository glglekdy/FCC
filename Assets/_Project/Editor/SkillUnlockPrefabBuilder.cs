#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 스킬 해금 알림(SkillUnlockUI) 프리팹을 찍어내는 에디터 도구.
//
// Figma 「FCC_UI」의 `스킬 해금` 안(1920×1080 전체화면)을 그대로 옮겼다. 화면을 어둡게 깔고 가운데에
// 제목 · 스킬 이름 · 아이콘 · 안내 멘트 · 닫는 법을 세로로 쌓은 모달이다 (TutorialPrefabBuilder와 같은 전체화면 구조).
//
// 배경 어둠 값(#0e0b0c 50%)이 Figma에서부터 UiTheme.Stage와 같은 값이라 토큰을 그대로 쓴다
// (TutorialPrefabBuilder의 Dim과 같은 방식).
//
// **다시 실행하면 프리팹을 덮어씁니다** — 손으로 고친 배치가 날아간다. 디자인을 갈아엎을 때만 실행한다.
// 색만 다시 맞출 때는 Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 를 쓴다.
//
// 사용법: Tools ▸ FCC ▸ Build Skill Unlock Prefab
public static class SkillUnlockPrefabBuilder {
    #region 경로 · 치수

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/UI";
    const string PrefabPath = PrefabDir + "/SkillUnlockUI.prefab";

    const float ScreenW = 1920f, ScreenH = 1080f;

    const float TitleY = 207f, TitleH = 53f;
    const float SubtitleY = 275f, SubtitleH = 24f;
    const float IconY = 371f, IconSize = 293f;
    const float MessageY = 775f, MessageH = 48f;
    const float HintY = 843f, HintH = 24f;

    // 대화창(0)보다 위, 스킬 정비창(200)보다 아래 — TutorialUI와 같은 층. 이동을 잠그는 전체화면
    // 알림끼리는(튜토리얼 · 스킬 해금) 게임 설계상 동시에 뜨지 않으므로 순서를 더 따질 필요는 없다.
    const int SortingOrder = 100;

    // 한글이 섞인 줄은 TMP가 받침 폰트(Hahmlet)의 ascender로 줄 높이를 잡아 Figma보다 처진다
    // (PauseMenuPrefabBuilder에서 잰 값을 그대로 쓴다). **대표 폰트나 받침 폰트를 바꾸면 다시 재야 한다.**
    const float HangulBaselineShift = 0.19f;

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Build Skill Unlock Prefab")]
    public static void BuildPrefab() {
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[SkillUnlockUI] 프리팹 편집 모드를 닫고 다시 실행하세요. 임시 오브젝트가 섞여 들어갑니다.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(PrefabDir)) {
            Debug.LogError("[SkillUnlockUI] 폴더가 없습니다 — " + PrefabDir);
            return;
        }

        TMP_FontAsset font = PrefabBuilderFont.LoadProjectFont("SkillUnlockUI");

        GameObject root = new GameObject("SkillUnlockUI", typeof(RectTransform));

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ScreenW, ScreenH);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        SkillUnlockView view = root.AddComponent<SkillUnlockView>();

        GameObject windowObj = new GameObject("Window", typeof(RectTransform));
        windowObj.transform.SetParent(root.transform, false);
        RectTransform window = (RectTransform)windowObj.transform;
        Stretch(window);

        Image dim = Box(window, "Dim", UiTheme.With(UiTheme.Stage, 0.5f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true; // 뒤에 깔린 HUD가 눌리면 안 된다.

        Label(window, font, "Title", "새로운 스킬 해금!", 44f, UiTheme.TextHigh, TitleY, TitleH);
        TMP_Text subtitle = Label(window, font, "Subtitle", "스킬 이름", 20f, UiTheme.TextMuted, SubtitleY, SubtitleH);

        BuildIcon(window, font, out Image icon, out TMP_Text monogram);

        Label(window, font, "Message", "세이브 거울에서 해당 스킬을 장착할수 있습니다!", 32f, UiTheme.TextHigh, MessageY, MessageH);
        Label(window, font, "Hint", "Enter 확인", 20f, UiTheme.TextMuted, HintY, HintH);

        view.windowRoot = windowObj;
        view.subtitleLabel = subtitle;
        view.iconImage = icon;
        view.monogramLabel = monogram;

        windowObj.SetActive(false); // 평소에는 꺼져 있다. 루트(Canvas)는 켜 둔 채로 저장한다 — 싱글턴 Awake가 돌아야 한다.

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (saved == null) {
            Debug.LogError("[SkillUnlockUI] 프리팹을 저장하지 못했습니다 — " + PrefabPath);
            return;
        }

        Debug.Log("[SkillUnlockUI] 프리팹을 만들었습니다 — " + PrefabPath + "\n" +
            "스킬 해금이 일어날 수 있는 씬마다 하나씩 놓으세요 (AreaTitle과 같은 싱글턴 방식).", saved);
    }

    #endregion
    #region 구성

    static void BuildIcon(Transform parent, TMP_FontAsset font, out Image icon, out TMP_Text monogram) {
        Image frame = Box(parent, "IconFrame", UiTheme.PanelRaised);
        RectTransform frameRect = frame.rectTransform;
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 1f);
        frameRect.pivot = new Vector2(0.5f, 1f);
        frameRect.anchoredPosition = new Vector2(0f, -IconY);
        frameRect.sizeDelta = new Vector2(IconSize, IconSize);
        Border(frameRect, "Border", UiTheme.Line, 1f);

        icon = Box(frameRect, "Icon", Color.white);
        Stretch(icon.rectTransform);
        icon.enabled = false; // 아이콘 아트가 없는 스킬은 Monogram으로 대신 그린다 (HudSkillSlotView와 같은 방식).

        TextMeshProUGUI mono = Text(frameRect, "Monogram", font, 96f, TextAlignmentOptions.Center, UiTheme.TextHigh);
        mono.text = "??";
        Stretch(mono.rectTransform);
        monogram = mono;
    }

    #endregion
    #region 생성 도우미

    // 화면 위쪽 기준(y)에서 아래로 h만큼, 가로는 화면 전체 폭을 쓰는 가운데 정렬 문구.
    static TMP_Text Label(Transform parent, TMP_FontAsset font, string name, string content, float size, Color color,
        float y, float h) {

        TextMeshProUGUI text = Text(parent, name, font, size, TextAlignmentOptions.Top, color);
        text.text = content;

        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -y + size * HangulBaselineShift);
        rect.sizeDelta = new Vector2(ScreenW, h);

        return text;
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
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        return rect;
    }

    static Image Box(Transform parent, string name, Color color) {
        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        Image image = obj.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false; // 기본은 끈다. 클릭을 받아야 하는 면(막)만 만든 쪽에서 켠다.
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

    // Unity UI의 Image는 테두리를 그리지 못해 얇은 이미지 4개를 변에 붙인다 (TutorialPrefabBuilder와 같은 방식).
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
