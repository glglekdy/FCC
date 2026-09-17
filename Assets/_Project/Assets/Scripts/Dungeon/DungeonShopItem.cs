using UnityEngine;
using UnityEngine.Localization;

// 뒷세계 상점에서 파는 물건 하나. 진열대(DungeonShopPedestal)가 이 에셋을 물고 이름 · 가격을 띄우고,
// 사면 Apply 로 효과를 건다. 값은 전부 뒷세계 코인(Player_DungeonCoinInventory)으로 치른다.
//
// 효과를 종류별 파생 에셋으로 나누지 않고 열거형 하나로 둔 이유: 지금 파는 것은 회복 두 종과 한정 강화 두 종뿐이고
// 전부 "플레이어의 값 하나를 바꾼다"로 끝난다. 종류가 늘어 각자 다른 흐름이 필요해지면 그때 스킬 에셋처럼 파생으로 나눈다.
//
// **한정 강화는 뒷세계를 나가는 순간 전부 걷힌다**(DungeonGate 가 ClearRunBuffs 를 부른다). 코인과 달리
// 한 판 안에서만 의미가 있는 보상이라, 남겨 두면 본편 전투 밸런스가 뒷세계를 몇 번 돌았는지에 따라 갈린다.
[CreateAssetMenu(menuName = "FCC/Dungeon/Shop Item", fileName = "ShopItem_")]
public class DungeonShopItem : ScriptableObject {
    public enum Effect {
        [InspectorName("자아 게이지 회복")] Heal,
        [InspectorName("자아 게이지 전량 회복")] HealFull,
        [InspectorName("기본 공격 피해 증가 (뒷세계 한정)")] AttackUp,
        [InspectorName("받는 피해 감소 (뒷세계 한정)")] DefenseUp,
    }

    #region 인스펙터 변수

    [Header("표시")]
    public LocalizedString displayName = new(); // 진열대 프롬프트와 구매 알림 제목에 뜨는 이름.
    public LocalizedString description = new(); // 구매 알림 아래 줄. 비워 두면 그 줄이 꺼진다.

    [Header("가격")]
    [Min(0)] public int price = 3; // 뒷세계 코인.

    [Header("효과")]
    public Effect effect;
    // 회복: 채울 자아 게이지 양 / 공격 증가: 더할 비율(0.3 = +30%) / 피해 감소: 뺄 비율(0.25 = -25%). 전량 회복은 쓰지 않는다.
    public float amount = 30f;

    #endregion
    #region 상수

    // 받는 피해 배율의 하한. 감소를 여러 번 겹쳐 사도 거의 맞지 않는 상태까지는 내려가지 않게 막는다.
    const float MinDamageTakenMultiplier = 0.4f;

    #endregion
    #region 효과

    // 지금 사도 의미가 있는지. 자아 게이지가 가득한데 회복을 사서 코인만 날리는 일을 막는다.
    public bool IsUseful(GameObject player) {
        if (player == null) return false;

        switch (effect) {
            case Effect.Heal:
            case Effect.HealFull:
                Health health = player.GetComponentInChildren<Health>();
                return health != null && health.CurrentHealth < health.MaxHealth;
            default:
                return true;
        }
    }

    public void Apply(GameObject player) {
        if (player == null) return;

        switch (effect) {
            case Effect.Heal: {
                // 데미지가 아닌 경로로 체력을 바꾸므로 SetHealth 를 쓴다. 최대치는 SetHealth 가 잘라 준다.
                Health health = player.GetComponentInChildren<Health>();
                if (health != null) health.SetHealth(health.CurrentHealth + Mathf.RoundToInt(amount));
                break;
            }
            case Effect.HealFull: {
                Health health = player.GetComponentInChildren<Health>();
                if (health != null) health.RestoreFull();
                break;
            }
            case Effect.AttackUp: {
                // 곱하지 않고 더한다. +30% 를 두 번 사면 +60% 여야 설명 문구와 맞는다.
                Player_Combat combat = player.GetComponentInChildren<Player_Combat>();
                if (combat != null) combat.DamageMultiplier += amount;
                break;
            }
            case Effect.DefenseUp: {
                Health health = player.GetComponentInChildren<Health>();
                if (health != null) health.DamageTakenMultiplier = Mathf.Max(MinDamageTakenMultiplier, health.DamageTakenMultiplier - amount);
                break;
            }
        }
    }

    // 뒷세계 한정 강화를 전부 걷는다. 들어갈 때와 나올 때 둘 다 부른다 — 나오는 길이 사망이든 걸어 나가기든,
    // 다음 판에 지난 판의 강화가 남아 있으면 안 되기 때문이다.
    public static void ClearRunBuffs(GameObject player) {
        if (player == null) return;

        Player_Combat combat = player.GetComponentInChildren<Player_Combat>();
        if (combat != null) combat.DamageMultiplier = 1f;

        Health health = player.GetComponentInChildren<Health>();
        if (health != null) health.DamageTakenMultiplier = 1f;
    }

    #endregion
}
