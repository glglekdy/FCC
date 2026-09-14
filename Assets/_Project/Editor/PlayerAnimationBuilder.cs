#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

// 플레이어 애니메이션 시트(공격 · 점프)를 게임에서 쓸 수 있는 상태로 맞춘 뒤 Player 프리팹에 연결한다.
//
// 손으로 맞추기 어려운 두 가지를 이 도구가 대신한다.
//
// 1) 크기 — 새 시트 도안은 걷기 도안과 크기가 달라서, 그대로 두면 공격·점프로 넘어가는 순간 캐릭터가
//    쪼그라든다. 다만 크기(PPU)는 이 도구가 정하지 않고 CharacterSpriteScaler 에 맡긴다 — 캐릭터 키는
//    플레이어와 몬스터가 함께 맞춰야 하는 값이라 한 곳에서만 정해야 한다. 여기서는 피벗을 계산하기 전에
//    그 도구를 먼저 불러 크기를 확정시킨다.
//
// 2) 피벗 — 자동 슬라이스는 각 칸을 그림에 딱 맞게 잘라내므로, 칸마다 그림의 크기가 다르면 가운데
//    피벗이 프레임마다 다른 곳을 가리켜 캐릭터가 위아래로 덜덜 떨린다. 그래서 피벗을 "모든 칸이
//    공유하는 한 점"으로 옮긴다 — 가로는 서 있는 칸의 그림 중심, 세로는 그 칸의 발밑이다.
//    발밑 높이는 걷기 스프라이트의 피벗↔발 거리를 그대로 가져와, 걷다가 점프해도 발 위치가 튀지 않는다.
//
//    다만 시트는 아래 줄이 위 줄보다 통째로 약 60~70px 높이 그려져 있다. 그대로 두면 공격 마지막 칸
//    (다시 서 있는 포즈)이 공중에 뜬 채 끝나므로, 줄마다 가장 낮게 그려진 칸을 그 줄의 바닥으로 보고
//    줄 단위 어긋남을 뺀다. 그러고도 남는 높이 차이(점프 정점의 다리 접기 등)는 그린 그대로 살린다.
//
// 사용법: Tools ▸ FCC ▸ Player ▸ Apply Player Animation
// 멱등이라 여러 번 눌러도 안전하며, 시트를 다시 그려 넣은 뒤에도 그대로 다시 실행하면 된다.
public static class PlayerAnimationBuilder {
    #region 상수

    const string SpriteDir = "Assets/_Project/Assets/Sprites/Player";
    const string AttackSheetPath = SpriteDir + "/Player_Attack.png";
    const string JumpSheetPath = SpriteDir + "/Player_Jump.png";
    const string WalkDir = SpriteDir + "/Walk";
    const int WalkFrameCount = 8;

    const string PlayerPrefabPath = "Assets/_Project/Assets/Prefabs/Playerble/Player.prefab";
    const string RendererName = "Player_Renderer";

    // 시트 한 장에 가로 3 · 세로 2 = 6칸이 그려져 있다.
    const int SheetColumns = 3;
    const int SheetRows = 2;
    const int SheetFrameCount = SheetColumns * SheetRows;

    const byte AlphaThreshold = 8; // 이보다 옅은 픽셀은 그림 가장자리의 흐린 자국으로 보고 범위에서 뺀다.

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Player/Apply Player Animation")]
    public static void Apply() {
        var log = new StringBuilder();

        // 피벗 계산이 PPU 위에 올라타므로(발까지 거리를 픽셀로 환산한다) 크기를 먼저 확정한다.
        CharacterSpriteScaler.ApplyCharacter(CharacterSpriteScaler.PlayerName);

        float feetUnits = ReadWalkFeetOffsetUnits();
        if (feetUnits <= 0f) {
            Debug.LogError($"[PlayerAnimationBuilder] 걷기 스프라이트({WalkDir}/Player_Walk_1.png)를 찾지 못해 기준 높이를 잴 수 없습니다.");
            return;
        }

        if (!SliceSheet(AttackSheetPath, feetUnits, log)) return;
        if (!SliceSheet(JumpSheetPath, feetUnits, log)) return;

        AssetDatabase.Refresh();

        Sprite[] walk = LoadWalkFrames();
        Sprite[] attack = LoadSheetFrames(AttackSheetPath);
        Sprite[] jump = LoadSheetFrames(JumpSheetPath);

        if (walk.Length < 1) {
            Debug.LogError($"[PlayerAnimationBuilder] 걷기 프레임을 찾지 못했습니다: {WalkDir}");
            return;
        }
        if (attack.Length != SheetFrameCount || jump.Length != SheetFrameCount) {
            Debug.LogError($"[PlayerAnimationBuilder] 시트 칸 수가 {SheetFrameCount}개가 아닙니다 (공격 {attack.Length} · 점프 {jump.Length}).");
            return;
        }

        if (!WirePrefab(walk, attack, jump, log)) return;

        Debug.Log($"[PlayerAnimationBuilder] 플레이어 애니메이션을 적용했습니다.\n{log}");
    }

    #endregion
    #region 시트 자르기

    // 걷기 도안에서 "피벗에서 발까지" 거리를 유닛으로 잰다. 걷기 스프라이트는 피벗이 그림 한가운데라
    // pivot.y 가 곧 그 거리이며, 새 시트를 이 거리에 맞춰야 걷다가 점프해도 발 높이가 그대로 이어진다.
    static float ReadWalkFeetOffsetUnits() {
        Sprite walk = LoadFirstSprite($"{WalkDir}/Player_Walk_1.png");
        if (walk == null) return 0f;

        return walk.pivot.y / walk.pixelsPerUnit;
    }

    static bool SliceSheet(string path, float feetUnits, StringBuilder log) {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) {
            Debug.LogError($"[PlayerAnimationBuilder] 시트를 찾지 못했습니다: {path}");
            return false;
        }

        if (importer.spriteImportMode != SpriteImportMode.Multiple) {
            Debug.LogError($"[PlayerAnimationBuilder] {System.IO.Path.GetFileName(path)} 이 Multiple 로 잘려 있지 않습니다. " +
                           "Sprite Editor 에서 Slice ▸ Automatic 으로 6칸을 자른 뒤 다시 실행해 주세요.");
            return false;
        }

        // 그림의 실제 발밑은 원본 PNG 를 직접 풀어서 잰다. 임포트된 텍스처는 압축되어 알파가 뭉개지고,
        // 읽으려면 Read/Write 를 켰다 끄느라 재임포트를 두 번 더 해야 한다.
        Texture2D source = CharacterSpriteScaler.LoadSourcePixels(path);
        if (source == null) {
            Debug.LogError($"[PlayerAnimationBuilder] 원본 PNG 를 읽지 못했습니다: {path}");
            return false;
        }

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        SpriteRect[] rects = provider.GetSpriteRects();
        if (rects.Length != SheetFrameCount) {
            Object.DestroyImmediate(source);
            Debug.LogError($"[PlayerAnimationBuilder] {System.IO.Path.GetFileName(path)} 의 칸이 {rects.Length}개입니다. " +
                           $"Sprite Editor 에서 Slice ▸ Automatic 으로 {SheetFrameCount}칸을 자른 뒤 다시 실행해 주세요.");
            return false;
        }

        Color32[] pixels = source.GetPixels32();
        int textureWidth = source.width;
        int textureHeight = source.height;
        Object.DestroyImmediate(source);

        int cellWidth = textureWidth / SheetColumns;
        int cellHeight = textureHeight / SheetRows;

        // 칸마다 그림의 실제 범위를 재 둔다.
        var bounds = new RectInt[rects.Length];
        var columns = new int[rects.Length];
        var rows = new int[rects.Length];
        var feetAboveCell = new float[rects.Length];

        for (int i = 0; i < rects.Length; i++) {
            Rect r = rects[i].rect;
            bounds[i] = MeasureInk(pixels, textureWidth, textureHeight, r);
            columns[i] = Mathf.Clamp(Mathf.FloorToInt(r.center.x / cellWidth), 0, SheetColumns - 1);
            rows[i] = Mathf.Clamp(Mathf.FloorToInt(r.center.y / cellHeight), 0, SheetRows - 1);
            feetAboveCell[i] = bounds[i].yMin - rows[i] * cellHeight;
        }

        // 줄마다 가장 낮게 그려진 칸을 그 줄의 바닥으로 본다 (위 설명 2) 참고).
        var rowFloor = new float[SheetRows];
        for (int row = 0; row < SheetRows; row++) {
            rowFloor[row] = float.MaxValue;
            for (int i = 0; i < rects.Length; i++) {
                if (rows[i] == row) rowFloor[row] = Mathf.Min(rowFloor[row], feetAboveCell[i]);
            }
        }

        int first = FrameOrder(rects, columns, rows).First();
        float anchorX = bounds[first].center.x - columns[first] * cellWidth; // 칸 안에서의 가로 기준점.
        // 걷기 기준으로 잰 "피벗에서 발까지" 거리를 이 시트의 픽셀로 환산한다. PPU 를 여기서 읽으므로
        // CharacterSpriteScaler 가 캐릭터 키를 바꿔도 발 높이는 저절로 따라온다.
        float feetPixels = feetUnits * importer.spritePixelsPerUnit;

        for (int i = 0; i < rects.Length; i++) {
            // 제 줄의 바닥에서 얼마나 떠 있는지만 "그린 대로의 들림"으로 남긴다. 줄 단위로 재므로
            // 아래 줄이 통째로 높이 그려진 어긋남은 여기서 함께 사라진다.
            float lift = feetAboveCell[i] - rowFloor[rows[i]];

            float pivotX = columns[i] * cellWidth + anchorX;
            float pivotY = bounds[i].yMin + feetPixels - lift;

            Rect r = rects[i].rect;
            rects[i].alignment = SpriteAlignment.Custom;
            rects[i].pivot = new Vector2((pivotX - r.x) / r.width, (pivotY - r.y) / r.height);

            log.AppendLine($"  {rects[i].name}: 들림 {lift:0}px");
        }

        provider.SetSpriteRects(rects);
        provider.Apply();
        importer.SaveAndReimport();

        log.AppendLine($"{System.IO.Path.GetFileName(path)} — PPU {importer.spritePixelsPerUnit:0.#} · {rects.Length}칸 피벗 정렬");
        return true;
    }


    // 잘린 칸 안에서 실제로 그림이 있는 범위를 잰다. 자동 슬라이스가 만든 칸은 흐린 자국까지 넉넉히
    // 물고 있어, 그 칸의 아래쪽 선을 발밑으로 삼으면 칸마다 몇십 픽셀씩 어긋난다.
    static RectInt MeasureInk(Color32[] pixels, int textureWidth, int textureHeight, Rect rect) {
        int x0 = Mathf.Clamp(Mathf.FloorToInt(rect.xMin), 0, textureWidth - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(rect.xMax), 0, textureWidth);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(rect.yMin), 0, textureHeight - 1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(rect.yMax), 0, textureHeight);

        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;

        for (int y = y0; y < y1; y++) {
            int row = y * textureWidth;
            for (int x = x0; x < x1; x++) {
                if (pixels[row + x].a < AlphaThreshold) continue;

                if (x < minX) minX = x;
                if (x > maxX) maxX = x;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
        }

        // 빈 칸이면 잘린 칸을 그대로 쓴다 (0으로 나누는 것을 막기 위한 안전장치).
        if (minX > maxX) return new RectInt(x0, y0, Mathf.Max(x1 - x0, 1), Mathf.Max(y1 - y0, 1));

        return new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    #endregion
    #region 프레임 불러오기

    // 시트는 왼쪽→오른쪽, 위→아래 순서로 그려져 있다. 스프라이트 좌표는 아래에서부터 재므로
    // 윗줄이 큰 값을 가진다 — 줄은 내림차순, 칸은 오름차순으로 세운다.
    static IEnumerable<int> FrameOrder(SpriteRect[] rects, int[] columns, int[] rows) {
        return Enumerable.Range(0, rects.Length)
            .OrderByDescending(i => rows[i])
            .ThenBy(i => columns[i]);
    }

    static Sprite[] LoadSheetFrames(string path) {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
        if (sprites.Length == 0) return sprites;

        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        int cellWidth = texture != null ? texture.width / SheetColumns : 1;
        int cellHeight = texture != null ? texture.height / SheetRows : 1;

        return sprites
            .OrderByDescending(s => Mathf.FloorToInt(s.rect.center.y / cellHeight))
            .ThenBy(s => Mathf.FloorToInt(s.rect.center.x / cellWidth))
            .ToArray();
    }

    static Sprite[] LoadWalkFrames() {
        var frames = new List<Sprite>();
        for (int i = 1; i <= WalkFrameCount; i++) {
            Sprite sprite = LoadFirstSprite($"{WalkDir}/Player_Walk_{i}.png");
            if (sprite != null) frames.Add(sprite);
        }

        return frames.ToArray();
    }

    static Sprite LoadFirstSprite(string path) {
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
    }

    #endregion
    #region 프리팹 연결

    static bool WirePrefab(Sprite[] walk, Sprite[] attack, Sprite[] jump, StringBuilder log) {
        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        if (root == null) {
            Debug.LogError($"[PlayerAnimationBuilder] 플레이어 프리팹을 열지 못했습니다: {PlayerPrefabPath}");
            return false;
        }

        try {
            Transform rendererRoot = FindChild(root.transform, RendererName);
            if (rendererRoot == null) {
                Debug.LogError($"[PlayerAnimationBuilder] 프리팹에서 '{RendererName}' 오브젝트를 찾지 못했습니다.");
                return false;
            }

            // 걷기만 돌리던 예전 컴포넌트가 남아 있으면 같은 렌더러의 스프라이트를 두 곳에서 바꿔 서로 덮는다.
            var flipbook = rendererRoot.GetComponent<SpriteFlipbook>();
            if (flipbook != null) {
                Object.DestroyImmediate(flipbook, true);
                log.AppendLine("Player_Renderer 의 SpriteFlipbook 제거 (Player_Animator 가 대신함)");
            }

            var animator = rendererRoot.GetComponent<Player_Animator>();
            if (animator == null) animator = rendererRoot.gameObject.AddComponent<Player_Animator>();

            animator.move = root.GetComponentInChildren<Player_move>(true);
            animator.combat = root.GetComponentInChildren<Player_Combat>(true);

            animator.idleFrames = new[] { walk[0] }; // 정지는 걷기 첫 칸을 그대로 쓴다 — 지금 서 있는 모습이 이것이다.
            animator.walkFrames = walk;

            // 점프 시트 순서: 0 서 있기 · 1 웅크리기 · 2 도약 · 3 정점 · 4 낙하 · 5 착지(눌림).
            animator.riseFrames = new[] { jump[2], jump[3] };
            animator.fallFrames = new[] { jump[4] };
            animator.landFrames = new[] { jump[5], jump[1] }; // 눌렸다가 웅크린 자세로 펴지며 일어난다.
            animator.attackFrames = attack;

            var renderer = rendererRoot.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite == null) renderer.sprite = walk[0];

            EditorUtility.SetDirty(root);
            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            log.AppendLine($"Player.prefab — 걷기 {walk.Length}칸 · 공격 {attack.Length}칸 · 점프 {jump.Length}칸 연결");
            return true;
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform FindChild(Transform parent, string name) {
        if (parent.name == name) return parent;

        foreach (Transform child in parent) {
            Transform found = FindChild(child, name);
            if (found != null) return found;
        }

        return null;
    }

    #endregion
}
#endif
