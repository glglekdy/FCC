using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

public class DialogueEffect : MonoBehaviour
{
    [SerializeField] private TMP_Text _textComponent;
    [SerializeField] private float _fontSize        = 0f;   // 0 = TMP 컴포넌트 설정값 그대로
    [SerializeField] private float _shakeIntensity = 2f;
    [SerializeField] private float _shakeSpeed     = 25f;
    [SerializeField] private float _waveAmplitude  = 3f;
    [SerializeField] private float _waveSpeed      = 3f;
    [SerializeField] private float _waveFrequency  = 1.5f;
    [SerializeField] private float _rainbowSpeed   = 1f;
    [SerializeField] private float _roundRadius    = 3f;
    [SerializeField] private float _roundSpeed     = 3f;
    [SerializeField] private float _typeSpeed      = 30f;   // 초당 출력 글자 수, 0 이하면 즉시 전체 출력

    // 설정의 「대사 속도」가 곱하는 배율. 인스펙터의 _typeSpeed 를 직접 덮어쓰지 않는 이유는,
    // 맞춰둔 연출 속도를 설정이 지워버리면 되돌릴 수가 없기 때문이다. GameSettings 가 적용 시점에 넣어준다.
    public static float SpeedScale = 1f;

    [Header("타이핑 사운드")]
    [SerializeField] private AudioSource _audioSource;            // 비우면 무음
    [SerializeField] private AudioClip[] _typeSounds;            // 여러 개면 랜덤 재생, 비우면 무음
    [SerializeField, Range(0f, 1f)] private float _typeVolume = 1f;
    [SerializeField] private int  _typeSoundStride    = 2;       // N글자마다 1회 재생(기관총음 방지), 1=매 글자
    [SerializeField] private bool _silentOnWhitespace = true;    // 공백/줄바꿈에서는 소리 안 냄

    // 타자기 출력 상태
    private int       _totalChars;
    private int       _visibleCount;
    private Coroutine _typeRoutine;

    // 글자별 이팩트 활성 여부
    private bool[] _shakeChars   = System.Array.Empty<bool>();
    private bool[] _waveChars    = System.Array.Empty<bool>();
    private bool[] _rainbowChars = System.Array.Empty<bool>();
    private bool[] _roundChars   = System.Array.Empty<bool>();

    // 글자별 해석된 강도 (마크업 태그에 파라미터가 없으면 Inspector 기본값)
    private float[] _charShakeIntensity = System.Array.Empty<float>();
    private float[] _charShakeSpeed     = System.Array.Empty<float>();
    private float[] _charWaveAmplitude  = System.Array.Empty<float>();
    private float[] _charWaveSpeed      = System.Array.Empty<float>();
    private float[] _charWaveFrequency  = System.Array.Empty<float>();
    private float[] _charRainbowSpeed   = System.Array.Empty<float>();
    private float[] _charRoundRadius    = System.Array.Empty<float>();
    private float[] _charRoundSpeed     = System.Array.Empty<float>();
    private float[] _charTypeSpeed      = System.Array.Empty<float>();

    private bool       _hasShakeEffect;
    private bool       _hasWaveEffect;
    private bool       _hasRainbowEffect;
    private bool       _hasRoundEffect;
    private Vector3[][] _baseVertices = System.Array.Empty<Vector3[]>();

    // ── 공개 API ──────────────────────────────────────────────────────────────

    /// <summary>TMP 컴포넌트의 fontSize를 직접 변경합니다. SetText 호출 전후 모두 유효합니다.</summary>
    public void SetFontSize(float size)
    {
        _fontSize = size;
        if (size > 0f) _textComponent.fontSize = size;
    }

    /// <summary>타자기 출력이 진행 중인지 여부. (버튼 입력 시 스킵 판단에 사용)</summary>
    public bool IsTyping => _visibleCount < _totalChars;

    /// <summary>진행 중인 타자기 출력을 즉시 끝내고 전체 텍스트를 한 번에 표시합니다.
    /// 출력 도중 버튼 등을 눌렀을 때 호출하세요.</summary>
    public void CompleteReveal()
    {
        if (_typeRoutine != null) { StopCoroutine(_typeRoutine); _typeRoutine = null; }
        _visibleCount = _totalChars;
        ApplyReveal();
    }

    /// <summary>마크업 텍스트로 설정합니다. 태그별 파라미터(&lt;shake a=8 s=50&gt; 등)와
    /// &lt;speed=6&gt; 출력 속도, 여러 줄(\n)을 지원합니다. 자세한 태그는 ParseTags 주석 참고.</summary>
    public void SetText(string rawText)
    {
        if (_fontSize > 0f) _textComponent.fontSize = _fontSize;
        _textComponent.text = ParseTags(rawText);
        CacheBaseVerticesIfNeeded();
        StartTypewriter();
    }

    // ── 내부 ──────────────────────────────────────────────────────────────────

    private void Update()
    {
        bool revealing = _visibleCount < _totalChars;
        if (!_hasShakeEffect && !_hasWaveEffect && !_hasRainbowEffect && !_hasRoundEffect && !revealing) return;

        TMP_TextInfo textInfo = _textComponent.textInfo;

        // 이전 프레임 오프셋 누적 방지 — 원본 위치로 복원
        if (_hasShakeEffect || _hasWaveEffect || _hasRoundEffect)
        {
            for (int m = 0; m < _baseVertices.Length && m < textInfo.meshInfo.Length; m++)
            {
                Vector3[] dst = textInfo.meshInfo[m].vertices;
                int len = Mathf.Min(_baseVertices[m].Length, dst.Length);
                System.Array.Copy(_baseVertices[m], dst, len);
            }
        }

        if (_hasShakeEffect)   AnimateShake(textInfo);
        if (_hasWaveEffect)    AnimateWave(textInfo);
        if (_hasRoundEffect)   AnimateRound(textInfo);
        if (_hasRainbowEffect) AnimateRainbow(textInfo);
        if (revealing)         ApplyRevealMask(textInfo);

        _textComponent.UpdateVertexData(
            TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
    }

    private void CacheBaseVerticesIfNeeded()
    {
        if (!_hasShakeEffect && !_hasWaveEffect && !_hasRoundEffect) return;

        _textComponent.ForceMeshUpdate();
        TMP_TextInfo textInfo = _textComponent.textInfo;
        _baseVertices = new Vector3[textInfo.meshInfo.Length][];
        for (int m = 0; m < textInfo.meshInfo.Length; m++)
        {
            Vector3[] src = textInfo.meshInfo[m].vertices;
            _baseVertices[m] = new Vector3[src.Length];
            src.CopyTo(_baseVertices[m], 0);
        }
    }

    // ── 타자기 출력 ───────────────────────────────────────────────────────────

    private void StartTypewriter()
    {
        // characterCount를 정확히 얻기 위해 메쉬를 먼저 갱신
        _textComponent.ForceMeshUpdate();
        _totalChars   = _textComponent.textInfo.characterCount;
        _visibleCount = 0;

        if (_typeRoutine != null) StopCoroutine(_typeRoutine);

        if (_typeSpeed <= 0f || _totalChars == 0)
        {
            CompleteReveal();   // 즉시 전체 출력
            return;
        }

        ApplyReveal();          // 첫 프레임 전부 보이는 현상 방지 (전부 숨김)
        _typeRoutine = StartCoroutine(TypeRoutine());
    }

    private IEnumerator TypeRoutine()
    {
        while (_visibleCount < _totalChars)
        {
            int idx = _visibleCount;
            _visibleCount++;
            ApplyReveal();
            PlayTypeSound(idx);

            // 방금 출력한 글자의 줄별 속도만큼 대기 (0 이하면 컴포넌트 기본값)
            float speed = idx < _charTypeSpeed.Length ? _charTypeSpeed[idx] : _typeSpeed;
            if (speed <= 0f) speed = _typeSpeed;

            // 설정의 「대사 속도」를 배율로 곱한다. 마크업 <speed> 로 지정한 줄별 속도도 같은 비율로 빨라진다.
            speed *= SpeedScale;
            if (speed <= 0f) { CompleteReveal(); break; }

            yield return new WaitForSeconds(1f / speed);
        }
        _typeRoutine = null;
    }

    /// <summary>idx번째 글자가 출력될 때 효과음을 재생합니다.
    /// stride로 빈도를 줄이고, 공백/줄바꿈은 (옵션에 따라) 건너뜁니다.</summary>
    private void PlayTypeSound(int idx)
    {
        if (_audioSource == null || _typeSounds == null || _typeSounds.Length == 0) return;
        if (_typeSoundStride > 1 && idx % _typeSoundStride != 0) return;

        if (_silentOnWhitespace && idx < _textComponent.textInfo.characterCount)
        {
            char c = _textComponent.textInfo.characterInfo[idx].character;
            if (char.IsWhiteSpace(c)) return;
        }

        AudioClip clip = _typeSounds.Length == 1
            ? _typeSounds[0]
            : _typeSounds[Random.Range(0, _typeSounds.Length)];
        if (clip != null) _audioSource.PlayOneShot(clip, _typeVolume);
    }

    /// <summary>colors32의 alpha만 조정해 _visibleCount까지만 보이게 합니다.</summary>
    private void ApplyReveal()
    {
        ApplyRevealMask(_textComponent.textInfo);
        _textComponent.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
    }

    private void ApplyRevealMask(TMP_TextInfo textInfo)
    {
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int vi = charInfo.vertexIndex;
            Color32[] colors = textInfo.meshInfo[charInfo.materialReferenceIndex].colors32;
            byte a = (byte)(i < _visibleCount ? 255 : 0);

            colors[vi + 0].a = a;
            colors[vi + 1].a = a;
            colors[vi + 2].a = a;
            colors[vi + 3].a = a;
        }
    }

    // ── 마크업 파서 (SetText 전용) ─────────────────────────────────────────────
    //
    // 지원 태그 (파라미터는 생략 시 컴포넌트 기본값 사용):
    //   <shake a=8 s=50> … </shake>     a=강도   s=속도
    //   <wave a=8 s=8 f=1.5> … </wave>  a=진폭   s=속도   f=주파수
    //   <round r=8 s=6> … </round>      r=반경   s=속도
    //   <rainbow s=2> … </rainbow>      s=속도
    //   <speed=6> … </speed>            타자기 출력 속도(초당 글자 수)
    //   <bold> … </bold>                굵게 (TMP <b> 와 동일)
    //   그 외 <color=...> 같은 TMP 태그는 그대로 통과시킵니다.

    private string ParseTags(string input)
    {
        input = input.Replace("\r\n", "\n").Replace('\r', '\n');

        var sb          = new StringBuilder();
        var shakeList   = new List<bool>();
        var waveList    = new List<bool>();
        var rainbowList = new List<bool>();
        var roundList   = new List<bool>();
        var shakeIntL   = new List<float>();
        var shakeSpdL   = new List<float>();
        var waveAmpL    = new List<float>();
        var waveSpdL    = new List<float>();
        var waveFreqL   = new List<float>();
        var rainbowSpdL = new List<float>();
        var roundRadL   = new List<float>();
        var roundSpdL   = new List<float>();
        var typeSpdL    = new List<float>();

        // 현재 효과 상태 (중첩 복원용 스택)
        int shakeDepth = 0, waveDepth = 0, rainbowDepth = 0, roundDepth = 0;
        float shA = _shakeIntensity, shS = _shakeSpeed;
        float wvA = _waveAmplitude,  wvS = _waveSpeed, wvF = _waveFrequency;
        float rbS = _rainbowSpeed;
        float rdR = _roundRadius,    rdS = _roundSpeed;
        float tpS = _typeSpeed;
        var shakeStack   = new Stack<(float, float)>();
        var waveStack    = new Stack<(float, float, float)>();
        var rainbowStack = new Stack<float>();
        var roundStack   = new Stack<(float, float)>();
        var speedStack   = new Stack<float>();

        int i = 0;
        while (i < input.Length)
        {
            if (input[i] == '<')
            {
                int close = input.IndexOf('>', i);
                if (close < 0)
                {
                    // 닫히지 않은 '<' — 일반 문자로 처리
                    AppendChar(sb, '<',
                        shakeList, waveList, rainbowList, roundList,
                        shakeIntL, shakeSpdL, waveAmpL, waveSpdL, waveFreqL, rainbowSpdL, roundRadL, roundSpdL, typeSpdL,
                        shakeDepth, waveDepth, rainbowDepth, roundDepth,
                        shA, shS, wvA, wvS, wvF, rbS, rdR, rdS, tpS);
                    i++;
                    continue;
                }

                string inside  = input.Substring(i + 1, close - i - 1).Trim();
                bool   isClose = inside.StartsWith("/");
                string body    = isClose ? inside.Substring(1).Trim() : inside;
                int    sep     = body.IndexOfAny(new[] { ' ', '=' });
                string name    = (sep < 0 ? body : body.Substring(0, sep)).ToLowerInvariant();

                switch (name)
                {
                    case "shake":
                        if (isClose) { if (shakeDepth > 0) { shakeDepth--; (shA, shS) = shakeStack.Pop(); } }
                        else { shakeStack.Push((shA, shS)); shA = GetArg(body, "a", _shakeIntensity); shS = GetArg(body, "s", _shakeSpeed); shakeDepth++; }
                        break;
                    case "wave":
                        if (isClose) { if (waveDepth > 0) { waveDepth--; (wvA, wvS, wvF) = waveStack.Pop(); } }
                        else { waveStack.Push((wvA, wvS, wvF)); wvA = GetArg(body, "a", _waveAmplitude); wvS = GetArg(body, "s", _waveSpeed); wvF = GetArg(body, "f", _waveFrequency); waveDepth++; }
                        break;
                    case "rainbow":
                        if (isClose) { if (rainbowDepth > 0) { rainbowDepth--; rbS = rainbowStack.Pop(); } }
                        else { rainbowStack.Push(rbS); rbS = GetArg(body, "s", _rainbowSpeed); rainbowDepth++; }
                        break;
                    case "round":
                        if (isClose) { if (roundDepth > 0) { roundDepth--; (rdR, rdS) = roundStack.Pop(); } }
                        else { roundStack.Push((rdR, rdS)); rdR = GetArg(body, "r", _roundRadius); rdS = GetArg(body, "s", _roundSpeed); roundDepth++; }
                        break;
                    case "speed":
                        if (isClose) { if (speedStack.Count > 0) tpS = speedStack.Pop(); }
                        else { speedStack.Push(tpS); tpS = GetArg(body, "speed", _typeSpeed); }
                        break;
                    case "bold":
                        sb.Append(isClose ? "</b>" : "<b>");
                        break;
                    default:
                        // 알 수 없는 태그 = TMP 네이티브 태그로 간주, 그대로 통과
                        sb.Append(input, i, close - i + 1);
                        break;
                }

                i = close + 1;
            }
            else
            {
                AppendChar(sb, input[i],
                    shakeList, waveList, rainbowList, roundList,
                    shakeIntL, shakeSpdL, waveAmpL, waveSpdL, waveFreqL, rainbowSpdL, roundRadL, roundSpdL, typeSpdL,
                    shakeDepth, waveDepth, rainbowDepth, roundDepth,
                    shA, shS, wvA, wvS, wvF, rbS, rdR, rdS, tpS);
                i++;
            }
        }

        _shakeChars         = shakeList.ToArray();
        _waveChars          = waveList.ToArray();
        _rainbowChars       = rainbowList.ToArray();
        _roundChars         = roundList.ToArray();
        _charShakeIntensity = shakeIntL.ToArray();
        _charShakeSpeed     = shakeSpdL.ToArray();
        _charWaveAmplitude  = waveAmpL.ToArray();
        _charWaveSpeed      = waveSpdL.ToArray();
        _charWaveFrequency  = waveFreqL.ToArray();
        _charRainbowSpeed   = rainbowSpdL.ToArray();
        _charRoundRadius    = roundRadL.ToArray();
        _charRoundSpeed     = roundSpdL.ToArray();
        _charTypeSpeed      = typeSpdL.ToArray();
        _hasShakeEffect     = shakeList.Contains(true);
        _hasWaveEffect      = waveList.Contains(true);
        _hasRainbowEffect   = rainbowList.Contains(true);
        _hasRoundEffect     = roundList.Contains(true);

        return sb.ToString();
    }

    private static void AppendChar(
        StringBuilder sb, char c,
        List<bool> shakeList, List<bool> waveList, List<bool> rainbowList, List<bool> roundList,
        List<float> shakeIntL, List<float> shakeSpdL,
        List<float> waveAmpL, List<float> waveSpdL, List<float> waveFreqL,
        List<float> rainbowSpdL, List<float> roundRadL, List<float> roundSpdL, List<float> typeSpdL,
        int shakeDepth, int waveDepth, int rainbowDepth, int roundDepth,
        float shA, float shS, float wvA, float wvS, float wvF, float rbS, float rdR, float rdS, float tpS)
    {
        sb.Append(c);
        shakeList.Add(shakeDepth > 0);   waveList.Add(waveDepth > 0);
        rainbowList.Add(rainbowDepth > 0); roundList.Add(roundDepth > 0);
        shakeIntL.Add(shA);   shakeSpdL.Add(shS);
        waveAmpL.Add(wvA);    waveSpdL.Add(wvS);   waveFreqL.Add(wvF);
        rainbowSpdL.Add(rbS); roundRadL.Add(rdR);  roundSpdL.Add(rdS);   typeSpdL.Add(tpS);
    }

    /// <summary>"a=8 s=50" 형태 본문에서 key 값을 읽습니다. 없으면 fallback.</summary>
    private static float GetArg(string body, string key, float fallback)
    {
        foreach (string tok in body.Split(' '))
        {
            int eq = tok.IndexOf('=');
            if (eq <= 0) continue;
            if (tok.Substring(0, eq).Trim().ToLowerInvariant() != key) continue;
            if (float.TryParse(tok.Substring(eq + 1).Trim(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float v))
                return v;
        }
        return fallback;
    }

    // ── 애니메이션 ────────────────────────────────────────────────────────────

    private void AnimateShake(TMP_TextInfo textInfo)
    {
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            if (i >= _shakeChars.Length || !_shakeChars[i]) continue;
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int vi = charInfo.vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;

            float intensity = i < _charShakeIntensity.Length ? _charShakeIntensity[i] : _shakeIntensity;
            float speed     = i < _charShakeSpeed.Length     ? _charShakeSpeed[i]     : _shakeSpeed;
            float t = Time.time * speed;
            Vector3 offset = new(
                Mathf.Sin(t + i * 1.3f) * intensity,
                Mathf.Cos(t + i * 2.1f) * intensity,
                0f);

            vertices[vi + 0] += offset;
            vertices[vi + 1] += offset;
            vertices[vi + 2] += offset;
            vertices[vi + 3] += offset;
        }
    }

    private void AnimateWave(TMP_TextInfo textInfo)
    {
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            if (i >= _waveChars.Length || !_waveChars[i]) continue;
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int vi = charInfo.vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;

            float amplitude = i < _charWaveAmplitude.Length ? _charWaveAmplitude[i] : _waveAmplitude;
            float speed     = i < _charWaveSpeed.Length     ? _charWaveSpeed[i]     : _waveSpeed;
            float frequency = i < _charWaveFrequency.Length ? _charWaveFrequency[i] : _waveFrequency;
            float offsetY = Mathf.Sin(Time.time * speed + i * frequency) * amplitude;

            Vector3 offset = new(0f, offsetY, 0f);
            vertices[vi + 0] += offset;
            vertices[vi + 1] += offset;
            vertices[vi + 2] += offset;
            vertices[vi + 3] += offset;
        }
    }

    private void AnimateRound(TMP_TextInfo textInfo)
    {
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            if (i >= _roundChars.Length || !_roundChars[i]) continue;
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int vi = charInfo.vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[charInfo.materialReferenceIndex].vertices;

            float radius = i < _charRoundRadius.Length ? _charRoundRadius[i] : _roundRadius;
            float speed  = i < _charRoundSpeed.Length  ? _charRoundSpeed[i]  : _roundSpeed;
            // 글자마다 위상을 달리해 서로 다른 지점을 공전하도록 함
            float angle = Time.time * speed + i * 0.5f;

            // 4개 꼭짓점 모두 같은 오프셋 → 글자는 기울지 않고 위치만 원을 그림(공전)
            Vector3 offset = new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            vertices[vi + 0] += offset;
            vertices[vi + 1] += offset;
            vertices[vi + 2] += offset;
            vertices[vi + 3] += offset;
        }
    }

    private void AnimateRainbow(TMP_TextInfo textInfo)
    {
        for (int i = 0; i < textInfo.characterCount; i++)
        {
            if (i >= _rainbowChars.Length || !_rainbowChars[i]) continue;
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int vi = charInfo.vertexIndex;
            Color32[] colors = textInfo.meshInfo[charInfo.materialReferenceIndex].colors32;

            float speed = i < _charRainbowSpeed.Length ? _charRainbowSpeed[i] : _rainbowSpeed;
            float hue   = Mathf.Repeat(Time.time * speed + i * 0.1f, 1f);
            Color32 color = Color.HSVToRGB(hue, 1f, 1f);

            colors[vi + 0] = color;
            colors[vi + 1] = color;
            colors[vi + 2] = color;
            colors[vi + 3] = color;
        }
    }
}
