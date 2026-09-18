#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

// King And Pig 지형 시트를 32px 칸으로 다시 잘라, 이음새 47가지를 모두 채운 블롭(blob) 룰 타일을 만든다.
//
// 이 시트를 고른 이유: 흔한 타일셋은 3x3 아홉 칸만 주고 "안쪽 모서리"(네 이웃은 다 있는데 대각선만 빈 구석)를
// 빼먹는다. 그러면 지형이 ㄱ 자로 꺾이는 자리마다 구석이 뭉개진다. 이 시트는 그 경우까지 전부 그려 두어
// 47가지를 하나도 대체 없이 채울 수 있다.
//
// 자동 잘라내기로 들어온 원래 상태는 3x3 · 2x2 덩어리가 스프라이트 한 장으로 묶여 있어 룰 타일에 쓸 수 없다.
// 그래서 잘라내기부터 이 도구가 다시 정한다.
//
// 사용법: Tools ▸ FCC ▸ Tilemap ▸ Build King And Pig Rule Tiles
public static class KingAndPigTileBuilder {
    #region 상수

    const string SheetPath = "Assets/_Project/Assets/Tiles/Tile Assets/King And Pig/Terrain (32x32).png";
    const string TileDir = "Assets/_Project/Assets/Tiles/Tile Rule";

    // 32px = 1유닛. 씬의 Grid 가 1×1 칸이고 기존 Tile_Solid 계열도 32 PPU 라, 여기서 어긋나면 칸이 안 맞는다.
    const int CellPixels = 32;

    // 시트에는 색만 다른 같은 47칸 세트가 위아래로 2벌 들어 있다. 아래 벌은 위 벌에서 정확히 6칸 아래.
    const int VariantRowStride = 6;

    #endregion
    #region 조각 정의

    // 이웃 조건을 3×3 글자판으로 적는다. 위 줄부터 왼쪽→오른쪽이며 가운데 칸(자기 자신)은 읽지 않는다.
    //   1 = 같은 타일이 있어야 함 · 0 = 없어야 함 · 마침표 = 상관없음
    // 마침표가 핵심이다. 위 이웃이 없으면 위쪽 대각선 두 칸은 그림에 영향을 주지 않으므로 상관없음으로 둔다.
    // 전부 못 박으면 규칙이 47가지가 아니라 256가지로 불어난다.
    readonly struct Piece {
        public readonly int Col;    // 시트 칸 좌표(왼쪽에서 0부터).
        public readonly int Row;    // 시트 칸 좌표(위에서 0부터). 황금빛 벌 기준이며 장밋빛 벌은 6칸 아래.
        public readonly string Name;
        public readonly string Top;
        public readonly string Mid;
        public readonly string Bottom;

        public Piece(int col, int row, string name, string top, string mid, string bottom) {
            Col = col;
            Row = row;
            Name = name;
            Top = top;
            Mid = mid;
            Bottom = bottom;
        }
    }

    // 47가지 전부. 네 방향 이웃을 모두 못 박아 두어 조건이 서로 겹치지 않으므로 순서가 결과를 바꾸지는 않지만,
    // 룰 타일 인스펙터에서 눈으로 훑기 좋도록 "이웃이 많은 쪽 → 적은 쪽"으로 묶어 둔다.
    static readonly Piece[] Pieces = {
        // 사방이 다 채워진 안쪽 칸. 대각선이 어디까지 비었는지로 16가지가 갈린다.
        new Piece( 2, 2, "Inner",             "111", "1.1", "111"),
        new Piece( 7, 1, "Inner_DR",          "111", "1.1", "110"),
        new Piece( 8, 1, "Inner_DL",          "111", "1.1", "011"),
        new Piece( 7, 2, "Inner_UR",          "110", "1.1", "111"),
        new Piece( 8, 2, "Inner_UL",          "011", "1.1", "111"),
        new Piece(10, 1, "Inner_UL_DL",       "011", "1.1", "011"),
        new Piece(11, 1, "Inner_UL_UR",       "010", "1.1", "111"),
        new Piece(10, 2, "Inner_DL_DR",       "111", "1.1", "010"),
        new Piece(11, 2, "Inner_UR_DR",       "110", "1.1", "110"),
        new Piece(16, 2, "Inner_UR_DL",       "110", "1.1", "011"),
        new Piece(17, 2, "Inner_UL_DR",       "011", "1.1", "110"),
        new Piece(13, 1, "Inner_UR_DL_DR",    "110", "1.1", "010"),
        new Piece(14, 1, "Inner_UL_DL_DR",    "011", "1.1", "010"),
        new Piece(13, 2, "Inner_UL_UR_DR",    "010", "1.1", "110"),
        new Piece(14, 2, "Inner_UL_UR_DL",    "010", "1.1", "011"),
        new Piece(16, 1, "Inner_NoDiagonals", "010", "1.1", "010"),

        // 한쪽 면만 바깥으로 드러난 가장자리. 안쪽으로 남은 대각선 두 칸으로 4가지씩 갈린다.
        new Piece( 2, 1, "Top",               ".0.", "1.1", "111"),
        new Piece( 7, 4, "Top_DR",            ".0.", "1.1", "110"),
        new Piece( 8, 4, "Top_DL",            ".0.", "1.1", "011"),
        new Piece(13, 4, "Top_DL_DR",         ".0.", "1.1", "010"),

        new Piece( 2, 3, "Bottom",            "111", "1.1", ".0."),
        new Piece( 7, 5, "Bottom_UR",         "110", "1.1", ".0."),
        new Piece( 8, 5, "Bottom_UL",         "011", "1.1", ".0."),
        new Piece(14, 5, "Bottom_UL_UR",      "010", "1.1", ".0."),

        new Piece( 1, 2, "Left",              ".11", "0.1", ".11"),
        new Piece(10, 4, "Left_DR",           ".11", "0.1", ".10"),
        new Piece(10, 5, "Left_UR",           ".10", "0.1", ".11"),
        new Piece(13, 5, "Left_UR_DR",        ".10", "0.1", ".10"),

        new Piece( 3, 2, "Right",             "11.", "1.0", "11."),
        new Piece(11, 4, "Right_DL",          "11.", "1.0", "01."),
        new Piece(11, 5, "Right_UL",          "01.", "1.0", "11."),
        new Piece(14, 4, "Right_UL_DL",       "01.", "1.0", "01."),

        // 두 면이 드러난 바깥 모서리. 안쪽으로 남은 대각선 하나로 2가지씩 갈린다.
        new Piece( 1, 1, "CornerTL",          ".0.", "0.1", ".11"),
        new Piece(16, 4, "CornerTL_DR",       ".0.", "0.1", ".10"),
        new Piece( 3, 1, "CornerTR",          ".0.", "1.0", "11."),
        new Piece(17, 4, "CornerTR_DL",       ".0.", "1.0", "01."),
        new Piece( 1, 3, "CornerBL",          ".11", "0.1", ".0."),
        new Piece(16, 5, "CornerBL_UR",       ".10", "0.1", ".0."),
        new Piece( 3, 3, "CornerBR",          "11.", "1.0", ".0."),
        new Piece(17, 5, "CornerBR_UL",       "01.", "1.0", ".0."),

        // 한 칸 두께의 기둥 · 발판 · 외딴 블록. 맞닿은 면이 없으니 대각선은 전부 상관없음.
        new Piece( 5, 2, "ColumnMid",         ".1.", "0.0", ".1."),
        new Piece( 5, 1, "ColumnTop",         ".0.", "0.0", ".1."),
        new Piece( 5, 3, "ColumnBottom",      ".1.", "0.0", ".0."),
        new Piece( 2, 5, "RowMid",            ".0.", "1.1", ".0."),
        new Piece( 1, 5, "RowLeft",           ".0.", "0.1", ".0."),
        new Piece( 3, 5, "RowRight",          ".0.", "1.0", ".0."),
        new Piece( 5, 5, "Single",            ".0.", "0.0", ".0."),
    };

    // 색만 다른 두 벌. 순서는 시트에서 위 벌 · 아래 벌 순이다.
    static readonly (string Name, int RowOffset, string Label)[] Variants = {
        ("Gold", 0, "황금빛"),
        ("Rose", VariantRowStride, "장밋빛"),
    };

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Tilemap/Build King And Pig Rule Tiles")]
    public static void Build() {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
        if (texture == null) {
            Debug.LogError($"[Tilemap] 지형 시트를 찾지 못했습니다: {SheetPath}");
            return;
        }

        Dictionary<string, Sprite> sprites = SliceSheet(texture.height);
        if (sprites == null) return;

        foreach ((string name, int rowOffset, string label) in Variants) {
            string path = $"{TileDir}/Tile_KingAndPig_{name}.asset";
            BuildRuleTile(path, name, rowOffset, sprites);
            Debug.Log($"[Tilemap] {label} 벌 룰 타일을 만들었습니다 (규칙 {Pieces.Length}개): {path}");
        }

        AssetDatabase.SaveAssets();
    }

    #endregion
    #region 시트 잘라내기

    // 시트를 32px 칸으로 다시 자르고, 조각 이름을 붙인 스프라이트를 이름표와 함께 돌려준다.
    static Dictionary<string, Sprite> SliceSheet(int textureHeight) {
        var importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (importer == null) {
            Debug.LogError($"[Tilemap] 텍스처 임포터를 열지 못했습니다: {SheetPath}");
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = CellPixels;
        importer.filterMode = FilterMode.Point;                                // 칸 경계에 이웃 색이 번지지 않도록.
        importer.textureCompression = TextureImporterCompression.Uncompressed; // 압축은 픽셀아트 테두리를 뭉갠다.
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;          // 타이트 메시는 투명한 줄을 잘라 칸 크기를 어긋나게 한다.
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteGenerateFallbackPhysicsShape = true;         // 충돌을 스프라이트 모양으로 잡으므로 물리 형태가 있어야 한다.
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        // 이미 있던 이름은 GUID 를 그대로 물려준다. 새로 발급하면 이 도구를 다시 돌릴 때마다
        // 룰 타일과 이미 칠해 둔 타일맵의 스프라이트 참조가 전부 끊긴다.
        var keepIds = new Dictionary<string, GUID>();
        foreach (SpriteRect old in provider.GetSpriteRects()) keepIds[old.name] = old.spriteID;

        var rects = new List<SpriteRect>();
        foreach ((string variant, int rowOffset, _) in Variants) {
            foreach (Piece piece in Pieces) {
                string name = $"{variant}_{piece.Name}";
                int row = piece.Row + rowOffset;
                rects.Add(new SpriteRect {
                    name = name,
                    spriteID = keepIds.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
                    // 시트 칸은 위에서부터 세지만 텍스처 좌표는 아래가 0이라 뒤집어 준다.
                    rect = new Rect(piece.Col * CellPixels, textureHeight - (row + 1) * CellPixels, CellPixels, CellPixels),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = Vector4.zero,
                });
            }
        }

        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        provider.Apply();
        importer.SaveAndReimport();

        var sprites = new Dictionary<string, Sprite>();
        foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(SheetPath)) {
            if (obj is Sprite sprite) sprites[sprite.name] = sprite;
        }

        Debug.Log($"[Tilemap] 지형 시트를 {CellPixels}px 칸 {rects.Count}개로 다시 잘랐습니다: {SheetPath}");
        return sprites;
    }

    #endregion
    #region 룰 타일 만들기

    static void BuildRuleTile(string path, string variant, int rowOffset, Dictionary<string, Sprite> sprites) {
        RuleTile tile = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
        if (tile == null) {
            // 있으면 덮어쓰기만 한다. 새로 만들면 GUID 가 바뀌어 이미 칠해 둔 타일맵이 전부 빈칸이 된다.
            tile = ScriptableObject.CreateInstance<RuleTile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        var rules = new List<RuleTile.TilingRule>();
        var missing = new List<string>();
        foreach (Piece piece in Pieces) {
            string name = $"{variant}_{piece.Name}";
            if (!sprites.TryGetValue(name, out Sprite sprite)) {
                missing.Add(name);
                continue;
            }

            var rule = new RuleTile.TilingRule {
                m_Sprites = new[] { sprite },
                m_ColliderType = Tile.ColliderType.Grid, // 아래 기본값과 같은 이유로 칸 전체 충돌.
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single,
            };
            rule.ApplyNeighbors(ParseMask(piece.Top, piece.Mid, piece.Bottom));
            rules.Add(rule);
        }

        if (missing.Count > 0) {
            Debug.LogWarning($"[Tilemap] 스프라이트를 찾지 못한 조각: {string.Join(", ", missing)}");
        }

        // 어느 규칙에도 안 걸린 칸에 쓰이는 그림. 47가지를 다 채웠으므로 실제로는 나오지 않아야 한다.
        sprites.TryGetValue($"{variant}_Inner", out Sprite fallback);
        tile.m_DefaultSprite = fallback;
        // 막히는 지형은 칸 전체가 충돌해야 한다. 그림 윤곽(Sprite)을 따르면 조각마다 투명 여백이 달라
        // 윗면 높이가 칸마다 미세하게 어긋나고(실제로 -37 과 -37.00003 이 섞여 나왔다), 그 틈이
        // 발밑 판정과 이동에 그대로 얹힌다. 임시 타일(Tile_Solid)도 같은 이유로 Grid 를 쓴다.
        tile.m_DefaultColliderType = Tile.ColliderType.Grid;
        tile.m_TilingRules = rules;
        EditorUtility.SetDirty(tile);
    }

    internal static Dictionary<Vector3Int, int> ParseMask(string top, string mid, string bottom) {
        var map = new Dictionary<Vector3Int, int>();
        AddNeighbor(map, top[0], -1, 1);
        AddNeighbor(map, top[1], 0, 1);
        AddNeighbor(map, top[2], 1, 1);
        AddNeighbor(map, mid[0], -1, 0);
        AddNeighbor(map, mid[2], 1, 0);
        AddNeighbor(map, bottom[0], -1, -1);
        AddNeighbor(map, bottom[1], 0, -1);
        AddNeighbor(map, bottom[2], 1, -1);
        return map;
    }

    static void AddNeighbor(Dictionary<Vector3Int, int> map, char mark, int x, int y) {
        // 마침표는 목록에 아예 넣지 않는다. 룰 타일에서 "상관없음"은 조건을 빼는 것으로 표현한다.
        if (mark == '1') map[new Vector3Int(x, y, 0)] = RuleTile.TilingRuleOutput.Neighbor.This;
        else if (mark == '0') map[new Vector3Int(x, y, 0)] = RuleTile.TilingRuleOutput.Neighbor.NotThis;
    }

    #endregion
}
#endif
