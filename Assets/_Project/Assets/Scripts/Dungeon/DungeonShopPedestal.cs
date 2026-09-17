using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

// 뒷세계 상점의 진열대 하나. 다가가면 「[F] 이름 · 코인 가격」이 뜨고 F 로 바로 산다.
//
// 상점 창을 따로 열지 않고 진열대마다 상호작용으로 파는 이유: 거울 · 곁가지 문과 같은 방식이라 새로 익힐 조작이
// 없고, 진열된 물건을 보면서 걸어가며 고르는 편이 메뉴를 여닫는 것보다 방 하나짜리 상점의 흐름을 덜 끊는다.
//
// 한 판에 한 번만 판다. 던전은 들어갈 때마다 통째로 새로 만들어지므로 매진 상태를 세이브에 남기지 않아도
// 다음 판에는 다시 진열된다(DungeonBonusPickup 과 같은 이유).
//
// **Is Trigger 콜라이더를 붙이세요.** (PlayerInteractor 가 트리거만 탐지합니다.)
[RequireComponent(typeof(Collider2D))]
public class DungeonShopPedestal : MonoBehaviour, IInteractable {
    #region 인스펙터 변수

    [Header("판매")]
    public DungeonShopItem item; // **진열할 물건 에셋을 연결하세요.** 비우면 빈 진열대라 프롬프트가 뜨지 않습니다.

    [Header("연결")]
    public GameObject itemVisual; // 팔리면 숨길 진열품 그림. 비워도 된다.

    [Header("프롬프트")]
    public Vector3 promptOffset = new(0f, 2.8f, 0f); // 진열품 위에 프롬프트를 띄울 오프셋. 진열대 피벗은 발밑이다.
    public string playerTag = "Player";

    #endregion
    #region 문구

    // 진열대마다 같은 문구라 인스펙터 칸 대신 키로 문다. 칸으로 두면 진열대를 늘릴 때마다 세 칸씩 다시 연결해야 한다.
    // {0} = 물건 이름, {1} = 가격.
    static readonly LocalizedString PriceFormat = new("Ui", "dungeon_shop.price_format");
    static readonly LocalizedString LackingFormat = new("Ui", "dungeon_shop.lacking_format");
    static readonly LocalizedString NotNeededFormat = new("Ui", "dungeon_shop.not_needed_format");

    #endregion
    #region 런타임 변수

    enum LabelState { Buy, Lacking, NotNeeded }

    bool sold;
    GameObject player;
    Player_DungeonCoinInventory coins;

    // PlayerInteractor 가 매 프레임 문구를 읽어 가므로, 상태나 언어가 바뀔 때만 문자열을 새로 만든다.
    string cachedLabel;
    LabelState cachedState;
    Locale cachedLocale;
    bool labelDirty = true;

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (item == null) Debug.LogWarning($"[DungeonShopPedestal] '{name}' — item 이 비어 있어 아무것도 팔지 않습니다.", this);
    }

    #endregion
    #region IInteractable

    public string InteractLabel {
        get {
            LabelState state = CurrentState();
            Locale locale = LocalizationSettings.HasSettings ? LocalizationSettings.SelectedLocale : null;

            if (labelDirty || state != cachedState || locale != cachedLocale) {
                cachedLabel = BuildLabel(state);
                cachedState = state;
                cachedLocale = locale;
                labelDirty = false;
            }
            return cachedLabel;
        }
    }

    // 코인이 모자라도 프롬프트는 띄운다. 숨겨 버리면 왜 못 사는지, 얼마가 필요한지를 알 길이 없다.
    public bool CanInteract => !sold && item != null;

    public Vector3 PromptAnchor => transform.position + promptOffset;

    public void Interact(GameObject interactor) {
        if (!CanInteract || interactor == null) return;

        // PlayerInteractor 가 플레이어 루트가 아닌 자식에 붙어 있어도 되도록 루트를 찾아 쓴다(DungeonGate 와 같은 이유).
        Health health = interactor.GetComponentInParent<Health>();
        Bind(health != null ? health.gameObject : interactor);

        if (coins == null) {
            Debug.LogWarning($"[DungeonShopPedestal] 플레이어에 Player_DungeonCoinInventory 가 없어 '{item.name}' 을(를) 팔 수 없습니다.", this);
            return;
        }

        // 두 경우 모두 프롬프트에 이유가 이미 떠 있으므로 따로 알리지 않는다.
        if (!item.IsUseful(player)) return;
        if (!coins.Spend(item.price)) return;

        item.Apply(player);
        sold = true;
        if (itemVisual != null) itemVisual.SetActive(false);

        AreaTitleView.Announce(LocalizationText.Resolve(item.displayName, item.name), LocalizationText.Resolve(item.description));
    }

    #endregion
    #region 문구 만들기

    LabelState CurrentState() {
        if (player == null && !string.IsNullOrEmpty(playerTag)) {
            GameObject found = GameObject.FindGameObjectWithTag(playerTag);
            if (found != null) Bind(found);
        }

        if (player != null && !item.IsUseful(player)) return LabelState.NotNeeded;
        if (coins == null || coins.Count < item.price) return LabelState.Lacking;
        return LabelState.Buy;
    }

    string BuildLabel(LabelState state) {
        string itemName = LocalizationText.Resolve(item.displayName, item.name);

        // 테이블 연결이 끊겨도 빈 프롬프트가 뜨지 않도록 원문을 대체값으로 들고 있다.
        string format = state switch {
            LabelState.Lacking => LocalizationText.Resolve(LackingFormat, "{0} · 코인 {1} (부족)"),
            LabelState.NotNeeded => LocalizationText.Resolve(NotNeededFormat, "{0} · 지금은 필요 없음"),
            _ => LocalizationText.Resolve(PriceFormat, "{0} · 코인 {1}"),
        };

        // 번역문에서 자리표시자가 깨져 있어도 게임이 멈추지 않게 한다. 이름과 가격만이라도 보이는 편이 낫다.
        try {
            return string.Format(format, itemName, item.price);
        }
        catch (FormatException) {
            return $"{itemName} · {item.price}";
        }
    }

    void Bind(GameObject target) {
        if (target == player) return;

        player = target;
        coins = player != null ? player.GetComponentInChildren<Player_DungeonCoinInventory>() : null;
        labelDirty = true;
    }

    #endregion
    #region 에디터 표시

#if UNITY_EDITOR
    void OnDrawGizmos() {
        Gizmos.color = new Color(0.6f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireSphere(PromptAnchor, 0.2f);
    }
#endif

    #endregion
}
