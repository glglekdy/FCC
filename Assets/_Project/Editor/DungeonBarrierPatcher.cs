#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// 전투방 프리팹에 양쪽 벽(출구 쪽 LockBarrier · 입구 쪽 EntryBarrier)과, 다가가면 빛나는 연출(DungeonBarrierGlow)을 채워 넣는다.
//
// Build All Rooms 를 다시 돌리지 않고 따로 두는 이유: 방 지형은 그 뒤 타일맵으로 전환해 손으로 칠해 두었기 때문에,
// 통째로 다시 찍으면 그 작업이 전부 날아간다. 여기서는 프리팹을 열어 없는 것만 채우고 연결을 맞춘다(여러 번 눌러도 안전).
// 이미 있는 벽 · 빛의 위치 · 크기 · 색은 손댄 그대로 둔다.
//
// DungeonRoomPrefabBuilder.BuildCombat 도 방을 찍은 뒤 EnsureBarriers 를 불러, 새로 찍은 전투방에도 같은 구성이 들어간다.
//
// 사용법: Tools ▸ FCC ▸ Dungeon ▸ Patch Combat Room Barriers
public static class DungeonBarrierPatcher {
    #region 상수

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/Dungeon";
    const string GlowSpritePath = "Assets/_Project/Assets/Sprites/VFX/DungeonBarrierGlow.png";

    // 빛은 조명과 무관하게 보여야 한다. 2D 렌더러 기본(Lit) 머티리얼이면 나중에 Light2D 를 깐 어두운 방에서 빛이 같이 어두워진다.
    const string UnlitSpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    const string ExitBarrierName = "LockBarrier";
    const string EntryBarrierName = "EntryBarrier";
    const string GlowName = "Glow";

    const float GlowWidth = 1.6f;     // 벽 콜라이더 두께(1)보다 넓게 — 빛이 벽 바깥으로 번져야 막이 서 있는 것처럼 보인다.
    const int GlowSortingOrder = 30;  // 앞 장식 타일(20)보다 앞, 플레이어(50)보다 뒤. 벽에 붙은 플레이어가 빛에 묻히지 않게 한다.

    // 낡은 벨벳 커튼의 적색 계열(UiTheme.Accent)을 밝게 올린 값. 어두운 무대에서 빛으로 읽히려면 Accent 그대로는 너무 어둡다.
    static readonly Color GlowColor = new(0.9f, 0.32f, 0.26f, 1f);

    // 빛 그림 규격. 가로는 가운데가 밝은 띠, 세로는 양 끝만 흐려지고 가운데는 늘려 쓴다(9-slice).
    const int GlowTexWidth = 64;
    const int GlowTexHeight = 128;
    const int GlowTexBorder = 32; // 위아래 흐려지는 구간(px). 스프라이트 테두리로 잡아 벽 길이가 달라도 끝 모양이 같다.

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Dungeon/Patch Combat Room Barriers")]
    public static void PatchCombatRoomBarriers() {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[Dungeon] 프리팹 편집 모드를 닫고 다시 실행하세요.");
            return;
        }

        int patched = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { PrefabDir })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || !asset.TryGetComponent(out DungeonRoom assetRoom)) continue;
            if (assetRoom.role != DungeonRoom.RoomRole.CombatArena) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            if (EnsureBarriers(root)) {
                PrefabUtility.SaveAsPrefabAsset(root, path);
                patched++;
            }
            PrefabUtility.UnloadPrefabContents(root);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Dungeon] 전투방 {patched}개에 양쪽 벽(LockBarrier · EntryBarrier)과 빛 연출을 채웠습니다.");
    }

    #endregion
    #region 벽 채우기

    // 전투방이 아니거나 출구 쪽 벽을 찾지 못하면 false. 입구 쪽 벽은 출구 쪽 벽을 거울처럼 뒤집어 만든다.
    public static bool EnsureBarriers(GameObject root) {
        DungeonRoom room = root.GetComponent<DungeonRoom>();
        if (room == null || room.role != DungeonRoom.RoomRole.CombatArena) return false;

        BoxCollider2D exit = room.lockBarrier as BoxCollider2D;
        if (exit == null) exit = FindBarrier(root, ExitBarrierName);
        if (exit == null) {
            Debug.LogWarning($"[Dungeon] '{root.name}' 에 출구 쪽 벽({ExitBarrierName} · BoxCollider2D)이 없어 건너뜁니다.", root);
            return false;
        }

        BoxCollider2D entry = room.entryBarrier as BoxCollider2D;
        if (entry == null) entry = FindBarrier(root, EntryBarrierName);
        if (entry == null) entry = CreateEntryBarrier(root, room, exit);

        room.lockBarrier = exit;
        room.entryBarrier = entry;

        // 벽은 잠기기 전까지 꺼져 있어야 한다. 켜진 채 저장되면 방에 들어가는 순간부터 막힌다.
        exit.enabled = false;
        entry.enabled = false;

        Sprite sprite = EnsureGlowSprite();
        EnsureGlow(exit, sprite);
        EnsureGlow(entry, sprite);

        EditorUtility.SetDirty(room);
        return true;
    }

    static BoxCollider2D FindBarrier(GameObject root, string name) {
        Transform found = root.transform.Find(name);
        return found != null ? found.GetComponent<BoxCollider2D>() : null;
    }

    // 출구 소켓에서 벽까지의 거리를 입구 소켓 바깥쪽으로 똑같이 옮겨 놓는다. 소켓이 없으면 방 원점 기준으로 뒤집는다.
    static BoxCollider2D CreateEntryBarrier(GameObject root, DungeonRoom room, BoxCollider2D exit) {
        Transform rootT = root.transform;
        Vector3 exitLocal = rootT.InverseTransformPoint(exit.transform.position);

        Vector3 entryLocal;
        if (room.entryAnchor != null && room.exitAnchor != null) {
            Vector3 entryAnchor = rootT.InverseTransformPoint(room.entryAnchor.position);
            Vector3 exitAnchor = rootT.InverseTransformPoint(room.exitAnchor.position);
            entryLocal = new Vector3(entryAnchor.x - (exitLocal.x - exitAnchor.x), exitLocal.y, exitLocal.z);
        }
        else {
            entryLocal = new Vector3(-exitLocal.x, exitLocal.y, exitLocal.z);
        }

        GameObject obj = new(EntryBarrierName);
        obj.layer = exit.gameObject.layer;
        obj.transform.SetParent(rootT, false);
        obj.transform.localPosition = entryLocal;
        obj.transform.localScale = exit.transform.localScale;

        BoxCollider2D col = obj.AddComponent<BoxCollider2D>();
        col.size = exit.size;
        col.offset = new Vector2(-exit.offset.x, exit.offset.y);
        col.sharedMaterial = exit.sharedMaterial;
        col.enabled = false;

        // 출구 쪽 벽 바로 뒤에 둔다. 하이어라키에서 두 벽이 나란히 보여야 찾기 쉽다.
        obj.transform.SetSiblingIndex(exit.transform.GetSiblingIndex() + 1);
        return col;
    }

    // 벽 자식으로 빛 스프라이트를 두고 DungeonBarrierGlow 를 붙여 잇는다.
    static void EnsureGlow(BoxCollider2D barrier, Sprite sprite) {
        Transform glowT = barrier.transform.Find(GlowName);
        SpriteRenderer renderer;

        if (glowT == null) {
            GameObject obj = new(GlowName);
            obj.layer = barrier.gameObject.layer;
            obj.transform.SetParent(barrier.transform, false);
            obj.transform.localPosition = barrier.offset;

            renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Sliced;
            renderer.size = new Vector2(GlowWidth, barrier.size.y);
            renderer.color = GlowColor;
            renderer.sortingOrder = GlowSortingOrder;

            Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
            if (unlit != null) renderer.sharedMaterial = unlit;
            else Debug.LogWarning($"[Dungeon] '{UnlitSpriteMaterialPath}' 를 찾지 못해 기본 스프라이트 머티리얼을 씁니다.");
        }
        else {
            renderer = glowT.GetComponent<SpriteRenderer>();
            if (renderer == null) renderer = glowT.gameObject.AddComponent<SpriteRenderer>();
            if (renderer.sprite == null) renderer.sprite = sprite; // 손으로 바꾼 그림 · 크기 · 색은 그대로 둔다.
        }

        // 평소에는 그리지 않는다. DungeonBarrierGlow.Awake 도 끄지만, 프리팹을 열었을 때 벽이 빛나 보이면 헷갈린다.
        renderer.enabled = false;

        DungeonBarrierGlow glow = barrier.GetComponent<DungeonBarrierGlow>();
        if (glow == null) glow = barrier.gameObject.AddComponent<DungeonBarrierGlow>();
        glow.blocker = barrier;
        glow.glow = renderer;

        EditorUtility.SetDirty(renderer);
        EditorUtility.SetDirty(glow);
    }

    #endregion
    #region 빛 그림

    // 흰색 그림으로 만들고 색은 SpriteRenderer 에서 입힌다. 색만 바꿀 때 그림을 다시 뽑지 않아도 되게 하려는 것이다.
    // 이미 있으면 덮어쓰지 않는다 — 손으로 다시 그린 그림이 날아가면 안 된다.
    static Sprite EnsureGlowSprite() {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(GlowSpritePath);
        if (existing != null) return existing;

        string dir = Path.GetDirectoryName(GlowSpritePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var tex = new Texture2D(GlowTexWidth, GlowTexHeight, TextureFormat.RGBA32, false);
        var pixels = new Color32[GlowTexWidth * GlowTexHeight];

        for (int y = 0; y < GlowTexHeight; y++) {
            // 위아래 끝에서 border 만큼만 흐려진다. 가운데 구간은 9-slice 로 늘어나므로 균일해야 한다.
            float fromEnd = Mathf.Min(y + 0.5f, GlowTexHeight - y - 0.5f);
            float along = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fromEnd / GlowTexBorder));

            for (int x = 0; x < GlowTexWidth; x++) {
                float u = (x + 0.5f) / GlowTexWidth * 2f - 1f; // -1 ~ 1, 가운데가 0.

                // 가는 심지 + 넓게 번지는 빛. 심지만 있으면 선으로, 번짐만 있으면 뿌연 안개로 보인다.
                float core = Mathf.Exp(-(u * u) / (2f * 0.1f * 0.1f));
                float halo = Mathf.Exp(-(u * u) / (2f * 0.4f * 0.4f)) * 0.55f;
                float across = Mathf.Clamp01(core + halo);

                byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(across * along) * 255f);
                pixels[y * GlowTexWidth + x] = new Color32(255, 255, 255, a);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(GlowSpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(GlowSpritePath);

        var importer = (TextureImporter)AssetImporter.GetAtPath(GlowSpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = GlowTexWidth / GlowWidth; // 그림 가로 한 장 = GlowWidth 유닛.
        importer.spriteBorder = new Vector4(0f, GlowTexBorder, 0f, GlowTexBorder); // 왼 · 아래 · 오른 · 위.
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed; // 부드러운 그라데이션이 압축되면 계단이 진다.

        // 9-slice 는 Full Rect 메시에서만 늘어난다. Tight 이면 테두리가 무시돼 끝이 같이 늘어난다.
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        importer.SetTextureSettings(settings);

        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(GlowSpritePath);
    }

    #endregion
}
#endif
