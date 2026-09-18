using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 대사 리스트를 한 칸씩 재생하는 공용 루프.
// DialogueTriggerZone(영역 진입)과 DialogueStep(컷씬)이 같은 재생 규칙을 쓰도록 여기로 모았다.
//
// 한 칸의 진행 규칙:
//   입력(Client ▸ Ui ▸ NextDialogue) — 타자기 출력 중이면 즉시 완성, 완성된 뒤면 다음 칸으로
//   AUTO 켜짐                        — 출력이 끝나고 DialogueView.AutoAdvanceDelay 초가 지나면 자동으로 다음 칸으로
//
// 원문은 칸을 띄우기 직전에 String Table에서 한 칸씩 받아온다. 재생을 시작할 때 전부 미리 받아두지
// 않는 이유는, 대사 도중에 언어를 바꿔도 다음 칸부터 바뀐 언어로 이어지게 하기 위함이다.
public static class DialoguePlayer {
    // entries를 순서대로 view에 표시한다. 호출 측 코루틴에서 yield return 하면 된다.
    public static IEnumerator Play(DialogueView view, IList<DialogueEntry> entries,
                                   InputActionReference advanceAction, bool hideOnFinish) {
        if (view == null || entries == null || entries.Count == 0) yield break;

        // 첫 칸의 배경을 대화창보다 먼저 깔고, 전환이 끝난 뒤에 대사를 시작한다. 이미 깔려 있으면
        // (강제 시작형이 씬 시작 때 미리 깔아둔 경우) 같은 배경이라 아무 일도 없이 지나간다.
        PrepareBackground(view, entries);
        yield return view.WaitForBackground();

        // 첫 칸을 띄우기 전에 테이블 로드를 끝내둔다. 그러지 않으면 대화가 시작되는 순간 끊긴다.
        yield return LocalizationText.WaitForInitialization();

        InputAction action = advanceAction != null ? advanceAction.action : null;
        bool enabledByUs = action != null && !action.enabled;
        if (enabledByUs) action.Enable();

        view.HideOtherUi();

        foreach (DialogueEntry entry in entries) {
            if (entry == null) continue; // 인스펙터에서 비워둔 칸.

            string speaker = string.Empty;
            string text = string.Empty;
            yield return LocalizationText.ResolveAsync(entry.Speaker, value => speaker = value);
            yield return LocalizationText.ResolveAsync(entry.Text, value => text = value);

            view.Show(entry, speaker, text);
            yield return null;   // 표시 직후 같은 프레임의 입력은 무시

            float autoTimer = 0f;

            while (true) {
                if (view.SkipRequested) break; // SKIP — 안쪽 루프를 빠져나온 뒤 바깥에서 한 번 더 확인한다.

                // BlockAdvanceThisFrame: AUTO 버튼을 누른 클릭이 "다음 칸"으로도 먹히는 것을 막는다.
                bool advanced = action != null
                             && action.WasPerformedThisFrame()
                             && !view.BlockAdvanceThisFrame;

                if (advanced) {
                    if (view.IsTyping) view.CompleteReveal();   // 출력 중 → 즉시 완성
                    else break;                                 // 완성됨 → 다음 칸으로
                }

                if (view.IsTyping) {
                    autoTimer = 0f;                             // 아직 출력 중이면 자동 진행 대기를 미룸
                }
                else if (view.IsAutoAdvance) {
                    autoTimer += Time.deltaTime;
                    if (autoTimer >= view.AutoAdvanceDelay) break;
                }

                yield return null;
            }

            // 안쪽 루프는 "이 칸이 끝났다" 로도 빠져나오므로, 건너뛰기인지 여기서 다시 가른다.
            if (view.SkipRequested) break;
        }

        // 깃발을 지우는 것은 재생이 끝난 뒤여야 한다. 미리 지우면 바깥 루프가 건너뛰기를 놓친다.
        view.ClearSkipRequest();
        view.RestoreOtherUi();

        if (enabledByUs) action.Disable();
        if (hideOnFinish) view.Hide();
    }

    // 첫 칸(비워둔 칸은 건너뜀)의 배경 지시만 대화창 없이 먼저 반영한다. 배경이 비어 있으면 이전 배경을
    // 그대로 두는 규칙은 Show 와 같다. 대사 재생보다 앞서 화면을 준비해야 하는 쪽(강제 시작형)도 쓴다.
    public static void PrepareBackground(DialogueView view, IList<DialogueEntry> entries) {
        if (view == null || entries == null) return;

        foreach (DialogueEntry entry in entries) {
            if (entry == null) continue;

            view.PrepareBackground(entry);
            return;
        }
    }
}
