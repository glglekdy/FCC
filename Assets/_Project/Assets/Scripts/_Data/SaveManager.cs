using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

// 세이브의 소유자. 파일 입출력(Write/Read)과 게임 상태 수집/복원(SaveGame/LoadGame)을 모두 여기서 한다.
// 정식 저장 지점은 거울(SaveMirror)이고, Player_move의 F5/F9는 개발용 단축키로 남겨두었다.
public class SaveManager : MonoBehaviour {
    public static SaveManager Instance;

    #region 인스펙터 변수

    [Header("파일")]
    public string fileName = "save.json"; // 01번 기록의 파일 이름. Application.persistentDataPath 아래에 저장된다.
    public int slotCount = 3; // 기록 선택 화면에 보이는 자리 수. 02번부터는 save_2.json 처럼 번호가 붙는다.

    [Header("대상")]
    public string playerTag = "Player"; // 저장·복원할 플레이어를 찾을 태그. SaveManager는 씬을 넘어 살아남으므로 매번 새로 찾는다.

    #endregion
    #region 이벤트

    public event Action<SaveData> OnSaved; // 저장 직후. 저장 알림 UI가 구독할 자리.
    public event Action<SaveData> OnLoaded; // 복원 직후.

    #endregion
    #region 컴포넌트 변수

    // 마지막으로 고른 기록 자리. 다음 실행의 [이어하기]가 이 자리를 연다.
    // 세이브 파일 안이 아니라 PlayerPrefs 에 두는 이유: "어느 파일을 열지"를 파일을 열기 전에 알아야 하기 때문이다.
    const string ActiveSlotKey = "FCC_ActiveSaveSlot";

    string savePath;

    public int ActiveSlot { get; private set; } // 지금 저장·불러오기가 향하는 자리(0부터).

    public bool HasSave => !string.IsNullOrEmpty(savePath) && File.Exists(savePath);

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        if (Instance == null) {
            Instance = this;
            transform.SetParent(null); // DontDestroyOnLoad는 루트 오브젝트에서만 동작 (GAME_MANAGER 하위에 정리용으로 배치되어 있음)
            DontDestroyOnLoad(gameObject);
        }
        else {
            Destroy(gameObject);
            return;
        }

        ActiveSlot = Mathf.Clamp(PlayerPrefs.GetInt(ActiveSlotKey, 0), 0, Mathf.Max(0, slotCount - 1));
        savePath = SlotPath(ActiveSlot);
    }

    #endregion
    #region 파일 입출력

    public void Write(SaveData data) {
        File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
    }

    public SaveData Read() {
        if (!HasSave) return null;
        return JsonUtility.FromJson<SaveData>(File.ReadAllText(savePath));
    }

    public void DeleteSave() {
        if (HasSave) File.Delete(savePath);
    }

    #endregion
    #region 기록 자리

    // 01번은 예전 단일 세이브의 파일 이름을 그대로 쓴다. 여기에도 번호를 붙이면 기존 플레이어의 save.json 이
    // 어느 자리에도 잡히지 않아 [이어하기]에서 사라진다. 02번부터는 화면에 보이는 번호를 파일 이름에 붙인다.
    public string SlotPath(int slot) {
        if (slot <= 0) return Path.Combine(Application.persistentDataPath, fileName);

        string name = Path.GetFileNameWithoutExtension(fileName) + "_" + (slot + 1) + Path.GetExtension(fileName);
        return Path.Combine(Application.persistentDataPath, name);
    }

    public bool HasSlot(int slot) {
        return File.Exists(SlotPath(slot));
    }

    // 로비가 [이어하기]를 잠글지 정할 때 쓴다. 마지막 자리가 비어도 다른 자리에 기록이 있으면 불러올 수 있어야 한다.
    public bool HasAnySlot() {
        for (int i = 0; i < slotCount; i++) {
            if (HasSlot(i)) return true;
        }
        return false;
    }

    // 기록 선택 화면이 자리마다 부른다. 파일 하나가 깨졌다고 화면 전체가 예외로 멈추면 안 되므로
    // 읽지 못한 자리는 경고만 남기고 빈자리로 돌려준다 (JsonUtility 는 형식이 틀리면 예외를 던진다).
    public SaveData ReadSlot(int slot) {
        string path = SlotPath(slot);
        if (!File.Exists(path)) return null;

        try {
            return JsonUtility.FromJson<SaveData>(File.ReadAllText(path));
        } catch (Exception e) {
            Debug.LogWarning($"[SaveManager] {slot + 1}번 기록을 읽지 못했습니다({path}) — {e.Message}", this);
            return null;
        }
    }

    public void DeleteSlot(int slot) {
        string path = SlotPath(slot);
        if (File.Exists(path)) File.Delete(path);
    }

    // 이후의 저장·불러오기(거울·이어하기)가 이 자리를 향하게 한다. 새로 시작할 자리를 골랐을 때 부른다.
    public void UseSlot(int slot) {
        ActiveSlot = Mathf.Clamp(slot, 0, Mathf.Max(0, slotCount - 1));
        savePath = SlotPath(ActiveSlot);

        PlayerPrefs.SetInt(ActiveSlotKey, ActiveSlot);
        PlayerPrefs.Save(); // 곧바로 씬을 넘기므로 종료 시점의 자동 저장을 기다리지 않는다.
    }

    #endregion
    #region 저장

    // 거울에서 호출하는 정식 저장. respawnPosition은 불러왔을 때 플레이어가 설 위치(보통 거울 발밑)다.
    // 플레이어 현재 위치를 그대로 쓰면 거울에 딱 붙어 있다 저장했을 때 복귀 지점이 어긋난다.
    public SaveData SaveGame(string checkpointId, Vector2 respawnPosition) {
        SaveData data = new SaveData {
            sceneName = SceneManager.GetActiveScene().name,
            checkpointId = checkpointId,
            posX = respawnPosition.x,
            posY = respawnPosition.y,
            savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };

        GameObject player = FindPlayer();
        if (player != null && player.TryGetComponent(out Health health)) {
            data.currentHealth = health.CurrentHealth;
            data.maxHealth = health.MaxHealth;
        }

        if (player != null && player.TryGetComponent(out Player_MemoryShardInventory shards)) {
            data.memoryShardCount = shards.Count;
        }

        if (player != null && player.TryGetComponent(out Player_DungeonCoinInventory coins)) {
            data.dungeonCoinCount = coins.Count;
        }

        if (player != null && player.TryGetComponent(out Player_move move)) {
            data.abilitiesSaved = true;
            data.hasDoubleJump = move.HasAbility(Player_Ability.DoubleJump);
            data.hasDash = move.HasAbility(Player_Ability.Dash);
        }

        if (player != null && player.TryGetComponent(out SkillManager skills)) {
            data.unlockedSkillIds = skills.CaptureUnlocked();
            data.skillLevels = skills.CaptureLevels();
            data.equippedSkillIds = skills.CaptureEquipped();
        }

        if (ObjectiveManager.Instance != null) {
            data.objectives = ObjectiveManager.Instance.CaptureObjectives();
            data.startedMissions = ObjectiveManager.Instance.CaptureStartedMissions();
        }

        if (DungeonManager.Instance != null) {
            data.clearedDungeonIds = DungeonManager.Instance.CaptureClearedDungeons();
        }

        if (NpcDialogueManager.Instance != null) {
            data.npcDialogueCounts = NpcDialogueManager.Instance.CaptureState();
        }

        if (TutorialManager.Instance != null) {
            data.shownTutorialIds = TutorialManager.Instance.CaptureState();
        }

        Write(data);
        OnSaved?.Invoke(data);
        return data;
    }

    // 복귀 지점을 따로 정하지 않는 퀵세이브. 플레이어가 지금 서 있는 자리를 그대로 저장한다.
    public SaveData SaveGame(string checkpointId) {
        GameObject player = FindPlayer();
        if (player == null) {
            Debug.LogWarning($"[SaveManager] '{playerTag}' 태그가 붙은 플레이어를 찾지 못해 저장을 건너뜁니다.", this);
            return null;
        }

        return SaveGame(checkpointId, player.transform.position);
    }

    #endregion
    #region 불러오기

    // 같은 씬 안에서 저장 시점 상태로 되돌린다.
    // 씬 전환이 필요한 경우(메인 메뉴 → 이어하기)는 Read()로 sceneName을 먼저 확인해 씬을 연 뒤 호출할 것.
    public SaveData LoadGame() {
        SaveData data = Read();
        if (data == null) return null;

        Apply(data);
        OnLoaded?.Invoke(data);
        return data;
    }

    void Apply(SaveData data) {
        GameObject player = FindPlayer();
        if (player != null) {
            // z는 건드리지 않는다. 2D라도 카메라/정렬 때문에 z를 따로 잡아둔 경우가 있다.
            player.transform.position = new Vector3(data.posX, data.posY, player.transform.position.z);

            // 남아 있던 속도를 지우지 않으면 복원 직후 그대로 미끄러지거나 낙하 속도를 이어받는다.
            if (player.TryGetComponent(out Rigidbody2D rigid)) rigid.linearVelocity = Vector2.zero;

            // maxHealth가 0이면 체력을 기록하지 않던 구버전 세이브이므로 현재 체력을 그대로 둔다.
            if (data.maxHealth > 0 && player.TryGetComponent(out Health health)) health.SetHealth(data.currentHealth);

            if (player.TryGetComponent(out Player_MemoryShardInventory shards)) shards.SetCount(data.memoryShardCount);
            if (player.TryGetComponent(out Player_DungeonCoinInventory coins)) coins.SetCount(data.dungeonCoinCount);

            // 기술 상태를 적지 않던 구버전 세이브면 인스펙터 상태를 그대로 둔다(SaveData.abilitiesSaved 주석 참고).
            if (data.abilitiesSaved && player.TryGetComponent(out Player_move move)) {
                move.SetAbility(Player_Ability.DoubleJump, data.hasDoubleJump);
                move.SetAbility(Player_Ability.Dash, data.hasDash);
            }

            // 조각을 먼저 되돌린 뒤 스킬을 복원한다. 강화 비용이 보유량에서 계산되므로,
            // 복원 직후 정비 화면을 열었을 때 표시되는 비용이 저장 시점과 어긋나지 않게 하기 위함이다.
            if (player.TryGetComponent(out SkillManager skills)) {
                skills.RestoreState(data.unlockedSkillIds, data.skillLevels, data.equippedSkillIds);
            }
        }

        if (ObjectiveManager.Instance != null) {
            ObjectiveManager.Instance.RestoreState(data.objectives, data.startedMissions);
        }

        if (DungeonManager.Instance != null) {
            DungeonManager.Instance.RestoreClearedDungeons(data.clearedDungeonIds);
        }

        if (NpcDialogueManager.Instance != null) {
            NpcDialogueManager.Instance.RestoreState(data.npcDialogueCounts);
        }

        if (TutorialManager.Instance != null) {
            TutorialManager.Instance.RestoreState(data.shownTutorialIds);
        }
    }

    GameObject FindPlayer() {
        return GameObject.FindGameObjectWithTag(playerTag);
    }

    #endregion
}
