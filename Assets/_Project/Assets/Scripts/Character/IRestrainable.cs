// 구속(제자리에 묶여 이동·공격을 못 하는 상태)을 받을 수 있는 몬스터 컴포넌트가 구현한다.
// 곡예사의 Close Call 이 줄로 묶을 때 대상에 붙은 이 인터페이스를 전부 찾아 부른다.
//
// 넉백(ApplyKnockback)처럼 스킬 쪽이 이동 컴포넌트 종류를 하나씩 확인하지 않고 인터페이스로 묶은 이유는,
// 몬스터마다 이동과 공격을 맡는 구성이 달라서다(GroundMoveSystem + Attack / FlyMoveSystem 단독 / Monster_Wraith 단독).
// **새 몬스터 AI를 만들면 이것만 구현하면 구속이 걸립니다.** 구현하지 않은 몬스터는 피해만 받고 묶이지 않는다.
public interface IRestrainable {
    // duration 초 동안 묶는다. 이미 묶여 있으면 남은 시간과 비교해 긴 쪽을 남긴다.
    void Restrain(float duration);
}
