using System.Collections;
using UnityEngine;

// 컷씬 도중 스킬(또는 이동 패시브)을 되찾게 하는 단계. 단원을 만나거나 이긴 장면에서 "기억이 돌아온다" 는 순간에 둔다.
// 해금 알림(AreaTitleView)은 화면 위에 겹쳐 뜨므로 컷씬을 붙잡아 두지 않는다. 알림을 보여준 뒤 넘어가고 싶으면 waitAfter 를 준다.
//
// **CutSceneManager 의 자식 오브젝트에 붙이세요.** (다른 CutSceneStep 과 같은 방식)
public class SkillUnlockStep : CutSceneStep {
    #region 인스펙터 변수

    [Header("해금")]
    public SkillBase skill; // 되찾게 할 스킬 에셋.
    public Player_Ability ability; // 함께 배우게 할 이동 패시브. **skill · ability 중 하나 이상은 채우세요.**
    public float waitAfter = 0f; // 해금 뒤 다음 단계로 넘어가기까지 기다릴 시간(초). 알림을 보여주고 싶을 때만.

    #endregion
    #region 실행

    // 컷씬은 일시정지 중에도 흘러야 할 수 있어 실제 시간 기준으로 기다린다 (ScreenFader 연출과 같은 이유).
    public override IEnumerator Execute() {
        if (skill != null) SkillUnlocker.Unlock(skill);
        if (ability != Player_Ability.None) Player_AbilityUnlocker.Unlock(ability);
        if (waitAfter > 0f) yield return new WaitForSecondsRealtime(waitAfter);
    }

    #endregion
}
