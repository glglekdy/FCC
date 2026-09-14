#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.Tilemaps;

// 타일맵 지형의 뼈대(그리드 · 층 구성 · 충돌)와 임시 타일을 한 번에 맞춰 주는 에디터 도구.
//
// 층을 손으로 만들면 "ground 레이어 · 정적 Rigidbody2D · 합성 충돌 · 이펙터" 중 하나만 빠져도 조용히
// 못 밟는 지형이 되고, 방마다 그 실수가 따로 생긴다. 그래서 층 구성은 이 도구 한 곳에서만 정한다.
//
// 임시 타일은 아트가 들어오기 전까지 레벨을 칠해 볼 수 있게 하는 자리표시다. 타일 에셋(.asset)은 GUID 를
// 유지한 채 스프라이트만 바꿔 끼우면 되므로, 이미 칠해 둔 방이 아트 교체로 끊기지 않는다.
//
// 사용법:
//   Tools ▸ FCC ▸ Tilemap ▸ Build Placeholder Tiles       → 임시 스프라이트 + Rule Tile 생성(멱등)
//   Tools ▸ FCC ▸ Tilemap ▸ Add Grid To Dungeon Rooms     → Room_*.prefab 전부에 그리드 추가(칠한 타일 유지)
//   Tools ▸ FCC ▸ Tilemap ▸ Place Grid In Open Scene      → 열린 씬의 MAP 루트 아래에 그리드 배치
public static class TilemapGridBuilder {
    #region 상수

    const string SpriteDir = "Assets/_Project/Assets/Sprites/Env";
    const string TileDir = "Assets/_Project/Assets/Tiles";
    const string RoomPrefabDir = "Assets/_Project/Assets/Prefabs/Dungeon";

    public const string GridName = "Tilemap";

    // 32px = 1유닛. 방 빌더가 1유닛 격자(폭 38 · 높이 18 등 정수)로 방을 짜 두었으므로 타일 한 칸을 1유닛에 맞춘다.
    const int TilePixels = 32;

    // 발판 두께(픽셀). 방 빌더의 발판 두께 0.6유닛에 가장 가까운 반 칸으로 맞춘다.
    const int PlatformPixels = 16;

    // 정렬 순서. 플레이어 스프라이트가 10 이므로 배경·지형·장식은 그 뒤, 전경만 앞에 둔다.
    const int OrderBackground = -10;
    const int OrderTerrain = 0;
    const int OrderDecor = 5;
    const int OrderForeground = 20;

    #endregion
    #region 층 정의

    enum LayerKind { Visual, Solid, Platform }

    struct LayerSpec {
        public string name;
        public LayerKind kind;
        public int order;

        public LayerSpec(string name, LayerKind kind, int order) {
            this.name = name;
            this.kind = kind;
            this.order = order;
        }
    }

    // 막히는 지형과 통과 발판을 층으로 나누는 이유: 이펙터는 콜라이더 단위로 걸리는데, 타일맵은 층 전체가
    // 콜라이더 하나로 합쳐진다. 한 층에 섞으면 벽까지 아래에서 뚫리는 지형이 된다.
    static readonly LayerSpec[] Layers = {
        new LayerSpec("Tilemap_Background", LayerKind.Visual, OrderBackground),
        new LayerSpec("Tilemap_Solid", LayerKind.Solid, OrderTerrain),
        new LayerSpec("Tilemap_Platform", LayerKind.Platform, OrderTerrain),
        new LayerSpec("Tilemap_Decor", LayerKind.Visual, OrderDecor),
        new LayerSpec("Tilemap_Foreground", LayerKind.Visual, OrderForeground), // 비밀 통로를 덮어 가리는 용도.
    };

    #endregion
    #region 메뉴 — 임시 타일

    [MenuItem("Tools/FCC/Tilemap/Build Placeholder Tiles")]
    public static void BuildPlaceholderTiles() {
        EnsureFolder(SpriteDir);
        EnsureFolder(TileDir);

        // 색은 UI 테마 토큰에서 가져온다. 임시 타일이라도 따로 색을 만들면 화면 톤이 테마에서 벗어난다.
        Sprite solidTop = WriteSprite("Tile_Solid_Top", (x, y) => {
            if (y >= TilePixels - 3) return UiTheme.TextDim; // 밟는 면을 한눈에 알아보도록 윗줄만 밝게.
            if (x == 0 || y == 0) return UiTheme.Line;
            return UiTheme.PanelRaised;
        });
        Sprite solidInner = WriteSprite("Tile_Solid_Inner", (x, y) => {
            if (x == 0 || y == 0) return UiTheme.Line; // 칸 경계가 보여야 단차를 셀 수 있다.
            return UiTheme.Panel;
        });
        Sprite platMid = WriteSprite("Tile_Platform_M", (x, y) => PlatformPixel(x, y, false, false));
        Sprite platLeft = WriteSprite("Tile_Platform_L", (x, y) => PlatformPixel(x, y, true, false));
        Sprite platRight = WriteSprite("Tile_Platform_R", (x, y) => PlatformPixel(x, y, false, true));
        Sprite background = WriteSprite("Tile_Background", (x, y) => {
            if (x == 0 || y == 0) return UiTheme.Stage;
            return UiTheme.Curtain;
        });

        // 발판 충돌은 그림의 윗부분(반 칸)만 차지해야 한다. 기본 생성 형태는 알파 윤곽을 따라가며 가장자리가
        // 조금씩 깎이므로, 사각형을 직접 지정해 발판 윗면 높이를 픽셀 단위로 고정한다.
        SetPhysicsRect(platMid, PlatformPixels);
        SetPhysicsRect(platLeft, PlatformPixels);
        SetPhysicsRect(platRight, PlatformPixels);

        BuildSolidTile(solidTop, solidInner);
        BuildPlatformTile(platMid, platLeft, platRight);
        BuildBackgroundTile(background);

        AssetDatabase.SaveAssets();
        Debug.Log($"[Tilemap] 임시 타일을 만들었습니다. 스프라이트: {SpriteDir}/Tile_*.png · 타일: {TileDir}/");
    }

    static Color PlatformPixel(int x, int y, bool leftEnd, bool rightEnd) {
        int bottom = TilePixels - PlatformPixels;
        if (y < bottom) return UiTheme.Transparent;
        if (y >= TilePixels - 2) return UiTheme.TextDim;
        if (leftEnd && x < 2) return UiTheme.TextDim; // 끝단을 표시해 발판이 어디서 끊기는지 보이게 한다.
        if (rightEnd && x >= TilePixels - 2) return UiTheme.TextDim;
        if (y == bottom) return UiTheme.Line;
        return UiTheme.PanelRaised;
    }

    static void BuildSolidTile(Sprite top, Sprite inner) {
        RuleTile tile = LoadOrCreate<RuleTile>($"{TileDir}/Tile_Solid.asset");
        tile.m_DefaultSprite = inner;
        tile.m_DefaultColliderType = Tile.ColliderType.Grid; // 막힘 지형은 칸 전체가 충돌. 그림 윤곽과 무관하게 정확하다.
        tile.m_TilingRules = new List<RuleTile.TilingRule> {
            Rule(top, Tile.ColliderType.Grid, new Dictionary<Vector3Int, int> {
                { Vector3Int.up, RuleTile.TilingRuleOutput.Neighbor.NotThis },
            }),
        };
        EditorUtility.SetDirty(tile);
    }

    static void BuildPlatformTile(Sprite mid, Sprite left, Sprite right) {
        RuleTile tile = LoadOrCreate<RuleTile>($"{TileDir}/Tile_Platform.asset");
        tile.m_DefaultSprite = mid;
        tile.m_DefaultColliderType = Tile.ColliderType.Sprite; // 반 칸 두께를 쓰려면 스프라이트 물리 형태를 따라야 한다.
        tile.m_TilingRules = new List<RuleTile.TilingRule> {
            Rule(left, Tile.ColliderType.Sprite, new Dictionary<Vector3Int, int> {
                { Vector3Int.left, RuleTile.TilingRuleOutput.Neighbor.NotThis },
                { Vector3Int.right, RuleTile.TilingRuleOutput.Neighbor.This },
            }),
            Rule(right, Tile.ColliderType.Sprite, new Dictionary<Vector3Int, int> {
                { Vector3Int.left, RuleTile.TilingRuleOutput.Neighbor.This },
                { Vector3Int.right, RuleTile.TilingRuleOutput.Neighbor.NotThis },
            }),
        };
        EditorUtility.SetDirty(tile);
    }

    static void BuildBackgroundTile(Sprite sprite) {
        Tile tile = LoadOrCreate<Tile>($"{TileDir}/Tile_Background.asset");
        tile.sprite = sprite;
        tile.colliderType = Tile.ColliderType.None;
        EditorUtility.SetDirty(tile);
    }

    static RuleTile.TilingRule Rule(Sprite sprite, Tile.ColliderType collider, Dictionary<Vector3Int, int> neighbors) {
        var rule = new RuleTile.TilingRule();
        rule.m_Sprites = new[] { sprite };
        rule.m_ColliderType = collider;
        rule.m_Output = RuleTile.TilingRuleOutput.OutputSprite.Single;
        rule.ApplyNeighbors(neighbors);
        return rule;
    }

    #endregion
    #region 메뉴 — 그리드 배치

    [MenuItem("Tools/FCC/Tilemap/Add Grid To Dungeon Rooms")]
    public static void AddGridToDungeonRooms() {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[Tilemap] 프리팹 편집 모드를 닫고 다시 실행하세요.");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("Room_ t:Prefab", new[] { RoomPrefabDir });
        int count = 0;
        var failed = new List<string>();
        foreach (string guid in guids) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!Path.GetFileName(path).StartsWith("Room_")) continue;

            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try {
                EnsureGrid(root);
                // Missing 스크립트가 남은 프리팹은 유니티가 저장을 거부한다. 개수에 섞이면 적용된 줄 알고 넘어가게 된다.
                PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                if (success) count++;
                else failed.Add(Path.GetFileNameWithoutExtension(path));
            } finally {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        Debug.Log($"[Tilemap] 던전 방 프리팹 {count}개에 타일맵 그리드를 맞췄습니다. 이미 칠한 타일은 그대로 둡니다.");
        if (failed.Count > 0) {
            Debug.LogWarning($"[Tilemap] 저장하지 못한 방: {string.Join(", ", failed)} — Missing 스크립트를 지운 뒤 다시 실행하세요.");
        }
    }

    [MenuItem("Tools/FCC/Tilemap/Place Grid In Open Scene")]
    public static void PlaceGridInOpenScene() {
        var scene = EditorSceneManager.GetActiveScene();

        // 씬의 구획 루트("---------- MAP ----------")가 있으면 그 아래에 둔다. 없으면 씬 최상단.
        Transform parent = null;
        foreach (GameObject obj in scene.GetRootGameObjects()) {
            if (obj.name.Trim('-', ' ') == "MAP") {
                parent = obj.transform;
                break;
            }
        }

        GameObject grid = null;
        if (parent != null) {
            Transform found = parent.Find(GridName);
            if (found != null) grid = found.gameObject;
        } else {
            foreach (GameObject obj in scene.GetRootGameObjects()) {
                if (obj.name == GridName && obj.GetComponent<Grid>() != null) grid = obj;
            }
        }

        if (grid == null) {
            grid = new GameObject(GridName);
            Undo.RegisterCreatedObjectUndo(grid, "Place Tilemap Grid");
            if (parent != null) grid.transform.SetParent(parent, false);
        }

        ConfigureGrid(grid);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = grid;
        Debug.Log($"[Tilemap] '{scene.name}' 씬에 타일맵 그리드를 맞췄습니다{(parent != null ? " (MAP 아래)" : "")}. 씬을 저장해야 반영됩니다.");
    }

    // 방 빌더도 이 함수를 불러, "Build All Rooms" 로 방을 다시 찍어도 그리드 구성이 빠지지 않게 한다.
    public static GameObject EnsureGrid(GameObject roomRoot) {
        Transform found = roomRoot.transform.Find(GridName);
        GameObject grid = found != null ? found.gameObject : new GameObject(GridName);
        grid.transform.SetParent(roomRoot.transform, false);
        grid.transform.localPosition = Vector3.zero;
        ConfigureGrid(grid);
        return grid;
    }

    static void ConfigureGrid(GameObject grid) {
        Grid g = GetOrAdd<Grid>(grid);
        g.cellSize = Vector3.one;
        g.cellLayout = GridLayout.CellLayout.Rectangle;

        foreach (LayerSpec spec in Layers) {
            Transform child = grid.transform.Find(spec.name);
            GameObject obj = child != null ? child.gameObject : new GameObject(spec.name);
            obj.transform.SetParent(grid.transform, false);
            ConfigureLayer(obj, spec);
        }
    }

    static void ConfigureLayer(GameObject obj, LayerSpec spec) {
        GetOrAdd<Tilemap>(obj);
        TilemapRenderer renderer = GetOrAdd<TilemapRenderer>(obj);
        renderer.sortingOrder = spec.order;
        renderer.mode = TilemapRenderer.Mode.Chunk;

        if (spec.kind == LayerKind.Visual) {
            obj.layer = 0;
            return;
        }

        int ground = LayerMask.NameToLayer("ground");
        if (ground < 0) {
            Debug.LogWarning("[Tilemap] 'ground' 레이어를 찾지 못했습니다. 플레이어가 지형을 밟지 못하니 레이어를 확인하세요.");
            ground = 0;
        }
        obj.layer = ground;

        // 칸마다 콜라이더가 따로 있으면 칸 경계의 틈에 캐릭터가 걸려 멈추는(고스트 충돌) 일이 생긴다.
        // 합성 충돌로 한 덩어리로 묶어 경계를 없앤다.
        Rigidbody2D body = GetOrAdd<Rigidbody2D>(obj);
        body.bodyType = RigidbodyType2D.Static;

        TilemapCollider2D tileCol = GetOrAdd<TilemapCollider2D>(obj);
        tileCol.compositeOperation = Collider2D.CompositeOperation.Merge;

        CompositeCollider2D composite = GetOrAdd<CompositeCollider2D>(obj);
        // Outlines(외곽선)는 속이 빈 판정이라 OverlapBox · OverlapPoint 로 접지를 재는 코드에 걸리지 않는다.
        composite.geometryType = CompositeCollider2D.GeometryType.Polygons;
        composite.generationType = CompositeCollider2D.GenerationType.Synchronous;

        if (spec.kind == LayerKind.Platform) {
            // 이펙터 배선은 기존 발판과 똑같이 OneWayPlatform 에 맡겨, 통과 규칙이 두 갈래로 갈라지지 않게 한다.
            GetOrAdd<OneWayPlatform>(obj).Apply();
        }
    }

    #endregion
    #region 생성 도우미

    delegate Color PixelFunc(int x, int y);

    static Sprite WriteSprite(string name, PixelFunc pixel) {
        string path = $"{SpriteDir}/{name}.png";

        var tex = new Texture2D(TilePixels, TilePixels, TextureFormat.RGBA32, false);
        for (int y = 0; y < TilePixels; y++) {
            for (int x = 0; x < TilePixels; x++) {
                tex.SetPixel(x, y, pixel(x, y));
            }
        }
        tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG()); // 같은 경로에 덮어써 .meta(GUID)를 유지한다.
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = TilePixels;
        importer.filterMode = FilterMode.Point; // 이웃 칸 색이 번져 타일 경계에 줄이 생기지 않도록.
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // 타이트 메시는 투명한 줄을 잘라내 칸 크기가 어긋난다.
        settings.spriteGenerateFallbackPhysicsShape = true;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // 스프라이트 윗부분 heightPixels 만큼을 물리 형태로 지정한다. 좌표는 스프라이트 중심 기준 픽셀.
    static void SetPhysicsRect(Sprite sprite, int heightPixels) {
        string path = AssetDatabase.GetAssetPath(sprite);
        var importer = AssetImporter.GetAtPath(path);

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        ISpriteEditorDataProvider provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();

        SpriteRect rect = provider.GetSpriteRects()[0];
        float half = TilePixels / 2f;
        float bottom = half - heightPixels;
        var outline = new[] {
            new Vector2(-half, bottom),
            new Vector2(-half, half),
            new Vector2(half, half),
            new Vector2(half, bottom),
        };

        provider.GetDataProvider<ISpritePhysicsOutlineDataProvider>()
            .SetOutlines(rect.spriteID, new List<Vector2[]> { outline });
        provider.Apply();
        importer.SaveAndReimport();
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset; // 새로 만들면 GUID 가 바뀌어 칠해 둔 타일맵이 전부 끊긴다.

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static T GetOrAdd<T>(GameObject obj) where T : Component {
        T comp = obj.GetComponent<T>();
        return comp != null ? comp : obj.AddComponent<T>();
    }

    static void EnsureFolder(string path) {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    #endregion
}
#endif
