using System;
using System.Collections.Generic;

// 세이브 파일에 그대로 직렬화되는 데이터 덩어리. JsonUtility가 다루므로 전부 public 필드여야 한다.
// 필드를 새로 추가해도 기존 세이브 파일은 그대로 읽히며(없는 필드는 기본값 0/null이 된다),
// 그래서 오염도·기억 조각 같은 후속 시스템은 이 클래스에 필드만 늘리면 붙는다.
[Serializable]
public class SaveData {
    public string sceneName;    // 저장 시점의 씬 이름. 메인 메뉴에서 "이어하기"로 어느 씬을 열지 판단한다.
    public string checkpointId; // 저장을 발생시킨 거울(SaveMirror)의 id. 비어 있으면 개발용 퀵세이브다.

    public float posX;          // 불러왔을 때 플레이어가 서 있을 위치.
    public float posY;

    public int currentHealth;   // 자아 게이지.
    public int maxHealth;       // 0이면 체력 정보가 없는 구버전 세이브로 취급해 체력을 건드리지 않는다.

    public List<ObjectiveSaveEntry> objectives = new List<ObjectiveSaveEntry>();

    // 이미 시작된 미션의 id. 목표의 Active 상태는 "선행 조건이 채워졌는가"로 다시 계산되지만,
    // "미션이 시작됐는가"는 목표 진행도에서 파생되지 않아 따로 기록해야 한다
    // (시작 구역을 밟고 저장했으면 그 사실 자체를 기억해야 한다).
    // 이 필드가 없던 구버전 세이브는 진행 흔적이 있는 미션을 시작된 것으로 보고 복구한다.
    public List<string> startedMissions = new List<string>();

    public string savedAt;      // 표시용 저장 시각. 나중에 불러오기 슬롯 UI에서 쓴다.

    public int memoryShardCount; // 보유한 기억 조각 수.
    public List<string> clearedDungeonIds = new List<string>(); // 뒷세계 등 1회성 몬스터 구역 중 이미 클리어한 dungeonId 목록.
    public List<NpcDialogueSaveEntry> npcDialogueCounts = new List<NpcDialogueSaveEntry>(); // NPC별로 지금까지 대화를 건 횟수.

    public List<string> unlockedSkillIds = new List<string>(); // 스토리 진행으로 해금된 스킬 id.
    public List<SkillSaveEntry> skillLevels = new List<SkillSaveEntry>(); // 스킬별 강화 레벨과 단계별 지불액.

    // 슬롯 0·1·2에 장착된 스킬 id. 빈 슬롯은 빈 문자열로 자리를 채워 슬롯 번호가 밀리지 않게 한다.
    public List<string> equippedSkillIds = new List<string>();

    // 곡예사에게 배운 이동 패시브. 아래 두 칸만으로는 "안 배웠다"와 "기록이 없던 구버전 세이브"를 구분할 수 없어
    // (둘 다 false) abilitiesSaved 를 함께 적는다. 이게 false 면 불러올 때 기술 상태를 건드리지 않는다
    // — 대시가 해금제로 바뀌기 전의 세이브를 불러왔다가 대시를 잃는 일이 없게 하기 위함이다(maxHealth == 0 규칙과 같은 방식).
    public bool abilitiesSaved;
    public bool hasDoubleJump;
    public bool hasDash;
}

// 스킬 하나의 강화 상태. 스킬 에셋 자체를 직렬화하면 수치 테이블 같은 기획 데이터까지 세이브에
// 굳어버리므로, 진행 상태만 떼어 저장한다 (ObjectiveSaveEntry와 같은 이유).
[Serializable]
public class SkillSaveEntry {
    public string id;
    public int level;

    // 강화 단계별로 실제 지불한 조각 수(인덱스 = 단계). 환불이 "그때 낸 값"을 그대로 돌려주려면
    // 반드시 남겨야 한다. 환불 시점의 보유량으로 다시 계산하면, 조각이 적을 때 강화해 두고 많을 때
    // 되돌려 차액을 버는 무한 증식이 생긴다.
    public List<int> paid = new List<int>();
}

// 목표 하나의 진행도. Objective 자체를 직렬화하면 description·targetCount 같은
// 기획 데이터까지 세이브에 굳어버려서, 진행도만 따로 떼어 저장한다.
[Serializable]
public class ObjectiveSaveEntry {
    public string id;
    public int currentCount;
    public bool isCompleted;
}

// NPC 한 명과 지금까지 나눈 대화 횟수. 대화 세트(1번째/2번째/3번째...)를 고르는 데 쓰인다.
[Serializable]
public class NpcDialogueSaveEntry {
    public string npcId;
    public int talkCount;
}
