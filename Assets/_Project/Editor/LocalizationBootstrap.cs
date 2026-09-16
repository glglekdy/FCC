#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Metadata;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

// 다국어(Localization) 뼈대를 메뉴 한 번으로 만들어주는 에디터 도구.
//
// Localization 패키지는 설정 에셋 · Locale 에셋 · String Table 컬렉션이 전부 갖춰지고 Addressables에
// 등록까지 되어야 동작한다. 손으로 만들면 등록을 빠뜨리기 쉬운데, 그러면 에러 없이 조용히 빈 문자열만
// 나와서 원인을 찾기 어렵다. 그래서 순서를 여기 고정해 두었다.
//
// 이미 있는 것은 건너뛰므로 몇 번을 눌러도 안전하다(멱등).
//
// **메뉴: Tools ▸ FCC ▸ Localization ▸ 초기 셋업**
public static class LocalizationBootstrap {
    #region 상수

    const string AssetsFolder = "Assets/_Project/Assets";
    const string RootFolder = AssetsFolder + "/Localization";
    const string LocaleFolder = RootFolder + "/Locales";
    const string TableFolder = RootFolder + "/Tables";
    const string SettingsPath = RootFolder + "/LocalizationSettings.asset";

    // 대사와 목표를 다른 테이블로 나눈 이유: 번역가에게 넘길 때 대사(긴 문장·말투 통일 필요)와
    // UI 문구(단문·길이 제한 있음)는 검수 기준이 달라서 시트를 따로 두는 편이 관리가 쉽다.
    public const string DialogueTable = "Dialogue";
    public const string ObjectiveTable = "Objective";
    // 메뉴·HUD·설정·정비 화면처럼 화면에 고정으로 붙는 문구. 목표 문구와도 나눈 이유는 목표는 기획이
    // 늘 때마다 키가 불어나는 반면 UI 문구는 화면 단위로 묶여 있어 검수 시점이 다르기 때문이다.
    public const string UiTable = "Ui";

    // 원문 언어. 번역이 비어 있을 때 이 언어로 되돌아간다.
    const string SourceLocaleCode = "ko";

    // 만들어 둘 언어. 여기에 코드를 추가하고 셋업을 다시 실행하면 기존 테이블에도 열이 추가된다.
    static readonly string[] LocaleCodes = { "ko", "en" };

    #endregion
    #region 메뉴

    [MenuItem("Tools/FCC/Localization/초기 셋업", priority = 100)]
    public static void Setup() {
        EnsureFolders();

        LocalizationSettings settings = EnsureSettings();
        List<Locale> locales = EnsureLocales();
        EnsureSourceLocale(settings);
        EnsureFallbackToSource(settings, locales);

        EnsureTable(DialogueTable, locales);
        EnsureTable(ObjectiveTable, locales);
        EnsureTable(UiTable, locales);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[Localization] 셋업 완료 — 언어 {string.Join(" / ", LocaleCodes)}, " +
            $"테이블 '{DialogueTable}' · '{ObjectiveTable}' · '{UiTable}'. " +
            "번역은 Window ▸ Asset Management ▸ Localization Tables 에서 편집하세요.");

        Selection.activeObject = settings;
    }

    // 전환 전에 인스펙터에 직접 적혀 있던 문구를 테이블 키로 옮긴다.
    // 목표·미션 에셋은 키 연결까지 자동으로 해주고, 씬에 있는 대사는 키만 만들어 둔다
    // (씬 오브젝트까지 건드리면 열려 있는 씬과 어긋날 수 있어 연결은 인스펙터에서 직접 고르게 했다).
    [MenuItem("Tools/FCC/Localization/기존 문구 테이블로 옮기기", priority = 101)]
    public static void MigrateExistingText() {
        if (LocalizationEditorSettings.GetStringTableCollection(ObjectiveTable) == null) {
            Debug.LogError("[Localization] 테이블이 아직 없습니다. Tools ▸ FCC ▸ Localization ▸ 초기 셋업 을 먼저 실행하세요.");
            return;
        }

        MigrateObjectiveText();
        MigrateDialogueText();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    #endregion
    #region 셋업 단계

    static void EnsureFolders() {
        EnsureFolder(AssetsFolder, "Localization");
        EnsureFolder(RootFolder, "Locales");
        EnsureFolder(RootFolder, "Tables");
    }

    static void EnsureFolder(string parent, string child) {
        string path = $"{parent}/{child}";
        if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child);
    }

    // 설정 에셋. ActiveLocalizationSettings에 물려야 Project Settings와 Preloaded Assets에 등록되어
    // 빌드에도 따라간다 — 에셋만 만들어두면 에디터에서만 동작하고 빌드에서 문자열이 전부 비게 된다.
    static LocalizationSettings EnsureSettings() {
        LocalizationSettings settings = LocalizationEditorSettings.ActiveLocalizationSettings;
        if (settings != null) return settings;

        settings = AssetDatabase.LoadAssetAtPath<LocalizationSettings>(SettingsPath);

        if (settings == null) {
            settings = ScriptableObject.CreateInstance<LocalizationSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
        }

        LocalizationEditorSettings.ActiveLocalizationSettings = settings;
        return settings;
    }

    static List<Locale> EnsureLocales() {
        List<Locale> locales = new();

        foreach (string code in LocaleCodes) {
            LocaleIdentifier id = new LocaleIdentifier(code);
            Locale locale = FindLocale(id);

            if (locale == null) {
                locale = Locale.CreateLocale(id);
                AssetDatabase.CreateAsset(locale, $"{LocaleFolder}/{code}.asset");
                LocalizationEditorSettings.AddLocale(locale); // Addressables 등록까지 함께 처리된다.
            }

            locales.Add(locale);
        }

        return locales;
    }

    static Locale FindLocale(LocaleIdentifier id) {
        foreach (Locale locale in LocalizationEditorSettings.GetLocales()) {
            if (locale != null && locale.Identifier == id) return locale;
        }
        return null;
    }

    // 플레이어의 시스템 언어가 ko/en 어느 쪽도 아닐 때 무엇을 띄울지 정한다.
    // 패키지 기본값이 영어라, 그대로 두면 해외 PC에서 원문(한국어)이 아니라 미번역 영어가 뜬다.
    static void EnsureSourceLocale(LocalizationSettings settings) {
        Locale source = FindLocale(new LocaleIdentifier(SourceLocaleCode));
        if (source == null) return;

        LocalizationSettings.ProjectLocale = source;

        foreach (IStartupLocaleSelector selector in settings.GetStartupLocaleSelectors()) {
            if (selector is SpecificLocaleSelector specific) specific.LocaleId = source.Identifier;
        }

        EditorUtility.SetDirty(settings);
    }

    // 번역이 아직 없는 칸을 원문(한국어)으로 메운다.
    //
    // **이 설정이 없으면** 미번역 칸에 "No translation found for 'obj.xxx' in Objective" 라는
    // 디버그 문구가 화면에 그대로 뜬다. 번역은 항상 원문보다 늦게 들어오므로, 작업 중인 언어로
    // 플레이해도 최소한 한국어 문장은 보이게 해 두어야 한다.
    static void EnsureFallbackToSource(LocalizationSettings settings, IList<Locale> locales) {
        Locale source = FindLocale(new LocaleIdentifier(SourceLocaleCode));
        if (source == null) return;

        // 1) 각 언어에 "번역이 없으면 한국어를 보라"는 표식을 붙인다.
        foreach (Locale locale in locales) {
            if (locale == source) continue;
            if (locale.Metadata.GetMetadata<FallbackLocale>() != null) continue;

            locale.Metadata.AddMetadata(new FallbackLocale(source));
            EditorUtility.SetDirty(locale);
        }

        // 2) 표식만 붙여서는 동작하지 않는다 — 데이터베이스의 폴백 사용 스위치가 기본 꺼짐이라
        //    켜주지 않으면 위 FallbackLocale이 통째로 무시된다. 실제로 이것 때문에 미번역 칸에
        //    디버그 문구가 그대로 떴었다.
        settings.GetStringDatabase().UseFallback = true;
        settings.GetAssetDatabase().UseFallback = true;

        EditorUtility.SetDirty(settings);
    }

    static void EnsureTable(string tableName, IList<Locale> locales) {
        if (LocalizationEditorSettings.GetStringTableCollection(tableName) != null) return;

        // 테이블마다 언어 수만큼 에셋이 생기므로(공유 데이터 + ko + en …) 이름별 하위 폴더에 모아 둔다.
        EnsureFolder(TableFolder, tableName);
        LocalizationEditorSettings.CreateStringTableCollection(tableName, $"{TableFolder}/{tableName}", locales);
    }

    #endregion
    #region 기존 문구 이전

    // 전환 직전에 인스펙터·에셋에 직접 적혀 있던 문구.
    //
    // 필드 타입이 string에서 LocalizedString으로 바뀌면서 유니티가 더 이상 읽을 수 없게 된 값들이라,
    // 원문을 잃지 않도록 여기에 받아 적어 두고 테이블 원문(ko) 칸에 심는다.
    // **한 번 실행해 테이블에 들어간 뒤로는 쓸모가 없으니 이 표와 두 Migrate 메서드는 지워도 된다.**
    // 앞으로 추가되는 대사·목표는 처음부터 Localization Tables 창에서 작성한다.
    static readonly (string key, string text)[] ObjectiveSourceText = {
        ("obj.reach_test_zone",         "테스트 구역에 도달하기"),
        ("obj.finish_sample_dialogue",  "샘플 대화 완료하기"),
        ("mission.mission_test.name",   "테스트 무대"),
        ("mission.mission_test.desc",   "구역에 도달해 상황을 파악하고,\n대화를 마친다."),
    };

    static readonly (string key, string text)[] DialogueSourceText = {
        ("dlg.sample.speaker", "dd"),
        ("dlg.sample.001",     "안녕! 여기까지 들어오다니 용감한걸."),
        ("dlg.sample.002",     "<wave>이 영역에 들어오면 대화가 시작돼.</wave>"),
        ("dlg.sample.003",     "대화가 끝나면 다시 움직일 수 있어. 좋은 여행 되길!"),
    };

    // 목표·미션은 에셋이라 id로 찾을 수 있어 키 연결까지 자동으로 끝낸다.
    // 키 이름은 "obj.{id}" / "mission.{id}.name" 규칙이라 목표가 늘어도 규칙이 유지된다.
    static void MigrateObjectiveText() {
        foreach ((string key, string text) in ObjectiveSourceText) AddKeyIfMissing(ObjectiveTable, key, text);

        int wired = 0;

        foreach (ObjectiveDefinition objective in LoadAll<ObjectiveDefinition>()) {
            string key = $"obj.{objective.ObjectiveId}";
            AddKeyIfMissing(ObjectiveTable, key, string.Empty); // 표에 없던 목표도 빈 키는 만들어 둔다.
            WireKey(objective, "description", ObjectiveTable, key);
            wired++;
        }

        foreach (Mission mission in LoadAll<Mission>()) {
            string nameKey = $"mission.{mission.MissionId}.name";
            string descKey = $"mission.{mission.MissionId}.desc";

            AddKeyIfMissing(ObjectiveTable, nameKey, string.Empty);
            AddKeyIfMissing(ObjectiveTable, descKey, string.Empty);

            WireKey(mission, "missionName", ObjectiveTable, nameKey);
            WireKey(mission, "description", ObjectiveTable, descKey);
            wired++;
        }

        Debug.Log($"[Localization] 목표·미션 에셋 {wired}개를 '{ObjectiveTable}' 테이블에 연결했습니다. " +
            "원문(ko)이 비어 있는 키는 Localization Tables 창에서 채우세요.");
    }

    // 대사는 에셋이 아니라 씬 오브젝트(DialogueTriggerZone·DialogueStep)의 리스트에 들어 있다.
    // 자동으로 연결하려면 씬을 열어 저장해야 하는데, 작업 중인 씬과 어긋날 수 있어 키까지만 만들어 둔다.
    // 연결은 인스펙터에서 드롭다운으로 고른다.
    static void MigrateDialogueText() {
        foreach ((string key, string text) in DialogueSourceText) AddKeyIfMissing(DialogueTable, key, text);

        Debug.Log($"[Localization] 대사 원문 {DialogueSourceText.Length}줄을 '{DialogueTable}' 테이블에 넣었습니다. " +
            "CoreScene ▸ DialogueTriggerZone 인스펙터에서 각 칸의 Speaker / Text 키를 골라 연결하세요 " +
            "(dlg.sample.speaker, dlg.sample.001~003).");
    }

    #endregion
    #region 테이블 · 키 조작

    // 키가 없으면 만든다. 원문(ko) 칸에 넣을 값이 있으면 함께 채운다.
    // 이미 있는 키는 건드리지 않는다 — 두 번 실행해도 손으로 넣은 번역이 지워지지 않게 하기 위함.
    static bool AddKeyIfMissing(string tableName, string key, string sourceText) {
        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if (collection == null) return false;

        SharedTableData shared = collection.SharedData;
        if (shared.Contains(key)) return true;

        shared.AddKey(key);
        EditorUtility.SetDirty(shared);

        if (string.IsNullOrEmpty(sourceText)) return true;

        foreach (StringTable table in collection.StringTables) {
            if (table.LocaleIdentifier.Code != SourceLocaleCode) continue;

            table.AddEntry(key, sourceText);
            EditorUtility.SetDirty(table);
        }

        return true;
    }

    // LocalizedString 필드에 테이블·키를 써 넣는다.
    // 필드가 private일 수도 있고 Undo·Dirty 처리도 필요해 SerializedObject로 다룬다.
    static void WireKey(Object asset, string fieldName, string tableName, string key) {
        StringTableCollection collection = LocalizationEditorSettings.GetStringTableCollection(tableName);
        if (collection == null) return;

        SerializedObject serialized = new SerializedObject(asset);
        SerializedProperty field = serialized.FindProperty(fieldName);

        if (field == null) {
            Debug.LogWarning($"[Localization] '{asset.name}' 에서 '{fieldName}' 필드를 찾지 못했습니다.", asset);
            return;
        }

        // 이름이 아니라 KeyId로 물려둔다. 나중에 키 이름을 바꿔도 연결이 끊기지 않는다.
        field.FindPropertyRelative("m_TableReference.m_TableCollectionName").stringValue = tableName;
        field.FindPropertyRelative("m_TableEntryReference.m_Key").stringValue = key;
        field.FindPropertyRelative("m_TableEntryReference.m_KeyId").longValue = collection.SharedData.GetId(key);

        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(asset);
    }

    static List<T> LoadAll<T>() where T : Object {
        List<T> found = new();

        foreach (string guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}")) {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) found.Add(asset);
        }

        return found;
    }

    #endregion
}
#endif
