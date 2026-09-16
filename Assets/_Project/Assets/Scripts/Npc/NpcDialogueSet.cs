using System.Collections.Generic;

// "몇 번째 대화"인지에 대응하는 대사 한 묶음. Unity 인스펙터가 List<List<T>>를 다루기 어려워하므로
// DialogueEntry처럼 이름 있는 클래스로 한 겹 감싼다.
[System.Serializable]
public class NpcDialogueSet {
    public List<DialogueEntry> entries = new(); // 이 순번에 재생할 대사.
    public Player_Ability unlockAbility; // None 이 아니면 이 순번의 대사가 끝날 때 이 이동 패시브를 배운다. 곡예사 NPC가 대화마다 하나씩 가르칠 때 쓴다.
}
