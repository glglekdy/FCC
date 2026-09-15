#if UNITY_EDITOR
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

// Game View 툴바에 스킬 교체용 드롭다운을 띄우는 에디터 전용 도구.
// Localization 패키지의 GameViewLanguageMenu(언어 전환 드롭다운, Packages/com.unity.localization/Editor/UI/)와
// 같은 방식으로 구현했다 — PopupField를 GameView의 rootVisualElement에 직접 얹는다.
//
// 플레이 모드에서만 의미가 있으므로(SkillManager가 씬에 있어야 함) 플레이 모드 진입 시에만 붙이고 나가면 뗀다.
// 슬롯(1·2·3) 하나당 드롭다운 하나씩 세로로 쌓아 놓고, 고르면 그 자리에서 바로 장착된다.
//
// 목록은 프로젝트에 있는 SkillBase 에셋 전부를 훑는다. 아직 해금되지 않은 스킬도 테스트할 수 있어야 하므로
// SkillManager.unlockedSkills에 없으면 고르는 순간 자동으로 해금 처리한다 — **테스트 전용 동작이며 실제
// 기억 조각 해금 로직을 대신하지 않는다.**
[InitializeOnLoad]
static class GameViewSkillMenu {
    #region 상태

    static readonly List<PopupField<SkillBase>> menus = new();
    static readonly List<SkillBase> allSkills = new();

    #endregion
    #region 초기화

    static GameViewSkillMenu() {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    static void OnPlayModeStateChanged(PlayModeStateChange state) {
        if (state == PlayModeStateChange.EnteredPlayMode) Show();
        else if (state == PlayModeStateChange.ExitingPlayMode) Hide();
    }

    #endregion
    #region 표시 · 제거

    static void Show() {
        RefreshSkillList();
        if (allSkills.Count == 0) return;

        SkillManager skillManager = Object.FindAnyObjectByType<SkillManager>();
        if (skillManager == null) return; // 씬에 플레이어(SkillManager)가 없으면 붙일 자리가 없다.

        Hide();

        Assembly assembly = typeof(EditorWindow).Assembly;
        System.Type gameViewType = assembly.GetType("UnityEditor.GameView");
        Object[] gameViews = Resources.FindObjectsOfTypeAll(gameViewType);

        foreach (EditorWindow gameView in gameViews) {
            for (int slot = 0; slot < SkillManager.SlotCount; slot++) {
                menus.Add(BuildSlotMenu(gameView, skillManager, slot));
            }
        }
    }

    static void Hide() {
        foreach (PopupField<SkillBase> menu in menus) {
            menu.RemoveFromHierarchy();
        }
        menus.Clear();
    }

    #endregion
    #region 드롭다운 생성

    static PopupField<SkillBase> BuildSlotMenu(EditorWindow gameView, SkillManager skillManager, int slotIndex) {
        List<SkillBase> choices = new() { null };
        choices.AddRange(allSkills);

        // PopupField(label, choices, T defaultValue, ...) 오버로드는 defaultValue가 null이면 예외를 던진다.
        // 빈 슬롯(null)도 골라야 하므로 인덱스를 넘기는 오버로드를 쓴다 — choices[0]이 null이라 안전하다.
        int defaultIndex = Mathf.Max(0, choices.IndexOf(skillManager.GetSkillInSlot(slotIndex)));

        PopupField<SkillBase> menu = new(SlotLabel(skillManager, slotIndex), choices, defaultIndex, FormatSkill, FormatSkill) {
            focusable = false
        };
        menu.style.position = Position.Absolute;
        menu.style.top = 22 + slotIndex * 20;
        menu.style.right = 0;
        menu.style.minWidth = 170;
        menu.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.55f));

        int capturedSlot = slotIndex; // 람다가 매 반복의 값을 잡도록 로컬로 복사.
        menu.RegisterValueChangedCallback(evt => EquipForTest(skillManager, capturedSlot, evt.newValue));

        gameView.rootVisualElement.Add(menu);
        menu.BringToFront();
        return menu;
    }

    // 슬롯 키는 이제 Input System 액션(Skill1~3)에서 나오므로 에디터가 키 코드를 직접 읽을 수 없다.
    // 기본 바인딩이 숫자 1 · 2 · 3 이라 번호를 그대로 적고, 어떤 액션에 물려 있는지 괄호로 덧붙인다.
    static string SlotLabel(SkillManager skillManager, int slotIndex) {
        string action = skillManager.slotActionNames != null && slotIndex < skillManager.slotActionNames.Length
            ? skillManager.slotActionNames[slotIndex]
            : null;

        return string.IsNullOrEmpty(action) ? $"슬롯{slotIndex + 1}" : $"{slotIndex + 1} ({action})";
    }

    static string FormatSkill(SkillBase skill) {
        return skill == null ? "(비어 있음)" : skill.DisplayName;
    }

    #endregion
    #region 장착

    static void EquipForTest(SkillManager skillManager, int slotIndex, SkillBase skill) {
        if (skill != null) skillManager.UnlockSkill(skill); // 해금 전 스킬도 즉시 테스트할 수 있게 한다.
        skillManager.EquipSkill(slotIndex, skill);
    }

    #endregion
    #region 스킬 목록 조회

    static void RefreshSkillList() {
        allSkills.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:SkillBase")) {
            SkillBase skill = AssetDatabase.LoadAssetAtPath<SkillBase>(AssetDatabase.GUIDToAssetPath(guid));
            if (skill != null) allSkills.Add(skill);
        }
    }

    #endregion
}
#endif
