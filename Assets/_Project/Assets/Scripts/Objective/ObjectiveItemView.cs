using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 체크리스트 한 줄. ObjectiveChecklistView가 목표 수만큼 이 프리팹을 찍어낸다.
//
// 생김새(체크박스·설명·수량)는 전부 프리팹의 자식 오브젝트이고 이 스크립트는 인스펙터로 주입받은
// 참조를 갱신하기만 한다. 예전에는 "☐ 설명 (1/3)" 을 한 줄 문자열로 조립했는데, 그러면 체크박스만
// 색을 바꾸거나 수량을 오른쪽으로 밀 수가 없어 인스펙터에서 만질 여지가 없었다.
//
// 체크 표시를 글자가 아니라 Image로 둔 이유가 하나 더 있다 — ☐(U+2610)·☑(U+2611)·✓(U+2713)는
// 한글 SDF 아틀라스(SCDream6)와 TMP 폴백 폰트 양쪽에 모두 없어서 네모(tofu)로 렌더된다.
// 하필 네모가 빈 체크박스처럼 보여서 오래 눈에 띄지 않았다. 스프라이트를 쓰면 폰트와 무관해진다.
//
// **Prefabs/UI/ObjectiveItemRow.prefab 의 루트에 붙어 있습니다.** (SkillRowView와 같은 구조)
public class ObjectiveItemView : MonoBehaviour {
    #region 인스펙터 변수

    [Header("연결")]
    public Image background; // 줄 배경. 없어도 동작한다 (완료 강조를 배경으로 주고 싶을 때만 물린다).
    public Image checkbox; // 체크 표시.
    public TMP_Text label; // 목표 설명문.
    public TMP_Text countLabel; // "(2/10)". 수집형이 아닌 목표에서는 오브젝트째로 꺼진다.

    [Header("체크 표시")]
    // 비워두면 스프라이트를 바꾸지 않고 아래 색만으로 켜짐/꺼짐을 구분한다.
    public Sprite uncheckedSprite;
    public Sprite checkedSprite;

    [Header("색상")]
    public Color labelColor = UiTheme.TextBody;
    public Color completedLabelColor = UiTheme.TextDim; // 완료된 줄은 흐리게.
    public Color checkboxOffColor = UiTheme.Line;
    public Color checkboxOnColor = UiTheme.Accent; // 완료 체크. 화면에서 포인트 컬러가 쓰이는 몇 안 되는 자리다.

    [Header("문구")]
    // 코드가 상황에 따라 갈아끼우는 문구만 여기 둔다. 고정 문구는 프리팹의 TMP에 직접 적는다.
    public string countFormat = "({0}/{1})"; // {0} 현재 수량, {1} 목표 수량.

    #endregion
    #region 런타임 변수

    // 이 줄이 대표하는 목표. 체크리스트가 갱신 대상을 찾을 때 쓴다.
    public ObjectiveRuntime Objective { get; private set; }

    bool isReady; // 연결이 온전한지. 어긋난 채로 갱신하면 NullReference가 쏟아지므로 Awake에서 한 번만 검사한다.

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        isReady = ValidateReferences();
    }

    // 프리팹을 손보다 참조를 끊었을 때 조용히 죽지 않도록 무엇이 비었는지 이름으로 찍어준다.
    // (SkillLoadoutView.ValidateReferences와 같은 방식)
    bool ValidateReferences() {
        List<string> missing = new();

        if (label == null) missing.Add(nameof(label));
        if (checkbox == null) missing.Add(nameof(checkbox));

        if (missing.Count == 0) return true;

        Debug.LogError($"[ObjectiveItemView] '{name}' 의 프리팹 연결이 비어 있습니다 — {string.Join(", ", missing)}. " +
            "Prefabs/UI/ObjectiveItemRow 프리팹을 쓰세요. 없으면 Tools ▸ FCC ▸ Build Objective Checklist Prefab 으로 만들 수 있습니다.", this);
        return false;
    }

    #endregion
    #region 표시 갱신

    public void Set(ObjectiveRuntime objective) {
        Objective = objective;
        if (!isReady || objective == null) return;

        bool completed = objective.IsCompleted;

        label.text = objective.Description;
        label.color = completed ? completedLabelColor : labelColor;
        label.fontStyle = completed ? FontStyles.Strikethrough : FontStyles.Normal;

        // 스프라이트를 안 넣어도 색만으로 구분되도록, 스프라이트가 있을 때만 갈아끼운다.
        Sprite target = completed ? checkedSprite : uncheckedSprite;
        if (target != null) checkbox.sprite = target;
        checkbox.color = completed ? checkboxOnColor : checkboxOffColor;

        RefreshCount(objective, completed);
    }

    // 수량 표기는 수집형 목표에서만 쓴다. 아닐 때는 오브젝트를 꺼서 빈 칸이 자리를 먹지 않게 한다.
    void RefreshCount(ObjectiveRuntime objective, bool completed) {
        if (countLabel == null) return;

        bool showCount = objective.Definition.type == MissionType.CollectItem;
        countLabel.gameObject.SetActive(showCount);
        if (!showCount) return;

        countLabel.text = string.Format(countFormat, objective.currentCount, objective.TargetCount);
        countLabel.color = completed ? completedLabelColor : labelColor;
    }

    // 목표가 방금 완료됐을 때. 지금은 완료 표시를 확정하는 것까지만 하고,
    // 효과음이나 번쩍임 같은 연출을 붙이려면 여기에 얹으면 된다.
    // **히트스톱 중에도 정상 속도로 보여야 하므로 연출을 넣을 땐 Time.unscaledDeltaTime을 쓰세요.**
    public void PlayCompleted() {
        if (!isReady) return;
        Set(Objective);
    }

    #endregion
}
