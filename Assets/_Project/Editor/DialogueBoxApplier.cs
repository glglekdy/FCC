#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 대화창을 Figma 파일 「FCC_UI」의 `대화창` 안에 맞춰 다시 배치·채색하는 도구.
//
// 프리팹을 새로 찍어내지 않고 기존 `InGameUi.prefab` 을 고치는 이유:
// 대화창에는 DialogueView 가 붙어 있고 본문 엔진(DialogueEffect) · 이름표 · 초상화 · AUTO 버튼이 전부
// 인스펙터로 물려 있다. 새로 찍으면 그 연결과 DialogueEffect 의 조정값을 전부 다시 맞춰야 한다.
// 그래서 있는 것은 옮기고, 설계도에만 있는 것(대화 상자 배경 · SKIP · 다음 표시 · 이름표 강조선)만 새로 만든다.
//
// 여러 번 실행해도 안전하다(멱등). 새로 만드는 오브젝트는 이름으로 먼저 찾아보고 없을 때만 만든다.
//
// 사용법: Tools ▸ FCC ▸ UI ▸ Apply Dialogue Layout
public static class DialogueBoxApplier {
    #region 경로 · 치수

    const string PrefabPath = "Assets/_Project/Assets/Prefabs/InGameUi.prefab";

    // Figma 안에서 읽은 값. 1920×1080 좌상단 기준이다.
    const float BoxX = 240f, BoxY = 740f, BoxW = 1440f, BoxH = 260f;
    const float BoxPadding = 48f; // 본문과 상자 벽 사이 여백.

    // 대화 상자 배경의 불투명도. 설계도는 불투명이지만, 뒤의 무대가 비쳐야 대사가 "화면 위에 얹힌 판"이 아니라
    // 장면의 일부처럼 읽힌다. 너무 낮추면 밝은 배경에서 글자가 묻히므로 이 값만 만진다.
    const float BoxAlpha = 0.5f;

    const float NameX = 280f, NameY = 700f, NameW = 280f, NameH = 52f;
    const float PortraitW = 420f, PortraitH = 560f, PortraitY = 280f;
    const float PortraitLeftX = 280f, PortraitRightX = 1220f;

    const float ButtonY = 26f, ButtonW = 96f, ButtonH = 40f;
    const float SkipX = 1689f, AutoX = 1802f;

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/UI/Apply Dialogue Layout")]
    public static void Apply() {
        TMP_FontAsset font = PrefabBuilderFont.LoadProjectFont("DialogueBox");

        GameObject contents = PrefabUtility.LoadPrefabContents(PrefabPath);

        try {
            Transform ui = contents.transform.Find("DialogueUI");
            if (ui == null) {
                Debug.LogError("[DialogueBox] InGameUi.prefab 안에서 DialogueUI 를 찾지 못했습니다.");
                return;
            }

            // 좌표를 Figma 값 그대로 쓰려면 캔버스 기준 해상도가 1920×1080 이어야 한다.
            CanvasScaler scaler = ui.GetComponent<CanvasScaler>();
            if (scaler != null && scaler.referenceResolution != new Vector2(1920f, 1080f)) {
                Debug.LogWarning($"[DialogueBox] DialogueUI 의 기준 해상도가 {scaler.referenceResolution} 라서 " +
                    "1920×1080 으로 맞췄습니다. 설계도 좌표가 그 기준입니다.");
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }

            RectTransform box = BuildBox(ui, font);
            LayoutPortraits(ui);
            LayoutNameBox(ui, font);
            LayoutButtons(ui, font);
            MoveBodyInto(ui, box, font);
            SortDrawOrder(ui);

            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        } finally {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("[DialogueBox] 대화창을 설계도(Figma 「대화창」)에 맞췄습니다.\n" +
            "· 대화 상자 배경 · SKIP 버튼 · 다음 표시 · 이름표 강조선을 새로 만들었습니다.\n" +
            "· 초상화는 스프라이트가 붙는 자리라 흰색 틴트를 그대로 두었습니다(설계도의 자리표시 테두리는 옮기지 않았습니다).");
    }

    #endregion
    #region 대화 상자

    // Unity UI 는 나중 형제가 위에 그려진다. 설계도와 같은 겹침이 되도록 순서를 못 박는다.
    //
    // 초상화는 대화 상자보다 아래(280~840)에서 시작해 상자 윗부분(740~)과 100px 겹친다. 상자가 초상화를
    // 덮어야 인물이 상자 뒤에 서 있는 것처럼 보이는데, 순서가 뒤집히면 초상화가 글자를 가린다.
    static void SortDrawOrder(Transform ui) {
        string[] order = { "PortraitLeft", "PortraitRight", "SkipButton", "AutoButton", "DialogueBox", "NameBox" };

        int index = 0;
        foreach (string name in order) {
            Transform t = ui.Find(name);
            if (t == null) continue;

            t.SetSiblingIndex(index);
            index++;
        }
    }

    static RectTransform BuildBox(Transform ui, TMP_FontAsset font) {
        Image box = Ensure(ui, "DialogueBox", UiTheme.With(UiTheme.Panel, BoxAlpha));
        Place(box.rectTransform, BoxX, BoxY, BoxW, BoxH);

        // 상자가 반투명이면 클릭이 뒤로 새면 안 된다. 대사 진행이 마우스 클릭에도 묶여 있어서다.
        box.raycastTarget = true;

        // 설계도의 테두리는 50% 투명이다. 상자가 화면을 가리지 않고 얹혀 있는 느낌을 준다.
        Border(box.rectTransform, UiTheme.With(UiTheme.Line, 0.5f), 1f);

        Image next = Ensure(box.rectTransform, "NextIndicator", UiTheme.AccentBright);
        Place(next.rectTransform, 1408f, 215f, 10f, 10f);

        return box.rectTransform;
    }

    // 본문은 상자의 자식으로 옮긴다. 상자를 움직이면 글자도 따라와야 하기 때문이다.
    static void MoveBodyInto(Transform ui, RectTransform box, TMP_FontAsset font) {
        Transform body = ui.Find("Text (TMP)");
        if (body == null) { Debug.LogWarning("[DialogueBox] 본문(Text (TMP))을 찾지 못했습니다."); return; }

        body.SetParent(box, false);

        RectTransform rect = (RectTransform)body;
        // 설계도의 본문 칸(558×68)은 예시 문장의 실제 글자 크기라 그대로 쓰면 긴 대사가 잘린다.
        // 상자 안쪽을 여백만 남기고 꽉 채운다.
        Place(rect, BoxPadding, 60f, BoxW - BoxPadding * 2f, BoxH - 60f - BoxPadding);

        TMP_Text text = body.GetComponent<TMP_Text>();
        if (text == null) return;

        text.font = font;
        text.fontSharedMaterial = font.material;
        text.color = UiTheme.TextBody;
        text.fontSize = 28f;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
    }

    #endregion
    #region 초상화 · 이름표 · 버튼

    static void LayoutPortraits(Transform ui) {
        // 색은 건드리지 않는다. 초상화 스프라이트가 붙는 Image 라 흰색이어야 원래 색으로 나온다.
        RectTransform left = Find(ui, "PortraitLeft");
        if (left != null) Place(left, PortraitLeftX, PortraitY, PortraitW, PortraitH);

        RectTransform right = Find(ui, "PortraitRight");
        if (right != null) Place(right, PortraitRightX, PortraitY, PortraitW, PortraitH);
    }

    static void LayoutNameBox(Transform ui, TMP_FontAsset font) {
        Transform nameBox = ui.Find("NameBox");
        if (nameBox == null) { Debug.LogWarning("[DialogueBox] NameBox 를 찾지 못했습니다."); return; }

        Image bg = nameBox.GetComponent<Image>();
        if (bg != null) bg.color = UiTheme.PanelRaised;

        RectTransform rect = (RectTransform)nameBox;
        Place(rect, NameX, NameY, NameW, NameH);


        Border(rect, UiTheme.Line, 1f);

        Image accent = Ensure(rect, "AccentBar", UiTheme.Accent);
        Place(accent.rectTransform, 0f, 0f, 3f, NameH);

        Transform nameText = nameBox.Find("NameText");
        if (nameText != null) {
            RectTransform tr = (RectTransform)nameText;
            Place(tr, 24f, 11f, NameW - 48f, 29f);

            TMP_Text t = nameText.GetComponent<TMP_Text>();
            if (t != null) {
                t.font = font;
                t.fontSharedMaterial = font.material;
                t.color = UiTheme.TextHigh;
                t.fontSize = 24f;
                t.alignment = TextAlignmentOptions.Left;
                t.raycastTarget = false;
            }
        }
    }

    static void LayoutButtons(Transform ui, TMP_FontAsset font) {
        StyleButton(ui, "AutoButton", "AutoLabel", "AUTO", AutoX, font);

        // SKIP 은 설계도에만 있던 버튼이라 새로 만든다. 동작은 DialogueView._skipButton 에 물려야 켜진다.
        Transform skip = ui.Find("SkipButton");
        if (skip == null) {
            Image made = Ensure(ui, "SkipButton", UiTheme.PanelRaised);
            made.raycastTarget = true;
            made.gameObject.AddComponent<Button>();

            TextMeshProUGUI label = NewText(made.rectTransform, "SkipLabel", font, 18f,
                TextAlignmentOptions.Center, UiTheme.TextMuted);
            label.text = "SKIP";
            Stretch(label.rectTransform);
        }
        StyleButton(ui, "SkipButton", "SkipLabel", "SKIP", SkipX, font);

        WireSkipButton(ui);
    }

    static void StyleButton(Transform ui, string buttonName, string labelName, string text, float x, TMP_FontAsset font) {
        Transform button = ui.Find(buttonName);
        if (button == null) return;

        Image bg = button.GetComponent<Image>();
        if (bg != null) {
            bg.color = UiTheme.PanelRaised;
            bg.raycastTarget = true; // 배경이 클릭을 받아야 버튼이 눌린다.
        }

        RectTransform rect = (RectTransform)button;
        Place(rect, x, ButtonY, ButtonW, ButtonH);
        Border(rect, UiTheme.Line, 1f);

        Transform label = button.Find(labelName);
        if (label == null) return;

        Stretch((RectTransform)label);
        TMP_Text t = label.GetComponent<TMP_Text>();
        if (t == null) return;

        t.font = font;
        t.fontSharedMaterial = font.material;
        t.color = UiTheme.TextMuted;
        t.fontSize = 18f;
        t.alignment = TextAlignmentOptions.Center;
        t.text = text;
        t.raycastTarget = false;
    }

    // 새로 만든 SKIP 버튼을 DialogueView 에 물려준다. 안 물리면 버튼만 있고 눌러도 아무 일이 없다.
    static void WireSkipButton(Transform ui) {
        DialogueView view = ui.GetComponent<DialogueView>();
        Transform skip = ui.Find("SkipButton");
        if (view == null || skip == null) return;

        Button button = skip.GetComponent<Button>();
        if (button == null) return;

        SerializedObject so = new SerializedObject(view);
        SerializedProperty prop = so.FindProperty("_skipButton");
        if (prop == null) {
            Debug.LogWarning("[DialogueBox] DialogueView 에 _skipButton 필드가 없습니다. 스크립트가 옛 버전인지 확인하세요.");
            return;
        }

        prop.objectReferenceValue = button;

        // AUTO 켜짐/꺼짐 색도 팔레트 밖(금색·순백)이던 것을 토큰으로 맞춘다.
        SerializedProperty on = so.FindProperty("_autoOnColor");
        SerializedProperty off = so.FindProperty("_autoOffColor");
        if (on != null) on.colorValue = UiTheme.AccentBright;
        if (off != null) off.colorValue = UiTheme.TextMuted;

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    #endregion
    #region 생성 도우미

    static RectTransform Find(Transform parent, string name) {
        Transform t = parent.Find(name);
        return t == null ? null : (RectTransform)t;
    }

    // 이름으로 찾아보고 없을 때만 만든다. 여러 번 실행해도 같은 결과가 되도록.
    static Image Ensure(Transform parent, string name, Color color) {
        Transform found = parent.Find(name);

        if (found == null) {
            GameObject obj = new GameObject(name, typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            found = obj.transform;
        }

        Image image = found.GetComponent<Image>();
        if (image == null) image = found.gameObject.AddComponent<Image>();

        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static void Border(RectTransform parent, Color color, float thickness) {
        Transform found = parent.Find("Border");
        RectTransform border;

        if (found == null) {
            GameObject obj = new GameObject("Border", typeof(RectTransform));
            obj.transform.SetParent(parent, false);
            border = (RectTransform)obj.transform;
        } else {
            border = (RectTransform)found;
        }

        Stretch(border);

        Edge(border, "Top", color, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, thickness));
        Edge(border, "Bottom", color, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, thickness));
        Edge(border, "Left", color, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(thickness, 0f));
        Edge(border, "Right", color, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), new Vector2(thickness, 0f));
    }

    static void Edge(RectTransform parent, string name, Color color, Vector2 min, Vector2 max, Vector2 pivot, Vector2 size) {
        Image edge = Ensure(parent, name, color);

        RectTransform rect = edge.rectTransform;
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = size;
    }

    static TextMeshProUGUI NewText(Transform parent, string name, TMP_FontAsset font, float size,
        TextAlignmentOptions align, Color color) {

        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);

        TextMeshProUGUI text = obj.AddComponent<TextMeshProUGUI>();
        if (font != null) { text.font = font; text.fontSharedMaterial = font.material; }
        text.fontSize = size;
        text.alignment = align;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    // 설계도가 좌상단 기준이라 그대로 옮겨 적을 수 있게 앵커를 왼쪽 위로 고정한다.
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

    #endregion
}
#endif
