using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 화면 왼쪽 위 HUD 의 스킬 한 칸. PlayerHudView 가 슬롯 번호 순서대로 3칸을 들고 매 프레임 갱신한다.
//
// 스킬 아이콘 아트가 아직 없어서 이름의 머리글자(Broken Phantasm → BP)를 크게 적는다.
// 스킬 에셋에 icon 을 넣으면 그 칸은 아이콘이 대신 보인다.
//
// 쿨타임은 칸 위를 어두운 막으로 덮고, 시간이 지날수록 막이 위로 걷히게 그린다. 남은 초도 가운데에 적는다.
// 전부 Time.time 기준이다 — 스킬 쿨타임 자체가 게임 시간으로 흐르기 때문이다(히트스톱 · 정비 중에는 멈춘다).
//
// **Prefabs/UI/PlayerHud.prefab 의 SkillBar/SkillSlot0~2 에 붙어 있습니다.**
public class HudSkillSlotView : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public TMP_Text monogramLabel; // 머리글자. 빈 슬롯이면 emptyText.
    public Image cooldownFill; // Filled · Vertical · 위쪽 기준. 남은 쿨타임 비율만큼 칸을 덮는다.
    public TMP_Text cooldownLabel; // 남은 초. 준비되면 꺼진다.
    public Image iconImage; // 스킬 아이콘. 비워도 된다.

    [Header("색상")]
    public Color monogramColor = UiTheme.TextHigh;
    public Color emptyColor = UiTheme.TextDim;

    [Header("문구")]
    public string emptyText = "-"; // 빈 슬롯.
    public string cooldownFormat = "{0:0.0}"; // {0} = 남은 초. 1초 아래로 내려가도 숫자가 튀지 않게 소수 한 자리로 고정한다.

    #endregion
    #region 런타임 변수

    SkillBase shown; // 지금 칸에 그려진 스킬. 같은 스킬이면 머리글자를 다시 만들지 않는다.
    bool hasShown; // 빈 슬롯(null)도 한 번은 그려야 해서 따로 둔다.
    bool usesMonogram; // 아이콘이 없어 머리글자로 그리는 칸인지. 쿨타임 숫자와 자리를 다투므로 따로 기억해 둔다.

    #endregion
    #region 표시 갱신

    // 슬롯에 끼워진 스킬이 바뀌었을 때만 이름 · 아이콘을 다시 그린다. 쿨타임은 Tick 이 매 프레임 맡는다.
    public void Set(SkillBase skill) {
        if (hasShown && skill == shown) return;
        shown = skill;
        hasShown = true;

        bool hasIcon = skill != null && skill.icon != null;
        if (iconImage != null) {
            iconImage.sprite = hasIcon ? skill.icon : null;
            iconImage.enabled = hasIcon;
        }

        usesMonogram = !hasIcon;
        if (monogramLabel != null) {
            monogramLabel.gameObject.SetActive(usesMonogram);
            monogramLabel.text = skill != null ? Monogram(skill.DisplayName) : emptyText;
            monogramLabel.color = skill != null ? monogramColor : emptyColor;
        }

        if (skill == null) ShowCooldown(0f, 0f);
    }

    public void Tick() {
        if (shown == null) return;

        float remaining = shown.GetRemainingCooldown();
        float ratio = shown.cooldown > 0f ? Mathf.Clamp01(remaining / shown.cooldown) : 0f;
        ShowCooldown(ratio, remaining);
    }

    void ShowCooldown(float ratio, float remaining) {
        if (cooldownFill != null) cooldownFill.fillAmount = ratio;

        bool cooling = remaining > 0f;

        // 머리글자와 남은 초는 칸 한가운데를 함께 쓴다. 겹치면 둘 다 못 읽으므로 쿨타임 동안에는 숫자만 남긴다
        // (아이콘으로 그리는 칸은 그림 위에 숫자가 얹혀도 읽히므로 건드리지 않는다).
        if (monogramLabel != null && usesMonogram) monogramLabel.gameObject.SetActive(!cooling);

        if (cooldownLabel == null) return;
        cooldownLabel.gameObject.SetActive(cooling);
        if (cooling) cooldownLabel.text = string.Format(cooldownFormat, remaining);
    }

    // 단어 머리글자 최대 두 자. 한 단어짜리 이름은 앞 두 글자를 쓴다.
    static string Monogram(string displayName) {
        if (string.IsNullOrWhiteSpace(displayName)) return "?";

        string[] words = displayName.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
        if (words.Length >= 2) return (words[0].Substring(0, 1) + words[1].Substring(0, 1)).ToUpperInvariant();

        string word = words[0];
        return (word.Length >= 2 ? word.Substring(0, 2) : word).ToUpperInvariant();
    }

    #endregion
}
