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

        // 첫 칸을 띄우기 전에 테이블 로드를 끝내둔다. 그러지 않으면 대화가 시작되는 순간 끊긴다.
        yield return LocalizationText.WaitForInitialization();

        InputAction action = advanceAction != null ? advanceAction.action : null;
        bool enabledByUs = action != null && !action.enabled;
        if (enabledByUs) action.Enable();

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

        if (enabledByUs) action.Disable();
        if (hideOnFinish) view.Hide();
    }
}
