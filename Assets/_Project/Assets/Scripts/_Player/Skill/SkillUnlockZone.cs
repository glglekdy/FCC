using UnityEngine;

// 플레이어가 들어오면 스킬(또는 이동 패시브)을 되찾게 하는 영역. 대사나 컷씬 없이 "이 자리에 닿으면 해금" 을 걸 때 쓴다.
// 대사 끝에 붙이려면 DialogueTriggerZone.unlockSkill, 컷씬 중간이면 SkillUnlockStep 을 쓴다.
//
// **Is Trigger 콜라이더(2D)를 같은 오브젝트에 붙이세요.**
// 이미 가진 스킬이면 아무 일도 없으므로, 불러오기 뒤 같은 자리를 다시 지나가도 알림이 반복되지 않는다.
public class SkillUnlockZone : MonoBehaviour {
    #region 인스펙터 변수

    [Header("해금")]
    public SkillBase skill; // 되찾게 할 스킬 에셋. (Assets/_Project/Assets/Skill)
    public Player_Ability ability; // 함께 배우게 할 이동 패시브. **skill · ability 중 하나 이상은 채우세요.**
    public string playerTag = "Player";
    public bool disableAfterUnlock = true; // 해금한 뒤 이 영역을 끈다. 같은 판에서 다시 밟을 일이 없게.

    #endregion
    #region 유니티 라이프 사이클

    void OnTriggerEnter2D(Collider2D other) {
        if ((skill == null && ability == Player_Ability.None) || !other.CompareTag(playerTag)) return;

        if (skill != null) SkillUnlocker.Unlock(skill);
        if (ability != Player_Ability.None) Player_AbilityUnlocker.Unlock(ability);
        if (disableAfterUnlock) gameObject.SetActive(false);
    }

    #endregion
    #region 에디터 표시

    // 씬 뷰에서 어디에 해금이 걸려 있는지 보이게 한다. 레벨을 짤 때 영역을 잃어버리기 쉽다.
    void OnDrawGizmos() {
        Collider2D area = GetComponent<Collider2D>();
        if (area == null) return;

        Gizmos.color = new Color(0.66f, 0.19f, 0.19f, 0.35f);
        Gizmos.DrawCube(area.bounds.center, area.bounds.size);
    }

    #endregion
}
