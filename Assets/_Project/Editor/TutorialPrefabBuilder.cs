#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 튜토리얼 안내창(TutorialUI) 프리팹을 찍어내는 에디터 도구.
//
// Figma 「FCC_UI」의 `튜토리얼` 안(1920×1080 전체화면)을 그대로 옮겼다. 화면 전체를 어둡게 깔고
// 위에 제목·이름(부제)을 얹은 머리, 가운데에 안내 이미지와 멘트, 아래에 힌트를 얹은 꼬리로 구성된다
// (PauseMenuPrefabBuilder와 같은 머리·꼬리 구조이지만 화면을 통째로 덮으므로 기둥이 없다).
//
// 색은 Figma 쪽이 이미 UiTheme 토큰과 같은 값이라 토큰을 그대로 쓴다(PauseMenuPrefabBuilder와 같은 방식).
//
// **다시 실행하면 프리팹을 덮어씁니다** — 손으로 고친 배치가 날아간다. 디자인을 갈아엎을 때만 실행한다.
// 색만 다시 맞출 때는 Tools ▸ FCC ▸ UI ▸ Apply Theme Colors 를 쓴다.
//
// 사용법: Tools ▸ FCC ▸ Build Tutorial Prefab
public static class TutorialPrefabBuilder {
    #region 경로 · 치수

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/UI";
    const string PrefabPath = PrefabDir + "/TutorialUI.prefab";

    const float ScreenW = 1920f, ScreenH = 1080f;
    const float ContentX = 62f, ContentW = ScreenW - ContentX * 2f;
    const float HeaderY = 61f, HeaderH = 125f;
    const float FooterH = 56f, FooterBottom = 35f;

    const float ImageW = 822f, ImageH = 465f, ImageY = 277f;
    const float MessageY = 780f, MessageH = 140f;

    // 게임 HUD·대화창(0)보다 위, 스킬 정비창(200)보다 아래. 튜토리얼은 이동을 잠그므로 정비화면·일시정지
    // 메뉴와 동시에 뜰 일이 없어(둘 다 이동 잠금 중엔 열리지 않는다) 그 위일 필요는 없다.
    const int SortingOrder = 100;

    // 한글이 섞인 줄은 TMP가 받침 폰트(Hahmlet)의 ascender로 줄 높이를 잡아 Figma보다 처진다
    // (PauseMenuPrefabBuilder에서 잰 값을 그대로 쓴다). **대표 폰트나 받침 폰트를 바꾸면 다시 재야 한다.**
    const float HangulBaselineShift = 0.19f;
    const float HangulCenterShift = HangulBaselineShift * 0.5f;

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Build Tutorial Prefab")]
    public static void BuildPrefab() {
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[TutorialUI] 프리팹 편집 모드를 닫고 다시 실행하세요. 임시 오브젝트가 섞여 들어갑니다.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(PrefabDir)) {
            Debug.LogError("[TutorialUI] 폴더가 없습니다 — " + PrefabDir);
            return;
        }

        TMP_FontAsset font = PrefabBuilderFont.LoadProjectFont("TutorialUI");

        GameObject root = new GameObject("TutorialUI", typeof(RectTransform));

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(ScreenW, ScreenH);
        scaler.matchWidthOrHeight = 0.5f;

        root.AddComponent<GraphicRaycaster>();

        TutorialView view = root.AddComponent<TutorialView>();

        Image dim = Box(root.transform, "Dim", UiTheme.With(UiTheme.Stage, 0.5f));
        Stretch(dim.rectTransform);
        dim.raycastTarget = true; // 뒤에 깔린 HUD나 대화창 버튼이 눌리면 안 된다.

        BuildHeader(dim.rectTransform, font, out TMP_Text subtitle);
        BuildImage(dim.rectTransform, out Image image);
        BuildMessage(dim.rectTransform, font, out TMP_Text message);
        BuildFooter(dim.rectTransform, font);

        view.subtitleText = subtitle;
        view.messageText = message;
        view.image = image;

        root.SetActive(false); // 평소에는 꺼져 있다. 프리팹을 열어 볼 때는 루트를 켜서 확인한다.

        GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        if (saved == null) {
            Debug.LogError("[TutorialUI] 프리팹을 저장하지 못했습니다 — " + PrefabPath);
            return;
        }

        Debug.Log("[TutorialUI] 프리팹을 만들었습니다 — " + PrefabPath + "\n" +
            "게임 씬에 올려두고, TutorialTriggerZone의 view 칸에 연결하세요.", saved);
    }

    #endregion
    #region 구성

    static void BuildHeader(RectTransform parent, TMP_FontAsset font, out TMP_Text subtitle) {
        RectTransform header = Empty(parent, "Header", ContentX, HeaderY, ContentW, HeaderH);

        Label(header, font, "Title", "튜토리얼", 44f, UiTheme.TextHigh, 0f, 0f, ContentW, 53f);
        subtitle = Label(header, font, "Subtitle", "튜토리얼 이름", 20f, UiTheme.TextMuted, 0f, 68f, ContentW, 24f);

        Image divider = Box(header, "Divider", UiTheme.Line);
        Place(divider.rectTransform, 0f, HeaderH - 1f, ContentW, 1f);
    }

    // 안내 이미지. 스프라이트가 없는 칸에서는 TutorialView.Show가 꺼서 자리를 먹지 않게 한다.
    static void BuildImage(RectTransform parent, out Image image) {
        image = Box(parent, "Image", UiTheme.PanelRaised);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -ImageY);
        rect.sizeDelta = new Vector2(ImageW, ImageH);
        Border(rect, "Border", UiTheme.Line, 1f);
    }

    static void BuildMessage(RectTransform parent, TMP_FontAsset font, out TMP_Text message) {
        TextMeshProUGUI text = Text(parent, "Message", font, 32f, TextAlignmentOptions.Top, UiTheme.TextHigh);
        text.text = "튜토리얼 멘트 여기에 넣어야함";

        RectTransform rect = text.rectTransform;
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, -MessageY + 32f * HangulBaselineShift);
        rect.sizeDelta = new Vector2(ContentW, MessageH);

        message = text;
    }

    static void BuildFooter(RectTransform parent, TMP_FontAsset font) {
        RectTransform footer = Empty(parent, "Footer", 0f, 0f, ContentW, FooterH);
        footer.anchorMin = footer.anchorMax = footer.pivot = Vector2.zero;
        footer.anchoredPosition = new Vector2(ContentX, FooterBottom);

        Image divider = Box(footer, "Divider", UiTheme.Line);
        Place(divider.rectTransform, 0f, 0f, ContentW, 1f);

        Label(footer, font, "Hint", "Enter 다음   Esc 스킵", 18f, UiTheme.TextMuted, 0f, 24f, ContentW, 22f);
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

    // 좌상단 정렬 문구. 전부 한글이 섞여 있어 처짐 보정을 함께 건다.
    static TextMeshProUGUI Label(Transform parent, TMP_FontAsset font, string name, string content, float size, Color color,
        float x, float y, float w, float h) {

        TextMeshProUGUI label = Text(parent, name, font, size, TextAlignmentOptions.TopLeft, color);
        label.text = content;
        Place(label.rectTransform, x, y, w, h);
        label.rectTransform.anchoredPosition += new Vector2(0f, size * HangulBaselineShift);
        return label;
    }

    // Unity UI의 Image는 테두리를 그리지 못해 얇은 이미지 4개를 변에 붙인다 (PauseMenuPrefabBuilder와 같은 방식).
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
