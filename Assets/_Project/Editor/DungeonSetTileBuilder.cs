#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

// 「Dungeon Tile Set」 시트에서 룰 타일을 만든다.
//
// 이 시트는 47칸 블롭 세트가 아니라 **3×3 덩어리로 그려진 벽 두 종 + 소품**이다. 3×3 덩어리는 그 자체가
// 아홉 조각(모서리 4 · 변 4 · 속 1)이라, 칸 좌표를 그대로 이웃 조건에 옮기면 룰 타일이 된다.
//   벽돌 벽 (16,16)~(64,64)   · 돌 벽 (16,80)~(64,128)  · 나무 발판 (16,144)~(64,160)
//
// 왜 시트를 다시 굽지 않는가(MossyCaveTileBuilder 와 다른 점):
// 이쪽은 칸에 딱 맞게 그려진 픽셀 아트라 이음새가 어긋나지 않고, 화면에서 축소하지도 않아 밉맵 번짐이 없다.
// 원본을 16px 격자로 자르기만 하면 되므로 임포트 설정과 잘라내기만 손대고 픽셀은 건드리지 않는다.
//
// 대각선은 보지 않는다(16가지). 3×3 덩어리에는 안쪽 모서리 그림이 아예 없어 대각선을 따져도 고를 그림이 없다
// (Tile_Cave_Platform 과 같은 이유).
//
// 사용법: Tools ▸ FCC ▸ Tilemap ▸ Build Dungeon Set Rule Tiles
public static class DungeonSetTileBuilder {
    #region 상수

    const string SheetPath = "Assets/_Project/Assets/Tiles/Tile Assets/Dungeon Tile Set.png";
    const string TileDir = "Assets/_Project/Assets/Tiles/Tile Rule";

    const int Cell = 16; // 이 시트의 칸 한 변(px). 1칸 = 1유닛이 되도록 PPU 도 같은 값으로 맞춘다.

    #endregion
    #region 조각 정의

    // 이웃 조건은 KingAndPigTileBuilder 와 같은 3×3 글자판이다(1 있어야 함 · 0 없어야 함 · 마침표 상관없음).
    readonly struct Piece {
        public readonly int Col;    // 시트의 칸 좌표(왼쪽에서 0부터).
        public readonly int Row;    // 시트의 칸 좌표(위에서 0부터).
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

    // 3×3 벽 덩어리 하나를 아홉 조각으로 옮긴다. 두 벽이 좌표만 다르고 구성은 같아 만들어 쓴다.
    static Piece[] WallPieces(int col0, int row0) {
        return new[] {
            new Piece(col0,     row0,     "CornerTL",     ".0.", "0.1", ".1."),
            new Piece(col0 + 1, row0,     "Top",          ".0.", "1.1", ".1."),
            new Piece(col0 + 2, row0,     "CornerTR",     ".0.", "1.0", ".1."),
            new Piece(col0,     row0 + 1, "Left",         ".1.", "0.1", ".1."),
            new Piece(col0 + 1, row0 + 1, "Center",       ".1.", "1.1", ".1."),
            new Piece(col0 + 2, row0 + 1, "Right",        ".1.", "1.0", ".1."),
            new Piece(col0,     row0 + 2, "CornerBL",     ".1.", "0.1", ".0."),
            new Piece(col0 + 1, row0 + 2, "Bottom",       ".1.", "1.1", ".0."),
            new Piece(col0 + 2, row0 + 2, "CornerBR",     ".1.", "1.0", ".0."),
        };
    }

    // 한 줄짜리 나무 발판. 위아래는 보지 않는다 — 발판을 두 줄로 겹쳐 놓아도 같은 그림이 나와야 한다.
    static readonly Piece[] PlatformPieces = {
        new Piece(1, 9, "RowLeft",  "...", "0.1", "..."),
        new Piece(2, 9, "RowMid",   "...", "1.1", "..."),
        new Piece(3, 9, "RowRight", "...", "1.0", "..."),
        new Piece(2, 9, "Single",   "...", "0.0", "..."), // 외딴 한 칸은 가운데 그림으로 채운다.
    };

    // 만들 룰 타일 목록. 기본 그림은 어느 규칙에도 안 걸린 칸(한 칸 폭 기둥 등)에 쓰인다.
    static readonly (string TileName, string Prefix, Piece[] Pieces, string Default)[] Tiles = {
        ("Tile_Dungeon_BrickWall", "DungeonBrick", WallPieces(1, 1), "Center"),
        ("Tile_Dungeon_StoneWall", "DungeonStone", WallPieces(1, 5), "Center"),
        ("Tile_Dungeon_WoodPlatform", "DungeonWood", PlatformPieces, "RowMid"),
    };

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Tilemap/Build Dungeon Set Rule Tiles")]
    public static void Build() {
        Dictionary<string, Sprite> sprites = SliceSheet();
        if (sprites == null) return;

        foreach ((string tileName, string prefix, Piece[] pieces, string fallback) in Tiles) {
            BuildRuleTile($"{TileDir}/{tileName}.asset", prefix, pieces, fallback, sprites);
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"[Tilemap] Dungeon Tile Set 룰 타일 {Tiles.Length}종을 만들었습니다: {TileDir}");
    }

    #endregion
    #region 잘라내기

    // 쓰는 칸만 이름을 붙여 자른다. 자동 슬라이스로 들어온 이름 없는 조각(Dungeon Tile Set_0 …)은
    // 소품이 뭉쳐 잘려 있어 타일로 쓸 수 없으므로 이 목록으로 대체한다.
    static Dictionary<string, Sprite> SliceSheet() {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SheetPath);
        var importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
        if (texture == null || importer == null) {
            Debug.LogError($"[Tilemap] 시트를 찾지 못했습니다: {SheetPath}");
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = Cell; // 1칸 = 1유닛.
        importer.filterMode = FilterMode.Point;        // 픽셀 아트라 보간하면 뭉개진다.
        importer.mipmapEnabled = false;                // 축소해 그리지 않으므로 밉맵이 필요 없다.
        importer.textureCompression = TextureImporterCompression.Uncompressed; // 240×288 로 작아 무압축이 싸다.
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // 타이트 메시는 투명한 가장자리를 잘라 칸 크기를 어긋나게 한다.
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        // 이미 있던 이름은 GUID 를 그대로 물려준다. 새로 발급하면 다시 자를 때마다 룰 타일과
        // 이미 칠해 둔 타일맵의 스프라이트 참조가 끊긴다(MossyCaveTileBuilder 와 같은 이유).
        var keepIds = new Dictionary<string, GUID>();
        foreach (SpriteRect old in provider.GetSpriteRects()) keepIds[old.name] = old.spriteID;

        int height = texture.height;
        var rects = new List<SpriteRect>();
        foreach ((string _, string prefix, Piece[] pieces, string _) in Tiles) {
            foreach (Piece piece in pieces) {
                string name = $"{prefix}_{piece.Name}";
                if (rects.Any(r => r.name == name)) continue; // 같은 칸을 두 조건에 쓰는 경우(Single).

                rects.Add(new SpriteRect {
                    name = name,
                    spriteID = keepIds.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
                    // 유니티 스프라이트 좌표는 아래에서 위로 올라간다. 칸 좌표(위에서 아래)를 뒤집어 준다.
                    rect = new Rect(piece.Col * Cell, height - (piece.Row + 1) * Cell, Cell, Cell),
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

        Debug.Log($"[Tilemap] Dungeon Tile Set 을 {Cell}px 칸 {rects.Count}개로 잘랐습니다.");
        return sprites;
    }

    #endregion
    #region 룰 타일 만들기

    static void BuildRuleTile(string path, string prefix, Piece[] pieces, string fallbackPiece, Dictionary<string, Sprite> sprites) {
        RuleTile tile = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
        if (tile == null) {
            // 있으면 덮어쓰기만 한다. 새로 만들면 GUID 가 바뀌어 이미 칠해 둔 타일맵이 전부 빈칸이 된다.
            tile = ScriptableObject.CreateInstance<RuleTile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        var rules = new List<RuleTile.TilingRule>();
        var missing = new List<string>();
        foreach (Piece piece in pieces) {
            string name = $"{prefix}_{piece.Name}";
            if (!sprites.TryGetValue(name, out Sprite sprite)) {
                missing.Add(name);
                continue;
            }

            var rule = new RuleTile.TilingRule {
                m_Sprites = new[] { sprite },
                m_ColliderType = Tile.ColliderType.Grid, // 그림 윤곽을 따르면 칸마다 윗면 높이가 어긋나 발밑 판정에 걸린다.
                m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single,
            };
            rule.ApplyNeighbors(KingAndPigTileBuilder.ParseMask(piece.Top, piece.Mid, piece.Bottom));
            rules.Add(rule);
        }

        if (missing.Count > 0) Debug.LogWarning($"[Tilemap] 스프라이트를 찾지 못한 조각: {string.Join(", ", missing)}");

        sprites.TryGetValue($"{prefix}_{fallbackPiece}", out Sprite fallback);
        tile.m_DefaultSprite = fallback;
        tile.m_DefaultColliderType = Tile.ColliderType.Grid;
        tile.m_TilingRules = rules;
        EditorUtility.SetDirty(tile);
    }

    #endregion
}
#endif
