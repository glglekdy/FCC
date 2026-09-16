// 곡예사에게 배우는 이동 패시브 목록. 스킬(SkillBase)과 달리 슬롯을 차지하지 않고, 배우면 계속 켜져 있다.
//
// **세이브에는 이름이 아니라 기술별 bool 필드로 기록됩니다**(SaveData.hasDoubleJump 등).
// 항목을 추가하면 SaveData 필드 · SaveManager 저장/복원 · Player_move.HasAbility/SetAbility 도 함께 고치세요.
// 씬·프리팹에는 정수로 저장되므로 **중간 항목을 지우거나 순서를 바꾸지 말고 맨 뒤에 추가하세요.**
public enum Player_Ability {
    None,
    DoubleJump,
    Dash,
}
