using System;
using UnityEngine;

public class Health : MonoBehaviour
{
    #region 인스펙터 변수

    [Header("체력")]
    public int maxHealth = 100;

    [Header("피격 무적")]
    // 피격 직후 무적 시간. **몬스터는 0, 플레이어는 0.9 정도로 설정하세요.**
    // 몬스터에 무적을 주면 공격 쿨타임보다 길어져 때려도 반응이 없는 것처럼 보인다.
    public float invincibleTime = 0f;

    [Header("처형")]
    // 체력 비율로 즉사시키는 기술(곡예사의 Close Call)을 받지 않는다. **보스·정예 몬스터는 켜세요.**
    // 구속과 피해는 그대로 받는다 — 막는 것은 "남은 체력과 무관하게 한 번에 끝내는 것"뿐이다.
    public bool executionImmune = false;

    #endregion
    #region 이벤트

    public event Action<int, Vector2> OnDamaged; // 데미지가 실제로 적용된 순간 (피해량, 공격 원점) 전달. HitReactor/HitFlash/HealthBar가 구독.
    public event Action<Vector2> OnDeath; // 사망한 순간 (마지막 공격 원점) 전달. HitReactor가 구독해 사망 연출을 재생.

    // 무적이 걸리고 풀리는 순간 (현재 무적 여부) 전달. InvincibleBlink가 구독해 깜빡임을 켜고 끈다.
    // 매 프레임 IsInvincible을 폴링하지 않고 이벤트로 알리는 이유는, 연출 쪽이 무적 시작·종료
    // 시점을 정확히 알아야 깜빡이다가 어중간한 상태로 굳는 일이 없기 때문이다.
    public event Action<bool> OnInvincibleChanged;

    // 사망 직전에 물어보는 옵트인 훅. 던전처럼 "죽어도 게임오버가 아닌" 구역이 사망을 가로채기 위한 것.
    // true를 반환하면 Die()를 건너뛴다 (체력 복구는 가로챈 쪽이 SetHealth로 직접 책임진다).
    // 기본값 null이라 평소에는 기존 동작(Die → Destroy)이 그대로 유지된다. 하나만 등록된다.
    public Func<Vector2, bool> DeathInterceptor;

    #endregion
    #region 컴포넌트 변수

    public int currentHealth = 100;

    bool isInvincible;
    float invincibleTimer;
    bool isDead; // 같은 프레임에 여러 타격이 들어와도 Die()가 두 번 돌지 않게 막는 가드.

    public int MaxHealth => maxHealth;
    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;

    // 지금 무적인지. 피격 무적뿐 아니라 낙사 복귀 무적·연출용 무적까지 전부 이 하나로 모으므로,
    // 무적을 봐야 하는 쪽(연출·AI·기믹)은 어디서 걸린 무적인지 신경 쓰지 않고 이 값만 읽으면 된다.
    public bool IsInvincible => isInvincible;

    #endregion
    #region 유니티 라이프 사이클

    void Start() {
        currentHealth = maxHealth;
    }

    void Update() {
        if (!isInvincible) return;

        invincibleTimer -= Time.deltaTime;
        if (invincibleTimer <= 0f) {
            isInvincible = false;
            OnInvincibleChanged?.Invoke(false);
        }
    }

    #endregion
    #region 데미지 처리

    public void TakeDamage(int damage, Vector2 sourcePosition) {
        if (isDead || isInvincible) return;

        currentHealth = Mathf.Max(0, currentHealth - damage);

        // 치명타여도 항상 발행한다. 마지막 일격에서도 스파크/데미지 숫자/플래시가 나와야 하기 때문.
        // 구독자는 CurrentHealth <= 0 으로 처치 여부를 판단한다.
        OnDamaged?.Invoke(damage, sourcePosition);

        if (currentHealth <= 0) {
            // 던전 등에서 사망을 가로챌 기회. 훅이 true를 반환하면 Die()를 건너뛴다. 평소엔 null이라 그대로 진행된다.
            if (DeathInterceptor != null && DeathInterceptor(sourcePosition)) return;

            Die(sourcePosition);
            return;
        }

        // invincibleTime이 0이면 무적을 아예 걸지 않는다. 한 프레임짜리 무적도 남기지 않아 모든 타격이 확실히 들어간다.
        SetInvincible(invincibleTime);
    }

    void Die(Vector2 sourcePosition) {
        isDead = true;
        OnDeath?.Invoke(sourcePosition); // 오브젝트가 파괴되기 전에 사망 연출을 띄울 기회를 준다.
        Destroy(gameObject);
    }

    #endregion
    #region 무적

    // 지정한 시간만큼 무적을 건다. 낙사 복귀처럼 피격 무적(invincibleTime)과 길이가 다른 무적을
    // 밖에서 걸 때 쓴다. 이미 무적이면 남은 시간과 비교해 긴 쪽을 남긴다 — 0.9초짜리 피격 무적이
    // 3초짜리 낙사 무적을 덮어써서 줄여 버리면 안 되기 때문이다.
    public void SetInvincible(float duration) {
        if (isDead || duration <= 0f) return;

        invincibleTimer = Mathf.Max(invincibleTimer, duration);
        if (isInvincible) return;

        isInvincible = true;
        OnInvincibleChanged?.Invoke(true);
    }

    // 무적을 즉시 푼다. 연출이 끝나기 전에 무적을 걷어야 하는 경우를 위해 둔다.
    public void ClearInvincible() {
        if (!isInvincible) return;

        isInvincible = false;
        invincibleTimer = 0f;
        OnInvincibleChanged?.Invoke(false);
    }

    #endregion
    #region 체력 회복

    // 데미지가 아닌 경로(세이브 복원, 거울에서의 회복, 기억 조각 섭취)로 체력을 바꿀 때 쓴다.
    // 0을 넣어도 Die()는 돌지 않는다 — 사망은 TakeDamage를 통해서만 일어나야 연출이 한 번만 재생된다.
    // HealthBar는 매 프레임 CurrentHealth를 읽어 그리므로 별도 이벤트 없이도 표시가 따라온다.
    public void SetHealth(int value) {
        if (isDead) return;
        currentHealth = Mathf.Clamp(value, 0, maxHealth);
    }

    // 거울(체크포인트)에서 자아 게이지를 가득 채울 때 사용.
    public void RestoreFull() {
        SetHealth(maxHealth);
    }

    #endregion
}
