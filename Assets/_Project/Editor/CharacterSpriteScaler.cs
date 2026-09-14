#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// 캐릭터가 화면에서 가질 크기를 한 곳에서 정한다.
//
// 스프라이트의 화면 크기는 (그림 픽셀 ÷ PPU × 렌더러 스케일) 로 정해지는데, 이 셋을 캐릭터마다
// 다르게 주무르면 같은 게임 안에서 키가 제멋대로가 된다. 실제로 그렇게 어긋나 있었다 — 플레이어는
// PPU 100 짜리 1363px 그림을 렌더러 스케일 0.14 로 줄여 1.9유닛을 만들고 있었고, 몬스터는 스케일이
// 1 이라 620px 짜리 위저드가 그대로 6.2유닛, 플레이어의 3.2배로 나왔다.
//
// 그래서 기준을 하나로 못 박는다 — 표에는 "이 캐릭터는 화면에서 몇 유닛" 만 적고, 렌더러 스케일은
// 언제나 1 로 두며, 크기는 전부 PPU 로 맞춘다. 눈대중으로 스케일을 만질 일이 없어지고, 새 아트가
// 들어와도 표에 한 줄 적으면 끝난다.
//
// 한 캐릭터가 여러 장(걷기 8장 · 공격 시트 · 점프 시트)으로 나뉘어 있으면 전부 같은 배율로 움직여야
// 한다. 장마다 목표 높이에 따로 맞추면 포즈마다 키가 달라져 걸을 때와 뛸 때 캐릭터가 커졌다 작아졌다
// 한다. 그래서 기준 한 장의 PPU 만 목표 높이에서 역산하고, 나머지 장에는 그 배율을 그대로 곱한다.
//
// 사용법:
//   Tools ▸ FCC ▸ Character ▸ Report Sprite Sizes  → 지금 크기만 확인한다 (아무것도 바꾸지 않음)
//   Tools ▸ FCC ▸ Character ▸ Apply Sprite Sizes   → 표대로 PPU · 렌더러 스케일을 맞춘다
// 둘 다 멱등이라 여러 번 눌러도 안전하다.
public static class CharacterSpriteScaler {
    #region 크기 표

    // 목표 높이는 "실제로 그려진 부분"의 높이다. 그림 주변 투명 여백은 빼고 잰다 — 여백까지 포함해
    // 재면 같은 크기로 그린 캐릭터도 캔버스 여백에 따라 키가 달라진다.
    struct CharacterSize {
        public string name;
        public float targetHeight; // 기준 그림이 화면에서 가질 높이(유닛).
        public string referenceTexture; // 목표 높이를 재는 기준 그림. 서 있는 포즈를 쓴다.
        public string[] textures; // 같은 배율로 함께 움직일 그림들 (기준 그림 포함).
        public string prefab; // 렌더러 스케일을 1 로 되돌릴 프리팹. 비우면 건너뛴다(아직 붙지 않은 아트).
        public string rendererObject; // 그 안에서 스프라이트를 그리는 오브젝트 이름.
    }

    const string PlayerDir = "Assets/_Project/Assets/Sprites/Player";
    const string MonsterDir = "Assets/_Project/Assets/Sprites/Monster";
    const string MonsterPrefabDir = "Assets/_Project/Assets/Prefabs/Monster";

    public const string PlayerName = "플레이어"; // PlayerAnimationBuilder 가 크기를 먼저 맞출 때 쓴다.

    const byte AlphaThreshold = 8; // 이보다 옅은 픽셀은 그림 가장자리의 흐린 자국으로 보고 범위에서 뺀다.

    static readonly CharacterSize[] Sizes = {
        new CharacterSize {
            name = PlayerName,
            targetHeight = 1.91f, // 지금 화면에 나오는 키를 그대로 유지한다. 이 값이 다른 모든 캐릭터의 기준이 된다.
            referenceTexture = PlayerDir + "/Walk/Player_Walk_1.png",
            textures = PlayerTextures(),
            prefab = "Assets/_Project/Assets/Prefabs/Playerble/Player.prefab",
            rendererObject = "Player_Renderer",
        },
        new CharacterSize {
            name = "귀신 (Monster_Fly)",
            targetHeight = 1.80f, // 플레이 화면을 보고 정한 값. 원본 그림(3.59u)의 절반이며 플레이어와 거의 같은 덩치다.
            referenceTexture = MonsterDir + "/귀신 기본상태.png",
            textures = new[] {
                MonsterDir + "/귀신 기본상태.png",
                MonsterDir + "/귀신 돌진 전.png",
                MonsterDir + "/귀신 돌진.png",
            },
            prefab = MonsterPrefabDir + "/Monster_Fly.prefab",
            rendererObject = "Renderer",
        },
        new CharacterSize {
            name = "위저드 (Monster_Wraith)",
            // 플레이 화면을 보고 정한 값. 원본 그림(6.20u)의 1/4이며 플레이어보다 조금 작다. 그림이 옆으로
            // 넓어(공격 포즈는 가로가 세로의 1.8배) 키를 다른 몹보다 낮게 잡아야 화면을 과하게 가리지 않는다.
            targetHeight = 1.55f,
            referenceTexture = MonsterDir + "/위저드 기본.png",
            textures = new[] {
                MonsterDir + "/위저드 기본.png",
                MonsterDir + "/위저드 차징.png",
                MonsterDir + "/위저드 공격.png",
            },
            prefab = MonsterPrefabDir + "/Monster_Wraith.prefab",
            rendererObject = "Renderer",
        },
        new CharacterSize {
            name = "좀비괴물",
            targetHeight = 2.10f, // 아직 프리팹에 붙지 않았다. 크기만 미리 맞춰 두면 붙이는 순간 바로 맞는다.
            referenceTexture = MonsterDir + "/좀비괴물.png",
            textures = new[] { MonsterDir + "/좀비괴물.png" },
            prefab = string.Empty,
            rendererObject = string.Empty,
        },
    };

    static string[] PlayerTextures() {
        var paths = new string[10];
        for (int i = 0; i < 8; i++) paths[i] = $"{PlayerDir}/Walk/Player_Walk_{i + 1}.png";
        paths[8] = PlayerDir + "/Player_Attack.png";
        paths[9] = PlayerDir + "/Player_Jump.png";
        return paths;
    }

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Character/Report Sprite Sizes", priority = 200)]
    public static void Report() {
        var log = new StringBuilder("[CharacterSpriteScaler] 지금 화면에 나오는 크기\n");

        foreach (CharacterSize size in Sizes) {
            float scale = ReadRendererScale(size);
            log.AppendLine($"■ {size.name} — 목표 {size.targetHeight:0.00}u · 렌더러 스케일 {scale:0.###}");

            foreach (string path in size.textures) {
                float ink = MeasureInkHeight(path);
                float ppu = ReadPixelsPerUnit(path);
                if (ink <= 0f || ppu <= 0f) {
                    log.AppendLine($"    {System.IO.Path.GetFileName(path)} — 읽지 못했습니다");
                    continue;
                }

                float onScreen = ink / ppu * scale;
                log.AppendLine($"    {System.IO.Path.GetFileName(path),-22} 그림 {ink,5:0}px · PPU {ppu,7:0.#} → {onScreen:0.00}u");
            }
        }

        Debug.Log(log.ToString());
    }

    [MenuItem("Tools/FCC/Character/Apply Sprite Sizes", priority = 201)]
    public static void ApplyAll() {
        var log = new StringBuilder();
        foreach (CharacterSize size in Sizes) ApplyOne(size, log);

        AssetDatabase.Refresh();
        Debug.Log($"[CharacterSpriteScaler] 캐릭터 크기를 맞췄습니다.\n{log}");
    }

    // 특정 캐릭터만 맞춘다. PlayerAnimationBuilder 가 피벗을 계산하기 전에 크기를 먼저 확정하는 용도다.
    public static void ApplyCharacter(string name) {
        foreach (CharacterSize size in Sizes) {
            if (size.name != name) continue;

            var log = new StringBuilder();
            ApplyOne(size, log);
            if (log.Length > 0) Debug.Log($"[CharacterSpriteScaler] {name} 크기를 맞췄습니다.\n{log}");
            return;
        }

        Debug.LogWarning($"[CharacterSpriteScaler] 크기 표에 '{name}' 이 없습니다.");
    }

    #endregion
    #region 크기 맞추기

    static void ApplyOne(CharacterSize size, StringBuilder log) {
        float referenceInk = MeasureInkHeight(size.referenceTexture);
        float referencePpu = ReadPixelsPerUnit(size.referenceTexture);
        if (referenceInk <= 0f || referencePpu <= 0f) {
            Debug.LogError($"[CharacterSpriteScaler] {size.name} — 기준 그림을 읽지 못했습니다: {size.referenceTexture}");
            return;
        }

        // 기준 그림이 목표 높이가 되는 PPU. 렌더러 스케일은 1 로 되돌릴 것이므로 여기 곱하지 않는다.
        float targetReferencePpu = referenceInk / size.targetHeight;
        float ratio = targetReferencePpu / referencePpu;

        log.AppendLine($"■ {size.name} — 기준 {System.IO.Path.GetFileName(size.referenceTexture)} {referenceInk:0}px → {size.targetHeight:0.00}u (배율 {ratio:0.###})");

        foreach (string path in size.textures) {
            float ppu = ReadPixelsPerUnit(path);
            if (ppu <= 0f) {
                Debug.LogError($"[CharacterSpriteScaler] {size.name} — 그림을 찾지 못했습니다: {path}");
                continue;
            }

            float next = ppu * ratio;
            if (WritePixelsPerUnit(path, next)) {
                log.AppendLine($"    {System.IO.Path.GetFileName(path),-22} PPU {ppu:0.#} → {next:0.#}");
            }
        }

        ResetRendererScale(size, log);
    }

    static void ResetRendererScale(CharacterSize size, StringBuilder log) {
        if (string.IsNullOrEmpty(size.prefab)) return;

        GameObject root = PrefabUtility.LoadPrefabContents(size.prefab);
        if (root == null) {
            Debug.LogError($"[CharacterSpriteScaler] 프리팹을 열지 못했습니다: {size.prefab}");
            return;
        }

        try {
            Transform target = FindChild(root.transform, size.rendererObject);
            if (target == null) {
                Debug.LogError($"[CharacterSpriteScaler] {size.name} — 프리팹에서 '{size.rendererObject}' 를 찾지 못했습니다.");
                return;
            }

            if (target.localScale == Vector3.one) return; // 이미 맞으면 프리팹을 다시 쓰지 않는다.

            log.AppendLine($"    {System.IO.Path.GetFileName(size.prefab)} — '{size.rendererObject}' 스케일 {target.localScale.y:0.###} → 1");
            target.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(root, size.prefab);
        } finally {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static float ReadRendererScale(CharacterSize size) {
        if (string.IsNullOrEmpty(size.prefab)) return 1f;

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(size.prefab);
        if (prefab == null) return 1f;

        Transform target = FindChild(prefab.transform, size.rendererObject);
        return target != null ? target.localScale.y : 1f;
    }

    #endregion
    #region 그림 크기 재기

    // 그림이 실제로 그려진 부분의 높이(px). 여러 칸으로 잘린 시트면 읽기 순서상 첫 칸을 잰다.
    // 임포트된 텍스처가 아니라 원본 PNG 를 직접 푸는 이유는, 임포트본은 압축되어 알파가 뭉개지고
    // 읽으려면 Read/Write 를 켰다 끄느라 재임포트를 두 번 더 해야 하기 때문이다.
    public static float MeasureInkHeight(string texturePath) {
        Texture2D source = LoadSourcePixels(texturePath);
        if (source == null) return 0f;

        Color32[] pixels = source.GetPixels32();
        int width = source.width;
        int height = source.height;
        Object.DestroyImmediate(source);

        Rect region = FirstFrameRect(texturePath, width, height);
        int x0 = Mathf.Clamp(Mathf.FloorToInt(region.xMin), 0, width);
        int x1 = Mathf.Clamp(Mathf.CeilToInt(region.xMax), 0, width);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(region.yMin), 0, height);
        int y1 = Mathf.Clamp(Mathf.CeilToInt(region.yMax), 0, height);

        int top = -1;
        for (int y = y1 - 1; y >= y0; y--) {
            if (!RowHasInk(pixels, width, y, x0, x1)) continue;
            top = y;
            break;
        }

        if (top < 0) return 0f;

        int bottom = top;
        for (int y = y0; y <= top; y++) {
            if (!RowHasInk(pixels, width, y, x0, x1)) continue;
            bottom = y;
            break;
        }

        return top - bottom + 1;
    }

    static bool RowHasInk(Color32[] pixels, int width, int y, int x0, int x1) {
        int row = y * width;
        for (int x = x0; x < x1; x++) {
            if (pixels[row + x].a >= AlphaThreshold) return true;
        }

        return false;
    }

    // 읽기 순서상 첫 칸 = 가장 윗줄의 가장 왼쪽. 잘리지 않은 그림이면 텍스처 전체가 된다.
    static Rect FirstFrameRect(string texturePath, int width, int height) {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(texturePath).OfType<Sprite>().ToArray();
        if (sprites.Length == 0) return new Rect(0f, 0f, width, height);

        Sprite first = sprites
            .OrderByDescending(s => Mathf.FloorToInt(s.rect.center.y / Mathf.Max(height / 2f, 1f)))
            .ThenBy(s => s.rect.xMin)
            .First();

        return first.rect;
    }

    // 프로젝트에 남기지 않는 임시 텍스처로 원본 PNG 를 푼다. 쓰고 나면 바로 지워야 한다.
    public static Texture2D LoadSourcePixels(string texturePath) {
        if (!System.IO.File.Exists(texturePath)) return null;

        var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (texture.LoadImage(System.IO.File.ReadAllBytes(texturePath))) return texture;

        Object.DestroyImmediate(texture);
        return null;
    }

    #endregion
    #region 임포터

    static float ReadPixelsPerUnit(string texturePath) {
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        return importer != null ? importer.spritePixelsPerUnit : 0f;
    }

    static bool WritePixelsPerUnit(string texturePath, float pixelsPerUnit) {
        var importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer == null) return false;

        // 같은 값이면 건드리지 않는다. 재임포트는 느린 데다, 시트는 잘라 둔 칸까지 다시 읽어야 한다.
        if (Mathf.Abs(importer.spritePixelsPerUnit - pixelsPerUnit) < 0.01f) return false;

        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.SaveAndReimport();
        return true;
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
