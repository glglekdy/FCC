using UnityEngine;

// 스토리에서 스킬을 되찾게 하는 창구. 씬 쪽 장치(SkillUnlockZone · SkillUnlockStep · DialogueTriggerZone.unlockSkill)가
// 전부 여기를 거친다.
//
// 장치마다 플레이어의 SkillManager 를 인스펙터로 물리게 하면, 씬마다 플레이어가 새로 놓이는 구조라 연결이 자꾸 끊긴다.
// 그래서 호출 시점에 플레이어를 찾아 넘긴다. 해금 자체(알림 · 자동 장착 · 중복 방지)는 SkillManager.UnlockFromStory 가 맡는다.
public static class SkillUnlocker {
    #region 해금

    // 되찾았으면 true. 이미 가진 스킬이거나 플레이어를 못 찾으면 false.
    public static bool Unlock(SkillBase skill) {
        if (skill == null) return false;

        SkillManager manager = FindPlayerSkills();
        if (manager == null) {
            Debug.LogWarning($"[SkillUnlocker] 씬에서 SkillManager 를 찾지 못해 '{skill.DisplayName}' 을 해금하지 못했습니다. " +
                "플레이어 프리팹에 SkillManager 가 붙어 있어야 합니다.");
            return false;
        }

        return manager.UnlockFromStory(skill);
    }

    // 플레이어는 씬마다 새로 놓이므로 매번 찾는다. Player 태그를 먼저 보고, 없으면 씬 전체에서 찾는다.
    static SkillManager FindPlayerSkills() {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) {
            SkillManager onPlayer = player.GetComponentInChildren<SkillManager>();
            if (onPlayer != null) return onPlayer;
        }

        return Object.FindAnyObjectByType<SkillManager>();
    }

    #endregion
}
