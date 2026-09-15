using System.Collections;
using UnityEngine;

// 기억 조각으로 만들어진 거울. 기획서상 세이브포인트이자 스킬 "정비하기" 지점이다.
// 저장 + 자아 게이지 회복을 마친 뒤 SkillLoadoutView(스킬 정비 화면)를 띄운다.
//
// **콜라이더(Is Trigger 체크)를 함께 붙여야 PlayerInteractor가 탐지합니다.**
public class SaveMirror : MonoBehaviour, IInteractable {
    #region 인스펙터 변수

    [Header("식별")]
    // 세이브 파일에 기록되는 거울 고유 id. 불러오기 시 어느 거울에서 재개할지 판별한다.
    // **씬 안에서 겹치지 않게 지으세요.** 비워두면 오브젝트 이름을 대신 쓴다.
    public string mirrorId;

    [Header("프롬프트")]
    public string label = "정비하기"; // 플레이어에게 뜨는 문구.
    public Vector3 promptOffset = new(0f, 1.6f, 0f); // 거울 위쪽에 프롬프트를 띄울 오프셋.

    [Header("복귀 지점")]
    // 불러왔을 때 플레이어가 설 위치. 비워두면 거울 자신의 위치를 쓴다.
    // **거울이 벽에 붙어 있으면 앞쪽 바닥에 빈 오브젝트를 만들어 지정하세요.**
    public Transform respawnPoint;

    [Header("저장 시 처리")]
    public bool healOnSave = true; // 저장할 때 자아 게이지를 가득 채운다.
    public string objectiveId; // 비어있지 않으면 저장 시 해당 목표를 완료 처리 (예: "첫 거울 찾기").
    // 저장이 끝나면 스킬 정비 화면을 띄운다. **씬에 SkillLoadoutView가 없으면 그냥 넘어갑니다.**
    public bool openLoadoutOnSave = true;

    [Header("연출")]
    public SpriteRenderer glowTarget; // 저장 순간 번쩍일 스프라이트. 비워두면 자기 SpriteRenderer를 쓴다.
    public Color glowColor = Color.white;
    public float glowDuration = 0.45f; // 이 시간 동안은 재저장이 막힌다 (연타로 중복 저장되는 것 방지).
    public AudioClip saveSound;
    [Range(0f, 1f)]
    public float saveVolume = 0.8f;

    #endregion
    #region 컴포넌트 변수

    Color baseColor;
    Coroutine glowRoutine;
    bool isSaving; // 연출이 도는 동안 상호작용을 잠그는 가드.

    #endregion
    #region IInteractable

    public string InteractLabel => label;
    public bool CanInteract => !isSaving;
    public Vector3 PromptAnchor => transform.position + promptOffset;

    public void Interact(GameObject interactor) {
        if (isSaving) return;
        Save(interactor);
    }

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (glowTarget == null) glowTarget = GetComponent<SpriteRenderer>();
        if (glowTarget != null) baseColor = glowTarget.color;

        // id를 깜빡해도 최소한 오브젝트 이름으로는 구분되게 한다.
        if (string.IsNullOrEmpty(mirrorId)) mirrorId = name;
    }

    #endregion
    #region 저장

    void Save(GameObject interactor) {
        if (SaveManager.Instance == null) {
            Debug.LogError($"[SaveMirror] '{name}' — 씬에 SaveManager가 없어 저장할 수 없습니다.", this);
            return;
        }

        // 회복을 저장보다 먼저 해야 세이브 파일에 가득 찬 체력이 기록된다.
        // 안 그러면 불러왔을 때 저장 직전의 깎인 체력으로 되돌아온다.
        // PlayerInteractor가 플레이어 루트가 아닌 자식에 붙어 있어도 찾아지도록 GetComponentInParent를 쓴다.
        if (healOnSave && interactor != null) {
            Health health = interactor.GetComponentInParent<Health>();
            if (health != null) health.RestoreFull();
        }

        Vector2 respawnPosition = respawnPoint != null ? respawnPoint.position : transform.position;
        SaveData data = SaveManager.Instance.SaveGame(mirrorId, respawnPosition);
        if (data == null) return;

        if (!string.IsNullOrEmpty(objectiveId) && ObjectiveManager.Instance != null) {
            ObjectiveManager.Instance.CompleteObjective(objectiveId);
        }

        PlayFeedback();
        OpenLoadout(interactor);
    }

    // 정비 화면. 저장·회복·목표 처리가 전부 끝난 뒤에 연다 — 화면이 뜨면 timeScale이 0이 되므로
    // 그 앞에서 열면 뒤쪽 처리가 다음 프레임까지 밀린다.
    void OpenLoadout(GameObject interactor) {
        if (!openLoadoutOnSave || interactor == null) return;

        // 파괴된 뒤에도 C# 참조가 남을 수 있어 ?. 대신 != null 로 Unity의 == 오버로드를 탄다.
        if (SkillLoadoutView.Instance == null) return; // 아직 정비 화면을 씬에 올리지 않았으면 저장만 하고 끝낸다.

        // PlayerInteractor가 플레이어 루트가 아닌 자식에 붙어 있어도 찾아지도록 GetComponentInParent를 쓴다.
        SkillLoadoutView.Instance.Open(interactor.GetComponentInParent<SkillManager>(), SaveAfterLoadout);
    }

    // 정비 창은 저장이 끝난 뒤에 열리므로, 창에서 바꾼 장착 · 강화는 방금 저장에 들어가지 않았다.
    // 닫을 때 같은 거울 · 같은 복귀 지점으로 한 번 더 기록해 두면, 다음 거울까지 가기 전에 게임을 꺼도 정비가 남는다.
    // 회복 · 목표 · 연출은 이미 처리했으므로 저장만 다시 한다.
    void SaveAfterLoadout() {
        if (SaveManager.Instance == null) return;

        Vector2 respawnPosition = respawnPoint != null ? respawnPoint.position : transform.position;
        SaveManager.Instance.SaveGame(mirrorId, respawnPosition);
    }

    #endregion
    #region 연출

    void PlayFeedback() {
        if (saveSound != null) AudioSource.PlayClipAtPoint(saveSound, transform.position, saveVolume);

        if (glowRoutine != null) StopCoroutine(glowRoutine);
        glowRoutine = StartCoroutine(GlowRoutine());
    }

    // 확 밝아졌다가 원래 색으로 돌아온다. glowTarget이 없어도 상호작용 잠금 타이머 역할은 하도록 그대로 돈다.
    IEnumerator GlowRoutine() {
        isSaving = true;

        float elapsed = 0f;
        while (elapsed < glowDuration) {
            // 히트스톱이나 일시정지 배속에 끌려가지 않도록 unscaledDeltaTime 기준 (DamagePopup과 같은 이유).
            elapsed += Time.unscaledDeltaTime;
            if (glowTarget != null) glowTarget.color = Color.Lerp(glowColor, baseColor, Mathf.Clamp01(elapsed / glowDuration));
            yield return null;
        }

        if (glowTarget != null) glowTarget.color = baseColor;
        isSaving = false;
        glowRoutine = null;
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        Vector3 respawnPosition = respawnPoint != null ? respawnPoint.position : transform.position;

        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.9f);
        Gizmos.DrawWireSphere(respawnPosition, 0.3f);
        Gizmos.DrawLine(transform.position, respawnPosition); // 거울과 복귀 지점이 얼마나 떨어져 있는지 한눈에 보이도록.
    }
#endif

    #endregion
}
