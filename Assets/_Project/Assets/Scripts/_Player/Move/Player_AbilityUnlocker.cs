using UnityEngine;

// 스토리에서 이동 패시브를 배우게 하는 창구. 씬 쪽 장치(SkillUnlockZone · SkillUnlockStep ·
// DialogueTriggerZone · NpcDialogue)가 전부 여기를 거친다. 스킬의 SkillUnlocker 와 같은 구조다.
//
// 장치마다 플레이어를 인스펙터로 물리게 하면 씬마다 플레이어가 새로 놓이는 구조라 연결이 자꾸 끊기므로,
// 호출 시점에 플레이어를 찾아 넘긴다. 해금 자체(알림 · 중복 방지)는 Player_move.UnlockAbility 가 맡는다.
public static class Player_AbilityUnlocker {
    #region 해금

    // 배웠으면 true. None 이거나 이미 가진 기술이거나 플레이어를 못 찾으면 false.
    public static bool Unlock(Player_Ability ability) {
        if (ability == Player_Ability.None) return false;

        Player_move move = FindPlayerMove();
        if (move == null) {
            Debug.LogWarning($"[Player_AbilityUnlocker] 씬에서 Player_move 를 찾지 못해 '{ability}' 을 배우지 못했습니다.");
            return false;
        }

        return move.UnlockAbility(ability);
    }

    // 플레이어는 씬마다 새로 놓이므로 매번 찾는다. Player 태그를 먼저 보고, 없으면 씬 전체에서 찾는다.
    static Player_move FindPlayerMove() {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) {
            Player_move onPlayer = player.GetComponentInChildren<Player_move>();
            if (onPlayer != null) return onPlayer;
        }

        return Object.FindAnyObjectByType<Player_move>();
    }

    #endregion
}
