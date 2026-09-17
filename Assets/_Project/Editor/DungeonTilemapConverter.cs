#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// 이미 만들어 둔 던전 방 프리팹의 그레이박스 지형(Quad + BoxCollider2D)을 타일맵 칸으로 옮긴다.
//
// 방을 다시 찍지 않고 제자리에서 고치는 이유: 오비 방 4종에는 손으로 넣은 카메라 구역
// (`---------- CAMERA_AREA ----------` 아래 Player_AreaCameraController)이 들어 있는데,
// 방 빌더는 그것을 만들지 않는다. SaveAsPrefabAsset 으로 통째로 다시 찍으면 그 손질이 전부 날아가고
// 안쪽 오브젝트의 fileID 까지 바뀌어 바깥에서 꽂아 둔 참조가 끊긴다. 여기서는 지형 오브젝트만 골라
// 칸으로 옮기고 나머지는 건드리지 않는다.
//
// 어느 층에 칠할지는 "아래에서 뚫고 올라갈 수 있어야 하는가"로 가른다.
//   · 바닥·벽·천장처럼 막혀야 하는 지형 → Tilemap_Solid
//   · 공중에 뜬 발판처럼 통과해 올라서는 지형 → Tilemap_Platform (PlatformEffector2D)
// 막힘 지형을 Platform 층에 칠하면 벽이 옆·아래에서 뚫리고 접지 판정까지 통과형 분기를 타 점프가 죽는다.
//
// 특수 발판(이동·붕괴·가시)은 타일로 옮기지 않고 오브젝트로 남긴다. 칸 단위로 움직이거나 사라지는 것이
// 아니라 각자 콜라이더와 스크립트를 들고 따로 동작해야 하기 때문이다. 대신 보이는 것만 Quad 에서
// 스프라이트로 바꿔, 나중에 아트가 나오면 스프라이트만 갈아 끼우면 되게 한다.
//
// 사용법: Tools ▸ FCC ▸ Dungeon ▸ Convert Rooms To Tilemap  (멱등 — 이미 옮긴 방은 건너뛴다)
public static class DungeonTilemapConverter {
    #region 상수

    const string RoomPrefabDir = "Assets/_Project/Assets/Prefabs/Dungeon";

    // 특수 발판의 임시 그림. 칸 전체를 채우는 사각형이라 스케일을 그대로 늘리면 콜라이더와 크기가 정확히
    // 맞는다. 발판 모양(윗면 반 칸)인 Tile_Platform_M 을 쓰면 그림이 콜라이더보다 얇아 보인다.
    const string BlockSpritePath = "Assets/_Project/Assets/Sprites/Env/Tile_Solid_Inner.png";

    // 지형 타일(정렬 0)보다 한 단 앞. 겹치는 자리에서 특수 발판이 지형에 묻히지 않아야 한다.
    const int GimmickOrder = 1;

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Dungeon/Convert Rooms To Tilemap")]
    public static void ConvertRooms() {
        if (UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[Dungeon] 프리팹 편집 모드를 닫고 다시 실행하세요.");
            return;
        }

        TileBase solidTile = TilemapGridBuilder.SolidTile;
        TileBase platformTile = TilemapGridBuilder.PlatformTile;
        if (solidTile == null || platformTile == null) {
            Debug.LogError("[Dungeon] 지형 타일을 찾지 못했습니다. 먼저 Tools ▸ FCC ▸ Tilemap ▸ Build King And Pig Rule Tiles 를, 이어서 Weather Tileset ▸ Back World (사본) 을 실행하세요.");
            return;
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(BlockSpritePath);
        if (sprite == null) {
            Debug.LogWarning($"[Dungeon] 특수 발판 그림을 찾지 못했습니다: {BlockSpritePath} — 발판이 보이지 않게 됩니다.");
        }

        var paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RoomPrefabDir })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(path).StartsWith("Room_")) paths.Add(path);
        }
        paths.Sort();

        int converted = 0;
        var untouched = new List<string>();
        var failed = new List<string>();

        foreach (string path in paths) {
            string name = Path.GetFileNameWithoutExtension(path);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try {
                if (!Convert(root, solidTile, platformTile, sprite, name)) {
                    untouched.Add(name);
                    continue;
                }

                // Missing 스크립트가 남은 프리팹은 유니티가 저장을 거부한다. 성공 개수에 섞이면
                // 반영된 줄 알고 넘어가게 되므로 따로 센다.
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (success) converted++;
                else failed.Add(name);
            } finally {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Dungeon] 방 {converted}개의 지형을 타일맵으로 옮겼습니다. 바뀔 것이 없던 방: {untouched.Count}개.");
        if (failed.Count > 0) {
            Debug.LogWarning($"[Dungeon] 저장하지 못한 방: {string.Join(", ", failed)} — Missing 스크립트를 지운 뒤 다시 실행하세요.");
        }
    }

    #endregion
    #region 방 하나 변환

    static bool Convert(GameObject root, TileBase solidTile, TileBase platformTile, Sprite sprite, string roomName) {
        // 층 구성부터 다시 맞춘다. 예전에 만들어진 그리드는 통과 발판 층의 접촉 묶음이 켜진 채로 굳어
        // 있는데, 그대로 두면 방 안의 발판이 한 덩어리가 되어 위 발판에 머리가 닿는 순간 발밑이 꺼진다.
        TilemapGridBuilder.EnsureGrid(root);

        Tilemap solid = TilemapGridBuilder.Layer(root, TilemapGridBuilder.SolidLayerName);
        Tilemap platform = TilemapGridBuilder.Layer(root, TilemapGridBuilder.PlatformLayerName);
        if (solid == null || platform == null) {
            Debug.LogWarning($"[Dungeon] '{roomName}' 에서 타일맵 층을 찾지 못했습니다. 건너뜁니다.");
            return false;
        }

        float ground = GroundLevel(root, roomName);

        var blocks = new List<Transform>();
        Collect(root.transform, root.transform, blocks);
        if (blocks.Count == 0) return false;

        int solidCells = 0, platformCells = 0, gimmicks = 0;
        foreach (Transform block in blocks) {
            Rect rect = LocalRect(root.transform, block);

            if (TryGimmick(block.gameObject, sprite)) {
                gimmicks++;
                continue;
            }

            // 공중에 뜬 통과 발판만 Platform 층으로 보낸다. 바닥에 붙어 있으면(아랫면이 방 바닥면 이하)
            // 아래에서 올라올 일이 없으므로 막힘 지형으로 두는 편이 안전하다.
            bool passThrough = block.GetComponent<OneWayPlatform>() != null && rect.yMin > ground + 0.01f;
            RectInt cells = TilemapGridBuilder.ToCells(rect);

            if (passThrough) platformCells += TilemapGridBuilder.Paint(platform, platformTile, cells, solid);
            else solidCells += TilemapGridBuilder.Paint(solid, solidTile, cells, platform);

            Object.DestroyImmediate(block.gameObject);
        }

        Debug.Log($"[Dungeon] {roomName}: 막힘 {solidCells}칸 · 발판 {platformCells}칸 · 특수 발판 {gimmicks}개.");
        return true;
    }

    // 방 바닥면의 높이. 방 빌더의 GroundY(= -높이/2 + 1) 와 같은 값을 방 트리거 크기에서 되짚는다.
    static float GroundLevel(GameObject root, string roomName) {
        if (root.TryGetComponent(out BoxCollider2D trigger)) {
            return trigger.offset.y - trigger.size.y / 2f + 1f;
        }

        Debug.LogWarning($"[Dungeon] '{roomName}' 에 방 트리거(BoxCollider2D)가 없어 바닥 높이를 0 으로 봅니다. 층 배정이 어긋날 수 있습니다.");
        return 0f;
    }

    // 그레이박스 지형은 전부 Quad(MeshFilter + MeshRenderer)다. 타일맵 그리드 안쪽은 건드리지 않는다.
    static void Collect(Transform root, Transform current, List<Transform> found) {
        foreach (Transform child in current) {
            if (child.GetComponent<Grid>() != null) continue;

            if (child.GetComponent<MeshFilter>() != null && child.GetComponent<MeshRenderer>() != null) {
                found.Add(child);
                continue; // 지형 밑에 또 지형을 달아 두지는 않는다.
            }
            Collect(root, child, found);
        }
    }

    // 방 루트 기준의 사각형. Quad 는 1×1 이라 스케일이 곧 크기다.
    static Rect LocalRect(Transform root, Transform block) {
        Vector3 center = root.InverseTransformPoint(block.position);
        Vector3 size = block.lossyScale;
        return new Rect(center.x - size.x / 2f, center.y - size.y / 2f, size.x, size.y);
    }

    #endregion
    #region 특수 발판

    // 이동·붕괴·가시는 오브젝트로 남기고 보이는 것만 스프라이트로 바꾼다. 옮겼으면 true.
    static bool TryGimmick(GameObject obj, Sprite sprite) {
        Color color;
        if (obj.GetComponent<MovingPlatform>() != null) color = UiTheme.TextBody;        // 눈에 띄어야 타이밍을 잰다.
        else if (obj.GetComponent<CrumblingPlatform>() != null) color = UiTheme.TextMuted; // 바랜 색 = 곧 무너질 것.
        else if (obj.GetComponent<DamageZone>() != null) color = UiTheme.AccentBright;     // 위험은 포인트 컬러로.
        else return false;

        // CrumblingPlatform 이 Renderer 로 찾아 끄고 켜므로, Quad 를 지우기 전에 대신할 것을 붙여야 한다.
        Object.DestroyImmediate(obj.GetComponent<MeshFilter>());
        Object.DestroyImmediate(obj.GetComponent<MeshRenderer>());

        SpriteRenderer renderer = obj.GetComponent<SpriteRenderer>();
        if (renderer == null) renderer = obj.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = GimmickOrder;
        // Tiled·Sliced 는 스케일이 아니라 size 로 크기를 재는데, 이 발판들은 스케일로 크기를 잡아 두었다.
        // Simple 로 두어야 그림이 콜라이더(1×1 × 스케일)와 정확히 같은 크기로 늘어난다.
        renderer.drawMode = SpriteDrawMode.Simple;
        return true;
    }

    #endregion
}
#endif
