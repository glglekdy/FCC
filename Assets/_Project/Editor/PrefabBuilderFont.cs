#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;

// 프리팹 생성 메뉴들이 공유하는 한글 SDF 폰트 조회.
//
// 각 빌더가 폰트 경로를 문자열로 박아두고 있었는데, KwFontAssetBuilder로 폰트를 새로 뽑으면
// 파일 이름이 바뀐다. 경로가 어긋나도 TMP는 조용히 기본 폰트(LiberationSans)로 떨어지고
// 한글이 전부 네모(□)로 렌더되기 때문에, 이름이 안 맞으면 폴더를 훑어서라도 한글 폰트를 물려준다.
public static class PrefabBuilderFont {
    #region 경로

    public const string FontDir = "Assets/_Project/Assets/Font/SDF";

    // **프로젝트 대표 폰트. 폰트를 갈아끼울 때 고치는 곳은 여기 한 군데뿐입니다.**
    // 빌더들이 각자 경로를 박아두면 어떤 화면만 옛 폰트로 남는 일이 생겨서 한 곳으로 모았다.
    // 여기를 고친 뒤에는 Tools ▸ FCC ▸ UI ▸ Apply Project Font 를 돌려야 기존 프리팹·씬까지 따라온다.
    public const string ProjectFontPath = FontDir + "/Inter_18pt-Bold SDF.asset";

    // **한글 받침 폰트. 대표 폰트가 한글을 담고 있지 않을 때 반드시 있어야 합니다.**
    //
    // Inter 는 라틴 전용이라 한글이 한 자도 없다(ASCII 93자뿐). 이대로 두면 게임의 거의 모든 문구가
    // 네모(□)로 나온다 — 원문 언어가 한국어이기 때문이다. 그래서 Inter 를 주 서체로 쓰되 한글은
    // 이 폰트에서 가져오게 fallback 으로 물린다. Apply Project Font 가 연결해 준다.
    //
    // 대표 폰트를 한글이 든 서체로 바꾼다면 이 값을 비워도 된다(같은 폰트를 자기 fallback 으로 물지는 않는다).
    public const string FallbackFontPath = FontDir + "/Hahmlet-Bold SDF.asset";

    #endregion
    #region 조회

    // 프로젝트 대표 폰트. 새로 만드는 빌더는 경로를 따로 박지 말고 이쪽을 쓴다.
    public static TMP_FontAsset LoadProjectFont(string logTag) {
        return Load(ProjectFontPath, logTag);
    }

    // logTag는 경고 앞에 붙는 이름(예: "ObjectiveChecklist"). 어느 메뉴가 찍은 경고인지 구분하려고 받는다.
    public static TMP_FontAsset Load(string preferredPath, string logTag) {
        TMP_FontAsset font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(preferredPath);
        if (font != null) return font;

        foreach (string guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { FontDir })) {
            TMP_FontAsset found = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (found == null) continue;

            Debug.LogWarning($"[{logTag}] 지정한 폰트가 없어({preferredPath}) 같은 폴더의 '{found.name}'을 대신 물렸습니다. " +
                "PrefabBuilderFont.ProjectFontPath 를 실제 파일 이름으로 고쳐두세요.", found);
            return found;
        }

        Debug.LogError($"[{logTag}] {FontDir} 에 한글 SDF 폰트가 하나도 없습니다. " +
            "Tools ▸ KW Font 로 폰트를 먼저 만드세요. 지금 만든 프리팹의 한글은 전부 네모(□)로 나옵니다.");
        return null;
    }

    #endregion
}
#endif
