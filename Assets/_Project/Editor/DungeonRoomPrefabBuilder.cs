#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// 던전 방 프리팹 세트를 한 번에 찍어내는 에디터 도구.
//
// 방을 런타임 스크립트로 조립하지 않는다는 규칙을 지키려면 실물 프리팹이 있어야 한다. 이 도구는 소켓·
// 지형·스폰 지점·리스폰 지점이 전부 배선된 방을 카테고리별로 만들어 "그레이박스" 상태까지 올려 준다.
// 실제 아트·미세 배치는 만들어진 프리팹을 열어 인스펙터/씬에서 손본다.
//
// 지형 수치는 플레이어 점프(jumpForce 11, 중력 1배 → 최대 약 6유닛)를 기준으로, 누구나 쉽게 넘어갈 수
// 있도록 단차 2.5 · 간격 3~4유닛 안쪽으로만 잡는다.
//
// 지형은 오브젝트가 아니라 타일맵 칸으로 남긴다. 아래 방 정의에 적힌 수치는 그대로 두고, 칠하는 단계에서
// 1유닛 격자에 반올림해 옮긴다(두께 1.2 · 0.6 은 각각 한 칸이 된다). 그래서 지형을 손보고 싶을 때는
// 이 파일을 고쳐 다시 찍는 대신 타일 팔레트로 칸을 칠하면 되고, 방마다 오브젝트가 수십 개씩 쌓이지 않는다.
// 층은 "아래에서 뚫고 올라갈 수 있어야 하는가"로 가른다 — 바닥·벽·천장은 Tilemap_Solid, 공중에 뜬
// 발판만 Tilemap_Platform. 막힘 지형을 Platform 층에 칠하면 벽이 옆·아래로 뚫리고 접지 판정까지 죽는다.
//
// 특수 발판(이동·붕괴·가시)만 예외로 오브젝트로 남긴다. 칸 단위로 움직이거나 사라지는 것이 아니라
// 각자 콜라이더와 스크립트를 들고 따로 동작해야 하기 때문이다.
//
// 사용법:
//   Tools ▸ FCC ▸ Dungeon ▸ Build All Rooms        → 아래 18종 프리팹을 기존 경로에 덮어쓴다(GUID 유지).
//   Tools ▸ FCC ▸ Dungeon ▸ Place Dungeon Rig In Scene → 게이트·생성기·매니저를 씬에 놓고 풀을 채운다.
public static class DungeonRoomPrefabBuilder {
    #region 상수

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/Dungeon";

    // 지형 감각 수치.
    const float Step = 2.5f;      // 한 번에 오르내리는 세로 단차.
    const float PlatThick = 0.6f; // 발판 두께.
    const float WallThick = 1.2f; // 벽·바닥 두께.
    const float SpikeThick = 0.8f; // 가시밭이 바닥 위로 솟은 높이.

    // 특수 발판의 임시 그림. 칸 전체를 채우는 사각형이라 스케일을 늘리면 콜라이더와 크기가 정확히 맞는다.
    const string BlockSpritePath = "Assets/_Project/Assets/Sprites/Env/Tile_Solid_Inner.png";
    const int GimmickOrder = 1; // 지형 타일(정렬 0) 바로 앞. 겹치는 자리에서 발판이 지형에 묻히면 안 된다.

    static int groundLayer;
    static Sprite blockSprite;

    #endregion
    #region 메뉴 — 전체 빌드

    [MenuItem("Tools/FCC/Dungeon/Build All Rooms")]
    public static void BuildAllRooms() {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[Dungeon] 프리팹 편집 모드를 닫고 다시 실행하세요.");
            return;
        }

        EnsureFolder(PrefabDir);

        groundLayer = LayerMask.NameToLayer("ground");
        if (groundLayer < 0) {
            groundLayer = 0;
            Debug.LogWarning("[Dungeon] 'ground' 레이어를 찾지 못해 지형을 Default 레이어에 만듭니다. 플레이어가 밟지 못할 수 있으니 레이어를 확인하세요.");
        }

        if (TilemapGridBuilder.SolidTile == null || TilemapGridBuilder.PlatformTile == null) {
            Debug.LogError("[Dungeon] 지형 타일을 찾지 못했습니다. 먼저 Tools ▸ FCC ▸ Tilemap ▸ Build King And Pig Rule Tiles 를 실행하세요.");
            return;
        }

        blockSprite = AssetDatabase.LoadAssetAtPath<Sprite>(BlockSpritePath);
        if (blockSprite == null) {
            Debug.LogWarning($"[Dungeon] 특수 발판 그림을 찾지 못했습니다: {BlockSpritePath} — 이동·붕괴 발판이 보이지 않게 됩니다.");
        }

        BuildEntry();
        BuildExit();

        BuildHazardA();
        BuildHazardB();
        BuildHazardC();
        BuildObby();
        BuildObbyB();
        BuildObbyC();
        BuildObbyD();

        BuildVerticalA();
        BuildVerticalB();

        BuildCombat("Room_Combat_A", CombatVariant.OneHighPlatform);
        BuildCombat("Room_Combat_B", CombatVariant.TwoPlatforms);
        BuildCombat("Room_Combat_C", CombatVariant.WideFlat);
        BuildCombat("Room_Combat_D", CombatVariant.TwoTiers);
        BuildCombat("Room_Combat_E", CombatVariant.CenterPeak);

        BuildSecretA();
        BuildSecretB();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Dungeon] 방 프리팹 18종을 다시 만들었습니다. DungeonGenerator 풀에 이미 연결돼 있으면 그대로 쓰입니다.");
    }

    #endregion
    #region 방 빌드 — 입구 · 출구

    static void BuildEntry() {
        const float w = 38f, h = 18f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom("Room_Entry", w, h, out BoxCollider2D trigger);
        Frame(root, w, h, g, leftWall: true, rightWall: false, ceiling: true);

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 8f, a, 0f));
        Transform exit = Anchor(root, "ExitAnchor", new Vector3(w / 2f - 1.5f, a, 0f));
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 8f, a, 0f));

        // 뒤로 걸어 나가는 이탈 트리거. 스폰 지점(EntryAnchor)과 3유닛 이상 떨어뜨려 입장 직후 오발동을 막는다.
        TriggerVolume(root, "WalkOutZone", new Vector3(-w / 2f + 3f, g + h / 2f - 1f, 0f), new Vector2(2.5f, h - 2f))
            .AddComponent<DungeonExitZone>();

        WireRoom(root, DungeonRoom.RoomRole.Entry, entry, exit, null, respawn, trigger, null, null);
        Save(root, "Room_Entry");
    }

    static void BuildExit() {
        const float w = 38f, h = 18f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom("Room_Exit", w, h, out BoxCollider2D trigger);
        Frame(root, w, h, g, leftWall: false, rightWall: true, ceiling: true);
        Platform(root, "Step", new Vector3(2f, g + Step * 0.5f, 0f), new Vector2(6f, PlatThick));

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 1.5f, a, 0f));
        Transform exit = Anchor(root, "ExitAnchor", new Vector3(w / 2f - 1.5f, a, 0f));
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 3f, a, 0f));

        // 던전을 클리어하고 나가는 이탈 트리거.
        TriggerVolume(root, "ClearExitZone", new Vector3(w / 2f - 3f, g + h / 2f - 1f, 0f), new Vector2(2.5f, h - 2f))
            .AddComponent<DungeonExitZone>();

        WireRoom(root, DungeonRoom.RoomRole.Exit, entry, exit, null, respawn, trigger, null, null);
        Save(root, "Room_Exit");
    }

    #endregion
    #region 방 빌드 — 플랫포밍 기믹

    // 두 발판으로 건너는 넓은 틈. 떨어져도 죽지 않고 현재 방 리스폰으로 되돌린다.
    static void BuildHazardA() {
        const float w = 38f, h = 18f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom("Room_Hazard_A", w, h, out BoxCollider2D trigger);
        Ceiling(root, w, h);
        Platform(root, "Floor_L", new Vector3(-w / 2f + (w / 2f - 6f) / 2f, g - WallThick / 2f, 0f), new Vector2(w / 2f - 6f, WallThick));
        Platform(root, "Floor_R", new Vector3(w / 2f - (w / 2f - 6f) / 2f, g - WallThick / 2f, 0f), new Vector2(w / 2f - 6f, WallThick));

        Platform(root, "Plat_1", new Vector3(-2.5f, g + Step, 0f), new Vector2(3f, PlatThick));
        Platform(root, "Plat_2", new Vector3(2.5f, g + Step, 0f), new Vector2(3f, PlatThick));

        TriggerVolume(root, "FallZone", new Vector3(0f, g - 5f, 0f), new Vector2(13f, 3f)).AddComponent<DungeonFallZone>();

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 1.5f, a, 0f));
        Transform exit = Anchor(root, "ExitAnchor", new Vector3(w / 2f - 1.5f, a, 0f));
        Transform branch = Anchor(root, "BranchAnchor", new Vector3(w / 2f - 5f, a + Step, 0f));
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 3f, a, 0f));

        WireRoom(root, DungeonRoom.RoomRole.PlatformingHazard, entry, exit, branch, respawn, trigger, null, null);
        Save(root, "Room_Hazard_A");
    }

    // 올라갔다 내려오는 3단 계단.
    static void BuildHazardB() {
        const float w = 38f, h = 18f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom("Room_Hazard_B", w, h, out BoxCollider2D trigger);
        Ceiling(root, w, h);
        Platform(root, "Floor_L", new Vector3(-w / 2f + (w / 2f - 4f) / 2f, g - WallThick / 2f, 0f), new Vector2(w / 2f - 4f, WallThick));
        Platform(root, "Floor_R", new Vector3(w / 2f - (w / 2f - 10f) / 2f, g - WallThick / 2f, 0f), new Vector2(w / 2f - 10f, WallThick));

        Platform(root, "Plat_1", new Vector3(-1f, g + Step, 0f), new Vector2(3f, PlatThick));
        Platform(root, "Plat_2", new Vector3(3f, g + Step * 2f, 0f), new Vector2(3f, PlatThick));
        Platform(root, "Plat_3", new Vector3(7f, g + Step, 0f), new Vector2(3f, PlatThick));

        TriggerVolume(root, "FallZone", new Vector3(3f, g - 5f, 0f), new Vector2(15f, 3f)).AddComponent<DungeonFallZone>();

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 1.5f, a, 0f));
        Transform exit = Anchor(root, "ExitAnchor", new Vector3(w / 2f - 1.5f, a, 0f));
        Transform branch = Anchor(root, "BranchAnchor", new Vector3(3f, a + Step * 2f, 0f));
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 3f, a, 0f));

        WireRoom(root, DungeonRoom.RoomRole.PlatformingHazard, entry, exit, branch, respawn, trigger, null, null);
        Save(root, "Room_Hazard_B");
    }

    // 작은 틈 두 개를 한 번씩 톡톡 건넌다.
    static void BuildHazardC() {
        const float w = 38f, h = 18f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom("Room_Hazard_C", w, h, out BoxCollider2D trigger);
        Ceiling(root, w, h);
        float seg = (w - 8f) / 3f;
        Platform(root, "Floor_L", new Vector3(-w / 2f + seg / 2f, g - WallThick / 2f, 0f), new Vector2(seg, WallThick));
        Platform(root, "Floor_M", new Vector3(0f, g - WallThick / 2f, 0f), new Vector2(seg, WallThick));
        Platform(root, "Floor_R", new Vector3(w / 2f - seg / 2f, g - WallThick / 2f, 0f), new Vector2(seg, WallThick));

        float gapX = seg / 2f + 2f;
        Platform(root, "Plat_L", new Vector3(-gapX, g + 1.5f, 0f), new Vector2(2.5f, PlatThick));
        Platform(root, "Plat_R", new Vector3(gapX, g + 1.5f, 0f), new Vector2(2.5f, PlatThick));

        TriggerVolume(root, "FallZone_L", new Vector3(-gapX, g - 5f, 0f), new Vector2(4.5f, 3f)).AddComponent<DungeonFallZone>();
        TriggerVolume(root, "FallZone_R", new Vector3(gapX, g - 5f, 0f), new Vector2(4.5f, 3f)).AddComponent<DungeonFallZone>();

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 1.5f, a, 0f));
        Transform exit = Anchor(root, "ExitAnchor", new Vector3(w / 2f - 1.5f, a, 0f));
        Transform branch = Anchor(root, "BranchAnchor", new Vector3(0f, a + Step, 0f));
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 3f, a, 0f));

        WireRoom(root, DungeonRoom.RoomRole.PlatformingHazard, entry, exit, branch, respawn, trigger, null, null);
        Save(root, "Room_Hazard_C");
    }

    // 오비(장애물 코스) 전용 방. 몬스터도 전투 락인도 없이 점프 구간만으로 이루어진 순수 플랫포밍 방이라,
    // 다른 방보다 두 배 가까이 길고 발판 수도 그만큼 많다. 코스 자체가 방의 내용이라 쪼갤 수가 없다.
    //
    // 구간마다 성격이 다른 것을 하나씩 배치해 같은 점프를 반복하지 않게 했다.
    //   1구간 정적 발판 → 2구간 가로 이동 발판 → 3구간 통과 발판 탑 → 4구간 무너지는 발판.
    // 기믹을 1구간에 두지 않는 이유는, 간격 감각을 먼저 잡은 뒤에 타이밍을 요구해야 덜 억울하기 때문이다.
    static void BuildObby() {
        const float w = 76f, h = 18f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom("Room_Obby_A", w, h, out BoxCollider2D trigger);
        Ceiling(root, w, h);

        // 앞뒤 방과 이어지는 시작·도착 발판만 바닥 두께로 두어, 걸어 들어오고 나가는 높이를 맞춘다.
        Ledge(root, "Ledge_Start", -34f, g, 8f, WallThick);
        Ledge(root, "Ledge_End", 35.75f, g, 4.5f, WallThick);

        // 1구간 — 정적 워밍업. 기믹을 만나기 전에 간격 감각부터 잡게 한다.
        Ledge(root, "Plat_1", -25f, g, 4f, PlatThick);
        Ledge(root, "Plat_2", -18.5f, g + 2.4f, 3f, PlatThick);

        // 2구간 — 가로 이동 발판. 왼쪽 끝에 와 있을 때만 Plat_2 에서 건너탈 수 있고(간격 3.25),
        // 오른쪽 끝까지 실려 가야 Plat_3 에 닿는다. 왼쪽 끝에서 바로 뛰면 간격이 9가 넘어 못 넘어간다.
        Mover(root, "Mover_1", -12f, g + 2.4f, 3.5f, new Vector2(6f, 0f), 2.2f);
        Ledge(root, "Plat_3", 1f, g, 4f, PlatThick);

        // 3구간 — 통과 발판 탑. 바닥에서 한 번에 오를 수 있는 높이는 약 6유닛이라, 꼭대기(+7.2)는
        // 발판을 아래에서 뚫고 올라가야만 닿는다. Deck_0 은 벽에 0.1 물려 두어 사이로 빠지는 틈을 없앴다.
        Ledge(root, "Deck_0", 9.5f, g, 7f, PlatThick);
        Ledge(root, "Deck_1", 9f, g + 2.4f, 4.5f, PlatThick);
        Ledge(root, "Deck_2", 9f, g + 4.8f, 4.5f, PlatThick);
        Ledge(root, "Deck_3", 9f, g + 7.2f, 4.5f, PlatThick);
        Solid(root, "ShaftWall", new Vector3(13.5f, g + 3.4f, 0f), new Vector2(WallThick, 6.8f));
        Ledge(root, "Plat_4", 16.5f, g + 7.2f, 4f, PlatThick);

        // 4구간 — 무너지는 발판을 밟고 내려오는 마무리. 멈춰 서면 발밑이 사라지므로 리듬이 끊기지 않는다.
        Crumble(root, "Crumble_1", 23f, g + 4.8f, 3f);
        Crumble(root, "Crumble_2", 29f, g + 2.4f, 3f);

        // 코스 전체가 구덩이 위라, 어디서 헛디디든 방 리스폰으로 되돌린다.
        TriggerVolume(root, "FallZone", new Vector3(0f, g - 5f, 0f), new Vector2(w - 2f, 3f))
            .AddComponent<DungeonFallZone>();

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 1.5f, a, 0f));
        Transform exit = Anchor(root, "ExitAnchor", new Vector3(w / 2f - 1.5f, a, 0f));
        Transform branch = Anchor(root, "BranchAnchor", new Vector3(9f, g + 7.2f + 1.1f, 0f)); // 탑 꼭대기에서 곁가지가 갈라진다.
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 4f, a, 0f));

        WireRoom(root, DungeonRoom.RoomRole.PlatformingHazard, entry, exit, branch, respawn, trigger, null, null);
        Save(root, "Room_Obby_A");
    }

    // B — "붕괴 질주". 발판이 전부 무너지는 종류라, 멈춰 서는 것 자체가 실패가 된다.
    // A 가 "정확히 밟기"를 묻는다면 이 방은 "멈추지 않기"를 묻는다. 대신 간격을 2\~3으로 좁게 잡아
    // 점프 자체는 쉽게 두었다. 계속 달려야 하는 압박과 어려운 점프를 동시에 주면 금세 지친다.
    // 붕괴 구간 사이에는 무너지지 않는 쉼터를 둬서 숨 돌리고 다음 구간을 읽을 시간을 준다.
    static void BuildObbyB() {
        const float w = 76f, h = 18f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom("Room_Obby_B", w, h, out BoxCollider2D trigger);
        Ceiling(root, w, h);

        Ledge(root, "Ledge_Start", -34f, g, 8f, WallThick);
        Ledge(root, "Ledge_End", 34.75f, g, 6.5f, WallThick);

        // 1구간 — 평지 붕괴 다리. 네 장이 연달아 무너지므로 한 번 발을 디디면 끝까지 가야 한다.
        // 이 구간만 무너지는 시간을 1초로 늘렸다. 네 번 연속은 0.8초로는 첫 시도에 거의 못 넘는다.
        Crumble(root, "Crumble_1", -26f, g, 3f, 1f);
        Crumble(root, "Crumble_2", -20.5f, g, 3f, 1f);
        Crumble(root, "Crumble_3", -15f, g, 3f, 1f);
        Crumble(root, "Crumble_4", -9.5f, g, 3f, 1f);

        Ledge(root, "Rest_1", -4f, g, 4f, PlatThick); // 무너지지 않는 쉼터.

        // 2구간 — 오르는 붕괴 계단.
        Crumble(root, "Crumble_5", 2.5f, g + 2.4f, 3f, 0.9f);
        Crumble(root, "Crumble_6", 8.5f, g + 4.8f, 3f, 0.9f);

        Ledge(root, "Rest_2", 15f, g + 4.8f, 5f, PlatThick);

        // 3구간 — 내려오는 붕괴 계단.
        Crumble(root, "Crumble_7", 22f, g + 2.4f, 3f, 0.9f);
        Crumble(root, "Crumble_8", 28f, g, 3f, 0.9f);

        TriggerVolume(root, "FallZone", new Vector3(0f, g - 5f, 0f), new Vector2(w - 2f, 3f))
            .AddComponent<DungeonFallZone>();

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 1.5f, a, 0f));
        Transform exit = Anchor(root, "ExitAnchor", new Vector3(w / 2f - 1.5f, a, 0f));
        Transform branch = Anchor(root, "BranchAnchor", new Vector3(15f, g + 4.8f + 1.1f, 0f)); // 쉼터 위에서 갈라진다.
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 4f, a, 0f));

        WireRoom(root, DungeonRoom.RoomRole.PlatformingHazard, entry, exit, branch, respawn, trigger, null, null);
        Save(root, "Room_Obby_B");
    }

    // C — "이동 발판 릴레이". 정지 발판과 이동 발판을 번갈아 놓아, 뛰는 실력보다 기다리는 타이밍을 묻는다.
    // 각 이동 발판은 가까운 끝에 와 있을 때만 건너탈 수 있고(간격 2.75), 먼 끝에 있을 때 뛰면 간격이
    // 8을 넘어 절대 닿지 않는다. 그래서 "기다리다 탄다"가 강제된다.
    // 발판마다 속도와 출발 지연을 어긋나게 둬서, 네 번의 대기가 같은 리듬으로 반복되지 않게 했다.
    static void BuildObbyC() {
        const float w = 76f, h = 18f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom("Room_Obby_C", w, h, out BoxCollider2D trigger);
        Ceiling(root, w, h);

        Ledge(root, "Ledge_Start", -34.5f, g, 7f, WallThick);
        Ledge(root, "Ledge_End", 36f, g, 4f, WallThick);

        Mover(root, "Mover_1", -26f, g, 3.5f, new Vector2(5f, 0f), 2f);
        Ledge(root, "Perch_1", -15f, g + 2.4f, 3f, PlatThick);

        Mover(root, "Mover_2", -9f, g + 2.4f, 3.5f, new Vector2(6f, 0f), 2.6f, startDelay: 1f);
        Ledge(root, "Perch_2", 3f, g + 2.4f, 3f, PlatThick);

        Mover(root, "Mover_3", 9f, g + 4.8f, 3.5f, new Vector2(6f, 0f), 2.2f, startDelay: 0.5f);
        Ledge(root, "Perch_3", 21f, g + 4.8f, 3f, PlatThick);

        Mover(root, "Mover_4", 27f, g + 2.4f, 3.5f, new Vector2(3f, 0f), 1.8f);

        TriggerVolume(root, "FallZone", new Vector3(0f, g - 5f, 0f), new Vector2(w - 2f, 3f))
            .AddComponent<DungeonFallZone>();

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 1.5f, a, 0f));
        Transform exit = Anchor(root, "ExitAnchor", new Vector3(w / 2f - 1.5f, a, 0f));
        Transform branch = Anchor(root, "BranchAnchor", new Vector3(21f, g + 4.8f + 1.1f, 0f));
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 3.5f, a, 0f));

        WireRoom(root, DungeonRoom.RoomRole.PlatformingHazard, entry, exit, branch, respawn, trigger, null, null);
        Save(root, "Room_Obby_C");
    }

    // D — "가시밭 건너기". 다른 오비 방과 달리 바닥이 끝까지 이어져 있어, 실패해도 떨어져 죽지 않는다.
    // 대신 밑이 가시밭이라 헛디디면 자아 게이지가 깎인 채로 계속 가게 된다. "다시 하기"가 아니라
    // "손해를 안고 전진하기"라는 다른 종류의 실패를 만들어, 낙사 일변도인 다른 오비 방과 리듬을 다르게 했다.
    // 급하면 그냥 가시밭을 걸어서 돌파할 수도 있다. 체력을 얼마나 내줄지 스스로 정하는 방이다.
    static void BuildObbyD() {
        const float w = 76f, h = 18f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom("Room_Obby_D", w, h, out BoxCollider2D trigger);
        Ceiling(root, w, h);
        Ledge(root, "Floor", 0f, g, w, WallThick); // 구덩이가 없는 유일한 오비 방.

        // 1구간 — 발판 두 장으로 건너는 가시밭.
        Spikes(root, "Spikes_1", -20f, g + SpikeThick, 10f, 10);
        Ledge(root, "Plat_1", -22.5f, g + 2.4f, 2.5f, PlatThick);
        Ledge(root, "Plat_2", -18.5f, g + 2.4f, 2.5f, PlatThick);

        // 2구간 — 가장 넓은 가시밭. 이동 발판을 기다렸다 타고 건넌다.
        Spikes(root, "Spikes_2", 0f, g + SpikeThick, 12f, 10);
        Mover(root, "Mover_1", -3f, g + 2.4f, 3f, new Vector2(6f, 0f), 2f);

        // 3구간 — 다시 발판 두 장.
        Spikes(root, "Spikes_3", 20f, g + SpikeThick, 10f, 10);
        Ledge(root, "Plat_3", 17.5f, g + 2.4f, 2.5f, PlatThick);
        Ledge(root, "Plat_4", 21.5f, g + 2.4f, 2.5f, PlatThick);

        // 곁가지용 선반. 바닥에서 한 번에 오를 수 있는 높이라 가시를 밟지 않고도 닿는다.
        Ledge(root, "Alcove", 30f, g + 4.8f, 3f, PlatThick);

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 1.5f, a, 0f));
        Transform exit = Anchor(root, "ExitAnchor", new Vector3(w / 2f - 1.5f, a, 0f));
        Transform branch = Anchor(root, "BranchAnchor", new Vector3(30f, g + 4.8f + 1.1f, 0f));
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 4f, a, 0f));

        WireRoom(root, DungeonRoom.RoomRole.PlatformingHazard, entry, exit, branch, respawn, trigger, null, null);
        Save(root, "Room_Obby_D");
    }

    #endregion
    #region 방 빌드 — 수직 갱도

    // 발판 하나의 배치값. YUp 은 방 바닥면(g) 기준 상대 높이.
    readonly struct VPlat {
        public readonly float X, YUp, W;
        public VPlat(float x, float yUp, float w) { X = x; YUp = yUp; W = w; }
    }

    // A: 오른쪽으로 올랐다가 왼쪽으로 되꺾어 다시 오른쪽 출구로 가는 갈지자 경사로.
    // 중간에 넓은 쉼터(6번)를 두고, 되꺾이는 지점에 곁가지 니치를 붙였다. 헛디뎌도 아래 경사면에
    // 걸리거나 바닥으로 떨어져 다시 오르면 되도록 단차는 2.4 이하로만 둔다.
    static void BuildVerticalA() {
        VPlat[] path = {
            new(-8f, 2.6f, 5f),  new(-3.5f, 4.6f, 5f), new(1f, 6.6f, 5f),   new(5.5f, 8.6f, 6f),
            new(8f, 11f, 4f),    new(3f, 12.8f, 7f),
            new(-2.5f, 14.8f, 5f), new(-7.5f, 16.8f, 5f), new(-9f, 19.2f, 4f),
            new(-4.5f, 21f, 4f), new(0.5f, 22.9f, 4f), new(5.5f, 24.9f, 5f), new(9.5f, 26.9f, 5f),
        };
        BuildVertical("Room_Vertical_A", path, alcove: new VPlat(-11f, 20f, 3f));
    }

    // B: S자로 크게 휘어 오르며, 가운데에 좁은 디딤돌 구간(6~8번)을 끼워 리듬에 강약을 준다.
    // 넓은 쉼터를 두 곳(5번, 10번) 둔다.
    static void BuildVerticalB() {
        VPlat[] path = {
            new(7f, 2.4f, 5f),   new(2.5f, 4.4f, 6f),  new(-2.5f, 6.2f, 6f), new(-8f, 8.2f, 5f),
            new(-4.5f, 10.4f, 7f),
            new(0.5f, 12.4f, 3f), new(5f, 14.2f, 2.5f), new(9f, 16f, 4f),
            new(4.5f, 18f, 6f),  new(-0.5f, 20f, 8f),
            new(-5.5f, 22f, 5f), new(-1f, 24f, 4f),   new(4f, 26f, 5f),     new(9f, 28f, 5f),
        };
        BuildVertical("Room_Vertical_B", path, alcove: new VPlat(-11.5f, 9.2f, 3f));
    }

    static void BuildVertical(string prefabName, VPlat[] path, VPlat alcove) {
        const float w = 28f, h = 38f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom(prefabName, w, h, out BoxCollider2D trigger);
        Platform(root, "Floor", new Vector3(0f, g - WallThick / 2f, 0f), new Vector2(w, WallThick));

        for (int i = 0; i < path.Length; i++) {
            VPlat p = path[i];
            Platform(root, $"Plat_{i + 1}", new Vector3(p.X, g + p.YUp, 0f), new Vector2(p.W, PlatThick));
        }

        // 오르는 길에서 살짝 벗어난 막다른 니치. 곁가지(비밀방)가 여기 붙고, 없어도 잠깐 쉬어 가는 자리.
        Platform(root, "Alcove", new Vector3(alcove.X, g + alcove.YUp, 0f), new Vector2(alcove.W, PlatThick));

        float topY = g + path[path.Length - 1].YUp;

        // 맨 위 발판에서 오른쪽 퇴장 구멍까지 이어 주는 선반. 없으면 꼭대기에서 다음 방까지 허공이 뜬다.
        Platform(root, "ExitLedge", new Vector3(w / 4f + 1f, topY, 0f), new Vector2(w / 2f, PlatThick));

        // 옆벽은 헛디딤 방지용이지만 통째로 세우면 앞뒤 방과 이어지지 않는다.
        // 왼쪽은 아래(입장 통로)를, 오른쪽은 위(퇴장 통로)를 비운 반쪽짜리로 세운다.
        float roomTop = h / 2f;
        float roomBottom = -h / 2f;
        float entryOpeningTop = g + 4.5f;          // 왼쪽 아래로 걸어 들어오는 구멍의 천장.
        float exitOpeningBottom = topY - 1.5f;     // 오른쪽 위로 걸어 나가는 구멍의 바닥(맨 위 발판 아래).

        Solid(root, "Wall_L", new Vector3(-w / 2f + WallThick / 2f, (entryOpeningTop + roomTop) / 2f, 0f),
            new Vector2(WallThick, roomTop - entryOpeningTop));
        Solid(root, "Wall_R", new Vector3(w / 2f - WallThick / 2f, (roomBottom + exitOpeningBottom) / 2f, 0f),
            new Vector2(WallThick, exitOpeningBottom - roomBottom));

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 3f, a, 0f));
        // 퇴장 소켓은 오른쪽 퇴장 구멍 앞, 다른 방과 같은 "오른쪽 끝에서 1.5" 자리에 둔다.
        // 예전에는 가로 가운데(x=0)에 있어서 다음 방이 갱도 오른쪽 절반을 통째로 덮었고, 그 방의 발판·
        // 이동 발판·낙사 트리거가 갱도 안으로 내려와 오르던 플레이어가 낙사 판정에 걸렸다.
        Transform exit = Anchor(root, "ExitAnchor", new Vector3(w / 2f - 1.5f, topY + 1.4f, 0f));
        Transform branch = Anchor(root, "BranchAnchor", new Vector3(alcove.X, g + alcove.YUp + 0.3f, 0f));
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 3f, a, 0f));

        WireRoom(root, DungeonRoom.RoomRole.VerticalClimb, entry, exit, branch, respawn, trigger, null, null);
        Save(root, prefabName);
    }

    #endregion
    #region 방 빌드 — 전투방

    enum CombatVariant { OneHighPlatform, TwoPlatforms, WideFlat, TwoTiers, CenterPeak }

    static void BuildCombat(string prefabName, CombatVariant variant) {
        float w = variant == CombatVariant.WideFlat ? 48f : 44f;
        const float h = 18f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom(prefabName, w, h, out BoxCollider2D trigger);
        Ceiling(root, w, h);

        var spawns = new List<Vector3>();

        switch (variant) {
            case CombatVariant.OneHighPlatform:
                Platform(root, "Floor", new Vector3(0f, g - WallThick / 2f, 0f), new Vector2(w, WallThick));
                Platform(root, "Plat_C", new Vector3(0f, g + Step, 0f), new Vector2(9f, PlatThick));
                spawns.Add(new Vector3(-15f, g + 1f, 0f));
                spawns.Add(new Vector3(-8f, g + 1f, 0f));
                spawns.Add(new Vector3(0f, g + Step + 1f, 0f));
                spawns.Add(new Vector3(8f, g + 1f, 0f));
                spawns.Add(new Vector3(15f, g + 1f, 0f));
                break;

            case CombatVariant.TwoPlatforms:
                Platform(root, "Floor", new Vector3(0f, g - WallThick / 2f, 0f), new Vector2(w, WallThick));
                Platform(root, "Plat_L", new Vector3(-10f, g + Step, 0f), new Vector2(8f, PlatThick));
                Platform(root, "Plat_R", new Vector3(10f, g + Step, 0f), new Vector2(8f, PlatThick));
                spawns.Add(new Vector3(-16f, g + 1f, 0f));
                spawns.Add(new Vector3(-10f, g + Step + 1f, 0f));
                spawns.Add(new Vector3(-4f, g + 1f, 0f));
                spawns.Add(new Vector3(4f, g + 1f, 0f));
                spawns.Add(new Vector3(10f, g + Step + 1f, 0f));
                spawns.Add(new Vector3(16f, g + 1f, 0f));
                break;

            case CombatVariant.WideFlat:
                Platform(root, "Floor", new Vector3(0f, g - WallThick / 2f, 0f), new Vector2(w, WallThick));
                for (int i = 0; i < 6; i++) {
                    float x = -20f + i * 8f;
                    spawns.Add(new Vector3(x, g + 1f, 0f));
                }
                break;

            case CombatVariant.TwoTiers:
                // 양 끝은 바닥 높이로 두어 앞뒤 방과 이어지게 하고, 가운데만 한 단 올린 대(臺)를 둔다.
                Platform(root, "Floor", new Vector3(0f, g - WallThick / 2f, 0f), new Vector2(w, WallThick));
                Platform(root, "Dais", new Vector3(4f, g + Step - PlatThick / 2f, 0f), new Vector2(18f, PlatThick));
                Platform(root, "Dais_Step", new Vector3(-6.5f, g + Step * 0.5f, 0f), new Vector2(3f, PlatThick));
                spawns.Add(new Vector3(-16f, g + 1f, 0f));
                spawns.Add(new Vector3(-9f, g + 1f, 0f));
                spawns.Add(new Vector3(2f, g + Step + 1f, 0f));
                spawns.Add(new Vector3(9f, g + Step + 1f, 0f));
                spawns.Add(new Vector3(18f, g + 1f, 0f));
                break;

            case CombatVariant.CenterPeak:
                Platform(root, "Floor", new Vector3(0f, g - WallThick / 2f, 0f), new Vector2(w, WallThick));
                Platform(root, "Step_L", new Vector3(-6f, g + Step * 0.8f, 0f), new Vector2(3f, PlatThick));
                Platform(root, "Step_R", new Vector3(6f, g + Step * 0.8f, 0f), new Vector2(3f, PlatThick));
                Platform(root, "Peak", new Vector3(0f, g + Step * 1.8f, 0f), new Vector2(6f, PlatThick));
                spawns.Add(new Vector3(-16f, g + 1f, 0f));
                spawns.Add(new Vector3(-6f, g + Step * 0.8f + 1f, 0f));
                spawns.Add(new Vector3(0f, g + Step * 1.8f + 1f, 0f));
                spawns.Add(new Vector3(6f, g + Step * 0.8f + 1f, 0f));
                spawns.Add(new Vector3(16f, g + 1f, 0f));
                break;
        }

        // 진행 방향(오른쪽) 차단 바리어 — 입장 시 켜지고 전멸하면 꺼진다.
        GameObject barrierObj = new("LockBarrier");
        barrierObj.transform.SetParent(root.transform, false);
        barrierObj.transform.localPosition = new Vector3(w / 2f - 1.5f, g + h / 2f - 1f, 0f);
        var barrier = barrierObj.AddComponent<BoxCollider2D>();
        barrier.size = new Vector2(1f, h - 2f);
        barrier.enabled = false;

        DungeonRoomSpawner spawner = AddSpawner(root, spawns);

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 1.5f, a, 0f));
        Transform exit = Anchor(root, "ExitAnchor", new Vector3(w / 2f - 1.5f, a, 0f));
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 3f, a, 0f));

        WireRoom(root, DungeonRoom.RoomRole.CombatArena, entry, exit, null, respawn, trigger, barrier, spawner);
        Save(root, prefabName);
    }

    #endregion
    #region 방 빌드 — 곁가지

    static void BuildSecretA() {
        const float w = 16f, h = 12f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom("Room_Secret_A", w, h, out BoxCollider2D trigger);
        Frame(root, w, h, g, leftWall: true, rightWall: true, ceiling: true);

        GameObject pickup = TriggerVolume(root, "BonusShard", new Vector3(0f, g + 1.5f, 0f), new Vector2(1.6f, 1.6f));
        pickup.AddComponent<DungeonBonusPickup>();

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 2f, a, 0f));
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 2f, a, 0f));

        WireRoom(root, DungeonRoom.RoomRole.SecretBranch, entry, null, null, respawn, trigger, null, null);
        EnsureBranchDoors(root);
        Save(root, "Room_Secret_A");
    }

    static void BuildSecretB() {
        const float w = 16f, h = 12f;
        float g = GroundY(h);
        float a = AnchorY(h);

        GameObject root = NewRoom("Room_Secret_B", w, h, out BoxCollider2D trigger);
        Frame(root, w, h, g, leftWall: true, rightWall: true, ceiling: true);
        Platform(root, "Plat", new Vector3(3f, g + Step, 0f), new Vector2(4f, PlatThick));

        GameObject pickup = TriggerVolume(root, "BonusShard", new Vector3(3f, g + Step + 1.4f, 0f), new Vector2(1.6f, 1.6f));
        pickup.AddComponent<DungeonBonusPickup>();

        Transform entry = Anchor(root, "EntryAnchor", new Vector3(-w / 2f + 2f, a, 0f));
        Transform respawn = Anchor(root, "RespawnPoint", new Vector3(-w / 2f + 2f, a, 0f));

        WireRoom(root, DungeonRoom.RoomRole.SecretBranch, entry, null, null, respawn, trigger, null, null);
        EnsureBranchDoors(root);
        Save(root, "Room_Secret_B");
    }

    // 이미 만들어진 곁가지 방 프리팹에 드나드는 문 한 쌍만 채워 넣는다.
    //
    // Build All Rooms 를 다시 돌리지 않고 따로 두는 이유: 방 지형은 그 뒤 타일맵으로 전환하고 손으로 칠해
    // 두었기 때문에, 통째로 다시 찍으면 그 작업이 전부 날아간다. 여기서는 프리팹을 열어 없는 것만 채운다.
    [MenuItem("Tools/FCC/Dungeon/Patch Secret Branch Doors")]
    public static void PatchSecretBranchDoors() {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[Dungeon] 프리팹 편집 모드를 닫고 다시 실행하세요.");
            return;
        }

        int patched = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || !asset.TryGetComponent(out DungeonRoom assetRoom)) continue;
            if (assetRoom.role != DungeonRoom.RoomRole.SecretBranch) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            EnsureBranchDoors(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            PrefabUtility.UnloadPrefabContents(root);
            patched++;
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Dungeon] 곁가지 방 {patched}개에 드나드는 문(EntranceDoor · ReturnDoor)을 채웠습니다.");
    }

    const float DoorW = 1.6f, DoorH = 2.6f;

    // 곁가지 방 안에 문 두 짝을 두고 DungeonRoom 에 연결한다. 이미 있으면 위치는 손댄 그대로 두고 연결만 맞춘다.
    //  - ReturnDoor   : 입장 지점 바로 뒤. 들어온 자리에서 곧장 돌아 나갈 수 있게 한다.
    //  - EntranceDoor : 런타임에 생성기가 부모 방의 branchAnchor 로 옮긴다. 프리팹에서는 방 왼쪽 바깥에 세워 두어
    //                   방 안의 문과 헷갈리지 않게 한다.
    static void EnsureBranchDoors(GameObject root) {
        DungeonRoom room = root.GetComponent<DungeonRoom>();
        BoxCollider2D area = room.roomTrigger as BoxCollider2D;
        float w = area != null ? area.size.x : 16f;
        float h = area != null ? area.size.y : 12f;
        float g = GroundY(h);
        float entryX = room.entryAnchor != null ? room.entryAnchor.localPosition.x : -w / 2f + 2f;

        Material mat = EnsureMaterial("BranchDoor", new Color(0.24f, 0.17f, 0.14f));

        room.returnDoor = EnsureDoor(root, "ReturnDoor", new Vector3(entryX, g, 0f), "돌아가기", mat);
        room.entranceDoor = EnsureDoor(root, "EntranceDoor", new Vector3(-w / 2f - 2f, g, 0f), "들어가기", mat);
        EditorUtility.SetDirty(room);
    }

    // 문 피벗은 발밑이다. 생성기가 부모 방 바닥을 찾아 그 높이에 그대로 세우기 때문이다.
    static DungeonBranchDoor EnsureDoor(GameObject root, string name, Vector3 local, string label, Material mat) {
        Transform found = root.transform.Find(name);
        if (found != null && found.TryGetComponent(out DungeonBranchDoor existing)) return existing;

        GameObject door = new(name);
        door.transform.SetParent(root.transform, false);
        door.transform.localPosition = local;

        BoxCollider2D col = door.AddComponent<BoxCollider2D>();
        col.isTrigger = true; // PlayerInteractor 는 트리거만 탐지한다. 막히는 문이 아니라 지나쳐 서는 문이다.
        col.size = new Vector2(DoorW, DoorH);
        col.offset = new Vector2(0f, DoorH / 2f);

        DungeonBranchDoor branchDoor = door.AddComponent<DungeonBranchDoor>();
        branchDoor.label = label;

        // 그레이박스 문짝. z 를 살짝 뒤로 빼 플레이어가 문 앞에 선 것처럼 보이게 한다.
        MirrorQuad(door, "Visual", new Vector3(0f, DoorH / 2f, 0.1f), new Vector2(DoorW, DoorH), 0f, mat);
        return branchDoor;
    }

    #endregion
    #region 게이트 전신 거울

    const float MirrorW = 1.6f, MirrorH = 3f; // 사람 키만 한 전신 거울.

    // 던전 입구 전신 거울의 그레이박스 프리팹을 만든다. 멀쩡한 거울 / 부서진 거울 두 벌을 한 오브젝트에
    // 넣어 두고 DungeonGateMirror 가 켜고 끈다. 아트가 나오면 Intact·Broken 안의 Quad 만 갈아 끼우면 된다.
    //
    // 파티클은 일부러 만들지 않는다. DungeonGateMirror 가 비어 있으면 HitVfx 의 파열을 대신 쓰므로
    // 지금도 "팡" 은 터지고, 전용 이펙트가 필요해지면 그때 프리팹을 만들어 인스펙터에 꽂으면 된다.
    [MenuItem("Tools/FCC/Dungeon/Build Gate Mirror Prefab")]
    public static void BuildGateMirror() {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[Dungeon] 프리팹 편집 모드를 닫고 다시 실행하세요.");
            return;
        }

        EnsureFolder(PrefabDir);
        Material frameMat = EnsureMaterial("GateMirrorFrame", new Color(0.18f, 0.16f, 0.22f));
        Material glassMat = EnsureMaterial("GateMirrorGlass", new Color(0.72f, 0.85f, 0.95f));

        GameObject root = new("GateMirror");

        // 멀쩡한 거울 — 어두운 테두리 위에 밝은 거울면. z 가 작을수록 카메라에 가깝다.
        GameObject intact = Child(root, "Intact", Vector3.zero);
        MirrorQuad(intact, "Frame", new Vector3(0f, 0f, 0.01f), new Vector2(MirrorW, MirrorH), 0f, frameMat);
        MirrorQuad(intact, "Glass", Vector3.zero, new Vector2(MirrorW - 0.35f, MirrorH - 0.35f), 0f, glassMat);

        // 부서진 거울 — 같은 테두리에 파편만 몇 조각 남는다.
        GameObject broken = Child(root, "Broken", Vector3.zero);
        MirrorQuad(broken, "Frame", new Vector3(0f, 0f, 0.01f), new Vector2(MirrorW, MirrorH), 0f, frameMat);
        MirrorQuad(broken, "Shard_1", new Vector3(-0.25f, 0.75f, 0f), new Vector2(0.5f, 1.1f), 14f, glassMat);
        MirrorQuad(broken, "Shard_2", new Vector3(0.3f, -0.1f, 0f), new Vector2(0.42f, 0.8f), -21f, glassMat);
        MirrorQuad(broken, "Shard_3", new Vector3(-0.15f, -0.95f, 0f), new Vector2(0.34f, 0.6f), 27f, glassMat);
        broken.SetActive(false); // DungeonGateMirror 가 Awake 에서도 끄지만, 씬 뷰에서 겹쳐 보이지 않도록 저장 상태부터 꺼 둔다.

        DungeonGateMirror mirror = root.AddComponent<DungeonGateMirror>();
        mirror.intactVisual = intact;
        mirror.brokenVisual = broken;
        EnsureEnterVfx(intact, mirror);

        PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/GateMirror.prefab");
        Object.DestroyImmediate(root);

        AssetDatabase.SaveAssets();
        Debug.Log("[Dungeon] GateMirror 프리팹을 만들었습니다. DungeonGate 의 자식으로 놓고 게이트의 mirror 에 연결하세요.");
    }

    // 이미 씬에 놓여 있는 GateMirror 에 진입 연출용 판(빛 띠 · 밝아지는 판)만 덧붙인다.
    //
    // Build 를 다시 돌리지 않고 이 메뉴를 따로 두는 이유: SaveAsPrefabAsset 으로 통째로 다시 찍으면
    // 프리팹 안의 오브젝트들이 새 fileID 를 받아, 씬 인스턴스에서 손으로 맞춰 둔 값과 바깥에서 꽂아 둔
    // 참조(DungeonGate.mirror 등)가 끊긴다. 여기서는 기존 프리팹을 열어 없는 것만 채우고 저장한다.
    [MenuItem("Tools/FCC/Dungeon/Patch Gate Mirror Enter VFX")]
    public static void PatchGateMirrorEnterVfx() {
        string path = $"{PrefabDir}/GateMirror.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        if (root == null) {
            Debug.LogError($"[Dungeon] '{path}' 를 열지 못했습니다. 먼저 Build Gate Mirror Prefab 을 실행하세요.");
            return;
        }

        DungeonGateMirror mirror = root.GetComponent<DungeonGateMirror>();
        GameObject intact = mirror != null && mirror.intactVisual != null
            ? mirror.intactVisual
            : root.transform.Find("Intact")?.gameObject;

        if (mirror == null || intact == null) {
            Debug.LogError("[Dungeon] GateMirror 프리팹에서 DungeonGateMirror 또는 Intact 를 찾지 못했습니다.");
            PrefabUtility.UnloadPrefabContents(root);
            return;
        }

        EnsureEnterVfx(intact, mirror);

        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
        AssetDatabase.SaveAssets();

        Debug.Log("[Dungeon] GateMirror 에 진입 연출(EnterGlow · EnterSweep)을 채웠습니다.");
    }

    // 거울면 위에 겹치는 두 장. 유리(z=0)보다 앞(z 가 작은 쪽)에 두어야 빛이 유리 위로 보인다.
    // 평소에는 꺼 두고 DungeonGateMirror 가 진입 순간에만 켠다.
    static void EnsureEnterVfx(GameObject intact, DungeonGateMirror mirror) {
        float glassW = MirrorW - 0.35f, glassH = MirrorH - 0.35f;

        Material glowMat = EnsureAdditiveMaterial("GateMirrorGlow", new Color(0.72f, 0.85f, 0.95f));
        Material sweepMat = EnsureAdditiveMaterial("GateMirrorSweep", new Color(1f, 0.97f, 0.92f));

        GameObject glow = EnsureQuad(intact, "EnterGlow", new Vector3(0f, 0f, -0.01f), new Vector2(glassW, glassH), glowMat);
        GameObject sweep = EnsureQuad(intact, "EnterSweep", new Vector3(0f, 0f, -0.02f), new Vector2(glassW, 0.35f), sweepMat);

        mirror.enterGlow = glow.GetComponent<MeshRenderer>();
        mirror.enterSweep = sweep.GetComponent<MeshRenderer>();
        mirror.enterSweepTravel = glassH; // 유리 위끝에서 아래끝까지 훑는다.
    }

    static GameObject EnsureQuad(GameObject parent, string name, Vector3 local, Vector2 size, Material material) {
        Transform found = parent.transform.Find(name);
        if (found == null) {
            MirrorQuad(parent, name, local, size, 0f, material);
            found = parent.transform.Find(name);
        }
        else {
            // 이미 있으면 위치·크기는 손댄 그대로 두고 머티리얼만 맞춘다.
            found.GetComponent<MeshRenderer>().sharedMaterial = material;
        }

        found.gameObject.SetActive(false);
        return found.gameObject;
    }

    // 빛 연출용 머티리얼. URP Unlit 은 기본이 불투명이라 알파를 내려도 그대로 보인다.
    // 인스펙터에서 Surface Type 을 Transparent · Blending Mode 를 Additive 로 고른 것과 같은 설정을 코드로 적는다.
    static Material EnsureAdditiveMaterial(string name, Color color) {
        Material mat = EnsureMaterial(name, new Color(color.r, color.g, color.b, 0f)); // 평소 알파 0 — 켜자마자 번쩍이지 않도록.

        mat.SetFloat("_Surface", 1f); // 0 Opaque / 1 Transparent
        mat.SetFloat("_Blend", 1f);   // 0 Alpha / 1 Premultiply / 2 Additive … URP 버전에 따라 다르므로 아래 블렌드도 직접 지정한다.
        mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
        mat.SetFloat("_ZWrite", 0f);
        mat.SetFloat("_AlphaClip", 0f);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        EditorUtility.SetDirty(mat);
        return mat;
    }

    static GameObject Child(GameObject parent, string name, Vector3 local) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = local;
        return obj;
    }

    static void MirrorQuad(GameObject parent, string name, Vector3 local, Vector2 size, float tilt, Material material) {
        GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Quad);
        obj.name = name;
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = local;
        obj.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);

        Object.DestroyImmediate(obj.GetComponent<Collider>()); // 거울은 보여 주기만 한다. 상호작용 판정은 게이트의 콜라이더가 맡는다.
        obj.GetComponent<MeshRenderer>().sharedMaterial = material;
    }

    // 그레이박스 색을 구분하려면 머티리얼이 필요하다. Unlit 을 쓰는 이유는 2D 씬에 3D 조명이 없어
    // Lit 로 두면 거울이 새까맣게 보이기 때문이다.
    static Material EnsureMaterial(string name, Color color) {
        const string dir = "Assets/_Project/Assets/Materials";
        EnsureFolder(dir);

        string path = $"{dir}/{name}.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            AssetDatabase.CreateAsset(mat, path);
        }

        mat.SetColor("_BaseColor", color);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    #endregion
    #region 씬 던전 리그

    [MenuItem("Tools/FCC/Dungeon/Place Dungeon Rig In Scene")]
    public static void PlaceDungeonRig() {
        DungeonGenerator generator = Object.FindAnyObjectByType<DungeonGenerator>(FindObjectsInactive.Include);

        if (generator == null) {
            GameObject rig = new("DungeonRig");
            Undo.RegisterCreatedObjectUndo(rig, "Place Dungeon Rig");

            GameObject origin = new("DungeonOrigin");
            origin.transform.SetParent(rig.transform, false);
            origin.transform.position = new Vector3(500f, -500f, 0f); // **오버월드와 겹치지 않는 곳으로 옮기세요.**

            generator = rig.AddComponent<DungeonGenerator>();
            generator.dungeonOrigin = origin.transform;
            rig.AddComponent<DungeonManager>();

            GameObject gateObj = new("DungeonGate_Backworld");
            gateObj.transform.SetParent(rig.transform, false);
            var gateCol = gateObj.AddComponent<BoxCollider2D>();
            gateCol.isTrigger = true;
            gateCol.size = new Vector2(2f, 3f);
            gateObj.AddComponent<DungeonGate>().generator = generator;

            Selection.activeGameObject = rig;
        }

        AssignPools(generator);
        EditorSceneManager.MarkSceneDirty(generator.gameObject.scene);
        Debug.Log("[Dungeon] DungeonGenerator 풀을 Prefabs/Dungeon 의 방 프리팹으로 채웠습니다. DungeonOrigin·outsideBounds·게이트 위치를 확인한 뒤 씬을 저장하세요.");
    }

    // 방 프리팹 폴더를 통째로 훑어, 프리팹이 스스로 선언한 DungeonRoom.role 로 분류해 담는다.
    //
    // 예전에는 프리팹 이름을 여기에 나열했다. 그러면 새 방을 만든 뒤 이 메뉴를 다시 누를 때마다 목록에
    // 없는 방이 조용히 빠지고, 인스펙터에 손으로 넣어 둔 것까지 통째로 덮어써 사라졌다. 실제로 오비 방
    // 4종을 추가했을 때 이 목록이 그대로여서 다시 실행하면 전부 날아가는 상태였다.
    // 이름이 아니라 역할로 분류하면 방을 새로 만들어도 이 파일을 고칠 일이 없다.
    static void AssignPools(DungeonGenerator g) {
        var buckets = new Dictionary<DungeonRoom.RoomRole, List<GameObject>>();
        foreach (DungeonRoom.RoomRole role in System.Enum.GetValues(typeof(DungeonRoom.RoomRole))) {
            buckets[role] = new List<GameObject>();
        }

        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir })) {
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }
        paths.Sort(); // FindAssets 의 순서는 보장되지 않는다. 인스펙터에서 목록을 눈으로 훑기 쉽도록 정렬해 둔다.

        foreach (string path in paths) {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;

            // 손으로 방을 만들 때 복제해 쓰는 원본이라 실제 던전에는 나오면 안 된다.
            if (prefab.name.EndsWith("_Template")) continue;

            // 게이트 거울처럼 방이 아닌 프리팹이 같은 폴더에 있을 수 있다.
            if (!prefab.TryGetComponent(out DungeonRoom room)) continue;

            // 소켓이 없으면 생성기가 앞뒤로 이어 붙이지 못한다. 곁가지는 막다른 방이라 exitAnchor 가 없어도 된다.
            bool needsExit = room.role != DungeonRoom.RoomRole.SecretBranch;
            if (room.entryAnchor == null || (needsExit && room.exitAnchor == null)) {
                Debug.LogWarning($"[Dungeon] '{prefab.name}' 은 소켓(entryAnchor/exitAnchor)이 비어 있어 풀에서 제외했습니다.", prefab);
                continue;
            }

            // 곁가지는 본 동선과 떨어진 곳에 만들어지므로 문이 없으면 들어갈 길이 없다.
            if (!needsExit && (room.entranceDoor == null || room.returnDoor == null)) {
                Debug.LogWarning($"[Dungeon] '{prefab.name}' 은 곁가지 문(entranceDoor/returnDoor)이 비어 있어 풀에서 제외했습니다. Tools ▸ FCC ▸ Dungeon ▸ Patch Secret Branch Doors 를 실행하세요.", prefab);
                continue;
            }

            buckets[room.role].Add(prefab);
        }

        g.entryRoomPrefabs = buckets[DungeonRoom.RoomRole.Entry];
        g.exitRoomPrefabs = buckets[DungeonRoom.RoomRole.Exit];
        g.hazardRoomPrefabs = buckets[DungeonRoom.RoomRole.PlatformingHazard];
        g.verticalRoomPrefabs = buckets[DungeonRoom.RoomRole.VerticalClimb];
        g.combatRoomPrefabs = buckets[DungeonRoom.RoomRole.CombatArena];
        g.secretRoomPrefabs = buckets[DungeonRoom.RoomRole.SecretBranch];

        // 비어 있는 역할이 있으면 생성 도중 그 단계가 통째로 건너뛰어진다. 그때 가서 원인을 찾기 어려우니 여기서 알린다.
        foreach (var pair in buckets) {
            if (pair.Value.Count == 0) Debug.LogWarning($"[Dungeon] '{pair.Key}' 역할의 방 프리팹이 하나도 없습니다. 먼저 Build All Rooms 를 실행하세요.");
        }

        EditorUtility.SetDirty(g);
    }

    #endregion
    #region 생성 도우미

    static float GroundY(float roomHeight) => -roomHeight / 2f + 1f;

    // 소켓·리스폰·워프 목표 Y. 플레이어 트랜스폼 피벗이 콜라이더(높이 약 2) 한가운데라, 바닥 표면보다
    // 몸통 반쯤 위에 두어야 입장·복귀 시 바닥에 파묻히지 않는다.
    static float AnchorY(float roomHeight) => GroundY(roomHeight) + 1.1f;

    static GameObject NewRoom(string name, float w, float h, out BoxCollider2D roomTrigger) {
        GameObject root = new(name);
        roomTrigger = root.AddComponent<BoxCollider2D>();
        roomTrigger.isTrigger = true;
        roomTrigger.size = new Vector2(w, h);

        // 방을 다시 찍을 때마다 통째로 새로 만들기 때문에, 여기서 붙이지 않으면 타일맵 그리드가 빠진 채 저장된다.
        TilemapGridBuilder.EnsureGrid(root);
        return root;
    }

    // 바닥 전폭 + 선택적 벽·천장.
    static void Frame(GameObject root, float w, float h, float g, bool leftWall, bool rightWall, bool ceiling) {
        Platform(root, "Floor", new Vector3(0f, g - WallThick / 2f, 0f), new Vector2(w, WallThick));
        if (ceiling) Ceiling(root, w, h);
        if (leftWall) Solid(root, "Wall_L", new Vector3(-w / 2f + WallThick / 2f, 0f, 0f), new Vector2(WallThick, h));
        if (rightWall) Solid(root, "Wall_R", new Vector3(w / 2f - WallThick / 2f, 0f, 0f), new Vector2(WallThick, h));
    }

    static void Ceiling(GameObject root, float w, float h) {
        Solid(root, "Ceiling", new Vector3(0f, h / 2f - WallThick / 2f, 0f), new Vector2(w, WallThick));
    }

    // 사방이 막히는 지형. 벽·천장처럼 어느 방향에서 와도 통과되면 안 되는 곳에만 쓴다.
    static void Solid(GameObject root, string name, Vector3 center, Vector2 size) {
        Paint(root, name, center, size, passThrough: false);
    }

    // 밟고 올라서는 가로 지형. 공중에 뜬 것만 통과형(OneWayPlatform)이 되고, 방 바닥에 붙은 것은
    // 막힘 지형으로 내려간다. 아래에서 올라올 일이 없는 지형까지 통과형으로 두면 옆에서 걸어 들어갈 때
    // 그대로 뚫려 버리고, 접지 판정도 통과형 분기를 타 점프가 죽는다.
    static void Platform(GameObject root, string name, Vector3 center, Vector2 size) {
        Paint(root, name, center, size, passThrough: true);
    }

    // 발판을 "밟는 면의 높이(topY)" 기준으로 놓는다. 코스를 짤 때는 단차를 눈으로 세면서 배치하게 되는데,
    // 중심 좌표로 적으면 두께의 절반을 매번 빼야 해서 숫자만 봐서는 단차가 맞는지 알 수 없다.
    static void Ledge(GameObject root, string name, float centerX, float topY, float width, float thickness) {
        Platform(root, name, new Vector3(centerX, topY - thickness / 2f, 0f), new Vector2(width, thickness));
    }

    // 방 정의에 적힌 사각형을 격자에 반올림해 타일로 칠한다. 이름은 칸을 하나도 못 채웠을 때 어느 지형이
    // 사라졌는지 알리기 위해 받는다 — 칠하고 나면 오브젝트가 남지 않아 씬 뷰에서 되짚을 수가 없다.
    static void Paint(GameObject root, string name, Vector3 center, Vector2 size, bool passThrough) {
        Tilemap solid = TilemapGridBuilder.Layer(root, TilemapGridBuilder.SolidLayerName);
        Tilemap platform = TilemapGridBuilder.Layer(root, TilemapGridBuilder.PlatformLayerName);

        var rect = new Rect(center.x - size.x / 2f, center.y - size.y / 2f, size.x, size.y);
        RectInt cells = TilemapGridBuilder.ToCells(rect);

        bool oneWay = passThrough && rect.yMin > RoomGround(root) + 0.01f;
        int painted = oneWay
            ? TilemapGridBuilder.Paint(platform, TilemapGridBuilder.PlatformTile, cells, solid)
            : TilemapGridBuilder.Paint(solid, TilemapGridBuilder.SolidTile, cells, platform);

        if (painted == 0) Debug.LogWarning($"[Dungeon] '{name}' 지형이 칸을 하나도 채우지 못했습니다. 위치·크기를 확인하세요.");
    }

    // 방 바닥면의 높이. GroundY 를 인자로 또 물려받는 대신 방 트리거 크기에서 되짚어, 지형을 칠하는 쪽이
    // 방 크기를 따로 들고 다니지 않게 한다(NewRoom 이 트리거를 방 크기 그대로 만들어 둔다).
    static float RoomGround(GameObject root) {
        BoxCollider2D trigger = root.GetComponent<BoxCollider2D>();
        return trigger != null ? trigger.offset.y - trigger.size.y / 2f + 1f : 0f;
    }

    // 왕복 이동 발판. 통과 발판 성질은 그대로 두고 이동만 얹는다.
    // offset 은 시작 위치 기준 상대 이동량이라, 방이 어디에 배치되든 같은 궤적을 그린다.
    static void Mover(GameObject root, string name, float centerX, float topY, float width,
        Vector2 offset, float speed, float startDelay = 0f) {
        GameObject obj = GimmickLedge(root, name, centerX, topY, width, UiTheme.TextBody); // 눈에 띄어야 타이밍을 잰다.

        // MovingPlatform 이 RequireComponent 로 알아서 붙이긴 하지만, 그 경우 기본값이 Dynamic 이라
        // 프리팹에 중력에 떨어지는 발판으로 저장된다. 여기서 먼저 붙여 Kinematic 으로 확정한다.
        obj.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;

        MovingPlatform mover = obj.AddComponent<MovingPlatform>();
        mover.moveOffset = offset;
        mover.speed = speed;
        mover.startDelay = startDelay;
    }

    // 밟으면 잠시 뒤 무너졌다가 되살아나는 발판. 기본값(0.4초)보다 넉넉히 잡는 이유는, 오비에서
    // 떨어지면 방 처음으로 되돌아가기 때문에 반응할 틈이 너무 짧으면 금세 지치기 때문이다.
    static void Crumble(GameObject root, string name, float centerX, float topY, float width, float fallDelay = 0.8f) {
        GameObject obj = GimmickLedge(root, name, centerX, topY, width, UiTheme.TextMuted); // 바랜 색 = 곧 무너질 것.

        CrumblingPlatform crumble = obj.AddComponent<CrumblingPlatform>();
        crumble.fallDelay = fallDelay;
        crumble.respawnDelay = 1.5f;
    }

    // 가시밭. 낙사와 달리 즉시 방을 다시 시작시키지 않고 자아 게이지만 깎아, "실수해도 계속 갈 수는 있는"
    // 실패를 만든다. 피격 무적(0.9초)이 걸리므로 밭을 가로질러도 한 번에 녹지는 않는다.
    static void Spikes(GameObject root, string name, float centerX, float topY, float width, int damage) {
        GameObject obj = Block(root, name, new Vector3(centerX, topY - SpikeThick / 2f, 0f),
            new Vector2(width, SpikeThick), UiTheme.AccentBright); // 위험은 포인트 컬러로.

        // 지형 레이어에서 빼 둔다. 트리거라 접지 판정에는 어차피 안 걸리지만, 지형 취급으로 남겨 두면
        // 나중에 레이어로 지형을 훑는 코드가 가시를 바닥으로 세게 된다.
        obj.layer = 0;

        BoxCollider2D col = obj.GetComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.usedByEffector = false;

        obj.AddComponent<DamageZone>().damage = damage;
    }

    // 특수 발판 하나. 지형과 달리 오브젝트로 남으므로 밟는 면 높이(topY)로 놓는 것까지 여기서 맞춘다.
    static GameObject GimmickLedge(GameObject root, string name, float centerX, float topY, float width, Color color) {
        GameObject obj = Block(root, name, new Vector3(centerX, topY - PlatThick / 2f, 0f),
            new Vector2(width, PlatThick), color);

        // PlatformEffector2D 와 usedByEffector 배선은 OneWayPlatform 이 잡아 준다. 다만 에디터에서
        // 붙이는 시점에는 Awake 가 돌지 않으므로, 프리팹에 값이 저장되도록 여기서 직접 한 번 적용한다.
        obj.AddComponent<OneWayPlatform>().Apply();
        return obj;
    }

    // 스케일로 크기를 잡는 사각 스프라이트 판. BoxCollider2D 는 붙는 순간 그림 크기(1×1)를 따라가므로
    // 스케일을 그대로 물려받아 보이는 것과 부딪히는 것이 정확히 같아진다.
    static GameObject Block(GameObject parent, string name, Vector3 center, Vector2 size, Color color) {
        GameObject obj = new(name);
        obj.layer = groundLayer;
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = center;
        obj.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = blockSprite;
        renderer.color = color;
        renderer.sortingOrder = GimmickOrder;

        // 크기를 붙는 순간의 그림 크기에 맡기지 않는다. 그림을 못 찾으면 0×0 콜라이더가 되어
        // "보이지도 밟히지도 않는" 발판이 조용히 만들어진다.
        obj.AddComponent<BoxCollider2D>().size = Vector2.one;
        return obj;
    }

    static GameObject TriggerVolume(GameObject parent, string name, Vector3 center, Vector2 size) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = center;
        var col = obj.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = size;
        return obj;
    }

    static Transform Anchor(GameObject parent, string name, Vector3 local) {
        GameObject obj = new(name);
        obj.transform.SetParent(parent.transform, false);
        obj.transform.localPosition = local;
        return obj.transform;
    }

    static DungeonRoomSpawner AddSpawner(GameObject root, List<Vector3> points) {
        var spawner = root.AddComponent<DungeonRoomSpawner>();
        for (int i = 0; i < points.Count; i++) {
            Transform p = Anchor(root, $"SpawnPoint_{i + 1}", points[i]);
            spawner.entries.Add(new DungeonRoomSpawner.SpawnEntry { point = p });
        }
        return spawner;
    }

    static void WireRoom(GameObject root, DungeonRoom.RoomRole role,
        Transform entry, Transform exit, Transform branch, Transform respawn,
        Collider2D roomTrigger, Collider2D lockBarrier, DungeonRoomSpawner spawner) {
        var room = root.AddComponent<DungeonRoom>();
        room.role = role;
        room.entryAnchor = entry;
        room.exitAnchor = exit;
        room.branchAnchor = branch;
        room.respawnPoint = respawn;
        room.roomTrigger = roomTrigger;
        room.lockBarrier = lockBarrier;
        room.spawner = spawner;
    }

    static void Save(GameObject root, string prefabName) {
        string path = $"{PrefabDir}/{prefabName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path); // 경로가 이미 있으면 GUID 를 유지한 채 덮어쓴다.
        Object.DestroyImmediate(root);
    }

    static void EnsureFolder(string path) {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    #endregion
}
#endif
