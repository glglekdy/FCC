#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

// Mossy · Cave 붓 그림 시트 세 장을 칸 단위 시트로 다시 구워 룰 타일을 만든다.
//
//   Mossy - TileSet            512px × 7×7 블롭 세트. 47가지가 전부 그려져 있어 칸을 그대로 옮긴다.
//   Mossy - FloatingPlatforms  통짜 발판 그림. 가로 발판이 512px 마다 똑같이 반복되는 구간이 있어 거기서 자른다.
//   Cave - Platforms           통짜 발판 그림. 반복 구간이 전혀 없어 억지로 잘라 섞는다(아래 「덩어리 늘리기」).
//
// 왜 원본을 잘라 쓰지 않고 새 시트로 굽는가:
// 1) 이음새 번짐. 칸 512px 를 화면에서 60px 남짓으로 줄여 그리므로 밉맵이 필요한데, 밉맵이 내려가면 칸 경계에서
//    옆 칸 픽셀이 섞여 타일 사이에 가는 줄이 생긴다. 칸마다 가장자리 픽셀을 바깥으로 늘린 여백을 둘러 막는다.
// 2) 끼어드는 이웃 그림. 통짜 발판 시트는 한 칸 크기로 자르면 옆 발판이 칸 안에 걸려 들어온다.
// 3) 없는 조각. 동굴 발판에는 "가운데 줄" 같은 조각이 아예 없어 만들어 넣어야 한다.
// 원본 PNG 와 그 임포트 설정은 건드리지 않는다. 굽기는 언제나 원본에서 다시 시작하므로 여러 번 돌려도 안전하다.
//
// 사용법: Tools ▸ FCC ▸ Tilemap ▸ Build Mossy · Cave Rule Tiles
//         Tile_Mossy · Tile_Mossy_Floating 는 같은 굽기에서 회색 버전(Tile_Mossy_Gray · Tile_Mossy_Floating_Gray)도
//         함께 만들어진다 — 이끼 낀 초록을 걷어낸 돌담 · 잿빛 폐허 지역용이다. 자르는 좌표(조각표)는 원본과
//         하나만 두고 공유하며, 색만 구운 뒤에 갈린다(아래 "회색 버전" 항목 참고).
public static class MossyCaveTileBuilder {
    #region 상수

    const string SheetDir = "Assets/_Project/Assets/Tiles/Tile Assets"; // 원본 시트.
    const string TileDir = "Assets/_Project/Assets/Tiles/Tile Rule";     // 만들어진 룰 타일.
    const string MossyDir = SheetDir + "/Mossy Tileset";
    const string CaveDir = SheetDir + "/Assets 1024 Cave";

    const string MossySheetPath = MossyDir + "/Mossy - TileSet.png";
    const string MossyAtlasPath = MossyDir + "/Mossy - TileSet_Baked.png";
    const string MossyTilePath = TileDir + "/Tile_Mossy.asset";
    const string MossyGrayAtlasPath = MossyDir + "/Mossy - TileSet_Baked_Gray.png";
    const string MossyGrayTilePath = TileDir + "/Tile_Mossy_Gray.asset";

    const string FloatingSheetPath = MossyDir + "/Mossy - FloatingPlatforms.png";
    const string FloatingAtlasPath = MossyDir + "/Mossy - FloatingPlatforms_Baked.png";
    const string FloatingTilePath = TileDir + "/Tile_Mossy_Floating.asset";
    const string FloatingGrayAtlasPath = MossyDir + "/Mossy - FloatingPlatforms_Baked_Gray.png";
    const string FloatingGrayTilePath = TileDir + "/Tile_Mossy_Floating_Gray.asset";

    const string CaveSheetPath = CaveDir + "/Cave - Platforms.png";
    const string CaveAtlasPath = CaveDir + "/Cave - Platforms_Baked.png";
    const string CaveTilePath = TileDir + "/Tile_Cave_Platform.asset";

    // 칸 한 변의 픽셀 수. 1칸 = 1유닛(씬 Grid 가 1×1)이 되도록 PPU 도 이 값으로 맞춘다.
    const int MossyCellPixels = 512; // 원본 시트의 칸 크기 그대로.
    const int CavePixels = 192;      // 동굴 발판 한 덩어리가 대략 190px 단위로 그려져 있다.

    // 칸 둘레에 가장자리 픽셀을 늘려 두르는 여백. 밉맵이 한 단계 내려갈 때마다 절반이 되므로
    // 화면 축소 비율(512px → 약 60px, 밉맵 3단계)에서도 1px 이상 남도록 잡았다.
    const int MossyPadding = 16;
    const int CavePadding = 8;

    #endregion
    #region 조각 정의

    // 이웃 조건은 KingAndPigTileBuilder 와 같은 3×3 글자판이다(1 있어야 함 · 0 없어야 함 · 마침표 상관없음).
    // 같은 조건의 조각이 둘 이상이면 한 규칙으로 묶어 무작위로 고른다.
    readonly struct Piece {
        public readonly int Col;       // 구운 시트의 칸 좌표(왼쪽에서 0부터).
        public readonly int Row;       // 구운 시트의 칸 좌표(위에서 0부터).
        public readonly string Name;
        public readonly string Top;    // null 이면 규칙에 넣지 않고 스프라이트만 남긴다.
        public readonly string Mid;
        public readonly string Bottom;

        public Piece(int col, int row, string name, string top = null, string mid = null, string bottom = null) {
            Col = col;
            Row = row;
            Name = name;
            Top = top;
            Mid = mid;
            Bottom = bottom;
        }

        public bool HasRule => Top != null;
        public string MaskKey => Top + Mid + Bottom;
    }

    // Mossy - TileSet. 칸 좌표가 원본 시트 좌표와 같다.
    // 이음새 판정은 칸 테두리의 불투명도를 읽어 뽑았다(변 가운데가 꽉 차 있으면 이어짐, 모서리가 비어 있으면 안쪽 모서리).
    static readonly Piece[] MossyPieces = {
        new Piece(1, 1, "Inner",             "111", "1.1", "111"),
        new Piece(4, 0, "Inner_DR",          "111", "1.1", "110"),
        new Piece(5, 0, "Inner_DL",          "111", "1.1", "011"),
        new Piece(4, 1, "Inner_UR",          "110", "1.1", "111"),
        new Piece(5, 1, "Inner_UL",          "011", "1.1", "111"),
        new Piece(6, 1, "Inner_UL_DL",       "011", "1.1", "011"),
        new Piece(6, 0, "Inner_UL_UR",       "010", "1.1", "111"),
        new Piece(6, 3, "Inner_DL_DR",       "111", "1.1", "010"),
        new Piece(6, 2, "Inner_UR_DR",       "110", "1.1", "110"),
        new Piece(2, 6, "Inner_UR_DL",       "110", "1.1", "011"),
        new Piece(3, 6, "Inner_UL_DR",       "011", "1.1", "110"),
        new Piece(4, 3, "Inner_UR_DL_DR",    "110", "1.1", "010"),
        new Piece(5, 2, "Inner_UL_DL_DR",    "011", "1.1", "010"),
        new Piece(4, 2, "Inner_UL_UR_DR",    "010", "1.1", "110"),
        new Piece(5, 3, "Inner_UL_UR_DL",    "010", "1.1", "011"),
        new Piece(5, 5, "Inner_NoDiagonals", "010", "1.1", "010"),

        new Piece(1, 0, "Top",               ".0.", "1.1", "111"),
        new Piece(2, 4, "Top_DR",            ".0.", "1.1", "110"),
        new Piece(3, 4, "Top_DL",            ".0.", "1.1", "011"),
        new Piece(5, 4, "Top_DL_DR",         ".0.", "1.1", "010"),

        new Piece(1, 2, "Bottom",            "111", "1.1", ".0."),
        new Piece(2, 5, "Bottom_UR",         "110", "1.1", ".0."),
        new Piece(3, 5, "Bottom_UL",         "011", "1.1", ".0."),
        new Piece(5, 6, "Bottom_UL_UR",      "010", "1.1", ".0."),

        new Piece(0, 1, "Left",              ".11", "0.1", ".11"),
        new Piece(0, 4, "Left_DR",           ".11", "0.1", ".10"),
        new Piece(0, 5, "Left_UR",           ".10", "0.1", ".11"),
        new Piece(4, 5, "Left_UR_DR",        ".10", "0.1", ".10"),

        new Piece(2, 1, "Right",             "11.", "1.0", "11."),
        new Piece(1, 4, "Right_DL",          "11.", "1.0", "01."),
        new Piece(1, 5, "Right_UL",          "01.", "1.0", "11."),
        new Piece(6, 5, "Right_UL_DL",       "01.", "1.0", "01."),

        new Piece(0, 0, "CornerTL",          ".0.", "0.1", ".11"),
        new Piece(4, 4, "CornerTL_DR",       ".0.", "0.1", ".10"),
        new Piece(2, 0, "CornerTR",          ".0.", "1.0", "11."),
        new Piece(6, 4, "CornerTR_DL",       ".0.", "1.0", "01."),
        new Piece(0, 2, "CornerBL",          ".11", "0.1", ".0."),
        new Piece(4, 6, "CornerBL_UR",       ".10", "0.1", ".0."),
        new Piece(2, 2, "CornerBR",          "11.", "1.0", ".0."),
        new Piece(6, 6, "CornerBR_UL",       "01.", "1.0", ".0."),

        new Piece(3, 1, "ColumnMid",         ".1.", "0.0", ".1."),
        new Piece(3, 0, "ColumnTop",         ".0.", "0.0", ".1."),
        new Piece(3, 2, "ColumnBottom",      ".1.", "0.0", ".0."),
        new Piece(1, 3, "RowMid",            ".0.", "1.1", ".0."),
        new Piece(0, 3, "RowLeft",           ".0.", "0.1", ".0."),
        new Piece(2, 3, "RowRight",          ".0.", "1.0", ".0."),
        new Piece(3, 3, "Single",            ".0.", "0.0", ".0."),

        // 가운데에 구멍이 뚫린 안쪽 칸 2장. 무작위에 섞으면 벽 속 아무 데나 구멍이 나고 충돌은 막혀 있어
        // 규칙에서 뺐다. 필요한 자리에 손으로 칠하도록 스프라이트로만 남긴다.
        new Piece(0, 6, "Inner_HoleA"),
        new Piece(1, 6, "Inner_HoleB"),
    };

    // Mossy - FloatingPlatforms. 한 줄 두께의 가로 발판 · 세로 기둥 · 외딴 덤불만 있다.
    // ㄱ 자나 두 줄 두께처럼 여기에 없는 모양은 기본 그림(RowMid)으로 칠해진다.
    static readonly Piece[] FloatingPieces = {
        new Piece(0, 0, "RowLeft",      ".0.", "0.1", ".0."),
        new Piece(1, 0, "RowMid",       ".0.", "1.1", ".0."),
        new Piece(2, 0, "RowRight",     ".0.", "1.0", ".0."),
        new Piece(0, 1, "ColumnTop",    ".0.", "0.0", ".1."),
        new Piece(1, 1, "ColumnMid",    ".1.", "0.0", ".1."),
        new Piece(2, 1, "ColumnBottom", ".1.", "0.0", ".0."),
        new Piece(0, 2, "Single_A",     ".0.", "0.0", ".0."),
        new Piece(1, 2, "Single_B",     ".0.", "0.0", ".0."),
        new Piece(2, 2, "Single_C",     ".0.", "0.0", ".0."),
    };

    // Cave - Platforms. 대각선은 보지 않는 16가지다. 안쪽 모서리 그림이 원본에 없으니
    // 대각선을 따져 봐야 고를 그림이 없고, 암벽 속이 고르게 어두워 꺾인 자리도 가운데 칸으로 충분하다.
    static readonly Piece[] CavePieces = {
        new Piece(0, 0, "CornerTL",     ".0.", "0.1", ".1."),
        new Piece(1, 0, "Top",          ".0.", "1.1", ".1."),
        new Piece(2, 0, "CornerTR",     ".0.", "1.0", ".1."),
        new Piece(0, 1, "Left",         ".1.", "0.1", ".1."),
        new Piece(1, 1, "Center",       ".1.", "1.1", ".1."),
        new Piece(2, 1, "Right",        ".1.", "1.0", ".1."),
        new Piece(0, 2, "CornerBL",     ".1.", "0.1", ".0."),
        new Piece(1, 2, "Bottom",       ".1.", "1.1", ".0."),
        new Piece(2, 2, "CornerBR",     ".1.", "1.0", ".0."),
        new Piece(0, 3, "RowLeft",      ".0.", "0.1", ".0."),
        new Piece(1, 3, "RowMid",       ".0.", "1.1", ".0."),
        new Piece(2, 3, "RowRight",     ".0.", "1.0", ".0."),
        new Piece(3, 0, "ColumnTop",    ".0.", "0.0", ".1."),
        new Piece(3, 1, "ColumnMid",    ".1.", "0.0", ".1."),
        new Piece(3, 2, "ColumnBottom", ".1.", "0.0", ".0."),
        new Piece(3, 3, "Single",       ".0.", "0.0", ".0."),
    };

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Tilemap/Build Mossy · Cave Rule Tiles")]
    public static void Build() {
        try {
            EditorUtility.DisplayProgressBar("Mossy · Cave 룰 타일", "Mossy - TileSet 굽는 중", 0f);
            BuildMossy();
            EditorUtility.DisplayProgressBar("Mossy · Cave 룰 타일", "Mossy - FloatingPlatforms 굽는 중", 0.4f);
            BuildFloating();
            EditorUtility.DisplayProgressBar("Mossy · Cave 룰 타일", "Cave - Platforms 굽는 중", 0.7f);
            BuildCave();
        } finally {
            EditorUtility.ClearProgressBar();
        }
        AssetDatabase.SaveAssets();
    }

    #endregion
    #region 시트별 굽기

    static void BuildMossy() {
        SourceSheet sheet = SourceSheet.Load(MossySheetPath);
        if (sheet == null) return;

        // 자르기는 한 번만 한다. 색 버전(원본 · 회색)마다 다시 자르면 같은 계산을 두 번 하는 데다,
        // Cut() 이 쌍선형 보간이라 두 번 돌리면 부동소수 반올림이 미세하게 달라질 수 있다.
        var cells = new Dictionary<string, Color[]>();
        foreach (Piece piece in MossyPieces) {
            var source = new Rect(piece.Col * MossyCellPixels, piece.Row * MossyCellPixels, MossyCellPixels, MossyCellPixels);
            cells[piece.Name] = sheet.Cut(source, MossyCellPixels, sheet.Bounds);
        }

        BakeAndFinish(cells, MossyCellPixels, MossyPadding, MossyPieces, MossyAtlasPath, "Mossy", MossyTilePath, "Inner", gray: false);
        BakeAndFinish(cells, MossyCellPixels, MossyPadding, MossyPieces, MossyGrayAtlasPath, "MossyGray", MossyGrayTilePath, "Inner", gray: true);
    }

    // 자르는 좌표는 시트를 픽셀 단위로 비교해 찾은 값이다. 원본 그림이 바뀌면 다시 찾아야 한다.
    static void BuildFloating() {
        SourceSheet sheet = SourceSheet.Load(FloatingSheetPath);
        if (sheet == null) return;

        const int n = MossyCellPixels;
        var cells = new Dictionary<string, Color[]>();

        // 가로 발판: 맨 윗줄 발판 하나에서 세 칸을 뜬다. 이 발판은 x=750 과 x=1262 의 세로줄이 픽셀까지 같아서
        // [750, 1262) 칸은 자기 자신과 이어 붙여도 끊기지 않고, 그 앞뒤 칸이 곧 양 끝 조각이 된다.
        // clip 은 이 발판만 남기는 사각형이다. 양 끝 칸이 옆 덤불 · 기둥 그림까지 걸쳐 잘리기 때문이다.
        const int RowTop = 5; // 발판(y 36~486)을 칸 가운데에 둔다.
        var rowClip = new RectInt(440, 0, 1120, 530);
        cells["RowLeft"] = sheet.Cut(new Rect(238, RowTop, n, n), n, rowClip);
        cells["RowMid"] = sheet.Cut(new Rect(750, RowTop, n, n), n, rowClip);
        cells["RowRight"] = sheet.Cut(new Rect(1262, RowTop, n, n), n, rowClip);

        // 세로 기둥: 픽셀까지 같은 줄은 없고 y=231 과 y=743 이 가장 비슷하다. 그 사이를 가운데 칸으로 쓰되,
        // 아래 끝 48px 를 윗칸의 같은 자리(y=183~231)로 서서히 바꿔 다음 가운데 칸의 첫 줄과 맞춘다.
        const int ColumnLeft = 1562; // 기둥(x 1594~2042)을 칸 가운데에 둔다.
        const int ColumnSeam = 231;
        const int ColumnFade = 48;
        var columnClip = new RectInt(1560, 0, 488, 1040);
        Color[] columnTop = sheet.Cut(new Rect(ColumnLeft, ColumnSeam - n, n, n), n, columnClip);
        Color[] columnMid = sheet.Cut(new Rect(ColumnLeft, ColumnSeam, n, n), n, columnClip);
        cells["ColumnTop"] = columnTop;
        cells["ColumnMid"] = Blend(columnMid, columnTop, n, false, 1f - (float)ColumnFade / n, 1f);
        cells["ColumnBottom"] = sheet.Cut(new Rect(ColumnLeft, ColumnSeam + n, n, n), n, columnClip);

        // 외딴 덤불 3종. 각 덤불의 테두리 상자를 칸 가운데에 둔다.
        (string Name, RectInt Box)[] bushes = {
            ("Single_A", new RectInt(116, 51, 256, 317)),
            ("Single_B", new RectInt(47, 542, 381, 368)),
            ("Single_C", new RectInt(113, 1110, 281, 275)),
        };
        foreach ((string name, RectInt box) in bushes) {
            int left = Mathf.RoundToInt(box.center.x) - n / 2;
            int top = Mathf.RoundToInt(box.center.y) - n / 2;
            // 20px 여유는 본체에서 떨어져 나간 잎 조각까지 담기 위해서다.
            var clip = new RectInt(box.x - 20, box.y - 20, box.width + 40, box.height + 40);
            cells[name] = sheet.Cut(new Rect(left, top, n, n), n, clip);
        }

        BakeAndFinish(cells, n, MossyPadding, FloatingPieces, FloatingAtlasPath, "MossyFloating", FloatingTilePath, "RowMid", gray: false);
        BakeAndFinish(cells, n, MossyPadding, FloatingPieces, FloatingGrayAtlasPath, "MossyFloatingGray", FloatingGrayTilePath, "RowMid", gray: true);
    }

    static void BuildCave() {
        SourceSheet sheet = SourceSheet.Load(CaveSheetPath);
        if (sheet == null) return;

        const int n = CavePixels;
        var atlas = new Atlas(n, CavePadding, CavePieces);

        // 3×2 덩어리 → 9분할. 두 줄뿐이라 가운데 줄은 윗줄 · 아랫줄을 섞어 만든다.
        Color[,][] block = ExpandBlock(sheet, new Rect(28, 219, 562, 369), 3, 2, n);
        string[,] blockNames = {
            { "CornerTL", "Top", "CornerTR" },
            { "Left", "Center", "Right" },
            { "CornerBL", "Bottom", "CornerBR" },
        };
        for (int row = 0; row < 3; row++) {
            for (int col = 0; col < 3; col++) atlas.Put(blockNames[row, col], block[row, col]);
        }

        // 가로 2칸 발판 → 한 줄 발판 세 조각.
        Color[,][] rowPlatform = ExpandBlock(sheet, new Rect(233, 23, 389, 197), 2, 1, n);
        atlas.Put("RowLeft", rowPlatform[0, 0]);
        atlas.Put("RowMid", rowPlatform[0, 1]);
        atlas.Put("RowRight", rowPlatform[0, 2]);

        // 세로 2칸 기둥 → 한 칸 기둥 세 조각.
        Color[,][] columnPlatform = ExpandBlock(sheet, new Rect(48, 638, 191, 370), 1, 2, n);
        atlas.Put("ColumnTop", columnPlatform[0, 0]);
        atlas.Put("ColumnMid", columnPlatform[1, 0]);
        atlas.Put("ColumnBottom", columnPlatform[2, 0]);

        atlas.Put("Single", sheet.Cut(new Rect(22, 19, 191, 195), n, sheet.Bounds));

        Finish(atlas, CaveAtlasPath, "Cave", CaveTilePath, CavePieces, "Center");
    }

    // 이미 잘라 둔 조각(cells)으로 아틀라스 하나를 굽고 룰 타일까지 만든다. gray 가 켜지면 칸마다
    // Desaturate 를 거친다 — 원본과 조각표 · 자르기 좌표를 그대로 공유하므로 색만 다른 형제 타일이 나온다.
    static void BakeAndFinish(Dictionary<string, Color[]> cells, int cellPixels, int padding, Piece[] pieces,
        string atlasPath, string prefix, string tilePath, string defaultPiece, bool gray) {
        var atlas = new Atlas(cellPixels, padding, pieces);
        foreach (Piece piece in pieces) {
            if (!cells.TryGetValue(piece.Name, out Color[] cell)) continue;
            atlas.Put(piece.Name, gray ? Desaturate(cell) : cell);
        }
        Finish(atlas, atlasPath, prefix, tilePath, pieces, defaultPiece);
    }

    static void Finish(Atlas atlas, string atlasPath, string prefix, string tilePath, Piece[] pieces, string defaultPiece) {
        atlas.Save(atlasPath);
        Dictionary<string, Sprite> sprites = ImportAtlas(atlasPath, atlas, prefix);
        if (sprites == null) return;

        BuildRuleTile(tilePath, prefix, pieces, defaultPiece, sprites);
        Debug.Log($"[Tilemap] {prefix} 룰 타일을 만들었습니다 (조각 {pieces.Length}개): {tilePath}");
    }

    #endregion
    #region 회색 버전

    // 명도(휘도)만 남기고 채도를 걷어낸다. 색이 알파를 미리 곱한 값(premultiplied)이라도 휘도는 선형
    // 결합이라 c*a 의 휘도가 곧 휘도(c)*a 와 같다 — 언프리멀티플라이 없이 그대로 계산해도 결과가 맞다.
    static Color[] Desaturate(Color[] cell) {
        var result = new Color[cell.Length];
        for (int i = 0; i < cell.Length; i++) {
            Color c = cell[i];
            float luma = 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
            result[i] = new Color(luma, luma, luma, c.a);
        }
        return result;
    }

    #endregion
    #region 덩어리 늘리기

    // 반복 구간이 없는 통짜 그림(동굴 발판)에서 [시작 · 가운데 · 끝] 세 조각을 만들 때 섞는 구간.
    // 칸의 앞 30% 와 뒤 30% 는 원본 그대로 두고 가운데 40% 만 섞는다. 가장자리를 원본으로 남겨야
    // 이음새 줄이 정확히 맞고, 섞이는 자리는 어두운 암벽 속이라 겹쳐 보이는 것이 덜 드러난다.
    const float BlendStart = 0.3f;
    const float BlendEnd = 0.7f;

    // 원본을 cols×rows 칸으로 나눈 뒤, 칸이 2개 이상인 축은 [시작 · 가운데 · 끝] 3칸으로 늘린다.
    // 결과는 [줄, 칸] 순서이며, 원본 칸이 1개인 축은 그대로 1칸이다.
    static Color[,][] ExpandBlock(SourceSheet sheet, Rect source, int cols, int rows, int size) {
        float cellWidth = source.width / cols;
        float cellHeight = source.height / rows;

        var widened = new Color[rows][][];
        for (int row = 0; row < rows; row++) {
            var cells = new Color[cols][];
            for (int col = 0; col < cols; col++) {
                var rect = new Rect(source.x + col * cellWidth, source.y + row * cellHeight, cellWidth, cellHeight);
                cells[col] = sheet.Cut(rect, size, sheet.Bounds);
            }
            widened[row] = ExpandAxis(cells, size, true);
        }

        int outCols = widened[0].Length;
        int outRows = rows == 1 ? 1 : 3;
        var result = new Color[outRows, outCols][];
        for (int col = 0; col < outCols; col++) {
            var column = new Color[rows][];
            for (int row = 0; row < rows; row++) column[row] = widened[row][col];

            Color[][] tall = ExpandAxis(column, size, false);
            for (int row = 0; row < outRows; row++) result[row, col] = tall[row];
        }
        return result;
    }

    // 한 축의 원본 칸(첫 · 둘째 · 마지막)으로 [시작 · 가운데 · 끝] 을 만든다.
    //   시작   = 첫 칸 그대로
    //   가운데 = 둘째 칸으로 시작해 첫 칸으로 끝남
    //   끝     = 둘째 칸으로 시작해 마지막 칸으로 끝남
    // 원본에서 첫 칸 끝 바로 뒤에는 둘째 칸 시작이 붙어 있다. 위처럼 만들면 어느 조각을 어떤 순서로 붙여도
    // 이음새가 전부 "첫 칸 끝 → 둘째 칸 시작" 한 가지가 되어 끊기지 않는다(가운데끼리 이어 붙여도 마찬가지).
    // 원본이 2칸이면 마지막 칸이 곧 둘째 칸이라 끝 조각은 둘째 칸 그대로다.
    static Color[][] ExpandAxis(Color[][] cells, int size, bool alongX) {
        if (cells.Length == 1) return cells;

        Color[] first = cells[0];
        Color[] second = cells[1];
        Color[] last = cells[cells.Length - 1];
        return new[] {
            first,
            Blend(second, first, size, alongX, BlendStart, BlendEnd),
            Blend(second, last, size, alongX, BlendStart, BlendEnd),
        };
    }

    // from 에서 to 로 넘어가는 칸. 축을 따라 [start, end] 구간에서만 부드럽게 섞고, 그 앞은 from · 뒤는 to 그대로다.
    static Color[] Blend(Color[] from, Color[] to, int size, bool alongX, float start, float end) {
        var weights = new float[size];
        for (int i = 0; i < size; i++) {
            float t = Mathf.Clamp01(((i + 0.5f) / size - start) / (end - start));
            weights[i] = t * t * (3f - 2f * t);
        }

        var result = new Color[size * size];
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                int index = y * size + x;
                result[index] = Color.LerpUnclamped(from[index], to[index], weights[alongX ? x : y]);
            }
        }
        return result;
    }

    #endregion
    #region 픽셀

    // 원본 시트. 좌표는 전부 그림판처럼 왼쪽 위가 (0, 0) 이다.
    sealed class SourceSheet {
        public readonly int Width;
        public readonly int Height;
        readonly Color32[] pixels; // 유니티 텍스처 순서라 아래 줄부터 들어 있다.

        public RectInt Bounds => new RectInt(0, 0, Width, Height);

        SourceSheet(int width, int height, Color32[] pixels) {
            Width = width;
            Height = height;
            this.pixels = pixels;
        }

        // 임포트된 텍스처가 아니라 PNG 파일을 직접 읽는다. 원본 임포트 설정이 최대 2048 로 줄여 두어서
        // 텍스처로 읽으면 3584px 시트의 칸 경계가 소수점으로 어긋난다.
        public static SourceSheet Load(string path) {
            if (!File.Exists(path)) {
                Debug.LogError($"[Tilemap] 원본 시트를 찾지 못했습니다: {path}");
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try {
                if (!texture.LoadImage(File.ReadAllBytes(path))) {
                    Debug.LogError($"[Tilemap] 원본 시트를 읽지 못했습니다: {path}");
                    return null;
                }
                return new SourceSheet(texture.width, texture.height, texture.GetPixels32());
            } finally {
                Object.DestroyImmediate(texture);
            }
        }

        // source 사각형을 size×size 칸 하나로 옮긴다. 크기가 다르면 쌍선형으로 늘리거나 줄인다.
        // clip 바깥 픽셀은 투명으로 본다. 결과는 알파를 미리 곱한 색이다 — 섞을 때 투명한 자리의 검은 색이
        // 번져 나와 테두리가 거뭇해지는 것을 막는다.
        public Color[] Cut(Rect source, int size, RectInt clip) {
            var cell = new Color[size * size];
            float stepX = source.width / size;
            float stepY = source.height / size;
            for (int y = 0; y < size; y++) {
                float sy = source.y + (y + 0.5f) * stepY - 0.5f;
                int y0 = Mathf.FloorToInt(sy);
                float fy = sy - y0;
                for (int x = 0; x < size; x++) {
                    float sx = source.x + (x + 0.5f) * stepX - 0.5f;
                    int x0 = Mathf.FloorToInt(sx);
                    float fx = sx - x0;
                    Color upper = Color.LerpUnclamped(Texel(x0, y0, clip), Texel(x0 + 1, y0, clip), fx);
                    Color lower = Color.LerpUnclamped(Texel(x0, y0 + 1, clip), Texel(x0 + 1, y0 + 1, clip), fx);
                    cell[y * size + x] = Color.LerpUnclamped(upper, lower, fy);
                }
            }
            return cell;
        }

        Color Texel(int x, int y, RectInt clip) {
            if (x < clip.xMin || x >= clip.xMax || y < clip.yMin || y >= clip.yMax) return Color.clear;
            if (x < 0 || x >= Width || y < 0 || y >= Height) return Color.clear;

            Color32 c = pixels[(Height - 1 - y) * Width + x];
            float a = c.a / 255f;
            return new Color(c.r / 255f * a, c.g / 255f * a, c.b / 255f * a, a);
        }
    }

    // 구운 시트. 칸마다 가장자리 픽셀을 padding 만큼 바깥으로 늘려 두른다.
    sealed class Atlas {
        public readonly int CellPixels;
        public readonly int Size;
        public readonly List<(string Name, RectInt Rect)> Cells = new List<(string, RectInt)>();

        readonly int padding;
        readonly int pitch;
        readonly Dictionary<string, Vector2Int> slots = new Dictionary<string, Vector2Int>();
        readonly Color32[] pixels; // 유니티 텍스처 순서(아래 줄부터).

        public Atlas(int cellPixels, int padding, Piece[] layout) {
            CellPixels = cellPixels;
            this.padding = padding;
            pitch = cellPixels + padding * 2;

            int span = 0;
            foreach (Piece piece in layout) {
                slots[piece.Name] = new Vector2Int(piece.Col, piece.Row);
                span = Mathf.Max(span, piece.Col + 1, piece.Row + 1);
            }
            // 2의 거듭제곱으로 맞춘다. 나중에 임포트 최대 크기를 줄여도 칸 경계가 정수로 떨어진다.
            Size = Mathf.NextPowerOfTwo(span * pitch);
            pixels = new Color32[Size * Size];
        }

        public void Put(string name, Color[] cell) {
            if (!slots.TryGetValue(name, out Vector2Int slot)) {
                Debug.LogError($"[Tilemap] 조각표에 없는 이름입니다: {name}");
                return;
            }

            int left = slot.x * pitch + padding;
            int top = slot.y * pitch + padding;
            for (int py = -padding; py < CellPixels + padding; py++) {
                int sourceRow = Mathf.Clamp(py, 0, CellPixels - 1) * CellPixels;
                int destRow = (Size - 1 - (top + py)) * Size;
                for (int px = -padding; px < CellPixels + padding; px++) {
                    int sourceX = Mathf.Clamp(px, 0, CellPixels - 1);
                    pixels[destRow + left + px] = Unpremultiply(cell[sourceRow + sourceX]);
                }
            }
            Cells.Add((name, new RectInt(left, Size - top - CellPixels, CellPixels, CellPixels)));
        }

        public void Save(string path) {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try {
                texture.SetPixels32(pixels);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            } finally {
                Object.DestroyImmediate(texture);
            }
        }

        static Color32 Unpremultiply(Color c) {
            if (c.a <= 0f) return new Color32(0, 0, 0, 0);
            return new Color(c.r / c.a, c.g / c.a, c.b / c.a, c.a); // Color → Color32 변환이 0~1 로 잘라 준다.
        }
    }

    #endregion
    #region 임포트 · 잘라내기

    static Dictionary<string, Sprite> ImportAtlas(string path, Atlas atlas, string prefix) {
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) {
            Debug.LogError($"[Tilemap] 텍스처 임포터를 열지 못했습니다: {path}");
            return null;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = atlas.CellPixels;
        importer.filterMode = FilterMode.Bilinear; // 붓 그림이라 점 필터로 줄이면 잎 테두리가 계단지고 지글거린다.
        importer.mipmapEnabled = true;             // 칸을 8배 가까이 줄여 그리므로 밉맵이 없으면 잎이 반짝이며 깨진다.
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.maxTextureSize = atlas.Size;      // 기본 2048 이면 4096 시트가 반으로 줄어든다.
        // 픽셀아트가 아니라 압축 손실이 눈에 띄지 않는다. 무압축이면 4096 시트 하나가 수십 MB 라 고품질 압축으로 둔다.
        // 칸 경계가 4의 배수라 압축 블록이 칸과 여백에 걸쳐 섞이지 않는다.
        importer.textureCompression = TextureImporterCompression.CompressedHQ;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;  // 타이트 메시는 투명한 가장자리를 잘라 칸 크기를 어긋나게 한다.
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        settings.spriteGenerateFallbackPhysicsShape = true; // 충돌을 그림 윤곽으로 바꿔 쓸 때를 위해 남겨 둔다.
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        // 이미 있던 이름은 GUID 를 그대로 물려준다. 새로 발급하면 다시 구울 때마다
        // 룰 타일과 이미 칠해 둔 타일맵의 스프라이트 참조가 전부 끊긴다.
        var keepIds = new Dictionary<string, GUID>();
        foreach (SpriteRect old in provider.GetSpriteRects()) keepIds[old.name] = old.spriteID;

        var rects = new List<SpriteRect>();
        foreach ((string pieceName, RectInt rect) in atlas.Cells) {
            string name = $"{prefix}_{pieceName}";
            rects.Add(new SpriteRect {
                name = name,
                spriteID = keepIds.TryGetValue(name, out GUID id) ? id : GUID.Generate(),
                rect = new Rect(rect.x, rect.y, rect.width, rect.height),
                alignment = SpriteAlignment.Center,
                pivot = new Vector2(0.5f, 0.5f),
                border = Vector4.zero,
            });
        }

        provider.SetSpriteRects(rects.ToArray());
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());
        provider.Apply();
        importer.SaveAndReimport();

        var sprites = new Dictionary<string, Sprite>();
        foreach (Object obj in AssetDatabase.LoadAllAssetsAtPath(path)) {
            if (obj is Sprite sprite) sprites[sprite.name] = sprite;
        }

        Debug.Log($"[Tilemap] {atlas.Size}px 시트에 {atlas.CellPixels}px 칸 {atlas.Cells.Count}개를 구웠습니다: {path}");
        return sprites;
    }

    #endregion
    #region 룰 타일 만들기

    static void BuildRuleTile(string path, string prefix, Piece[] pieces, string defaultPiece, Dictionary<string, Sprite> sprites) {
        RuleTile tile = AssetDatabase.LoadAssetAtPath<RuleTile>(path);
        if (tile == null) {
            // 있으면 덮어쓰기만 한다. 새로 만들면 GUID 가 바뀌어 이미 칠해 둔 타일맵이 전부 빈칸이 된다.
            tile = ScriptableObject.CreateInstance<RuleTile>();
            AssetDatabase.CreateAsset(tile, path);
        }

        var rules = new List<RuleTile.TilingRule>();
        var missing = new List<string>();
        // 같은 이웃 조건끼리 묶는다. 조건이 겹치는 규칙이 둘이면 앞의 것만 쓰이므로, 한 규칙의 무작위 그림으로 합친다.
        foreach (IGrouping<string, Piece> group in pieces.Where(p => p.HasRule).GroupBy(p => p.MaskKey)) {
            var variants = new List<Sprite>();
            foreach (Piece piece in group) {
                string name = $"{prefix}_{piece.Name}";
                if (sprites.TryGetValue(name, out Sprite sprite)) variants.Add(sprite);
                else missing.Add(name);
            }
            if (variants.Count == 0) continue;

            Piece first = group.First();
            var rule = new RuleTile.TilingRule {
                m_Sprites = variants.ToArray(),
                m_ColliderType = Tile.ColliderType.Grid, // KingAndPig 타일과 같은 이유로 칸 전체 충돌.
                m_Output = variants.Count > 1
                    ? RuleTile.TilingRuleOutput.OutputSprite.Random
                    : RuleTile.TilingRuleOutput.OutputSprite.Single,
            };
            rule.ApplyNeighbors(KingAndPigTileBuilder.ParseMask(first.Top, first.Mid, first.Bottom));
            rules.Add(rule);
        }

        if (missing.Count > 0) {
            Debug.LogWarning($"[Tilemap] 스프라이트를 찾지 못한 조각: {string.Join(", ", missing)}");
        }

        // 어느 규칙에도 안 걸린 칸에 쓰이는 그림. Mossy 는 47가지를 다 채워 실제로는 나오지 않고,
        // 떠 있는 발판 · 동굴 발판은 준비된 모양 밖(ㄱ 자 · T 자 등)에서 이 그림이 쓰인다.
        sprites.TryGetValue($"{prefix}_{defaultPiece}", out Sprite fallback);
        tile.m_DefaultSprite = fallback;
        // 그림 윤곽(Sprite)을 따르면 조각마다 투명 여백이 달라 윗면 높이가 칸마다 어긋나 발밑 판정에 걸린다.
        tile.m_DefaultColliderType = Tile.ColliderType.Grid;
        tile.m_TilingRules = rules;
        EditorUtility.SetDirty(tile);
    }

    #endregion
}
#endif
