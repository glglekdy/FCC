#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// King And Pig 타일 시트의 색을 「퇴락한 빈티지 극장」 쪽으로 낡혀서 굽는다.
//
// 왜 머티리얼 틴트가 아니라 굽기인가:
// 이 시트는 고유 색이 20가지뿐이라 색조별로 다르게 손댈 수 있는데, 틴트는 전체에 같은 곱셈만 걸려
// "붉은 축은 살리고 시안 축만 죽이기"가 안 된다. 원본 Decorations 의 유리창이 시안(#98CBD8)이라
// 아트 디렉션 금지 사항 1번(보라/시안 계열)에 걸리는데, 이건 색조 가중 탈채로만 잡힌다.
// 구워 두면 런타임 비용도 0이고 셰이더를 따로 물리지 않아 나중에 Light2D 를 넣어도 영향이 없다.
//
// 왜 원본 PNG 를 덮어쓰는데 안전한가:
// 룰 타일은 스프라이트를 (png 의 guid + fileID) 로 참조하고 그 fileID 는 .meta 의 internalIDToNameTable
// 에 들어 있다. 픽셀만 바뀌고 .meta 는 그대로이므로 룰 타일과 씬에 깔린 타일이 하나도 끊기지 않는다.
// 대신 원본은 Original~ 폴더에 한 번만 떠 둔다. **이름 끝의 물결표는 유니티가 통째로 무시하는 표시라
// 임포트도 .meta 생성도 일어나지 않는다.** 굽기는 언제나 이 원본에서 다시 시작하므로 세기를 바꿔
// 여러 번 돌려도 색이 겹쳐 타지 않는다.
//
// 사용법: Tools ▸ FCC ▸ Tilemap ▸ Weather Tileset ▸ (Light / Worn / Ruined)
//         되돌리기는 같은 메뉴의 Restore Originals
//         뒷세계 타일은 같은 메뉴의 Back World (사본) — 원본을 두고 옆에 사본 시트 · 룰 타일을 만든다.
public static class TilesetWeatherBaker {
    #region 상수

    const string SheetDir = "Assets/_Project/Assets/Tiles/King And Pig";
    const string TileDir = "Assets/_Project/Assets/Tiles";
    const string OriginalDirName = "Original~"; // **물결표로 끝나야 유니티가 무시한다. 이름을 바꾸면 원본까지 임포트된다.**

    static readonly string[] Sheets = {
        "Terrain (32x32).png",
        "Decorations (32x32).png",
    };

    // 사본으로 굽는 대상. 룰 타일이 물고 있는 시트는 Terrain 하나뿐이라 Decorations 는 뺐다.
    const string VariantSheet = "Terrain (32x32).png";

    // 사본을 뜰 룰 타일. 던전 방은 벽(Rose)과 발판(Gold)을 함께 칠하므로 한쪽만 뜨면 방 절반만 색이 바뀐다.
    static readonly string[] VariantTiles = {
        "Tile_KingAndPig_Rose",
        "Tile_KingAndPig_Gold",
    };

    #endregion
    #region 색 설정

    // 톤 램프의 세 점과, 채도를 지킬 색조 축. 굽기의 "분위기"는 전부 여기서 정해진다.
    readonly struct TonePalette {
        public readonly Color Shadow;         // 램프의 암부.
        public readonly Color Mid;            // 램프의 중간톤. 없으면 어둠에서 밝은 끝으로 곧장 이어져 회색으로 빠진다.
        public readonly Color High;           // 램프의 명부.
        public readonly float KeepHueDegrees; // 이 색조에서 멀수록 채도를 더 많이 버린다.

        public TonePalette(Color shadow, Color mid, Color high, float keepHueDegrees) {
            Shadow = shadow;
            Mid = mid;
            High = high;
            KeepHueDegrees = keepHueDegrees;
        }
    }

    // 극장. UiTheme 의 Panel 과 TextHigh 를 그대로 쓰고 중간톤만 "바랜 벨벳"으로 따로 잡았다.
    // 중간점이 없으면 어둠에서 상아로 곧장 이어져 회색으로 빠진다(실제로 2점 램프로 뽑았더니 그랬다).
    static readonly TonePalette Theater = new TonePalette(
        new Color32(0x17, 0x11, 0x13, 255), // Panel    — 검정이 아닌 따뜻한 어둠.
        new Color32(0x6B, 0x4F, 0x4A, 255), // 먼지 앉은 장미 갈색.
        new Color32(0xF0, 0xE6, 0xD8, 255), // TextHigh — 흰색이 아닌 바랜 상아.
        15f);                               // 적등색. 벨벳 적색만 살린다.

    // 뒷세계. 극장의 따뜻한 어둠을 뒤집은 차가운 어둠이다. 램프를 무채색으로 두면 청색이 전혀 읽히지 않고
    // 그냥 회색 돌이 되므로(실제로 채도 낮은 램프로 뽑았더니 그랬다), 램프 자체에 옅은 하늘색을 실었다.
    // 대신 명부를 #8DB9D0 에서 끊고 아래 Ceiling 으로 한 번 더 눌러 시안 네온으로는 가지 않게 했다.
    static readonly TonePalette BackWorldPalette = new TonePalette(
        new Color32(0x08, 0x0D, 0x14, 255), // 푸른 기가 도는 검정.
        new Color32(0x27, 0x44, 0x57, 255), // 젖은 청석.
        new Color32(0x8D, 0xB9, 0xD0, 255), // 흐린 하늘색.
        200f);                              // 하늘색. 원본의 남색 바탕만 채도를 조금 남긴다.

    #endregion
    #region 세기 설정

    // 수치 네 개 + 팔레트가 전부다. 감각을 조정할 때는 여기만 고치고 다시 구우면 된다(항상 원본에서 다시 시작한다).
    readonly struct Recipe {
        public readonly float Desaturate;   // 채도를 얼마나 버릴지(0~1). 색조별로 다시 가중된다.
        public readonly float ToneBlend;    // 톤 램프에 얼마나 실을지(0~1). 높을수록 팔레트 색에 가까워진다.
        public readonly float Floor;        // 암부를 이만큼 들어올린다. 순검정을 막는다.
        public readonly float Ceiling;      // 하이라이트를 이만큼에서 눌러 끊는다. 순백을 막는다.
        public readonly TonePalette Palette;

        public Recipe(float desaturate, float toneBlend, float floor, float ceiling, TonePalette palette) {
            Desaturate = desaturate;
            ToneBlend = toneBlend;
            Floor = floor;
            Ceiling = ceiling;
            Palette = palette;
        }
    }

    static readonly Recipe Light = new Recipe(0.30f, 0.22f, 0.05f, 0.93f, Theater);  // 살짝 바랜 정도.
    static readonly Recipe Worn = new Recipe(0.45f, 0.38f, 0.07f, 0.88f, Theater);   // 권장. 석재는 상아, 벽돌은 바랜 적갈.
    static readonly Recipe Ruined = new Recipe(0.60f, 0.55f, 0.10f, 0.82f, Theater); // 거의 무채색. 지하감옥용.

    // 원본의 주황 · 분홍을 전부 걷어내야 해서 극장 세기들보다 탈채와 램프 비중이 훨씬 높다.
    // Ceiling 0.70 이 "어둡게"의 핵심이다. 분홍 벽돌이 원래 밝아서(휘도 0.62) 이보다 높이면 벽이 발판보다 떠 보인다.
    static readonly Recipe BackWorld = new Recipe(0.80f, 0.80f, 0.03f, 0.70f, BackWorldPalette);

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Tilemap/Weather Tileset/Light")]
    static void BakeLight() => Bake(Light, "Light");

    [MenuItem("Tools/FCC/Tilemap/Weather Tileset/Worn (권장)")]
    static void BakeWorn() => Bake(Worn, "Worn");

    [MenuItem("Tools/FCC/Tilemap/Weather Tileset/Ruined")]
    static void BakeRuined() => Bake(Ruined, "Ruined");

    [MenuItem("Tools/FCC/Tilemap/Weather Tileset/Back World (사본)")]
    static void BakeBackWorld() => BakeVariant(BackWorld, "BackWorld");

    [MenuItem("Tools/FCC/Tilemap/Weather Tileset/Restore Originals")]
    static void Restore() {
        int restored = 0;
        foreach (string sheet in Sheets) {
            string original = Path.Combine(OriginalDir, sheet);
            if (!File.Exists(original)) {
                Debug.LogWarning($"[TilesetWeatherBaker] 원본이 없어 되돌리지 못했습니다: {sheet}");
                continue;
            }
            File.Copy(original, AbsolutePath(sheet), true);
            AssetDatabase.ImportAsset($"{SheetDir}/{sheet}", ImportAssetOptions.ForceUpdate);
            restored++;
        }
        Debug.Log($"[TilesetWeatherBaker] 원본으로 되돌렸습니다. ({restored}장)");
    }

    #endregion
    #region 굽기

    static void Bake(Recipe recipe, string label) {
        EnsureOriginals();

        int baked = 0;
        foreach (string sheet in Sheets) {
            byte[] png = BakePng(sheet, recipe);
            if (png == null) continue;

            File.WriteAllBytes(AbsolutePath(sheet), png);
            AssetDatabase.ImportAsset($"{SheetDir}/{sheet}", ImportAssetOptions.ForceUpdate);
            baked++;
        }

        Debug.Log($"[TilesetWeatherBaker] {label} 세기로 구웠습니다. ({baked}장) " +
                  "되돌리려면 Tools ▸ FCC ▸ Tilemap ▸ Weather Tileset ▸ Restore Originals");
    }

    // 원본 보관본에서 읽어 구운 PNG 바이트를 돌려준다. 실패하면 null.
    static byte[] BakePng(string sheet, Recipe recipe) {
        string original = Path.Combine(OriginalDir, sheet);
        if (!File.Exists(original)) {
            Debug.LogWarning($"[TilesetWeatherBaker] 원본이 없어 건너뜁니다: {sheet}");
            return null;
        }

        // AssetDatabase 로 불러오지 않고 파일 바이트를 직접 읽는 이유: 임포트된 텍스처는 압축되어 있거나
        // Read/Write 가 꺼져 있을 수 있어 픽셀을 그대로 못 꺼낸다. RGBA32 로 디코드하면 원본 바이트 그대로다.
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(File.ReadAllBytes(original))) {
            Debug.LogError($"[TilesetWeatherBaker] PNG 를 읽지 못했습니다: {sheet}");
            Object.DestroyImmediate(tex);
            return null;
        }

        // GetPixels32 만 쓴다. 실수형 GetPixels 는 색 공간 설정에 따라 감마 변환이 끼어들어
        // 구울 때마다 미세하게 밝기가 달라진다.
        Color32[] pixels = tex.GetPixels32();
        for (int i = 0; i < pixels.Length; i++) {
            pixels[i] = Weather(pixels[i], recipe);
        }
        tex.SetPixels32(pixels);
        tex.Apply();

        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        return png;
    }

    static Color32 Weather(Color32 src, Recipe recipe) {
        // 거의 투명한 칸은 손대지 않는다. 외곽의 반투명 픽셀 색이 바뀌면 이음새에 테두리가 생긴다.
        if (src.a <= 8) return src;

        float r = src.r / 255f, g = src.g / 255f, b = src.b / 255f;
        float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;

        // 1) 색조 가중 탈채. 지킬 축은 채도를 절반쯤만 버리고 반대편 축은 전부 버린다.
        //    같은 비율로 깎으면 극장의 벨벳 적색까지 함께 죽어 화면이 회색으로 주저앉는다.
        Color.RGBToHSV(new Color(r, g, b), out float hue, out _, out _);
        float amount = recipe.Desaturate * Mathf.Lerp(1f, 0.45f, ChromaKeep(hue, recipe.Palette.KeepHueDegrees));
        r = Mathf.Lerp(r, luma, amount);
        g = Mathf.Lerp(g, luma, amount);
        b = Mathf.Lerp(b, luma, amount);

        // 2) 밝기에 따라 팔레트의 암부 → 중간톤 → 명부 램프를 실어 전체 색온도를 하나로 모은다.
        Color tone = Ramp(luma, recipe.Palette);
        r = Mathf.Lerp(r, tone.r, recipe.ToneBlend);
        g = Mathf.Lerp(g, tone.g, recipe.ToneBlend);
        b = Mathf.Lerp(b, tone.b, recipe.ToneBlend);

        // 3) 명암 폭을 좁힌다. 낡은 인쇄물처럼 검정도 흰색도 끝까지 가지 않는 것이 "바램"의 핵심이다.
        float span = recipe.Ceiling - recipe.Floor;
        r = recipe.Floor + r * span;
        g = recipe.Floor + g * span;
        b = recipe.Floor + b * span;

        return new Color32(ToByte(r), ToByte(g), ToByte(b), src.a);
    }

    // 이 색조가 채도를 얼마나 지킬 수 있는지(0 = 전부 버림, 1 = 최대한 지킴).
    static float ChromaKeep(float hue01, float keepHueDegrees) {
        // Mathf.Repeat 을 쓰는 이유: C# 의 나머지 연산은 음수에서 음수를 돌려줘 색상환을 한 바퀴 돌 때 끊긴다.
        float distance = Mathf.Abs(Mathf.Repeat(hue01 * 360f - keepHueDegrees + 180f, 360f) - 180f);
        return Mathf.Max(0f, Mathf.Cos(Mathf.Min(distance, 180f) * 0.9f * Mathf.Deg2Rad));
    }

    static Color Ramp(float t, TonePalette palette) {
        return t < 0.5f
            ? Color.Lerp(palette.Shadow, palette.Mid, t / 0.5f)
            : Color.Lerp(palette.Mid, palette.High, (t - 0.5f) / 0.5f);
    }

    static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

    #endregion
    #region 사본 굽기

    // 뒷세계처럼 원본 색도 계속 써야 하는 곳이 생기면 시트를 덮어쓰지 않고 옆에 사본을 굽는다.
    // 사본 시트는 원본 .meta 를 guid 만 바꿔 옮겨 만든다. 슬라이스(칸 위치 · 이름 · internalID)가 그대로 따라오므로
    // 룰 타일의 스프라이트를 같은 이름끼리 짝지어 갈아 끼우기만 하면 규칙은 손대지 않아도 된다.
    static void BakeVariant(Recipe recipe, string suffix) {
        EnsureOriginals();

        string variantSheet = VariantSheetName(VariantSheet, suffix);
        byte[] png = BakePng(VariantSheet, recipe);
        if (png == null) return;

        // 메타를 PNG 보다 먼저 둔다. 메타 없이 임포트되면 슬라이스 없는 통짜 텍스처로 잡혀 짝지을 스프라이트가 없다.
        WriteVariantMeta(VariantSheet, variantSheet);
        File.WriteAllBytes(AbsolutePath(variantSheet), png);
        AssetDatabase.ImportAsset($"{SheetDir}/{variantSheet}", ImportAssetOptions.ForceUpdate);

        int cloned = 0;
        foreach (string tileName in VariantTiles) {
            if (CloneTile(tileName, suffix, $"{SheetDir}/{VariantSheet}", $"{SheetDir}/{variantSheet}")) cloned++;
        }
        AssetDatabase.SaveAssets();

        Debug.Log($"[TilesetWeatherBaker] {suffix} 사본을 구웠습니다. 시트: {SheetDir}/{variantSheet} · 룰 타일 {cloned}개: " +
                  $"{TileDir}/{{이름}}_{suffix}.asset");
    }

    // "Terrain (32x32).png" → "Terrain_BackWorld (32x32).png". 칸 크기 표기를 끝에 남겨야 원본과 나란히 정렬된다.
    static string VariantSheetName(string sheet, string suffix) {
        string stem = Path.GetFileNameWithoutExtension(sheet);
        int size = stem.IndexOf(" (");
        string renamed = size < 0 ? $"{stem}_{suffix}" : $"{stem.Substring(0, size)}_{suffix}{stem.Substring(size)}";
        return renamed + Path.GetExtension(sheet);
    }

    // 원본 메타를 매번 새로 옮기되 guid 는 사본의 것을 지킨다. 원본을 다시 잘랐을 때 사본도 따라가야 하고,
    // guid 가 바뀌면 사본 룰 타일로 칠해 둔 칸이 전부 끊기기 때문이다.
    static void WriteVariantMeta(string sheet, string variantSheet) {
        string variantMeta = AbsolutePath(variantSheet) + ".meta";
        string guid = null;
        if (File.Exists(variantMeta)) {
            Match kept = GuidLine.Match(File.ReadAllText(variantMeta));
            if (kept.Success) guid = kept.Groups[1].Value;
        }
        if (string.IsNullOrEmpty(guid)) guid = System.Guid.NewGuid().ToString("N");

        string text = File.ReadAllText(AbsolutePath(sheet) + ".meta");
        File.WriteAllText(variantMeta, GuidLine.Replace(text, $"guid: {guid}", 1));
    }

    static readonly Regex GuidLine = new Regex(@"^guid: ([0-9a-f]{32})", RegexOptions.Multiline);

    static bool CloneTile(string tileName, string suffix, string sheetPath, string variantSheetPath) {
        string sourcePath = $"{TileDir}/{tileName}.asset";
        string variantPath = $"{TileDir}/{tileName}_{suffix}.asset";

        var source = AssetDatabase.LoadAssetAtPath<TileBase>(sourcePath);
        if (source == null) {
            Debug.LogWarning($"[TilesetWeatherBaker] 룰 타일을 찾지 못해 건너뜁니다: {sourcePath}");
            return false;
        }

        // 사본이 이미 있으면 지우고 새로 뜨지 않고 내용만 덮어쓴다. 새로 뜨면 guid 가 바뀌어
        // 이 타일로 칠해 둔 방 프리팹의 칸이 전부 비어 버린다. 덮어쓰기라 원본의 규칙 수정도 따라온다.
        var variant = AssetDatabase.LoadAssetAtPath<TileBase>(variantPath);
        if (variant == null) {
            if (!AssetDatabase.CopyAsset(sourcePath, variantPath)) {
                Debug.LogError($"[TilesetWeatherBaker] 룰 타일을 복사하지 못했습니다: {variantPath}");
                return false;
            }
            variant = AssetDatabase.LoadAssetAtPath<TileBase>(variantPath);
        } else {
            EditorUtility.CopySerialized(source, variant);
        }
        variant.name = Path.GetFileNameWithoutExtension(variantPath);

        var twins = new Dictionary<string, Sprite>();
        foreach (Object asset in AssetDatabase.LoadAllAssetRepresentationsAtPath(variantSheetPath)) {
            if (asset is Sprite sprite) twins[sprite.name] = sprite;
        }

        // RuleTile 의 필드 이름에 기대지 않고 스프라이트 참조를 전부 훑는다. 기본 스프라이트 · 규칙별 스프라이트 ·
        // 애니메이션 칸이 전부 다른 배열에 흩어져 있어 하나라도 빠뜨리면 그 모양만 원본 색으로 튄다.
        var serialized = new SerializedObject(variant);
        SerializedProperty property = serialized.GetIterator();
        int swapped = 0, missing = 0;
        while (property.Next(true)) {
            if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
            if (!(property.objectReferenceValue is Sprite sprite)) continue;
            if (AssetDatabase.GetAssetPath(sprite) != sheetPath) continue;

            if (twins.TryGetValue(sprite.name, out Sprite twin)) {
                property.objectReferenceValue = twin;
                swapped++;
            } else {
                missing++;
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(variant);

        if (missing > 0) {
            Debug.LogWarning($"[TilesetWeatherBaker] {variantPath}: 사본 시트에서 짝을 못 찾은 스프라이트 {missing}개가 원본 색으로 남았습니다.");
        }
        return swapped > 0;
    }

    #endregion
    #region 원본 보관

    static string OriginalDir => Path.Combine(AbsoluteSheetDir, OriginalDirName);

    static string AbsoluteSheetDir =>
        Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? string.Empty, SheetDir);

    static string AbsolutePath(string sheet) => Path.Combine(AbsoluteSheetDir, sheet);

    // 원본은 딱 한 번만 뜬다. 이미 구워진 파일을 원본으로 덮어써 버리면 되돌릴 길이 사라진다.
    static void EnsureOriginals() {
        Directory.CreateDirectory(OriginalDir);
        foreach (string sheet in Sheets) {
            string original = Path.Combine(OriginalDir, sheet);
            if (File.Exists(original)) continue;

            string live = AbsolutePath(sheet);
            if (!File.Exists(live)) {
                Debug.LogWarning($"[TilesetWeatherBaker] 시트를 찾지 못했습니다: {SheetDir}/{sheet}");
                continue;
            }
            File.Copy(live, original);
            Debug.Log($"[TilesetWeatherBaker] 원본을 보관했습니다: {SheetDir}/{OriginalDirName}/{sheet}");
        }
    }

    #endregion
}
#endif
