using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// 튜토리얼 리스트를 한 칸씩 재생하는 공용 루프 (DialoguePlayer와 같은 구조).
//
// 한 칸의 진행 규칙:
//   advanceAction(Client ▸ Ui ▸ NextDialogue 재사용) — 다음 칸으로.
//   Esc                                            — 남은 칸을 전부 건너뛴다.
//
// 대사와 달리 타자기 출력을 쓰지 않는다 — 튜토리얼은 짧은 요약 안내문이라 연출성 효과가 필요하지 않다.
//
// 원문은 칸을 띄우기 직전에 String Table에서 한 칸씩 받아온다 (DialoguePlayer와 같은 이유).
public static class TutorialPlayer {
    // entries를 순서대로 view에 표시한다. 호출 측 코루틴에서 yield return 하면 된다.
    public static IEnumerator Play(TutorialView view, IList<TutorialEntry> entries, InputActionReference advanceAction) {
        if (view == null || entries == null || entries.Count == 0) yield break;

        // 첫 칸을 띄우기 전에 테이블 로드를 끝내둔다. 그러지 않으면 튜토리얼이 시작되는 순간 끊긴다.
        yield return LocalizationText.WaitForInitialization();

        InputAction action = advanceAction != null ? advanceAction.action : null;
        bool enabledByUs = action != null && !action.enabled;
        if (enabledByUs) action.Enable();

        bool skip = false;

        foreach (TutorialEntry entry in entries) {
            if (entry == null) continue; // 인스펙터에서 비워둔 칸.

            string subtitle = string.Empty;
            string message = string.Empty;
            yield return LocalizationText.ResolveAsync(entry.Subtitle, value => subtitle = value);
            yield return LocalizationText.ResolveAsync(entry.Message, value => message = value);

            view.Show(entry, subtitle, message);
            yield return null; // 표시 직후 같은 프레임의 입력은 무시

            while (true) {
                if (WasPressed(Key.Escape)) {
                    skip = true;
                    break;
                }

                if (action != null && action.WasPerformedThisFrame()) break;

                yield return null;
            }

            if (skip) break;
        }

        if (enabledByUs) action.Disable();
        view.Hide();
    }

    // PauseMenuView 등 다른 화면과 같은 방식으로 Keyboard를 직접 읽는다 — Esc는 액션에 물리지 않은 대기 취소 키다.
    static bool WasPressed(Key key) {
        Keyboard keyboard = Keyboard.current;
        return keyboard != null && keyboard[key].wasPressedThisFrame;
    }
}
