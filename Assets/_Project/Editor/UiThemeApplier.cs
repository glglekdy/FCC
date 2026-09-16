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
        touched += Apply("SaveSlotSelect", font, flat, SaveSlotSelect);
        touched += Apply("SaveSlotRow", font, flat, SaveSlotRow);
        touched += Apply("PauseMenu", font, flat, PauseMenu);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[UiTheme] 프리팹 {touched}개에 「퇴락한 빈티지 극장」 팔레트를 입혔습니다.\n" +
            "씬에 이미 올라가 있는 인스턴스에서 색을 따로 덮어쓴 곳(인스펙터에 굵게 표시되는 오버라이드)이 " +
            "있으면 그쪽이 이깁니다. 화면이 그대로면 씬 인스턴스의 오버라이드를 Revert 하세요.");
    }

    #endregion
    #region 프리팹별 적용표

    // Figma MainLobby_new 기준. 옛 안의 로고 틀 · 줄 테두리(Frame) · 구분선(Underline) · 보조 표기(Suffix) ·
    // 커튼(열리는 연출과 함께 지웠다)은 새 안에 없다. 표에 남겨두면 찾지 못했다는 경고만 쌓인다.
    static void MainLobby(GameObject root) {
        Img(root, "Screen", UiTheme.Stage); // 알파가 0이라 카메라의 기본 청회색이 그대로 비쳤다.

        Txt(root, "Title", UiTheme.TextHigh); // Figma 는 #DED6C7 이지만 토큰에 없는 값이라 가장 가까운 TextHigh 로 맞춘다.
        Txt(root, "Subtitle", UiTheme.TextMuted);
        Txt(root, "Label", UiTheme.TextBody);

        Txt(root, "Version", UiTheme.TextMuted);
        Txt(root, "Hint", UiTheme.TextMuted);

        foreach (MainLobbyItemView item in root.GetComponentsInChildren<MainLobbyItemView>(true)) {
            item.normalBackground = UiTheme.Transparent;
            item.selectedBackground = UiTheme.Transparent; // 새 안은 면을 칠하지 않고 글자 밝기만으로 고른 줄을 보여준다.
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

        // 스킬 칸 3개는 속이 같은 구조라 이름이 겹친다. 경로 끝으로 찾으므로 한 줄이 세 칸을 모두 칠한다.
        Img(root, "SkillSlot0", UiTheme.Panel);
        Img(root, "SkillSlot1", UiTheme.Panel);
        Img(root, "SkillSlot2", UiTheme.Panel);
        ImgTree(root, "Border", UiTheme.Line);
        Txt(root, "Key", UiTheme.TextMuted);
        Txt(root, "Monogram", UiTheme.TextHigh);
        Img(root, "Cooldown", UiTheme.With(UiTheme.Stage, 0.82f)); // 쿨타임 동안 칸을 덮는 막. 아래 글자가 비쳐야 해서 완전히 불투명하지는 않다.
        Txt(root, "CooldownText", UiTheme.TextHigh);

        Txt(root, "Shard_Label", UiTheme.TextMuted);
        Txt(root, "Shard_Value", UiTheme.TextHigh);

        foreach (HudSkillSlotView slot in root.GetComponentsInChildren<HudSkillSlotView>(true)) {
            slot.monogramColor = UiTheme.TextHigh;
            slot.emptyColor = UiTheme.TextDim;
        }
    }

    // 두 열 구조(왼쪽 장착 슬롯 · 보유 스킬 / 오른쪽 상세 · 강화)로 다시 지은 뒤의 적용표다.
    // 옛 표에 있던 ListTitle · Description · UpgradeButton 같은 이름은 프리팹에서 사라져 함께 지웠다.
    static void SkillLoadout(GameObject root) {
        Img(root, "Dim", UiTheme.Dim);
        Img(root, "Window", UiTheme.Panel);

        // 창 · 슬롯 · 되돌리기 버튼의 테두리가 모두 "Border" 라 한 줄로 함께 칠한다.
        ImgTree(root, "Border", UiTheme.Line);
        ImgTree(root, "FocusBorder", UiTheme.Accent); // 지금 고른 슬롯에만 켜지는 테두리.

        Txt(root, "Header/Title", UiTheme.TextHigh); // 금색이었다. 포인트 컬러를 둘로 늘리지 않으려고 상아로 내렸다.
        Txt(root, "Header/ShardLabel", UiTheme.TextMuted);
        Txt(root, "Header/ShardCount", UiTheme.TextHigh);
        Img(root, "Header/Divider", UiTheme.Line);

        Txt(root, "SlotsCaption", UiTheme.TextMuted);
        Txt(root, "ListCaption", UiTheme.TextMuted);
        Img(root, "Slot0", UiTheme.PanelRaised);
        Img(root, "Slot1", UiTheme.PanelRaised);
        Img(root, "Slot2", UiTheme.PanelRaised);
        Txt(root, "SlotLabel", UiTheme.TextMuted);
        Txt(root, "SkillLabel", UiTheme.TextHigh);
        Img(root, "ColumnDivider", UiTheme.Line);

        Txt(root, "Detail/Name", UiTheme.TextHigh);
        Txt(root, "Detail/Role", UiTheme.TextMuted);
        Txt(root, "Detail/Description", UiTheme.TextBody);
        Txt(root, "Detail/EquipStatus", UiTheme.TextMuted);

        Txt(root, "LevelLabel", UiTheme.TextMuted); // 슬롯 칸과 레벨 줄 양쪽에 있는 이름인데 둘 다 보조 표기라 한 줄로 칠한다.
        Txt(root, "StatsHeader", UiTheme.TextMuted);
        Img(root, "StatsDivider", UiTheme.Line);
        Txt(root, "Stats", UiTheme.TextBody);
        Img(root, "ActionDivider", UiTheme.Line);
        Txt(root, "Cost", UiTheme.TextHigh);

        Img(root, "Btn_Upgrade", UiTheme.Accent); // 조각을 쓰는 버튼 하나만 면을 칠한다.
        Txt(root, "Btn_Upgrade/Label", UiTheme.TextHigh);
        Img(root, "Btn_Refund", UiTheme.Panel);
        Txt(root, "Btn_Refund/Label", UiTheme.TextBody);

        Img(root, "FooterDivider", UiTheme.Line);
        Txt(root, "Help", UiTheme.TextMuted);

        foreach (SkillSlotView slot in root.GetComponentsInChildren<SkillSlotView>(true)) {
            slot.skillTextColor = UiTheme.TextHigh;
            slot.emptyTextColor = UiTheme.TextDim; // "비어 있음"은 잠긴 항목과 같은 밝기로 낮춘다.
        }

        foreach (SkillLoadoutView view in root.GetComponentsInChildren<SkillLoadoutView>(true)) {
            // 좋아지는 수치와 그대로인 수치를 초록/보라로 갈랐었다. 색을 늘리는 대신 밝기 차로 구분한다.
            view.statImprovedColor = UiTheme.TextHigh;
            view.statSameColor = UiTheme.TextDim;
            view.pipOnColor = UiTheme.AccentBright;
            view.pipOffColor = UiTheme.PanelRaised;
            view.mutedTextColor = UiTheme.TextMuted;
        }
    }

    static void SkillLoadoutRow(GameObject root) {
        // 줄 바탕은 커서가 올라온 줄에만 칠해진다. **투명이어도 raycastTarget 은 켜져 있어야 마우스를 받습니다.**
        Img(root, "SkillLoadoutRow", UiTheme.Transparent);
        Img(root, "FocusMarker", UiTheme.Accent);
        Txt(root, "Name", UiTheme.TextBody);
        Txt(root, "State", UiTheme.TextMuted);

        foreach (SkillRowView row in root.GetComponentsInChildren<SkillRowView>(true)) {
            row.normalColor = UiTheme.Transparent;
            row.highlightColor = UiTheme.PanelRaised;
            row.nameColor = UiTheme.TextBody;
            row.highlightNameColor = UiTheme.TextHigh;
            row.stateColor = UiTheme.TextMuted;
            // 글자에 쓰는 포인트 컬러. 가는 획에서는 Accent 가 바탕에 거의 묻혀서 한 단계 밝은 쪽을 쓴다
            // (기억 선택 화면의 「삭제」 글자와 같은 이유).
            row.equippedColor = UiTheme.AccentBright;
            row.lockedColor = UiTheme.TextDim;
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

    static void SaveSlotSelect(GameObject root) {
        Img(root, "Screen", UiTheme.Stage);
        Img(root, "Curtain_L", UiTheme.Curtain);
        Img(root, "Curtain_R", UiTheme.Curtain);

        Txt(root, "Header/Title", UiTheme.TextHigh);
        Txt(root, "Header/Subtitle", UiTheme.TextMuted);
        Img(root, "Header/Divider", UiTheme.Line);

        Img(root, "Footer/Divider", UiTheme.Line);
        Txt(root, "Footer/Hint", UiTheme.TextMuted);
        Img(root, "Btn_Back", UiTheme.Panel);
        ImgTree(root, "Btn_Back/Border", UiTheme.Line);
        Txt(root, "Btn_Back/Label", UiTheme.TextBody);

        Img(root, "ConfirmDim", UiTheme.Dim);
        Img(root, "ConfirmWindow", UiTheme.Panel);
        ImgTree(root, "ConfirmWindow/Border", UiTheme.Line);
        Txt(root, "ConfirmWindow/Title", UiTheme.TextHigh);
        Txt(root, "ConfirmWindow/Summary", UiTheme.TextMuted);
        Txt(root, "ConfirmWindow/Warning", UiTheme.TextBody);
        Img(root, "ConfirmWindow/Divider", UiTheme.Line);
        ImgTree(root, "Btn_Cancel/Border", UiTheme.Line);
        ImgTree(root, "Btn_Accept/Border", UiTheme.Line);
        ImgTree(root, "Btn_Cancel/FocusBorder", UiTheme.Accent);
        ImgTree(root, "Btn_Accept/FocusBorder", UiTheme.Accent);

        // 확인 창 버튼의 면과 글자는 포커스에 따라 뷰가 칠한다. 프리팹 색이 아니라 뷰의 색 필드를 맞춰야 실행 중에 따라온다.
        foreach (SaveSlotSelectView view in root.GetComponentsInChildren<SaveSlotSelectView>(true)) {
            view.buttonColor = UiTheme.Panel;
            view.focusedButtonColor = UiTheme.PanelRaised;
            view.cancelLabelColor = UiTheme.TextBody;
            view.focusedCancelLabelColor = UiTheme.TextHigh;
            view.dangerLabelColor = UiTheme.AccentBright; // 되돌릴 수 없는 버튼. 면이 아니라 글자에만 포인트 컬러를 쓴다.
        }
    }

    static void SaveSlotRow(GameObject root) {
        Img(root, "SaveSlotRow", UiTheme.Panel);
        ImgTree(root, "Border", UiTheme.Line); // 줄 테두리와 장착 스킬 칸 테두리가 같은 이름이라 한 줄로 함께 칠한다.
        ImgTree(root, "FocusBorder", UiTheme.Accent);
        Txt(root, "Number", UiTheme.TextMuted);
        Img(root, "VDivider", UiTheme.Line);

        Txt(root, "Filled/Region", UiTheme.TextHigh);
        Txt(root, "Filled/Checkpoint", UiTheme.TextMuted);
        Txt(root, "Filled/Ego_Label", UiTheme.TextMuted);
        Img(root, "Filled/Ego_Track", UiTheme.TextDim);
        Img(root, "Ego_Fill", UiTheme.AccentBright); // 자아 게이지. HUD 와 같은 색이어야 같은 값으로 읽힌다.
        Txt(root, "Filled/Ego_Value", UiTheme.TextBody);
        Txt(root, "Filled/Shard_Label", UiTheme.TextMuted);
        Txt(root, "Filled/Shard_Value", UiTheme.TextBody);
        Txt(root, "Filled/Skills_Label", UiTheme.TextMuted);
        Img(root, "Skill_0", UiTheme.PanelRaised);
        Img(root, "Skill_1", UiTheme.PanelRaised);
        Img(root, "Skill_2", UiTheme.PanelRaised);
        Txt(root, "Filled/SavedAt", UiTheme.TextMuted);

        Txt(root, "Empty/Title", UiTheme.TextBody);
        Txt(root, "Empty/Sub", UiTheme.TextMuted);
        Img(root, "Btn_Create", UiTheme.Accent);
        Txt(root, "Btn_Create/Label", UiTheme.TextHigh);

        foreach (SaveSlotRowView row in root.GetComponentsInChildren<SaveSlotRowView>(true)) {
            row.normalColor = UiTheme.Panel;
            row.focusedColor = UiTheme.PanelRaised;
            row.numberColor = UiTheme.TextMuted;
            row.focusedNumberColor = UiTheme.TextHigh;
            row.skillFilledColor = UiTheme.PanelRaised;
            row.skillEmptyColor = UiTheme.Panel;
        }
    }

    static void PauseMenu(GameObject root) {
        Img(root, "Column", UiTheme.With(UiTheme.Stage, 0.75f)); // 뒤의 게임 화면이 흐리게 비쳐야 멈춰 있다는 것이 보인다.

        Txt(root, "Header/Title", UiTheme.TextHigh);
        Txt(root, "Header/Subtitle", UiTheme.TextMuted);
        Img(root, "Header/Divider", UiTheme.Line);

        ImgTree(root, "FocusMarker", UiTheme.Accent);

        Img(root, "Footer/Divider", UiTheme.Line);
        Txt(root, "Footer/Hint", UiTheme.TextMuted);

        Img(root, "ConfirmDim", UiTheme.Dim);
        Img(root, "ConfirmWindow", UiTheme.Panel);
        ImgTree(root, "ConfirmWindow/Border", UiTheme.Line);
        Txt(root, "ConfirmWindow/Title", UiTheme.TextHigh);
        Txt(root, "ConfirmWindow/Message", UiTheme.TextBody);
        Img(root, "ConfirmWindow/Divider", UiTheme.Line);
        ImgTree(root, "Btn_Cancel/Border", UiTheme.Line);
        ImgTree(root, "Btn_Exit/Border", UiTheme.Line);
        ImgTree(root, "Btn_Cancel/FocusBorder", UiTheme.Accent);
        ImgTree(root, "Btn_Exit/FocusBorder", UiTheme.Accent);

        // 줄의 면 · 글자와 확인 창 버튼은 선택에 따라 뷰가 칠한다. 프리팹 색이 아니라 뷰의 색 필드를 맞춰야 실행 중에 따라온다.
        foreach (PauseMenuItemView item in root.GetComponentsInChildren<PauseMenuItemView>(true)) {
            item.normalBackground = UiTheme.Transparent;
            item.selectedBackground = UiTheme.With(UiTheme.PanelRaised, 0.75f);
            item.labelColor = UiTheme.TextBody;
            item.selectedLabelColor = UiTheme.TextHigh;
            item.lockedColor = UiTheme.TextDim;
        }

        foreach (PauseMenuView view in root.GetComponentsInChildren<PauseMenuView>(true)) {
            view.buttonColor = UiTheme.Panel;
            view.focusedButtonColor = UiTheme.PanelRaised;
            view.cancelLabelColor = UiTheme.TextBody;
            view.focusedCancelLabelColor = UiTheme.TextHigh;
            view.dangerLabelColor = UiTheme.AccentBright; // 되돌릴 수 없는 버튼. 면이 아니라 글자에만 포인트 컬러를 쓴다.
        }
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
