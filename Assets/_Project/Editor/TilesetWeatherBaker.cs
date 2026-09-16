#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

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
public static class TilesetWeatherBaker {
    #region 상수

    const string SheetDir = "Assets/_Project/Assets/Tiles/King And Pig";
    const string OriginalDirName = "Original~"; // **물결표로 끝나야 유니티가 무시한다. 이름을 바꾸면 원본까지 임포트된다.**

    static readonly string[] Sheets = {
        "Terrain (32x32).png",
        "Decorations (32x32).png",
    };

    // 톤 램프의 세 점. UiTheme 의 Panel 과 TextHigh 를 그대로 쓰고 중간톤만 "바랜 벨벳"으로 따로 잡았다.
    // 중간점이 없으면 어둠에서 상아로 곧장 이어져 회색으로 빠진다(실제로 2점 램프로 뽑았더니 그랬다).
    static readonly Color Shadow = new Color32(0x17, 0x11, 0x13, 255); // Panel    — 검정이 아닌 따뜻한 어둠.
    static readonly Color Mid = new Color32(0x6B, 0x4F, 0x4A, 255);    // 먼지 앉은 장미 갈색.
    static readonly Color High = new Color32(0xF0, 0xE6, 0xD8, 255);   // TextHigh — 흰색이 아닌 바랜 상아.

    // 색조 가중의 기준이 되는 축. 15도(적등색)에서 멀수록 채도를 더 많이 버린다.
    const float WarmHueDegrees = 15f;

    #endregion
    #region 세기 설정

    // 네 값이 전부다. 감각을 조정할 때는 여기만 고치고 다시 구우면 된다(항상 원본에서 다시 시작한다).
    readonly struct Recipe {
        public readonly float Desaturate; // 채도를 얼마나 버릴지(0~1). 색조별로 다시 가중된다.
        public readonly float ToneBlend;  // 위 3점 램프에 얼마나 실을지(0~1). 높을수록 세피아에 가깝다.
        public readonly float Floor;      // 암부를 이만큼 들어올린다. 순검정을 막는다.
        public readonly float Ceiling;    // 하이라이트를 이만큼에서 눌러 끊는다. 순백을 막는다.

        public Recipe(float desaturate, float toneBlend, float floor, float ceiling) {
            Desaturate = desaturate;
            ToneBlend = toneBlend;
            Floor = floor;
            Ceiling = ceiling;
        }
    }

    static readonly Recipe Light = new Recipe(0.30f, 0.22f, 0.05f, 0.93f);  // 살짝 바랜 정도.
    static readonly Recipe Worn = new Recipe(0.45f, 0.38f, 0.07f, 0.88f);   // 권장. 석재는 상아, 벽돌은 바랜 적갈.
    static readonly Recipe Ruined = new Recipe(0.60f, 0.55f, 0.10f, 0.82f); // 거의 무채색. 지하감옥용.

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Tilemap/Weather Tileset/Light")]
    static void BakeLight() => Bake(Light, "Light");

    [MenuItem("Tools/FCC/Tilemap/Weather Tileset/Worn (권장)")]
    static void BakeWorn() => Bake(Worn, "Worn");

    [MenuItem("Tools/FCC/Tilemap/Weather Tileset/Ruined")]
    static void BakeRuined() => Bake(Ruined, "Ruined");

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
            string original = Path.Combine(OriginalDir, sheet);
            if (!File.Exists(original)) {
                Debug.LogWarning($"[TilesetWeatherBaker] 원본이 없어 건너뜁니다: {sheet}");
                continue;
            }

            // AssetDatabase 로 불러오지 않고 파일 바이트를 직접 읽는 이유: 임포트된 텍스처는 압축되어 있거나
            // Read/Write 가 꺼져 있을 수 있어 픽셀을 그대로 못 꺼낸다. RGBA32 로 디코드하면 원본 바이트 그대로다.
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(original))) {
                Debug.LogError($"[TilesetWeatherBaker] PNG 를 읽지 못했습니다: {sheet}");
                Object.DestroyImmediate(tex);
                continue;
            }

            // GetPixels32 만 쓴다. 실수형 GetPixels 는 색 공간 설정에 따라 감마 변환이 끼어들어
            // 구울 때마다 미세하게 밝기가 달라진다.
            Color32[] pixels = tex.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) {
                pixels[i] = Weather(pixels[i], recipe);
            }
            tex.SetPixels32(pixels);
            tex.Apply();

            File.WriteAllBytes(AbsolutePath(sheet), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset($"{SheetDir}/{sheet}", ImportAssetOptions.ForceUpdate);
            baked++;
        }

        Debug.Log($"[TilesetWeatherBaker] {label} 세기로 구웠습니다. ({baked}장) " +
                  "되돌리려면 Tools ▸ FCC ▸ Tilemap ▸ Weather Tileset ▸ Restore Originals");
    }

    static Color32 Weather(Color32 src, Recipe recipe) {
        // 거의 투명한 칸은 손대지 않는다. 외곽의 반투명 픽셀 색이 바뀌면 이음새에 테두리가 생긴다.
        if (src.a <= 8) return src;

        float r = src.r / 255f, g = src.g / 255f, b = src.b / 255f;
        float luma = 0.2126f * r + 0.7152f * g + 0.0722f * b;

        // 1) 색조 가중 탈채. 붉은 축은 채도를 절반쯤만 버리고 시안 축은 전부 버린다.
        //    같은 비율로 깎으면 벨벳 적색까지 함께 죽어 화면이 회색으로 주저앉는다.
        Color.RGBToHSV(new Color(r, g, b), out float hue, out _, out _);
        float amount = recipe.Desaturate * Mathf.Lerp(1f, 0.45f, ChromaKeep(hue));
        r = Mathf.Lerp(r, luma, amount);
        g = Mathf.Lerp(g, luma, amount);
        b = Mathf.Lerp(b, luma, amount);

        // 2) 밝기에 따라 어둠 → 바랜 벨벳 → 상아 램프를 실어 전체 색온도를 하나로 모은다.
        Color tone = Ramp(luma);
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
    static float ChromaKeep(float hue01) {
        // Mathf.Repeat 을 쓰는 이유: C# 의 나머지 연산은 음수에서 음수를 돌려줘 색상환을 한 바퀴 돌 때 끊긴다.
        float distance = Mathf.Abs(Mathf.Repeat(hue01 * 360f - WarmHueDegrees + 180f, 360f) - 180f);
        return Mathf.Max(0f, Mathf.Cos(Mathf.Min(distance, 180f) * 0.9f * Mathf.Deg2Rad));
    }

    static Color Ramp(float t) {
        return t < 0.5f
            ? Color.Lerp(Shadow, Mid, t / 0.5f)
            : Color.Lerp(Mid, High, (t - 0.5f) / 0.5f);
    }

    static byte ToByte(float v) => (byte)Mathf.Clamp(Mathf.RoundToInt(v * 255f), 0, 255);

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
