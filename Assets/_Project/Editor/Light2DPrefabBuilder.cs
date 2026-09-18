#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// URP 2D 라이트(Light2D) 세팅을 한 번에 찍어내는 에디터 도구.
//
// 지금까지는 씬에 Light2D 가 하나도 없었다. 그런데도 화면이 검게 나오지 않았던 이유는,
// URP 2D 렌더러가 "씬에 Light2D 가 하나도 없으면 라이팅 패스를 건너뛰고 원본 색 그대로 그리는"
// 예외 처리를 갖고 있기 때문이다(타일 머티리얼은 이미 Sprite-Lit-Default 를 쓰고 있었다 —
// Renderer2D.asset 의 기본 머티리얼이 Lit 이라 새로 만든 타일맵 렌더러가 자동으로 물고 있었다).
// 그래서 라이트를 "하나라도" 놓는 순간부터는 라이팅이 실제로 적용되기 시작하며, 동시에
// 화면이 어두워지지 않도록 전역 라이트로 바닥 밝기를 깔아줘야 한다.
//
// 프리팹을 네 개로 나눈 이유:
// GlobalLight2D 는 씬 전체의 바닥 밝기(먼지 앉은 극장의 은은한 톤)를 까는 용도라 씬마다 하나만
// 있으면 된다(ScreenFader 와 같은 배치 방식). 나머지 셋은 필요한 만큼 여러 곳에 붙이는 장식용이라
// "Place" 메뉴를 따로 두지 않는다 — 프로젝트 창에서 드래그하거나 manage_prefabs 로 자식으로 붙인다.
// GlowLight2D 는 극장(따뜻한 적색)용, GlowLight2D_BackWorld 는 뒷세계(차가운 하늘색)용으로 색만
// 다르다 — TilesetWeatherBaker 가 두 공간의 타일 색을 이미 이렇게 갈라 구워뒀기 때문에 조명도
// 같은 축을 따라야 한 공간처럼 보이지 않는다. PlayerFlashlight2D 는 Player.prefab 루트의 자식으로
// 붙여 두는 손전등이다 — 몬스터마다 붙였던 은은한 오라(MonsterGlowLight2D)는 시야를 밝히는 역할이
// 아니라 빼고, 대신 플레이어가 실제로 앞을 비추는 손전등 하나로 옮겼다.
//
// 사용법: Tools ▸ FCC ▸ Lighting ▸ Build Light 2D Prefabs → Tools ▸ FCC ▸ Lighting ▸ Place Global Light In Scene
public static class Light2DPrefabBuilder {
    #region 경로 · 색

    const string PrefabDir = "Assets/_Project/Assets/Prefabs/Environment";
    const string GlobalLightPath = PrefabDir + "/GlobalLight2D.prefab";
    const string GlowLightPath = PrefabDir + "/GlowLight2D.prefab";
    const string BackWorldGlowLightPath = PrefabDir + "/GlowLight2D_BackWorld.prefab";
    const string PlayerFlashlightPath = PrefabDir + "/PlayerFlashlight2D.prefab";

    // TilesetWeatherBaker 의 Theater 팔레트(먼지 앉은 장미 갈색 중간톤)와 같은 방향의 색이다.
    // 흰색으로 두면 타일이 구워 넣은 낡은 톤 위에 순백 조명이 겹쳐 다시 밝아져 버린다.
    // 처음에는 0.85 로 낮춰 잡았는데, Multiply 라 화면 전체를 고르게 눌러 "안개 낀" 것처럼 보였다.
    // 거의 중립(0.95)까지 올려 전역 라이트는 톤만 살짝 얹고, 포인트 글로우 쪽에서 밝기 대비를 만든다.
    static readonly Color GlobalTint = new Color(0.95f, 0.92f, 0.88f, 1f);
    const float GlobalIntensity = 0.95f;

    // UiTheme.AccentBright 와 같은 방향의 붉은 기 — 벨벳 커튼 하나뿐인 포인트 컬러를 조명에도 지킨다.
    // Additive 는 1.2 정도로는 잘 안 보여 흐릿하기만 했다. 밝기를 올리고 FalloffIntensity 를 낮춰
    // (가장자리가 더 또렷하게 깎이도록) 안개처럼 퍼지지 않고 빛나는 덩어리로 보이게 했다.
    // **InnerRadius 는 반드시 작게 유지한다.** Light2D 는 InnerRadius 안쪽을 전부 같은 최대 밝기로
    // 칠하는데, Bloom 을 켠 상태에서 반경을 크게 잡으면 그 안쪽이 평평한 순백색 원반으로 뭉개져
    // 뒤에 있는 스프라이트(거울 등)를 완전히 지워버린 것처럼 보인다(실제로 InnerRadius 1 로 겪었다).
    static readonly Color GlowTint = new Color(0.95f, 0.4f, 0.28f, 1f);
    const float GlowIntensity = 1.6f;
    const float GlowFalloffIntensity = 0.25f;
    const float GlowInnerRadius = 0.1f;
    const float GlowOuterRadius = 2.5f; // 타일 1칸 = 1유닛 기준.

    // TilesetWeatherBaker 의 BackWorld 팔레트(젖은 청석 → 흐린 하늘색)와 같은 방향의 차가운 색.
    // 극장의 GlowTint 를 그대로 쓰면 뒷세계에서도 벨벳 적색이 섞여 극장과 뒷세계가 같은 분위기로 보인다.
    static readonly Color BackWorldGlowTint = new Color(0.5f, 0.78f, 0.95f, 1f);
    const float BackWorldGlowIntensity = 1.4f;
    const float BackWorldGlowFalloffIntensity = 0.25f;
    const float BackWorldGlowInnerRadius = 0.15f; // 위 GlowInnerRadius 주석 참고 — 크게 잡지 않는다.
    const float BackWorldGlowOuterRadius = 3.2f; // 방 하나를 덮을 정도. 타일 1칸 = 1유닛 기준.

    // 플레이어 손전등. Point 라이트에 각도(Inner/Outer Angle)를 좁게 줘서 원뿔 모양으로 만든다 —
    // URP 2D 는 3D 처럼 따로 "Spot" 타입이 있는 게 아니라 Point 라이트의 각도를 좁히는 방식이다.
    // 각도를 준 Point 라이트의 원뿔은 로컬 +Y(위쪽) 방향으로 열리므로, 앞(+X)을 비추려면 오브젝트를
    // -90도 돌려야 한다(PlayerFlashlightBuilder 의 Build 함수에서 직접 처리, 아래 참고).
    static readonly Color FlashlightTint = new Color(0.95f, 0.92f, 0.8f, 1f);
    const float FlashlightIntensity = 3f; // 좁은 원뿔이라 1~2대는 화면에서 거의 안 보인다. 확인하며 잡은 값.
    const float FlashlightFalloffIntensity = 0.4f;
    // InnerRadius 안쪽은 항상 최대 밝기라, 원점이 캐릭터 몸통 위에 있으면 자기 스프라이트가 빛을 뒤집어쓴다.
    // 그래서 반경은 작게 잡고, Player.prefab 에 붙일 때 위치 자체를 몸통 폭(약 0.5) 밖으로 빼서
    // (로컬 x=0.9, AttackPoint 와 같은 자리) 원점이 캐릭터 실루엣 밖에 있도록 한다.
    const float FlashlightInnerRadius = 0.3f;
    const float FlashlightOuterRadius = 7f;
    const float FlashlightInnerAngle = 20f;
    const float FlashlightOuterAngle = 55f;

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Lighting/Build Light 2D Prefabs")]
    public static void BuildPrefabs() {
        if (PrefabStageUtility.GetCurrentPrefabStage() != null) {
            Debug.LogError("[Light2D] 프리팹 편집 모드를 닫고 다시 실행하세요. 임시 오브젝트가 편집 중인 프리팹에 섞여 들어갑니다.");
            return;
        }

        EnsureFolder(PrefabDir);

        BuildGlobalLight();
        BuildGlowLight();
        BuildBackWorldGlowLight();
        BuildPlayerFlashlight();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Light2D] 프리팹을 만들었습니다.\n· {GlobalLightPath}\n· {GlowLightPath}\n· {BackWorldGlowLightPath}\n· {PlayerFlashlightPath}\n" +
            "전역 라이트는 Tools ▸ FCC ▸ Lighting ▸ Place Global Light In Scene 으로 씬마다 하나씩 놓고, " +
            "장식용 라이트는 거울 · 램프 · 게이트 옆에 프리팹을 직접 드래그해 놓으세요.");
    }

    static void BuildGlobalLight() {
        var rootObj = new GameObject("GlobalLight2D");

        Light2D light = rootObj.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Global;
        light.color = GlobalTint;
        light.intensity = GlobalIntensity;
        light.blendStyleIndex = 0; // Multiply — 바닥 밝기를 정하는 역할이라 곱연산이어야 한다.
        light.shadowsEnabled = false; // 전역 라이트는 그림자 개념이 없다.

        PrefabUtility.SaveAsPrefabAsset(rootObj, GlobalLightPath);
        Object.DestroyImmediate(rootObj);
    }

    static void BuildGlowLight() {
        var rootObj = new GameObject("GlowLight2D");

        Light2D light = rootObj.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = GlowTint;
        light.intensity = GlowIntensity;
        light.blendStyleIndex = 1; // Additive — 바닥 밝기 위에 더해지는 국소 광원이라 덧연산이어야 한다.
        light.pointLightInnerRadius = GlowInnerRadius;
        light.pointLightOuterRadius = GlowOuterRadius;
        light.falloffIntensity = GlowFalloffIntensity;
        light.shadowsEnabled = false; // 타일맵에 2D 그림자 캐스터가 없어 켜도 효과가 없다.

        PrefabUtility.SaveAsPrefabAsset(rootObj, GlowLightPath);
        Object.DestroyImmediate(rootObj);
    }

    static void BuildBackWorldGlowLight() {
        var rootObj = new GameObject("GlowLight2D_BackWorld");

        Light2D light = rootObj.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = BackWorldGlowTint;
        light.intensity = BackWorldGlowIntensity;
        light.blendStyleIndex = 1; // Additive.
        light.pointLightInnerRadius = BackWorldGlowInnerRadius;
        light.pointLightOuterRadius = BackWorldGlowOuterRadius;
        light.falloffIntensity = BackWorldGlowFalloffIntensity;
        light.shadowsEnabled = false;

        PrefabUtility.SaveAsPrefabAsset(rootObj, BackWorldGlowLightPath);
        Object.DestroyImmediate(rootObj);
    }

    static void BuildPlayerFlashlight() {
        var rootObj = new GameObject("PlayerFlashlight2D");

        // 각도를 준 Point 라이트의 원뿔은 로컬 +Y(위쪽)로 열린다. 플레이어 앞(+X)을 비추려면
        // 오브젝트 자체를 Z -90도 돌려야 한다 — 플레이어가 왼쪽을 볼 때는 루트 오브젝트가
        // (Player_move 의 반전 방식에 따라) X축 반전되거나 Y축으로 180도 돌므로, 이 라이트를
        // Player 루트의 자식으로 두기만 하면 손전등도 같이 반전되어 항상 보는 방향을 비춘다.
        rootObj.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

        Light2D light = rootObj.AddComponent<Light2D>();
        light.lightType = Light2D.LightType.Point;
        light.color = FlashlightTint;
        light.intensity = FlashlightIntensity;
        light.blendStyleIndex = 1; // Additive.
        light.pointLightInnerRadius = FlashlightInnerRadius;
        light.pointLightOuterRadius = FlashlightOuterRadius;
        light.pointLightInnerAngle = FlashlightInnerAngle;
        light.pointLightOuterAngle = FlashlightOuterAngle;
        light.falloffIntensity = FlashlightFalloffIntensity;
        light.shadowsEnabled = false;

        PrefabUtility.SaveAsPrefabAsset(rootObj, PlayerFlashlightPath);
        Object.DestroyImmediate(rootObj);
    }

    // 지금 열려 있는 씬에 전역 라이트를 한 번만 놓는다. 씬마다 한 번씩 실행하면 된다.
    [MenuItem("Tools/FCC/Lighting/Place Global Light In Scene")]
    public static void PlaceGlobalLightInScene() {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(GlobalLightPath);
        if (prefab == null) {
            Debug.LogError($"[Light2D] 프리팹이 없습니다({GlobalLightPath}). 먼저 Tools ▸ FCC ▸ Lighting ▸ Build Light 2D Prefabs 를 실행하세요.");
            return;
        }

        foreach (Light2D existing in Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if (existing.lightType != Light2D.LightType.Global) continue;
            Debug.Log($"[Light2D] 씬에 이미 전역 라이트 '{existing.name}' 이 있어 새로 놓지 않았습니다.", existing.gameObject);
            return;
        }

        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place Global Light 2D");

        Selection.activeGameObject = instance;
        EditorSceneManager.MarkSceneDirty(instance.scene);

        Debug.Log("[Light2D] 씬에 GlobalLight2D 프리팹을 놓았습니다. 씬을 저장하세요.", instance);
    }

    #endregion
    #region 생성 도우미

    static void EnsureFolder(string path) {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    #endregion
}
#endif
