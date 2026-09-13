#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// 이미 만들어진 UI 프리팹에 UiTheme(「퇴락한 빈티지 극장」) 팔레트를 한 번에 입히는 도구.
//
// 왜 프리팹 빌더를 다시 돌리지 않고 이런 도구를 따로 두는가:
// MainLobby 처럼 생성한 뒤 손으로 고친 프리팹이 있어서(자리표시 주석과 거울 소품을 이미 지웠다) 빌더를
// 다시 실행하면 지운 오브젝트가 되살아나고 손본 배치가 통째로 날아간다. 이 도구는 배치를 건드리지 않고
// 색 · 폰트 · 스프라이트만 덮어쓴다.
//
// 그래서 여러 번 실행해도 안전하며(멱등), 새 UI 프리팹을 만들면 아래 적용표에 한 줄만 추가하면 된다.
// 반대로 인스펙터에서 색을 손으로 바꿔둔 곳은 이 도구를 돌리면 표의 값으로 되돌아간다 — 색을 바꿀 일이
// 생기면 프리팹이 아니라 UiTheme 이나 이 표를 고치는 것이 맞다.
//
// 사용법: Tools ▸ FCC ▸ UI ▸ Apply Theme Colors
public static class UiThemeApplier {
    #region 경로

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/UI";

    // 유니티 기본 UISprite 는 모서리가 둥근 버튼용이라 각진 UI 규칙에 어긋난다. 같은 기본 리소스인
    // Background 가 평면 사각형이라 이쪽으로 갈아끼운다.
    const string RoundedSpriteName = "UISprite";
    const string FlatSpritePath = "UI/Skin/Background.psd";

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/UI/Apply Theme Colors")]
    public static void ApplyAll() {
        TMP_FontAsset font = PrefabBuilderFont.LoadProjectFont("UiTheme");
        Sprite flat = AssetDatabase.GetBuiltinExtraResource<Sprite>(FlatSpritePath);

        int touched = 0;
        touched += Apply("MainLobby", font, flat, MainLobby);
        touched += Apply("PlayerHud", font, flat, PlayerHud);
        touched += Apply("SkillLoadout", font, flat, SkillLoadout);
        touched += Apply("SkillLoadoutRow", font, flat, SkillLoadoutRow);
        touched += Apply("ObjectiveChecklist", font, flat, ObjectiveChecklist);
        touched += Apply("ObjectiveItemRow", font, flat, ObjectiveItemRow);
        touched += Apply("AreaTitle", font, flat, AreaTitle);
        touched += Apply("Settingpanel", font, flat, Settingpanel);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[UiTheme] 프리팹 {touched}개에 「퇴락한 빈티지 극장」 팔레트를 입혔습니다.\n" +
            "씬에 이미 올라가 있는 인스턴스에서 색을 따로 덮어쓴 곳(인스펙터에 굵게 표시되는 오버라이드)이 " +
            "있으면 그쪽이 이깁니다. 화면이 그대로면 씬 인스턴스의 오버라이드를 Revert 하세요.");
    }

    #endregion
    #region 프리팹별 적용표

    static void MainLobby(GameObject root) {
        Img(root, "Screen", UiTheme.Stage); // 알파가 0이라 카메라의 기본 청회색이 그대로 비쳤다.

        ImgTree(root, "LogoFrame/Border", UiTheme.Line);
        Txt(root, "LogoText", UiTheme.TextMuted); // 검정으로 손보여 있어 어두운 무대에서 보이지 않았다.
        Txt(root, "Subtitle", UiTheme.TextMuted);

        ImgTree(root, "Frame", UiTheme.Accent); // 골라진 줄을 감싸는 굵은 테두리. 이 화면에서 포인트 컬러가 쓰이는 자리다.
        Img(root, "Underline", UiTheme.Line);
        Txt(root, "Label", UiTheme.TextBody);
        Txt(root, "Suffix", UiTheme.TextMuted);

        Txt(root, "Version", UiTheme.TextMuted);
        Txt(root, "Hint", UiTheme.TextMuted);

        // 커튼은 자기 자신만 칠한다. 트리로 칠하면 안쪽 모서리 선(Edge)까지 벨벳색으로 덮인다.
        Img(root, "CurtainLeft", UiTheme.Curtain);
        Img(root, "CurtainRight", UiTheme.Curtain);
        Img(root, "Edge", UiTheme.Line);

        foreach (MainLobbyItemView item in root.GetComponentsInChildren<MainLobbyItemView>(true)) {
            item.normalBackground = UiTheme.Transparent;
            item.selectedBackground = UiTheme.With(UiTheme.PanelRaised, 0.92f);
            item.labelColor = UiTheme.TextBody;
            item.selectedLabelColor = UiTheme.TextHigh;
            item.suffixColor = UiTheme.TextMuted;
            item.lockedColor = UiTheme.TextDim;
        }
    }

    static void PlayerHud(GameObject root) {
        Img(root, "Track", UiTheme.With(UiTheme.Panel, 0.85f));
        Img(root, "DelayedFill", UiTheme.With(UiTheme.TextBody, 0.65f)); // 방금 깎인 만큼 뒤늦게 줄어드는 잔상.
        Img(root, "Fill", UiTheme.AccentBright); // 자아 게이지. 면적이 넓어 Accent 보다 한 단계 밝은 쪽을 쓴다.
        Txt(root, "Value", UiTheme.TextHigh);
    }

    static void SkillLoadout(GameObject root) {
        Img(root, "Dim", UiTheme.Dim);
        Img(root, "Window", UiTheme.With(UiTheme.Panel, 0.98f));
        Txt(root, "Title", UiTheme.TextHigh); // 금색이었다. 포인트 컬러를 둘로 늘리지 않으려고 상아로 내렸다.

        Img(root, "Slot0", UiTheme.PanelRaised);
        Img(root, "Slot1", UiTheme.PanelRaised);
        Img(root, "Slot2", UiTheme.PanelRaised);
        Txt(root, "SlotLabel", UiTheme.TextMuted);
        Txt(root, "SkillLabel", UiTheme.TextHigh);

        Txt(root, "ListTitle", UiTheme.TextMuted);
        Txt(root, "Description", UiTheme.TextBody);
        Txt(root, "Help", UiTheme.TextMuted);

        // 강화 패널은 빌더에만 있고 지금 프리팹에는 아직 없다(빌더를 다시 돌리지 않았다).
        // 나중에 생겼을 때 색이 어긋나지 않도록 미리 적어두되, 없다고 경고하지는 않는다.
        Txt(root, "LevelLabel", UiTheme.Accent, false);
        Txt(root, "StatsLabel", UiTheme.TextHigh, false);
        Txt(root, "CostLabel", UiTheme.Accent, false);
        Img(root, "UpgradeButton", UiTheme.Accent, false);
        Img(root, "RefundButton", UiTheme.PanelRaised, false);

        foreach (SkillSlotView slot in root.GetComponentsInChildren<SkillSlotView>(true)) {
            slot.normalColor = UiTheme.PanelRaised;
            slot.selectedColor = UiTheme.Accent;
            slot.skillTextColor = UiTheme.TextHigh;
            slot.emptyTextColor = UiTheme.TextMuted;
        }

        foreach (SkillLoadoutView view in root.GetComponentsInChildren<SkillLoadoutView>(true)) {
            // 좋아지는 수치와 그대로인 수치를 초록/보라로 갈랐었다. 색을 늘리는 대신 밝기 차로 구분한다.
            view.statImprovedColor = UiTheme.TextHigh;
            view.statSameColor = UiTheme.TextDim;
        }
    }

    static void SkillLoadoutRow(GameObject root) {
        Img(root, "SkillLoadoutRow", UiTheme.PanelRaised);
        Txt(root, "Name", UiTheme.TextHigh);
        Txt(root, "State", UiTheme.TextMuted);

        foreach (SkillRowView row in root.GetComponentsInChildren<SkillRowView>(true)) {
            row.normalColor = UiTheme.PanelRaised;
            row.highlightColor = UiTheme.Line; // 커서가 올라간 줄. 면으로 쓰기엔 Accent 가 세서 경계선 색을 면으로 돌려 쓴다.
            row.nameColor = UiTheme.TextHigh;
            row.equippedColor = UiTheme.Accent; // 이 줄에서 포인트 컬러를 쓰는 곳은 "장착 중" 하나뿐이다.
            row.cooldownColor = UiTheme.TextMuted;
        }
    }

    static void ObjectiveChecklist(GameObject root) {
        Txt(root, "MissionTitle", UiTheme.TextHigh); // 여기도 금색이었다.
        Txt(root, "MissionDescription", UiTheme.TextMuted);
    }

    static void ObjectiveItemRow(GameObject root) {
        Img(root, "Checkbox", UiTheme.Line);
        Txt(root, "Label", UiTheme.TextBody);
        Txt(root, "Count", UiTheme.TextMuted);

        foreach (ObjectiveItemView item in root.GetComponentsInChildren<ObjectiveItemView>(true)) {
            item.labelColor = UiTheme.TextBody;
            item.completedLabelColor = UiTheme.TextDim;
            item.checkboxOffColor = UiTheme.Line;
            item.checkboxOnColor = UiTheme.Accent;
        }
    }

    static void AreaTitle(GameObject root) {
        Txt(root, "Title", UiTheme.TextHigh);
        Txt(root, "Subtitle", UiTheme.TextMuted);
    }

    static void Settingpanel(GameObject root) {
        Img(root, "Settingpanel", UiTheme.With(UiTheme.Panel, 0.96f)); // 흰색 39% 반투명이라 유리판처럼 보였다.
        Txt(root, "Text (TMP)", UiTheme.TextHigh);
    }

    #endregion
    #region 적용 절차

    static int Apply(string prefabName, TMP_FontAsset font, Sprite flat, System.Action<GameObject> paint) {
        string path = $"{PrefabDir}/{prefabName}.prefab";

        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) {
            Debug.LogWarning($"[UiTheme] 프리팹이 없어 건너뛰었습니다 — {path}");
            return 0;
        }

        // 에셋을 직접 고치면 씬에 올라간 인스턴스와 어긋나므로, 편집용 사본을 열어서 고친 뒤 되돌려 쓴다.
        GameObject contents = PrefabUtility.LoadPrefabContents(path);

        try {
            paint(contents);
            ApplyFont(contents, font, prefabName);
            SquareOffSprites(contents, flat);

            PrefabUtility.SaveAsPrefabAsset(contents, path);
        } finally {
            PrefabUtility.UnloadPrefabContents(contents);
        }

        return 1;
    }

    // 폰트 참조가 어긋나면 TMP 가 조용히 LiberationSans 로 떨어져 한글이 전부 네모(□)가 된다.
    // 실제로 SkillLoadout 은 참조가 끊겨 있었고 Settingpanel 은 LiberationSans 를 그대로 쓰고 있었다.
    static void ApplyFont(GameObject root, TMP_FontAsset font, string prefabName) {
        if (font == null) return;

        foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) {
            if (text.font == font) continue;

            string before = text.font == null ? "(끊어진 참조)" : text.font.name;
            Debug.Log($"[UiTheme] {prefabName}/{text.name} 의 폰트를 '{before}' 에서 '{font.name}' 으로 바꿨습니다.");
            text.font = font;
        }
    }

    static void SquareOffSprites(GameObject root, Sprite flat) {
        if (flat == null) return;

        foreach (Image image in root.GetComponentsInChildren<Image>(true)) {
            if (image.sprite == null || image.sprite.name != RoundedSpriteName) continue;
            image.sprite = flat;
        }
    }

    #endregion
    #region 대상 찾기

    // required 를 끄면 찾지 못해도 경고하지 않는다. 아직 프리팹에 만들어지지 않은 부분을 미리 적어둘 때 쓴다.
    static void Img(GameObject root, string path, Color color, bool required = true) {
        foreach (Transform t in FindAll(root, path, required)) {
            Image image = t.GetComponent<Image>();
            if (image != null) image.color = color;
        }
    }

    // path 로 지정한 오브젝트와 그 아래 전부. 테두리처럼 얇은 Image 4개가 한 묶음인 곳에 쓴다.
    static void ImgTree(GameObject root, string path, Color color, bool required = true) {
        foreach (Transform t in FindAll(root, path, required)) {
            foreach (Image image in t.GetComponentsInChildren<Image>(true)) image.color = color;
        }
    }

    static void Txt(GameObject root, string path, Color color, bool required = true) {
        foreach (Transform t in FindAll(root, path, required)) {
            TMP_Text text = t.GetComponent<TMP_Text>();
            if (text != null) text.color = color;
        }
    }

    // 경로의 끝으로 찾는다. 메뉴 5줄의 Label 처럼 같은 구조가 반복되는 곳을 한 줄로 칠하기 위해서다.
    // 단순 EndsWith 를 쓰면 "SkillLabel" 이 "Label" 에 걸리므로 반드시 '/' 경계에서 끊어 비교한다.
    static List<Transform> FindAll(GameObject root, string path, bool required) {
        List<Transform> hits = new List<Transform>();

        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) {
            string full = PathOf(root.transform, t);
            if (full == path || full.EndsWith("/" + path)) hits.Add(t);
        }

        if (hits.Count == 0 && required) {
            Debug.LogWarning($"[UiTheme] '{root.name}' 안에서 '{path}' 를 찾지 못했습니다. 적용표가 프리팹 구조와 어긋났습니다.");
        }

        return hits;
    }

    static string PathOf(Transform root, Transform target) {
        string path = target.name;

        for (Transform t = target.parent; t != null; t = t.parent) {
            path = t.name + "/" + path;
            if (t == root) break;
        }

        return path;
    }

    #endregion
}
#endif
