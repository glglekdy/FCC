#if UNITY_EDITOR
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// 프로젝트 안의 모든 TMP 글자를 대표 폰트(PrefabBuilderFont.ProjectFontPath) 하나로 통일하는 도구.
//
// 폰트를 갈아끼울 때 프리팹만 고치면 씬에 직접 놓인 글자와 TMP 기본 설정이 옛 폰트로 남아서, 화면 한두 군데만
// 다른 서체로 나오는 일이 생긴다. 그게 눈에 띄기까지 한참 걸리므로 한 번에 훑어서 바꾼다.
//
// 하는 일은 넷이다.
//   1. Assets/_Project 아래 모든 프리팹의 TMP 폰트 교체 (머티리얼도 같이 — 안 바꾸면 옛 아틀라스를 물고 깨진다)
//   2. Assets/_Project 아래 모든 씬의 TMP 폰트 교체 (프리팹 인스턴스의 폰트 오버라이드는 되돌린다)
//   3. TMP 기본 폰트(TMP Settings)를 대표 폰트로 — 새로 만드는 TMP가 LiberationSans로 시작하지 않게
//   4. 대표 폰트에 없는 글자를 보조 폰트에서 가져오도록 fallback 연결
//
// 4번이 필요한 이유: 한글 SDF는 보통 상용 음절만 담고 있어서 ↑ ↓ · — ' 같은 기호가 빠진다.
// fallback 이 없으면 그 글자만 네모(□)로 나오는데, 한글은 멀쩡해서 눈치채기가 더 어렵다.
//
// 사용법: Tools ▸ FCC ▸ UI ▸ Apply Project Font
public static class UiFontApplier {
    #region 메뉴

    [MenuItem("Tools/FCC/UI/Apply Project Font")]
    public static void ApplyAll() {
        TMP_FontAsset font = PrefabBuilderFont.LoadProjectFont("UiFont");
        if (font == null) return;

        // 씬을 갈아끼우기 전에 현재 씬을 되돌려 놓을 수 있게 기억해 둔다.
        string openScene = SceneManager.GetActiveScene().path;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) {
            Debug.Log("[UiFont] 씬 저장을 취소해서 아무것도 바꾸지 않았습니다.");
            return;
        }

        LinkFallback(font);

        int prefabs = ApplyToPrefabs(font);
        int scenes = ApplyToScenes(font, openScene);
        bool settings = ApplyToTmpSettings(font);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[UiFont] 폰트를 '{font.name}' 으로 통일했습니다.\n" +
            $"· 프리팹 {prefabs}개 · 씬 {scenes}개" + (settings ? " · TMP 기본 폰트" : "") + "\n" +
            "대표 폰트를 바꾸려면 PrefabBuilderFont.ProjectFontPath 를 고치고 이 메뉴를 다시 실행하세요.");

        // 폰트를 바꾼 직후가 글자 빠짐이 생기는 순간이라 여기서 바로 확인한다.
        ReportMissingCharacters(font);
    }

    #endregion
    #region 프리팹

    static int ApplyToPrefabs(TMP_FontAsset font) {
        int changed = 0;

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);

            GameObject probe = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (probe == null || probe.GetComponentInChildren<TMP_Text>(true) == null) continue;

            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            bool touched = false;

            try {
                foreach (TMP_Text text in contents.GetComponentsInChildren<TMP_Text>(true)) {
                    if (Swap(text, font, path)) touched = true;
                }

                if (touched) PrefabUtility.SaveAsPrefabAsset(contents, path);
            } finally {
                PrefabUtility.UnloadPrefabContents(contents);
            }

            if (touched) changed++;
        }

        return changed;
    }

    #endregion
    #region 씬

    // 프리팹을 먼저 처리한 뒤에 부른다. 그래야 씬에 놓인 프리팹 인스턴스는 이미 새 폰트를 물고 있어서
    // 여기서는 씬에 직접 만들어진 글자와 남아 있는 오버라이드만 손보면 된다.
    static int ApplyToScenes(TMP_FontAsset font, string sceneToRestore) {
        int changed = 0;

        List<string> paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Project" })) {
            paths.Add(AssetDatabase.GUIDToAssetPath(guid));
        }

        foreach (string path in paths) {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool touched = false;

            foreach (GameObject root in scene.GetRootGameObjects()) {
                foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true)) {
                    if (RevertFontOverride(text, font)) touched = true;
                    if (Swap(text, font, path)) touched = true;
                }
            }

            if (touched) {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                changed++;
            }
        }

        // 작업 전에 열려 있던 씬으로 돌려놓는다. 안 그러면 마지막으로 훑은 씬이 열린 채 남는다.
        if (!string.IsNullOrEmpty(sceneToRestore)) {
            EditorSceneManager.OpenScene(sceneToRestore, OpenSceneMode.Single);
        }

        return changed;
    }

    // 프리팹 인스턴스가 씬에서 폰트를 따로 덮어쓴 경우. 프리팹 쪽이 이미 새 폰트라 오버라이드는 옛 폰트를
    // 붙들고 있는 셈이므로, 새로 칠하는 대신 오버라이드를 걷어내 프리팹을 따라가게 한다.
    static bool RevertFontOverride(TMP_Text text, TMP_FontAsset font) {
        if (!PrefabUtility.IsPartOfPrefabInstance(text)) return false;

        SerializedObject so = new SerializedObject(text);
        SerializedProperty prop = so.FindProperty("m_fontAsset");
        if (prop == null || !prop.prefabOverride) return false;

        PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
        return text.font == font;
    }

    #endregion
    #region TMP 기본 설정

    // 새로 만드는 TMP 오브젝트가 LiberationSans(한글 없음)로 시작하지 않도록 기본값 자체를 바꾼다.
    static bool ApplyToTmpSettings(TMP_FontAsset font) {
        TMP_Settings settings = TMP_Settings.instance;
        if (settings == null) return false;
        if (TMP_Settings.defaultFontAsset == font) return false;

        SerializedObject so = new SerializedObject(settings);
        SerializedProperty prop = so.FindProperty("m_defaultFontAsset");
        if (prop == null) return false;

        prop.objectReferenceValue = font;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(settings);

        return true;
    }

    #endregion
    #region 글자 빠짐 검사

    // 프로젝트에 실제로 쓰인 문구를 전부 훑어, 대표 폰트가 그리지 못하는 글자를 찾아 알린다.
    //
    // 이 검사가 필요한 이유: 한글 SDF는 보통 상용 음절만 담고 있어서 ↑ ↓ · — … 같은 기호가 빠지는데,
    // 한글은 멀쩡하게 나오는 탓에 화면을 봐도 잘 안 보인다. TMP는 없는 글자를 조용히 네모(□)로 그리고
    // 경고도 남기지 않는다. 그래서 사람 눈 대신 여기서 잡는다.
    //
    // 걸리면 Tools ▸ KW Font ▸ Patch Missing Symbols 로 원본 OTF에서 글자를 채워 넣으면 된다.
    // 대표 폰트가 한글을 담고 있지 않을 때(Inter 처럼) 한글을 대신 그려줄 폰트를 물려준다.
    //
    // 앞서 한글 SDF 끼리 fallback 을 걸었다가 아무 소용이 없던 적이 있는데, 그때는 두 폰트의 수록 범위가
    // 똑같아서였다. 지금은 라틴 전용 폰트에 한글 폰트를 물리는 것이라 실제로 빈 곳을 메운다.
    static void LinkFallback(TMP_FontAsset font) {
        TMP_FontAsset fallback = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(PrefabBuilderFont.FallbackFontPath);

        if (fallback == null || fallback == font) return;

        if (font.fallbackFontAssetTable == null) font.fallbackFontAssetTable = new List<TMP_FontAsset>();
        if (font.fallbackFontAssetTable.Contains(fallback)) return;

        font.fallbackFontAssetTable.Add(fallback);
        EditorUtility.SetDirty(font);

        Debug.Log($"[UiFont] '{font.name}' 에 없는 글자를 '{fallback.name}' 에서 가져오도록 fallback 을 걸었습니다.", font);
    }

    static void ReportMissingCharacters(TMP_FontAsset font) {
        // fallback 으로 메워지는 글자는 빠진 것이 아니다. 체인을 따라가며 전부 모은다.
        HashSet<uint> have = new HashSet<uint>();
        Collect(font, have, 0);

        SortedDictionary<uint, string> missing = new SortedDictionary<uint, string>();

        System.Action<string, string> scan = (text, where) => {
            if (string.IsNullOrEmpty(text)) return;
            foreach (char c in text) {
                if (c == '\n' || c == '\r' || c == '\t' || c == ' ') continue;
                if (have.Contains(c)) continue;
                if (!missing.ContainsKey(c)) missing[c] = where;
            }
        };

        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" })) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            string file = System.IO.Path.GetFileName(path);
            foreach (TMP_Text t in go.GetComponentsInChildren<TMP_Text>(true)) scan(t.text, file + "/" + t.name);
        }

        // 대사·목표 문구는 프리팹이 아니라 String Table 에 있다. 여기가 제일 양이 많아 빠뜨리면 안 된다.
        foreach (string guid in AssetDatabase.FindAssets("t:StringTable", new[] { "Assets/_Project" })) {
            var table = AssetDatabase.LoadAssetAtPath<UnityEngine.Localization.Tables.StringTable>(
                AssetDatabase.GUIDToAssetPath(guid));
            if (table == null) continue;

            string where = table.TableCollectionName + "(" + table.LocaleIdentifier.Code + ")";
            foreach (var entry in table.Values) scan(entry.LocalizedValue, where);
        }

        if (missing.Count == 0) {
            Debug.Log($"[UiFont] '{font.name}' 이 프로젝트의 모든 문구를 그릴 수 있습니다.");
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine($"[UiFont] '{font.name}' 에 없는 글자가 {missing.Count}종 쓰이고 있습니다. 화면에는 네모(□)로 나옵니다.");
        foreach (var kv in missing) {
            sb.AppendLine($"  '{(char)kv.Key}' (U+{kv.Key:X4})  예: {kv.Value}");
        }
        sb.Append("Tools ▸ KW Font ▸ Patch Missing Symbols 로 원본 폰트에서 채워 넣으세요.");

        Debug.LogWarning(sb.ToString(), font);
    }

    // fallback 체인을 따라 그릴 수 있는 글자를 전부 모은다. depth 는 폰트끼리 서로를 물어 맴도는 것을 막는 안전장치다.
    static void Collect(TMP_FontAsset font, HashSet<uint> into, int depth) {
        if (font == null || depth > 4) return;

        foreach (TMP_Character ch in font.characterTable) into.Add(ch.unicode);

        if (font.fallbackFontAssetTable == null) return;
        foreach (TMP_FontAsset next in font.fallbackFontAssetTable) Collect(next, into, depth + 1);
    }

    #endregion
    #region 교체

    static bool Swap(TMP_Text text, TMP_FontAsset font, string where) {
        if (text.font == font) return false;

        string before = text.font == null ? "(끊어진 참조)" : text.font.name;

        // 폰트만 바꾸고 머티리얼을 그대로 두면 옛 폰트의 아틀라스를 계속 읽어 글자가 깨진다.
        text.font = font;
        text.fontSharedMaterial = font.material;

        Debug.Log($"[UiFont] {System.IO.Path.GetFileName(where)} / {text.name} : {before} → {font.name}");
        return true;
    }

    #endregion
}
#endif
