using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// 타격 시 공통으로 재생되는 피드백(히트스톱 + 카메라 쉐이크)을 담당하는 싱글턴.
// HitReactor가 피격/사망 이벤트를 받을 때마다 PlayHit()을 호출한다.
public class HitFeedback : MonoBehaviour {
    public static HitFeedback Instance;

    #region 인스펙터 변수

    [Header("히트스톱")]
    public float minHitStopDuration = 0.02f; // 약한 타격의 정지 시간 (Realtime 기준, timeScale 영향 없음).
    public float maxHitStopDuration = 0.05f; // referenceDamage 이상일 때의 정지 시간.
    public float killHitStopDuration = 0.14f; // 처치했을 때 화면을 확 붙잡는 시간.
    public int referenceDamage = 50; // 이 피해량에서 maxHitStopDuration에 도달한다.
    [Range(0f, 1f)]
    public float hitStopTimeScale = 0.02f; // 정지 중 적용할 타임스케일.

    [Header("카메라 쉐이크")]
    public float shakeForce = 0.35f; // 일반 타격 시 CinemachineImpulseSource에 전달할 힘.
    public float killShakeForce = 0.7f; // 처치 시의 힘.

    [Header("쉐이크 감쇠")]
    public float shakeDuration = 0.55f; // 흔들림이 완전히 멎을 때까지의 시간. 길수록 천천히 풀린다.
    public float shakeOscillations = 1.5f; // 잦아드는 동안의 왕복 횟수. 낮을수록 흔들림보다 "밀렸다 돌아오는" 느낌이 된다.
    public float shakeDamping = 4.5f; // 감쇠 계수. 클수록 초반에 빨리 죽고 여운이 짧아진다.

    #endregion
    #region 컴포넌트 변수

    CinemachineImpulseSource impulseSource;
    Coroutine hitStopRoutine;
    float cachedTimeScale = 1f; // 히트스톱 시작 전의 timeScale. GameSpeedController 등 외부에서 바꾼 배속을 그대로 복원하기 위해 캐싱.

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (Instance == null) {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else {
            Destroy(gameObject);
            return;
        }

        impulseSource = GetComponent<CinemachineImpulseSource>();
        ApplyShakeProfile();
    }

    // 플레이 중 인스펙터로 값을 만지면 바로 반영되게. 쉐이크 감각은 실제로 때려보면서 맞춰야 한다.
    void OnValidate() {
        if (Application.isPlaying) ApplyShakeProfile();
    }

    #endregion
    #region 피드백 재생

    // hitPoint: 타격이 발생한 월드 좌표. hitDir: 공격자 → 피격자 방향(정규화).
    public void PlayHit(Vector2 hitPoint, Vector2 hitDir, int damage, bool isLethal) {
        TriggerHitStop(GetHitStopDuration(damage, isLethal));
        TriggerShake(hitPoint, hitDir, isLethal ? killShakeForce : shakeForce);
    }

    float GetHitStopDuration(int damage, bool isLethal) {
        if (isLethal) return killHitStopDuration;

        float t = referenceDamage > 0 ? Mathf.Clamp01((float)damage / referenceDamage) : 1f;
        return Mathf.Lerp(minHitStopDuration, maxHitStopDuration, t);
    }

    void TriggerHitStop(float duration) {
        if (hitStopRoutine == null) {
            cachedTimeScale = Time.timeScale; // 이미 정지 중이 아닐 때만 원래 배속을 새로 캐싱.
        }
        else {
            StopCoroutine(hitStopRoutine);
        }

        hitStopRoutine = StartCoroutine(HitStopRoutine(duration));
    }

    IEnumerator HitStopRoutine(float duration) {
        Time.timeScale = hitStopTimeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = cachedTimeScale;
        hitStopRoutine = null;
    }

    // 타격 방향으로 화면이 밀리도록 방향성 임펄스를 발생시킨다. 무지향 흔들림보다 타격이 훨씬 시원하게 읽힌다.
    void TriggerShake(Vector2 hitPoint, Vector2 hitDir, float force) {
        if (impulseSource == null) return;

        force *= ShakeScale;
        if (force <= 0f) return; // 흔들림을 0으로 꺼둔 사람에게는 임펄스 자체를 만들지 않는다.

        impulseSource.GenerateImpulseAtPositionWithVelocity(hitPoint, hitDir * force);
    }

    // 설정의 「화면 흔들림」이 곱하는 배율(0~1). 인스펙터의 shakeForce 를 직접 덮어쓰지 않는 이유는,
    // 맞춰둔 타격 감각을 설정이 지워버리면 다시 되돌릴 수가 없기 때문이다.
    // GameSettings 가 적용 시점에 넣어준다.
    public static float ShakeScale = 1f;

    #endregion
    #region 쉐이크 프로파일

    // 인스펙터의 ImpulseSource 설정(모양·길이)을 여기서 덮어쓴다.
    // 기본 제공 Bump 파형은 0.2초 안에 확 밀렸다 반대로 튕겨서 타격마다 화면이 툭툭 끊겨 보였다.
    // 대신 감쇠 진동 곡선을 직접 만들어 넣어서, 밀린 화면이 점점 작게 흔들리며 0으로 수렴하게 한다.
    // 쉐이크 감각을 HitFeedback 한 곳에서만 만지도록 모아둔 것이기도 하다.
    void ApplyShakeProfile() {
        if (impulseSource == null) return;

        var definition = impulseSource.ImpulseDefinition;
        definition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Custom;
        definition.CustomImpulseShape = BuildFalloffCurve();
        definition.ImpulseDuration = Mathf.Max(0.01f, shakeDuration); // 0이면 임펄스가 아예 생성되지 않는다.
    }

    // cos(왕복) × 지수 감쇠 를 샘플링한 곡선. x는 0~1(정규화된 경과 시간), y는 타격 방향 오프셋에 곱해질 배율이다.
    AnimationCurve BuildFalloffCurve() {
        const int sampleCount = 33; // 감쇠 진동이 각져 보이지 않을 정도의 해상도. 프로파일 갱신 때 한 번만 돌기에 이 정도면 충분하다.

        var keys = new Keyframe[sampleCount];
        for (int i = 0; i < sampleCount; i++) {
            float t = (float)i / (sampleCount - 1);
            float value = Mathf.Cos(t * shakeOscillations * 2f * Mathf.PI) * Mathf.Exp(-shakeDamping * t);
            keys[i] = new Keyframe(t, value);
        }
        keys[sampleCount - 1].value = 0f; // 끝값이 정확히 0이 아니면 임펄스가 끝나는 순간 화면이 툭 제자리로 튄다.

        var curve = new AnimationCurve(keys);
        for (int i = 0; i < sampleCount; i++) {
            curve.SmoothTangents(i, 0f); // 기본 탄젠트는 계단처럼 꺾여서, 샘플 사이를 부드럽게 이어준다.
        }
        return curve;
    }

    #endregion
}
