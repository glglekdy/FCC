#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using TMPro;

public static class KwFontAssetBuilder
{
    const int SamplingSize = 90;
    const int Padding = 9;
    const int AtlasWidth = 4096;
    const int AtlasHeight = 4096;
    const string OutDir = "Assets/_Project/Assets/Font/SDF";

    // ASCII와 한글만 담으면 화살표·가운뎃점·엠대시처럼 UI 문구에 실제로 쓰는 기호가 빠져서 네모(□)로 나온다.
    // 한글은 멀쩡하게 보이는 탓에 한참 뒤에야 눈에 띈다. 원본 OTF에는 들어 있는 글자들이므로 같이 뽑는다.
    public static readonly uint[] Symbols = {
        0x0027, // ' 어포스트로피 — ASCII 범위인데도 실제로 빠져 있었다(영어 대사의 don't·it's가 전부 네모였다).
        0x00B7, // · 가운뎃점 — "CH1 · 3일차", "[1·2·3] 슬롯 선택"
        0x00D7, // × 곱셈
        0x2013, // – 엔대시
        0x2014, // — 엠대시 — "— 수집 갤러리"
        0x2018, 0x2019, // ' ' 작은따옴표
        0x201C, 0x201D, // " " 큰따옴표
        0x2022, // • 불릿
        0x2026, // … 말줄임표 — 대사에서 자주 쓴다
        0x2190, 0x2191, 0x2192, 0x2193, // ← ↑ → ↓ — "[↑↓] 스킬 선택"
        0x25B6, 0x25B8, 0x25C0, 0x25C2, // ▶ ▸ ◀ ◂
        0x2605, 0x2606, // ★ ☆ — 스킬 레벨 표시
        0x2713, 0x2717, // ✓ ✗ — 목표 체크리스트
        0x20A9, // ₩
    };

    static uint[] BuildCharset(bool includeHangul)
    {
        var list = new List<uint>();
        for (uint c = 0x0020; c <= 0x007E; c++) list.Add(c);   // ASCII
        if (includeHangul)
            for (uint c = 0xAC00; c <= 0xD7A3; c++) list.Add(c);   // Hangul syllables (11,172)
        list.AddRange(Symbols);
        return list.ToArray();
    }

    static void Build(string srcPath, string outName)
    {
        Build(srcPath, outName, AtlasWidth, AtlasHeight, true);
    }

    // atlas 크기와 한글 포함 여부를 따로 받는 이유:
    // 라틴 전용 폰트(Inter)는 글자가 95자뿐이라 4096² 를 쓰면 16MB 를 빈 공간으로 채우게 된다.
    //
    // **SamplingSize 와 Padding 을 폰트마다 다르게 잡지 않는 것이 중요합니다.**
    // TMP 의 SDF 는 글자 바깥으로 Padding 픽셀에 걸쳐 거리값이 0까지 떨어지는데, 글자 크기에 비해 여백이
    // 좁으면 투명까지 내려가지 못하고 글자마다 네모난 자국이 남는다. 실제로 바깥에서 만들어 온 Inter SDF가
    // samplingPointSize 330 / padding 5(1.5%)로 되어 있어 글자마다 반투명 사각형이 보였다.
    // 여기 상수(90 / 9 = 10%)를 그대로 쓰면 그 문제가 생기지 않는다.
    static void Build(string srcPath, string outName, int atlasW, int atlasH, bool includeHangul)
    {
        var srcFont = AssetDatabase.LoadAssetAtPath<Font>(srcPath);
        if (srcFont == null) { Debug.LogError("[KwFont] source not found: " + srcPath); return; }

        // 1) Create in DYNAMIC mode so glyphs can be rasterized into the atlas.
        var fontAsset = TMP_FontAsset.CreateFontAsset(
            srcFont, SamplingSize, Padding, GlyphRenderMode.SDFAA,
            atlasW, atlasH, AtlasPopulationMode.Dynamic, true);
        if (fontAsset == null) { Debug.LogError("[KwFont] CreateFontAsset null: " + outName); return; }

        // 2) Add all glyphs.
        uint[] charset = BuildCharset(includeHangul);
        uint[] missing;
        bool ok = fontAsset.TryAddCharacters(charset, out missing);

        // 3) Switch to STATIC so it ships as a fixed atlas (no runtime growth).
        fontAsset.atlasPopulationMode = AtlasPopulationMode.Static;

        // Make atlas textures no longer readable/clearable as dynamic.
        foreach (var tex in fontAsset.atlasTextures)
            if (tex != null) tex.Apply(false, true);

        // 4) Save asset + sub-assets.
        if (!Directory.Exists(OutDir)) Directory.CreateDirectory(OutDir);
        string assetPath = OutDir + "/" + outName + " SDF.asset";

        AssetDatabase.CreateAsset(fontAsset, assetPath);

        for (int i = 0; i < fontAsset.atlasTextures.Length; i++)
        {
            var tex = fontAsset.atlasTextures[i];
            if (tex != null && !AssetDatabase.Contains(tex))
            {
                tex.name = outName + " Atlas " + i;
                AssetDatabase.AddObjectToAsset(tex, fontAsset);
            }
        }
        if (fontAsset.material != null && !AssetDatabase.Contains(fontAsset.material))
        {
            fontAsset.material.name = outName + " Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(assetPath);

        int missingCount = missing == null ? 0 : missing.Length;
        Debug.Log("[KwFont] " + outName + " done. glyphs=" + fontAsset.glyphTable.Count
            + " chars=" + fontAsset.characterTable.Count
            + " atlases=" + fontAsset.atlasTextures.Length
            + " missing=" + missingCount + " ok=" + ok
            + " -> " + assetPath);
    }

    [MenuItem("Tools/KW Font/Build Bold")]
    public static void BuildBold()
    {
        Build("Assets/_Project/Assets/Font/NotCreatedAsset/강원교육모두 Bold.ttf", "강원교육모두 Bold");
    }

    [MenuItem("Tools/KW Font/Build Light")]
    public static void BuildLight()
    {
        Build("Assets/_Project/Assets/Font/NotCreatedAsset/강원교육모두 Light.ttf", "강원교육모두 Light");
    }

    // Inter 는 라틴 전용이라 한글을 빼고 작은 아틀라스로 뽑는다.
    // **다시 뽑으면 GUID 가 바뀌어 참조가 끊깁니다** — 뽑은 뒤 Tools ▸ FCC ▸ UI ▸ Apply Project Font 를
    // 반드시 실행해서 프리팹·씬의 폰트를 새 자산으로 다시 물려야 합니다.
    [MenuItem("Tools/KW Font/Build Inter Bold")]
    public static void BuildInterBold()
    {
        Build("Assets/_Project/Assets/Font/NotCreatedAsset/Inter_18pt-Bold.ttf", "Inter_18pt-Bold", 1024, 1024, false);
    }

    [MenuItem("Tools/KW Font/Build Inter Regular")]
    public static void BuildInterRegular()
    {
        Build("Assets/_Project/Assets/Font/NotCreatedAsset/Inter_18pt-Regular.ttf", "Inter_18pt-Regular", 1024, 1024, false);
    }

    // 이미 만들어져 프리팹·씬에서 참조 중인 SDF 자산에 빠진 기호만 끼워 넣는다.
    //
    // Build 를 다시 돌리면 될 것 같지만 그러면 안 된다 — CreateAsset 이 파일을 새로 만들면서 GUID 가 바뀌고,
    // 그 폰트를 물고 있던 프리팹·씬의 참조가 전부 끊긴다. 그래서 자산은 그대로 두고 아틀라스에만 글자를 더한다.
    //
    // 잠깐 Dynamic 으로 바꿔 원본 OTF 에서 글자를 구워 넣은 뒤 다시 Static 으로 되돌리는 방식이다.
    [MenuItem("Tools/KW Font/Patch Missing Symbols")]
    public static void PatchMissingSymbols()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { OutDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
            if (font == null) continue;

            var need = new List<uint>();
            foreach (uint u in Symbols)
                if (!HasUnicode(font, u)) need.Add(u);

            if (need.Count == 0)
            {
                Debug.Log("[KwFont] " + font.name + " : 빠진 기호 없음.");
                continue;
            }

            // Build 가 아틀라스를 굳혀(makeNoLongerReadable) 저장하기 때문에, 이미 만들어진 폰트에는 글자를 더
            // 구워 넣을 수 없다. 이 폰트에 그 기호가 꼭 필요하면 Build 로 다시 뽑아야 하는데, 그러면 GUID 가
            // 바뀌어 참조가 끊긴다 — 그보다는 그 글자를 가진 폰트를 fallback 으로 물리는 편이 안전하다.
            bool readable = true;
            foreach (var tex in font.atlasTextures)
                if (tex != null && !tex.isReadable) readable = false;

            if (!readable)
            {
                Debug.Log("[KwFont] " + font.name + " : 아틀라스가 굳어 있어 기호 " + need.Count + "개를 넣지 못했습니다. "
                    + "그 글자가 필요하면 가진 폰트를 fallback 으로 물리세요(Apply Project Font 가 대표 폰트에 걸어 줍니다).");
                continue;
            }

            // Dynamic 이어야 원본에서 글자를 새로 구울 수 있다. 원본 참조가 비어 있으면 GUID 로 되찾는다.
            if (font.sourceFontFile == null)
            {
                var so = new SerializedObject(font);
                var sp = so.FindProperty("m_SourceFontFileGUID");
                string srcPath = sp == null ? null : AssetDatabase.GUIDToAssetPath(sp.stringValue);
                var src = string.IsNullOrEmpty(srcPath) ? null : AssetDatabase.LoadAssetAtPath<Font>(srcPath);

                if (src == null)
                {
                    Debug.LogError("[KwFont] " + font.name + " : 원본 폰트 파일을 찾지 못해 기호를 못 넣었습니다. "
                        + "인스펙터의 Source Font File 을 채우고 다시 실행하세요.", font);
                    continue;
                }

                so.FindProperty("m_SourceFontFile").objectReferenceValue = src;
                so.ApplyModifiedProperties();
            }

            var before = font.atlasPopulationMode;
            font.atlasPopulationMode = AtlasPopulationMode.Dynamic;

            uint[] missing;
            font.TryAddCharacters(need.ToArray(), out missing);

            font.atlasPopulationMode = before;

            // Build 와 달리 makeNoLongerReadable 을 켜지 않는다. 굳혀버리면 다음에 기호를 더 넣을 수 없다.
            foreach (var tex in font.atlasTextures)
                if (tex != null) tex.Apply(false, false);

            EditorUtility.SetDirty(font);

            int failed = missing == null ? 0 : missing.Length;
            Debug.Log("[KwFont] " + font.name + " : 기호 " + (need.Count - failed) + "개를 넣었습니다"
                + (failed > 0 ? " (원본에 없어 실패 " + failed + "개)" : "")
                + ". 이제 문자 " + font.characterTable.Count + "개.", font);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    static bool HasUnicode(TMP_FontAsset font, uint unicode)
    {
        foreach (var ch in font.characterTable)
            if (ch.unicode == unicode) return true;
        return false;
    }
}
#endif
