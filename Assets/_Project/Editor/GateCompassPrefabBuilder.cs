#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// T 키 나침반(Player_GateCompass)이 띄우는 화살표 그림과 프리팹을 만든다.
//
// 화살표는 런타임에 조립하지 않고 실물 프리팹으로 둔다. 색 · 크기 · 정렬 순서를 인스펙터에서 바로 보고 고칠 수
// 있어야 하기 때문이다(UI 구현 규칙과 같은 이유). 이 도구는 없는 것만 만들고, 이미 있으면 손댄 값을 그대로 둔다.
//
// 사용법: Tools ▸ FCC ▸ Build Gate Compass Arrow
public static class GateCompassPrefabBuilder {
    #region 상수

    const string SpritePath = "Assets/_Project/Assets/Sprites/VFX/GateCompassArrow.png";
    const string PrefabPath = "Assets/_Project/Assets/Prefabs/VFX/GateCompassArrow.prefab";

    const string UnlitSpriteMaterialPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    const int TexSize = 128;      // 정사각형. 화살표는 오른쪽(+x)을 보게 그리고, 회전은 스크립트가 준다.
    const float ArrowLength = 0.9f; // 그림 한 장의 가로 길이(유닛).
    const int SortingOrder = 60;  // 플레이어(50)보다 앞. 지형이나 몬스터에 가려지면 길잡이 구실을 못 한다.

    // 바랜 상아색(UiTheme.TextHigh). 포인트 적색은 위험 · 잠금 표시에 쓰고 있어서, 길잡이는 글자와 같은 밝기 계열로 둔다.
    static readonly Color ArrowColor = new Color(0.941f, 0.902f, 0.847f, 0.95f);

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Build Gate Compass Arrow")]
    public static void Build() {
        Sprite sprite = EnsureArrowSprite();
        if (sprite == null) {
            Debug.LogError("[GateCompass] 화살표 그림을 만들지 못했습니다.");
            return;
        }

        GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (existing != null) {
            Debug.Log("[GateCompass] 이미 '" + PrefabPath + "' 가 있어 그대로 둡니다. 다시 만들려면 프리팹을 지우고 실행하세요.");
            return;
        }

        EnsureFolder(Path.GetDirectoryName(PrefabPath).Replace('\\', '/'));

        GameObject root = new GameObject("GateCompassArrow");
        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = ArrowColor;
        renderer.sortingOrder = SortingOrder;

        Material unlit = AssetDatabase.LoadAssetAtPath<Material>(UnlitSpriteMaterialPath);
        if (unlit != null) renderer.sharedMaterial = unlit; // 조명이 깔린 구역에서도 밝기가 변하지 않아야 한다.

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();

        Debug.Log("[GateCompass] '" + PrefabPath + "' 를 만들었습니다. 플레이어의 Player_GateCompass ▸ arrowPrefab 에 연결하세요.");
    }

    #endregion
    #region 화살표 그림

    // 오른쪽을 보는 화살표(자루 + 머리). 이미 있으면 덮어쓰지 않는다 — 손으로 다시 그린 그림이 날아가면 안 된다.
    static Sprite EnsureArrowSprite() {
        Sprite existing = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
        if (existing != null) return existing;

        EnsureFolder(Path.GetDirectoryName(SpritePath).Replace('\\', '/'));

        Texture2D tex = new Texture2D(TexSize, TexSize, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[TexSize * TexSize];

        // 픽셀 하나를 4×4로 나눠 세어 가장자리를 부드럽게 만든다. 도형이 직선뿐이라 계단이 그대로 드러나기 때문이다.
        const int Samples = 4;
        for (int y = 0; y < TexSize; y++) {
            for (int x = 0; x < TexSize; x++) {
                int hit = 0;
                for (int sy = 0; sy < Samples; sy++) {
                    for (int sx = 0; sx < Samples; sx++) {
                        float u = (x + (sx + 0.5f) / Samples) / TexSize;
                        float v = (y + (sy + 0.5f) / Samples) / TexSize;
                        if (IsInsideArrow(u, v)) hit++;
                    }
                }

                byte a = (byte)Mathf.RoundToInt(hit / (float)(Samples * Samples) * 255f);
                pixels[y * TexSize + x] = new Color32(255, 255, 255, a); // 색은 SpriteRenderer 에서 입힌다.
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        File.WriteAllBytes(SpritePath, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(SpritePath);

        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(SpritePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = TexSize / ArrowLength;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
    }

    // u · v 는 0~1. 자루(가는 사각형)와 머리(삼각형)를 합친 모양이며, 회전 중심이 그림 한가운데에 오도록 좌우를 꽉 채우지 않는다.
    static bool IsInsideArrow(float u, float v) {
        float dy = Mathf.Abs(v - 0.5f);

        const float ShaftStart = 0.14f, ShaftEnd = 0.58f, ShaftHalf = 0.085f;
        if (u >= ShaftStart && u <= ShaftEnd && dy <= ShaftHalf) return true;

        const float HeadStart = 0.5f, HeadEnd = 0.93f, HeadHalf = 0.26f;
        if (u < HeadStart || u > HeadEnd) return false;

        float t = (u - HeadStart) / (HeadEnd - HeadStart); // 0 밑변 → 1 뾰족한 끝.
        return dy <= HeadHalf * (1f - t);
    }

    #endregion
    #region 도우미

    static void EnsureFolder(string path) {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    #endregion
}
#endif
