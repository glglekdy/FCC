# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## 말투 관련

~한다., ~교체. 등의 단답형/반말 형태가 아닌, ~습니다., ~합니다. 등 완성되고 자연스러운 존댓말 문장으로 작성해 주시기 바랍니다. 그리고 한국어를 완벽하게 구사해야 합니다.

## 프로젝트

**Final Curtain Call** (부제: 잊혀진 자들의 서커스) — Unity 6000.4.6f1 / URP / 2D 플랫포머 + 심리적 공포 + 액션. PC 타겟 프로젝트입니다.

프로젝트 코드와 에셋은 전부 `Assets/_Project/` 에 위치해 있습니다. `Assets/_Recovery/` 는 복구 잔해이므로 참조하지 않아야 합니다.

## 기획안 및 게임 구현 현황 (작업 전 확인)

전체 기획과 게임 구현 관리 페이지는 Notion 문서로 수시 관리되고 있습니다. **게임 로직·시스템·레벨 관련 작업 전 반드시 아래 두 문서를 읽고 구현 진행 상황 및 사양을 확인해야 합니다** (Notion MCP `notion-fetch` 등으로 조회):

1. **통합 기획서 (Notion)**: [https://tide-ink-208.notion.site/Ai-3a988e3012b280b58e60dcddb564c86e](https://tide-ink-208.notion.site/Ai-3a988e3012b280b58e60dcddb564c86e)
2. **게임 구현 관리 현황 (Notion)**: [https://tide-ink-208.notion.site/3ac88e3012b28071b3f2fcb8dfb31617?pvs=74](https://tide-ink-208.notion.site/3ac88e3012b28071b3f2fcb8dfb31617?pvs=74)

에이전트는 작업을 시작할 때 '게임 구현 관리 현황' 페이지를 읽어 게임 구현이 어디까지 진행되었는지(완료된 기능, 작업 중/미구현 기능)를 정확히 확인하고, 작업 진행 및 완료 후 해당 페이지의 상태를 갱신하고 정리해야 합니다.

코드에 직접 매핑되는 핵심 설정:

- **자아 게이지** = 체력입니다. 0이 되면 게임오버 처리됩니다. 코드상으로는 `Health` 컴포넌트입니다.
- **기억 조각** = 핵심 수집 재화입니다. 회복 아이템 겸 스킬 포인트 재료로 사용됩니다. 챕터1 10개 / 챕터2 20개 / 챕터4 큰 조각 1개입니다. **수집·소비 로직은 아직 미구현 상태입니다.**
- **거울** = 세이브포인트 겸 스킬 "정비하기" 지점입니다. 코드상으로는 `SaveMirror` 입니다 (저장 + 자아 게이지 전량 회복까지 구현되어 있으며, 정비 UI는 미구현 상태입니다).
- **오염도 게이지** (챕터2~) = 최대 100이며 시간당 누적되고, "팥" 섭취 시 0으로 초기화됩니다. 게이지가 다 차면 사망합니다. **아직 미구현 상태입니다.** 추가 시 `SaveData` 에 필드를 추가하는 방식으로 구현합니다 (구버전 세이브 호환은 아래 세이브 항목을 참고해 주시기 바랍니다).
- **스킬 6종** — Pure Dream(주인공, 정화) / Broken Phantasm(칼잡이, 투사체) / Cycle of Fate(저글러, 스택 폭발) / Invisible Reality(마임, 투명 벽) / Bent Spirit(컨토셔니스트, 회피+은신) / Close Call(곡예사, 이동기 🟡미확정). 최대 3개 장착 가능하며, 기억 조각으로 업그레이드합니다. `**Skill.cs`/`SkillManager.cs` 는 아직 뼈대(껍데기) 상태입니다.**
- **챕터 구조** — 1 서커스 극장 → 2 저승 도시 → 3 지하감옥 → 4 무대(보스전) → 엔딩 2분기로 구성되어 있습니다.

문서에서 🟡 표시는 미확정 항목입니다. **장르 구조는 메트로배니아로 최종 확정**되었으므로, 맵 구조 및 레벨 디자인 설계 시 메트로배니아 방식의 동선과 탐험 요소(능력 해금 기반 지형 통과 등)를 기본으로 적용해 주시기 바랍니다.

## 빌드 · 실행

CLI 빌드/테스트 스크립트나 CI는 없습니다. 모든 작업은 Unity Editor에서 수행합니다.

- MCP for Unity(`com.coplaydev.unity-mcp`)가 설치되어 있어 `mcp__UnityMCP__*` 툴로 에디터를 직접 조작할 수 있습니다. 스크립트를 수정한 뒤에는 `read_console` 로 컴파일 에러를 확인하고, `editor_state` 의 `isCompiling` 이 끝난 뒤에 새 타입을 사용해야 합니다.
- 한글 TMP 폰트 SDF 생성: 에디터 메뉴 `Tools ▸ KW Font ▸ Build Bold / Build Light` (`Assets/_Project/Editor/KwFontAssetBuilder.cs`, 아틀라스 4096², 한글 음절 11,172자 포함)로 실행합니다. 결과물 `Assets/_Project/Assets/Font/SDF/*.asset` 은 Git LFS 추적 대상입니다.
- 유닛 테스트는 없습니다. `com.unity.test-framework` 는 설치되어 있지만 테스트 어셈블리가 없으며, `DialogueTest.cs` 는 대사 마크업을 눈으로 확인하는 인게임 컴포넌트입니다.
- asmdef 없음 — 모든 스크립트가 `Assembly-CSharp` 한 덩어리로 이루어져 있습니다. 스크립트 하나만 고쳐도 전체가 재컴파일됩니다.

## 아키텍처

### 전투 파이프라인 (이벤트 기반)

`Health`가 허브입니다. **파일명은 `HealthSystem.cs` 이지만 클래스명은 `Health`** 입니다.

```
Player_Combat.DealDamage()          // OverlapCircleAll + HashSet 중복 방지
  → Health.TakeDamage(damage, sourcePosition)
      → OnDamaged(damage, sourcePosition)  ─┬→ HitReactor  넉백 · HitVfx 스파크 · DamagePopup · HitFeedback
                                            ├→ HitFlash    피격자 본인의 플래시/스쿼시
                                            └→ HealthBar
      → OnDeath(sourcePosition)            ─→ HitReactor  사망 연출
```

지켜야 할 규칙:

- `**sourcePosition`(공격 원점)이 모든 방향 계산의 출발점**입니다. 넉백·스파크 방향·이펙트 위치가 전부 여기서 파생되므로 데미지를 주는 쪽은 반드시 정확한 원점을 넘겨야 합니다.
- `Health.Die()` 는 이벤트 발행 직후 `Destroy(gameObject)` 를 호출합니다. **사망 연출은 반드시 오브젝트 바깥(싱글턴)에서 재생해야 합니다.**
- `OnDamaged` 는 치명타여도 항상 발행됩니다. 처치 여부는 구독자가 `CurrentHealth <= 0` 으로 판단합니다. 처치 시 `HitReactor` 는 일반 피드백을 건너뛰고 `OnDeath` 쪽에서 더 강한 피드백을 재생합니다 (히트스톱 이중 적용 방지).
- 피격 무적(`invincibleTime`)은 **플레이어만** 설정해야 합니다 (0.9 내외). 몬스터에 설정하면 공격 쿨타임보다 길어져 때려도 반응이 없는 것처럼 보이게 됩니다.
- 데미지가 아닌 경로(세이브 복원, 거울 회복, 기억 조각 섭취)로 체력을 바꿀 때는 `Health.SetHealth(value)` / `RestoreFull()` 을 사용합니다. `**SetHealth(0)` 은 `Die()` 를 호출하지 않습니다** — 사망은 `TakeDamage` 를 통해서만 일어나야 연출이 한 번만 정상 재생됩니다.
- 카메라 쉐이크 파형은 `HitFeedback.ApplyShakeProfile()` 이 코드로 생성하여 `CinemachineImpulseSource` 에 덮어씁니다. **인스펙터에서 Impulse Shape / Duration 을 변경하더라도 플레이 시 무시됩니다.** 감각 조정은 `HitFeedback` 의 `shakeDuration` · `shakeOscillations` · `shakeDamping` 으로 조정해야 합니다 (플레이 중 값을 바꾸면 `OnValidate` 로 즉시 반영됩니다).

### 싱글턴

`HitFeedback` · `HitVfx` · `DamagePopup` · `ObjectiveManager` · `SaveManager` · `SkillLoadoutView` · `ScreenFader` 7개입니다. 전부 `Awake()` 에서 중복 검사 후 `DontDestroyOnLoad` 처리합니다.

호출할 때는 `**?.` 대신 `!= null`** 을 사용해야 합니다. 파괴된 뒤에도 C# 참조가 남을 수 있어 Unity의 `==` 오버로드를 타야 하기 때문입니다.

### 씬 전환

씬 이동은 전부 `**ScreenFader.LoadScene(sceneName)**` 한 곳을 거칩니다 (`Scripts/System/`). 페이드 아웃 → 비동기 로드 → 페이드 인을 한 번에 맡으며, 전환 중 클릭 차단·연타 방지·`timeScale` 복구까지 처리합니다. 씬에 페이더가 없으면 연출 없이 바로 이동하므로 호출부에서 분기할 필요가 없습니다.

- 화면 요소는 프리팹 `Prefabs/UI/ScreenFader.prefab` 입니다. **씬 전환이 일어나는 씬마다 하나씩 놓아야 합니다** (`Tools ▸ FCC ▸ Build / Place Screen Fader Prefab`). 겹쳐도 **나중에 온 쪽이 물러납니다** — 먼저 있던 쪽이 화면을 덮은 채 넘어왔기 때문입니다.
- 도착한 씬이 페이드 인을 직접 연출하려면 `Start()` 에서 `SuppressAutoFadeIn()` 을 부른 뒤 `FadeCover()` / `SetCover()` 로 막을 직접 걷습니다. **부른 쪽이 반드시 걷어야 합니다** — 안 그러면 검은 화면에 갇힙니다. `Awake` 가 아니라 `Start` 인 이유는, 같은 씬의 `ScreenFader` 와 `Awake` 실행 순서가 정해져 있지 않기 때문입니다.
- `ScreenWakeUp` 이 그 예로, First 씬에서 암전 유지 → 눈 깜빡임 → 완전히 뜨기 순서의 깨어나는 연출을 재생하고 그동안 `Player_move.isMovementLocked` 로 이동을 잠급니다.
- 연출은 전부 `Time.unscaledDeltaTime` / `WaitForSecondsRealtime` 기준입니다 (일시정지 중에 씬을 나가도 페이드가 멈추지 않아야 하기 때문입니다).

### 스토리 (대사 · 컷씬)

- 재생 루프는 `DialoguePlayer` (static, `IEnumerator Play(...)`) 하나로 모여 있으며, **영역 진입형(`DialogueTriggerZone`)과 컷씬형(`DialogueStep`)이 이를 공유**합니다. 진행 규칙(타자기 스킵 / AUTO 대기)을 변경할 때는 이 부분만 수정하면 됩니다.
- `DialogueTriggerZone` 은 시작 방식이 2종입니다. 기본은 영역 진입형이고, `**autoStart` 를 켜면 조작과 무관하게 씬 시작 시 강제로 재생**됩니다 (이때 Collider 트리거는 무시되고 씬 뷰 기즈모도 그려지지 않습니다). `waitForWakeUp` 을 함께 켜면 `ScreenWakeUp.WaitUntilFinished()` 로 깨어나기 연출이 끝나기를 기다린 뒤 `autoStartDelay` 만큼 쉬었다가 시작합니다. First 씬의 `OpeningDialogue` 가 이 방식입니다.
- 대사의 **배치**(어느 칸에 어떤 초상화·효과음이 붙는지)는 ScriptableObject가 아니라 **컴포넌트 인스펙터의 `List<DialogueEntry>`** 에 직접 작성합니다. 기존 ScriptableObject 방식(`DialogueScript`/`DialogueLine`)은 사용되지 않으며 `쓰레기통/Dialogue/` 로 이동되었습니다.
- **대사 원문은 인스펙터에 직접 적지 않습니다.** `DialogueEntry.Speaker` · `Text` 는 String Table `Dialogue` 의 키를 가리키는 `LocalizedString` 입니다 (아래 다국어 항목 참고). 원문 편집은 키를 고른 뒤 인스펙터에서 바로 하거나 Localization Tables 창에서 합니다.
- 표시는 `DialogueView`, 본문 마크업 태그(`<shake>` `<wave>` `<rainbow>` `<round>` `<speed>`)는 `DialogueEffect`가 담당합니다. 마크업은 번역문에도 그대로 써야 하므로 **번역가에게 태그를 지우지 말라고 안내해야 합니다.**
- 컷씬은 `CutSceneManager` + 자식 오브젝트로 붙인 `CutSceneStep` 들을 순서대로 `yield return step.Execute()` 로 실행합니다. 새 연출을 추가하려면 `Story/CutScene/Steps/` 에 `CutSceneStep` 파생 클래스를 추가하면 됩니다.

### 목표(퀘스트) 시스템

`ObjectiveManager` 의 진입점은 `CompleteObjective(id)` 와 `AddProgress(id, amount)` 둘뿐입니다. `DialogueTriggerZone` · `CutSceneManager` · `SaveMirror` 에는 `objectiveId` 필드가 있어, 비어 있지 않으면 재생/저장이 끝날 때 자동으로 해당 목표를 완료 처리합니다. UI는 `ObjectiveChecklistView` 가 이벤트를 구독해 갱신합니다.

세이브용으로 `CaptureState()` / `RestoreState(snapshot)` 가 따로 존재합니다. 진행도(`id`·`currentCount`·`isCompleted`)만 `ObjectiveSaveEntry` 로 분리하여 저장하므로, 설명문·목표 수량 같은 기획 데이터를 고쳐도 기존 세이브가 이를 덮어쓰지 않습니다. `**RestoreState` 는 완료 이벤트를 다시 발생시키지 않습니다** — 불러올 때마다 컷신/보상이 재재생되는 것을 방지하기 위함입니다.

### 다국어 (Localization)

`com.unity.localization` 을 사용합니다. **원문 언어는 한국어(`ko`)이고 번역 대상은 영어(`en`)** 입니다. 에셋은 전부 `Assets/_Project/Assets/Localization/` 에 있습니다 (`LocalizationSettings.asset` · `Locales/` · `Tables/`).

String Table은 2종입니다 — `**Dialogue**`(대사·화자명), `**Objective**`(목표·미션 문구). 대사와 UI 단문은 번역 검수 기준이 달라 시트를 나눴습니다.

- 플레이어에게 보이는 문구는 `string` 이 아니라 `**LocalizedString**` 필드로 둡니다. 현재 전환된 곳은 `DialogueEntry.Speaker`·`Text`, `ObjectiveDefinition.description`, `Mission.missionName`·`description` 입니다. (스킬 이름·설명과 메뉴 UI 문구는 **아직 미전환**입니다.)
- 조회는 `**LocalizationText**`(`Scripts/System/`) 한 곳을 거칩니다. 코루틴에서는 `ResolveAsync(source, onDone)`, 즉시 값이 필요하면 `Resolve(source, fallback)` 을 씁니다. 화면을 처음 그리기 전에는 `WaitForInitialization()` 으로 한 번 기다려야 첫 조회에서 프레임이 끊기지 않습니다.
- 대사 원문은 **칸을 띄우기 직전에** 한 칸씩 받아옵니다(`DialoguePlayer`). 미리 전부 받아두지 않는 이유는 대사 도중 언어를 바꿔도 다음 칸부터 이어지게 하기 위함입니다.
- `DialogueView.Show(entry, speaker, text)` 는 **해석이 끝난 문자열을 밖에서 받습니다.** 조회가 비동기라 코루틴 쪽에서만 기다릴 수 있기 때문입니다.
- 상시 노출 UI(`ObjectiveChecklistView`)는 `LocalizationSettings.SelectedLocaleChanged` 를 구독해 언어가 바뀌면 다시 그립니다. **static 이벤트이므로 `OnDestroy` 에서 반드시 구독을 해제해야 합니다.**
- 번역이 비어 있는 칸은 `en` Locale의 `FallbackLocale` 메타데이터 + 데이터베이스의 `UseFallback` 로 **한국어 원문으로 대체**됩니다. **둘 중 하나만 설정하면 동작하지 않고** 화면에 `No translation found for '...'` 라는 디버그 문구가 그대로 뜹니다.
- 셋업·문구 이전은 에디터 메뉴 `**Tools ▸ FCC ▸ Localization ▸ 초기 셋업` / `기존 문구 테이블로 옮기기`**(`Editor/LocalizationBootstrap.cs`)로 실행합니다. 멱등이라 여러 번 눌러도 안전하며, 언어를 추가할 때는 `LocaleCodes` 에 코드를 넣고 다시 실행하면 기존 테이블에도 열이 추가됩니다.
- 번역 편집·진행 상황 확인은 `Window ▸ Asset Management ▸ Localization Tables` 에서 하고, 외부 번역은 같은 창의 CSV / Google Sheets 내보내기를 씁니다.
- **영어 외 언어를 추가할 때는 폰트를 함께 확인해야 합니다.** 한글 SDF 아틀라스에는 가나·한자가 없어 일본어·중국어를 넣으면 두부(tofu)로 렌더됩니다.

### 상호작용 (E키)

`Scripts/Interaction/` 내 3종 클래스로 구성됩니다.

- `IInteractable` — `InteractLabel` / `CanInteract` / `PromptAnchor` / `Interact(interactor)`. **거울·NPC·조사 오브젝트를 새로 만들 땐 이것만 구현하면** 탐지·프롬프트·입력 전달이 자동으로 처리됩니다.
- `PlayerInteractor` (플레이어에 부착) — 매 프레임 `OverlapCircle` 로 주변을 훑어 가장 가까운 대상을 탐지하고, `[E] 문구` 프롬프트(`TextMeshPro` 를 코드로 생성)를 띄웁니다. 거리는 콜라이더가 아닌 `PromptAnchor` 기준으로 측정합니다.
- `SaveMirror` — 세이브포인트 구현체입니다.

주의할 점:

- 대상 오브젝트에는 `**Is Trigger` 콜라이더**가 있어야 탐지됩니다 (`ContactFilter2D.useTriggers = true`). 콜라이더가 자식에 있더라도 `GetComponentInParent` 로 찾습니다.
- `Player_move.isMovementLocked` 가 켜져 있으면(대사·컷씬 중) 상호작용이 통째로 차단됩니다. 대사 진행키와 겹쳐 대화가 끝나는 순간 동일한 입력이 중복 실행되는 것을 방지하기 위함입니다.
- 프롬프트 연출은 전부 `Time.unscaledDeltaTime` 기준입니다 (히트스톱 중에도 정상 속도로 표시되어야 하기 때문입니다).

### 세이브

`SaveManager` 가 파일 입출력(`Write`/`Read`/`DeleteSave`)과 상태 수집·복원(`SaveGame`/`LoadGame`)을 전담합니다. 저장 위치는 `Application.persistentDataPath/save.json` 이며, 직렬화에는 `JsonUtility` 를 사용합니다.

```
SaveMirror.Interact()  → Health.RestoreFull()          // 회복이 먼저 진행되어야 합니다. 그렇지 않으면 깎인 체력이 그대로 기록됩니다.
                       → SaveManager.SaveGame(mirrorId, respawnPosition)
                             → Health / ObjectiveManager.CaptureState() 수집 → Write → OnSaved
                       → ObjectiveManager.CompleteObjective(objectiveId)   // objectiveId 가 비어있지 않을 때
```

- 정식 저장 지점은 **거울(`SaveMirror`)** 입니다. `Player_move` 의 **F5/F9 는 개발용 단축키**이며, `checkpointId` 를 빈 문자열로 넘겨 퀵세이브로 기록합니다.
- `SaveGame` 오버로드는 2종류가 있습니다. `SaveGame(id, respawnPosition)` 은 복귀 지점을 명시하고(거울용, 플레이어 현재 위치를 사용할 경우 거울에 밀착 저장 시 복귀 위치가 어긋날 수 있음), `SaveGame(id)` 는 플레이어의 현재 위치를 그대로 사용합니다.
- `SaveData` 는 `**JsonUtility` 가 다루므로 전부 public 필드**여야 합니다. 필드를 새로 추가하더라도 기존 세이브는 그대로 읽히며(없는 필드는 0/null), `maxHealth == 0` 이면 체력을 기록하지 않던 구버전 세이브로 판단하여 체력을 건드리지 않습니다. 후속 시스템(오염도·기억 조각)은 여기에 필드를 추가하여 확장합니다.
- `LoadGame()` 은 **같은 씬 안에서만** 복원합니다. 메인 메뉴 → 이어하기처럼 씬 전환이 필요한 경우 `Read()` 로 `sceneName` 을 먼저 확인하여 씬을 전환한 뒤 호출해야 합니다.
- `SaveManager.Awake()` 는 `transform.SetParent(null)` 을 먼저 호출합니다. `DontDestroyOnLoad` 는 루트 오브젝트에서만 동작하는데, 씬에서는 `GAME_MANAGER` 하위에 배치되어 있기 때문입니다.

### 입력

`Assets/_Project/Assets/Input/Client.inputactions` 를 사용합니다 (루트의 `InputSystem_Actions.inputactions` 는 Unity 기본 템플릿으로 미사용됩니다).

- `Client ▸ Player` — Move / Jump / Attack / Interact (`E` · 패드 남쪽 버튼)
- `Client ▸ Ui` — NextDialogue (대사 다음/스킵)

`Player_Combat`(`OnAttack`)과 `PlayerInteractor`(`OnInteract`)는 `PlayerInput` 의 SendMessage 방식(`void OnXxx(InputValue)`)을 사용하며, 대사 쪽은 `InputActionReference` 를 인스펙터로 주입받습니다.

키 바인딩을 변경하면 `PlayerInteractor.keyLabel`(프롬프트에 표시되는 `[E]`)도 함께 수정해야 합니다. 자동 연동되지 않습니다.

### 씬

`Main_menu` → `First`(0장 프롤로그, `Chapter0/CutScenePage/`) → `CoreScene`(인게임 본편). 빌드 세팅에는 이 3개가 이 순서로 등록되어 있으며 `Main_menu` 가 시작 씬(인덱스 0)입니다. 메인 메뉴의 `[Game Start]` 는 `First` 로 넘어갑니다 (`MainMenuController.startSceneName`, 버튼의 `Ui_LoadScenes.sceneName`).

## 코드 컨벤션

`.editorconfig` 기준 **K&amp;R 중괄호**(여는 중괄호 같은 줄)를 사용합니다. `Assets/_Project/Assets/Scripts/` 아래 `Character/` · `Combat/` · `Objective/` · `Interaction/` · `_Data/` 계열이 표준 스타일입니다:

```csharp
public class HitReactor : MonoBehaviour {
    #region 인스펙터 변수

    [Header("넉백")]
    public float knockbackForce = 6f; // 넉백 세기.

    #endregion
    #region 컴포넌트 변수

    Health health;

    #endregion
    #region 유니티 라이프 사이클

    void Awake() {
        health = GetComponent<Health>();
    }

    #endregion
}
```

- `#region` 구획을 한글로 구분합니다: `인스펙터 변수` / `컴포넌트 변수` / `유니티 라이프 사이클` / 기능별 구획.
- 인스펙터 노출 필드는 `public` + `[Header]` + **줄 끝 한글 주석으로 용도 설명**을 추가합니다. 접근제한자 `private` 는 생략합니다.
- 씬에서 직접 배치·연결해야 하는 필드는 `// **몬스터 발밑에 빈 오브젝트를 만드세요.**` 처럼 `**` 로 강조합니다.
- 주석은 한글로 작성하며, "무엇"이 아닌 **"왜 이렇게 구현했는지"** 이유를 명시합니다 (기존 코드 스타일 준수).
- 비동기 처리에는 코루틴만 사용합니다. async/await, UniTask, DOTween은 사용하지 않습니다 (`Plugins/Demigiant` 는 패키지만 포함되어 있고 코드에서는 미사용).
- `Story/` 계열(Allman 중괄호 + `_` 접두사 private 필드 + XML 주석)과 `_Player/Move/Player_move.cs` 는 기존 스타일이 다릅니다. 이는 **레거시로 취급**하며, 새 코드는 위의 K&amp;R 스타일로 작성하되 해당 파일을 수정할 때 점진적으로 정돈합니다 (`_Data/` 는 이미 K&amp;R 스타일로 이전 완료).

## UI 구현 규칙 (예외 없음)

**UI는 절대 스크립트로 조립하지 않습니다.** Canvas·패널·버튼·라벨 같은 화면 요소는 전부 **프리팹 또는 씬 오브젝트**로 제작하여, 에디터 인스펙터에서 직접 확인하고 수정할 수 있어야 합니다. 코드로 `new GameObject()` + `AddComponent<Image>()` 를 호출하여 화면을 구축할 경우, 디자인 요소 수정 시마다 스크립트를 고쳐야 하고 씬 뷰 사전 확인이 불가능해집니다.

준수 사항:

1. **화면 구조·디자인 = 프리팹**, **상태 계산·입력 = 스크립트.** 스크립트는 인스펙터로 주입받은 참조를 갱신하는 역할만 담당합니다 (`SkillLoadoutView` ← `SkillSlotView` · `SkillRowView`, `ObjectiveChecklistView` ← `ObjectiveItemView` 구조 준수).
2. 목록처럼 개수가 변동되는 UI는 **단일 항목 프리팹을 따로 제작**하여 `Instantiate(rowPrefab, container)` 로 생성합니다. 항목 내부의 라벨·아이콘·색상 등은 해당 항목 프리팹의 컴포넌트가 관리합니다.
3. 인스펙터로 주입받는 참조는 `public` + `[Header("연결")]` 로 정리하고, 미연결 시 **누락된 참조 이름을 명확히 알리는 검사**를 `Awake` 에 배치합니다 (NullReference 예외 방지).
4. 고정 문구(제목·도움말)는 프리팹의 TMP에 직접 입력합니다. 상황에 따라 동적으로 변경되는 문구만 인스펙터 `string` 필드로 노출합니다.
5. 한글을 사용하는 TMP에는 **반드시 한글 SDF 폰트**(`Font/SDF/SCDream6 SDF.asset` 등)를 지정해야 합니다. 비워둘 경우 기본 폰트로 처리되어 글자가 깨질 수 있습니다.
6. 프리팹을 다량 생성해야 하는 경우 `**Assets/_Project/Editor/` 에 프리팹 생성 메뉴 도구를 구현**하여 일괄 생성한 후 세부 디자인을 편집합니다 (`SkillLoadoutPrefabBuilder.cs` → `Tools ▸ FCC ▸ Build Skill Loadout Prefab`). 에디터 전용 도구 생성은 허용되나, **런타임 시 동적 UI 조립은 금지합니다.**
7. 예외적으로 런타임 동적 생성이 남아있는 구현부(`HealthBar` · `DamagePopup` · `PlayerInteractor` 프롬프트)는 **레거시**입니다. 새로 작성 시 참고하지 않으며, 해당 기능 수정 시 프리팹 구조로 전환해야 합니다.

## 파일 · 폴더 정리 규칙

기본 원칙: **큰 틀(주체·시스템)은 폴더로 구분하고, 세부 기능은 하위 폴더를 추가 생성하지 않고 스크립트 파일명으로 구분합니다.**

```
Scripts/
  _Player/      플레이어 주체 (Camera/ · Combat/ · Move/ · Skill/ 로 갈라짐)
  Monster/      몬스터 주체
  (Npc/)        NPC가 생기면 여기에 새로 만든다
  Character/    플레이어·몬스터가 함께 쓰는 공용 컴포넌트 (Health · HitReactor · HitFlash · HealthBar · GroundMoveSystem · FlyMoveSystem)
  Combat/       전투 연출 싱글턴 (HitFeedback · HitVfx · DamagePopup)
  Interaction/  상호작용 (IInteractable · PlayerInteractor · SaveMirror)
  Objective/    목표(퀘스트)
  Story/        대사 · 컷씬 (Dialogue/ · CutScene/Steps/)
  Ui/           메뉴 · 설정 UI
  System/       게임 전역 (GameSpeedController · GameDebugLog · LocalizationText · ScreenFader · ScreenWakeUp)
  _Data/        세이브 데이터
```

1. 새 스크립트는 대상 주체를 확인하여 폴더를 지정합니다. 플레이어 전용은 `_Player/`, 몬스터 전용은 `Monster/`, **공용 요소는 `Character/`** 폴더에 배치합니다.
2. **폴더 깊이는 큰 틀 상위 수준에서 유지합니다.** 소수 파일 관리를 위한 과도한 하위 폴더 생성을 지양합니다. 파일 수가 많고 성격이 명확히 구분될 경우에만 분리합니다 (`_Player/` 및 `Story/` 참고).
3. 파일 이름은 `**{큰 틀}{기능}**` 형태로 소속을 명시합니다 — `Player_Combat` · `ObjectiveManager` · `DialogueView` · `CutSceneStep` · `SaveManager`. 폴더 경로 없이도 소속을 식별할 수 있도록 합니다. `_Player/` 계열만 `Player_` 형태의 밑줄 표기를 사용하고 나머지는 이어서 표기합니다.
4. **파일명과 클래스명은 일치시킵니다.** 명칭이 어긋나 있는 `HealthSystem.cs`(클래스 `Health`) 는 남은 레거시 항목입니다 (`Ui/Ui_LoadScenes.cs` 는 클래스명을 `Ui_LoadScenes` 로 맞춰 해소했습니다). `Monster/Attack.cs` 등은 수정 작업 시 `Monster_Attack` 으로 정형화해야 합니다.
5. 인터페이스는 `I` 접두사(`IInteractable`)를 사용하며, 파생 클래스는 베이스 클래스명을 접미사로 붙입니다(`CutSceneStep` ← `DialogueStep` · `CameraStep` · `WaitStep` · `ImageStep`).
6. 에셋 관리 또한 동일 원칙을 적용합니다 — `Prefabs/Monster` · `Prefabs/UI` · `Prefabs/VFX` · `Sprites/Player` 처럼 상위 폴더 구조만 유지하고 파일명으로 상세 구분합니다.
7. 폴더명의 `_` 접두사(`_Player` · `_Data`)는 프로젝트 창 정렬 목적입니다. **신규 상위 폴더 생성 시 `_` 접두사를 사용하지 않습니다** (`Monster/` · `Interaction/` · `Objective/` 준수).
8. 파일 이동 시 반드시 `**.meta` 파일을 동시 이동**시켜야 합니다 (`git mv`). 누락 시 GUID 재발급으로 인해 씬/프리팹 컴포넌트 연결이 유실됩니다. Unity 에디터가 실행 중인 경우 프로젝트 창 드래그 방식을 권장합니다.

## 쓰레기통 규칙

1. 미사용 스크립트/에셋 삭제 요청 시 직접 삭제하지 않고 `쓰레기통/` 폴더로 `git mv` 이동합니다.
2. 쓰레기통 위치는 반드시 **프로젝트 루트**(= `Assets/` 외부)여야 합니다. `Assets/` 내부 배치 시 `.cs` 컴파일이 계속 진행되거나 `.asset` 참조 유실 문제가 발생합니다.
3. 이동 시 원본 파일의 **부모 폴더명**으로 하위 폴더를 생성한 후 이동시킵니다. (예: `Scripts/Story/Dialogue/DialogueLine.cs` → `쓰레기통/Dialogue/DialogueLine.cs`) 동일 시스템 관련 파일인 경우 시스템명 폴더로 통합 관리합니다. (예: `Assets/Story/NewDialogue.asset` → `쓰레기통/Dialogue/`)
4. `.meta` 파일도 반드시 함께 이동시켜야 합니다.
5. 쓰레기통과 프로젝트 내에 동일한 파일이 중복 존재하는 경우, 쓰레기통 파일만 유지하고 프로젝트 측 중복 파일은 지웁니다.

## 📝 [필수 준수] 커밋 메시지 규칙

이 프로젝트에서 git 커밋을 생성할 때는(사용자가 명시적으로 커밋을 요청한 경우) **예외 없이** 아래 컨벤션을 따라야 합니다.

### 기본 구조

커밋 메시지는 제목(Header) / 본문(Body) / 바닥글(Footer) 3개 파트로 구성하며, 제목 외 영역은 선택 사항입니다.

```
<type>(<scope>): <subject>

<body>

<footer>
```

### 1. 타입(Type) 분류

- `feat`: 새로운 기능 추가
- `fix`: 버그 수정
- `refactor`: 버그 수정이나 기능 추가 없이 코드 구조만 개선
- `perf`: 성능 향상을 위한 코드 변경
- `style`: 코드 동작에 영향 없는 포맷팅, 세미콜론 누락, 공백 수정
- `docs`: 문서 수정 (README, 주석, 위키 등)
- `test`: 테스트 코드 추가 또는 기존 테스트 수정
- `chore`: 빌드 업무 수정, 패키지 매니저 설정, 기타 자잘한 작업
- `ci`: CI/CD 설정 파일 및 스크립트 변경 (GitHub Actions 등)

### 2. 작성 규칙

- **커밋 메시지는 무조건 한국어로 작성**합니다.
- 제목(Subject)
  - 50자 내외로 간결하게 작성하며 마침표(`.`)를 찍지 않습니다.
  - 명확한 명사형 종결이나 간결한 서술형을 유지합니다 (`~ 수정`, `~ 추가` 등).
- 스코프(Scope, 선택)
  - 변경된 모듈이나 범위를 괄호 안에 표기합니다 (`feat(auth): ...`, `fix(ui): ...`).
- 본문(Body, 선택)
  - "무엇을 어떻게"보다는 **"왜(Why)" 변경했는지**를 72자 줄바꿈 규칙에 맞춰 설명합니다.
- 바닥글(Footer, 선택)
  - 이슈 트래킹 번호나 Breaking Changes(주요 하위 호환성 깨짐)를 명시합니다 (예: `Resolves: #42`, `BREAKING CHANGE: ...`).

