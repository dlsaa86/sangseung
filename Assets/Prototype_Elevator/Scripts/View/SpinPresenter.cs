using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Ascend.Prototype.Run;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.View
{
    /// <summary>
    /// 확정된 <see cref="SpinResolution"/>을 시간축에 펼친다.
    ///
    /// 이 클래스가 없으면 스핀이 한 프레임에 끝나고 플레이어는 최종 보드만 본다.
    /// `MASTER_PRD.md` §6은 "제거 → 빈칸 → 신규 심볼 유입 → 재판정 단계를 **시각적으로
    /// 생략하지 않는다**"고 못박는다. 이 게임의 핵심 체감이 그 사이에 있기 때문이다.
    ///
    /// **불변식: 여기서 판정을 다시 하지 않는다.** 입력은 읽기 전용이고 출력은 화면뿐이다
    /// (`TECH_SPEC.md` §5). 이 컴포넌트를 통째로 꺼도 게임과 테스트는 그대로 돌아야 한다.
    ///
    /// 연출 타이밍은 전부 인스펙터 값이다 — 최종 연출은 승인 대기 항목이라 코드에 잠그지 않는다.
    ///
    /// **정지 화면에서도 순차 공개가 읽혀야 한다(`UP-FIX-20`).** 판정은 캡처 한 장으로 하는데,
    /// 예전에는 아직 안 열린 칸이 `SymbolKind.Empty`로 그려져 **정화로 비워진 칸과 픽셀이
    /// 같았다.** 그래서 「공개 중」과 「빈 판」이 구분되지 않았고, `UP-CORE-11`은 어떤 캡처로도
    /// 증명될 수 없었다. 지금은 <see cref="PurifyMarkerView"/>가 칸마다 셔터 막대를 세운다 —
    /// 색이 아니라 **막대 개수·위치·두께**로 갈리므로 회색조에서도 살아남는다.
    /// </summary>
    public sealed class SpinPresenter : MonoBehaviour, ISpinPresentation
    {
        /// <summary>연출 속도 프리셋. 최종안 하나로 잠그지 않는다(`MASTER_PRD.md` §1).</summary>
        public enum Tempo
        {
            /// <summary>검증·캡처용. 각 단계가 확실히 읽힌다.</summary>
            Readable = 0,

            /// <summary>플레이 기본값.</summary>
            Standard = 1,

            /// <summary>연쇄가 길어질 때의 압축. 10연쇄까지 판독성 유지가 기준(TECH_SPEC §9).</summary>
            Brisk = 2,
        }

        /// <summary>
        /// 순차 공개에서 한 칸(정확히는 그 칸이 속한 통관)이 놓인 단계.
        ///
        /// **셋이 한 프레임에서 갈려야 한다**(`UP-FIX-20`). 갈리는 축은 색이 아니라 형태다 —
        /// 그룹 B가 남긴 교훈이 「색상축만 움직이고 명도축이 그대로면 회색조에서 같은 밴드」였다.
        /// </summary>
        public enum RevealStage
        {
            /// <summary>아직 안 열린 칸. 셔터가 칸 한가운데를 가로지른다 — 「빈 판」과 여기서 갈린다.</summary>
            Sealed = 0,

            /// <summary>지금 열리는 칸. 셔터가 갈라져 위아래로 물러나 있다(막대 2개, 밝다).</summary>
            Opening = 1,

            /// <summary>다 열린 칸. 표식이 없고 심볼만 남는다.</summary>
            Open = 2,
        }

        /// <summary>
        /// 「공개가 끝났다」를 뜻하는 <see cref="RevealedColumns"/> 값.
        ///
        /// 3(마지막 열이 방금 내려앉음)과 구별해야 한다 — 3은 세 번째 통관이 아직
        /// <see cref="RevealStage.Opening"/>인 상태이고, 여기서는 표식이 전부 사라진다.
        /// </summary>
        public const int RevealComplete = SpinBoard.Columns + 1;

        [SerializeField] private SpinBoardView _board;
        [SerializeField] private PurifyMarkerView _markers;
        [SerializeField] private Tempo _tempo = Tempo.Standard;

        [Header("템포 (초)")]
        [Tooltip("아무 열도 열리기 전 — 셔터가 전부 닫힌 판을 보여주는 시간. " +
                 "0으로 두면 '닫힌 판'이라는 시작 상태가 한 프레임도 안 남아 " +
                 "순차 공개가 1/3에서 시작하는 것처럼 보인다.")]
        [SerializeField] private float _sealedHold = 0.22f;
        [Tooltip("통관 한 열이 결과를 드러내는 간격. 세 열이 순차로 선다.")]
        [SerializeField] private float _columnRevealInterval = 0.32f;
        [Tooltip("판을 읽을 시간. 정화가 시작되기 전 정지.")]
        [SerializeField] private float _readPause = 0.45f;
        [Tooltip("정화 칸이 맥동하는 시간.")]
        [SerializeField] private float _purifyPulse = 0.55f;
        [Tooltip("제거된 판(빈칸)을 보여주는 시간. 이걸 0으로 두면 '무엇이 사라졌는지'가 사라진다.")]
        [SerializeField] private float _emptyHold = 0.30f;
        [Tooltip("재충전된 판을 보여주는 시간. 다음 판정 전 정지.")]
        [SerializeField] private float _refillHold = 0.40f;

        [Header("연쇄 압축")]
        [Tooltip("연쇄가 길어질수록 이 비율로 빨라진다. 1이면 압축 없음.")]
        [SerializeField, Range(0.3f, 1f)] private float _chainSpeedup = 0.86f;
        [Tooltip("아무리 빨라져도 이 배율 아래로는 내려가지 않는다 — 20연쇄가 순식간에 지나가면 안 된다.")]
        [SerializeField, Range(0.05f, 1f)] private float _minTempoScale = 0.35f;

        private Coroutine _playing;
        private SpinResolution _pending;

        public bool IsPresenting => _playing != null;

        /// <summary>지금 재생 중인 연쇄 단계(1부터). 0이면 재생 중이 아니다. HUD가 표시한다.</summary>
        public int CurrentDepth { get; private set; }

        /// <summary>재생 중인 결과. HUD가 "지금 무엇 때문에 터졌는가"를 읽는다.</summary>
        public SpinResolution Current => _pending;

        /// <summary>이번 단계에 발동한 정화 설명. 한 화면에 모든 숫자를 띄우지 않기 위한 한 줄.</summary>
        public string CurrentCause { get; private set; } = string.Empty;

        /// <summary>
        /// 지금까지 내려앉은 통관 수(0~3), 공개가 끝나면 <see cref="RevealComplete"/>.
        ///
        /// 표식 뷰와 캡처 하네스가 「지금 몇 열까지 열렸는가」를 물어보는 유일한 창구다.
        /// 상태를 두 군데서 세면 표식과 판이 어긋난다.
        /// </summary>
        public int RevealedColumns { get; private set; } = RevealComplete;

        /// <summary>
        /// **결과 숫자를 화면에 내보내도 되는가.** 연출이 도는 동안 거짓이다.
        ///
        /// 왜 <see cref="IsPresenting"/>과 따로 두는가: `IsPresenting`은 「입력을 잠글까」를
        /// 묻는 질문이고(`RouletteInteractionBridge.IsLocked`), 이쪽은 「전력·스핀·잔류를
        /// 그려도 되는가」를 묻는 질문이다. 둘은 지금 같은 값이지만 **바뀌는 이유가 다르다** —
        /// 예컨대 연출 마지막 여운 동안 입력만 먼저 풀어 주는 변경이 오면 여기만 늦춰야 한다.
        ///
        /// `UP-FIX-15`: `RunSession.Spin()`은 레버를 당긴 **그 프레임에** 전력·스핀·잔류를
        /// 전부 확정하는데, 연출은 그때부터 1~3초를 더 돈다. 매 프레임 런 상태를 그대로
        /// 읽는 표시물은 결과를 30프레임 먼저 뱉는다. `GameHudView.UpdateAux`는 이미 이
        /// 구간에 값을 얼리고 있고, **`InstrumentPanelView`는 아직 얼리지 않는다**(씬 오너 요청).
        /// </summary>
        public bool ResultRevealed => _playing == null;

        private void Awake()
        {
            if (_board == null) _board = FindAnyObjectByType<SpinBoardView>();
            if (_markers == null) _markers = FindAnyObjectByType<PurifyMarkerView>();
            ApplyTempoPreset();
        }

        private void OnValidate() => ApplyTempoPreset();

        private void ApplyTempoPreset()
        {
            switch (_tempo)
            {
                case Tempo.Readable:
                    _sealedHold = 0.32f;
                    _columnRevealInterval = 0.45f; _readPause = 0.70f; _purifyPulse = 0.80f;
                    _emptyHold = 0.45f; _refillHold = 0.60f;
                    break;
                case Tempo.Brisk:
                    _sealedHold = 0.12f;
                    _columnRevealInterval = 0.18f; _readPause = 0.25f; _purifyPulse = 0.32f;
                    _emptyHold = 0.16f; _refillHold = 0.22f;
                    break;
                default:
                    _sealedHold = 0.22f;
                    _columnRevealInterval = 0.32f; _readPause = 0.45f; _purifyPulse = 0.55f;
                    _emptyHold = 0.30f; _refillHold = 0.40f;
                    break;
            }
        }

        public void Present(SpinResolution resolution)
        {
            if (resolution.Steps == null) return;
            if (_playing != null) StopCoroutine(_playing);
            _pending = resolution;
            _playing = StartCoroutine(Play(resolution));
        }

        public void Clear()
        {
            if (_playing != null) { StopCoroutine(_playing); _playing = null; }
            CurrentDepth = 0;
            CurrentCause = string.Empty;
            RevealedColumns = RevealComplete;
            _markers?.Clear();
            if (_board != null)
            {
                _board.DrivenExternally = false;
                _board.ClearAll();
            }
        }

        /// <summary>
        /// 판독 순서(`MASTER_PRD.md` §6.1)를 그대로 따른다:
        /// 심볼 구분 → 개수 → 기본 정화 → 패턴 → 제거 대상 → 캐스케이드 신규 심볼.
        /// </summary>
        private IEnumerator Play(SpinResolution resolution)
        {
            if (_board == null) { _playing = null; RevealedColumns = RevealComplete; yield break; }
            _board.DrivenExternally = true;

            // 1. 세 통관이 순차로 선다. 9칸이 동시에 뜨면 "세 통관 × 3칸" 구조가 안 읽힌다
            //    (visual-criteria B-2.4).
            yield return RevealColumns(resolution.InitialBoard);
            yield return Wait(_readPause, 1);

            // 2. 단계별 재생.
            for (int i = 0; i < resolution.Steps.Length; i++)
            {
                CascadeStep step = resolution.Steps[i];
                CurrentDepth = step.Depth;

                _board.ShowBoard(step.BoardBefore);

                if (step.Purifies != null && step.Purifies.Length > 0)
                {
                    CurrentCause = DescribeStep(step);
                    yield return PulseCells(step, step.Depth);
                }
                else
                {
                    CurrentCause = step.NormalSoulsHarvested > 0
                        ? $"정상 영혼 {step.NormalSoulsHarvested}개 수확"
                        : "정화 없음";
                    yield return Wait(_readPause, step.Depth);
                }

                // 3. 제거된 판을 실제로 보여준다. 빈칸을 건너뛰면 무엇이 사라졌는지 알 수 없다.
                _board.ClearHighlights();
                _board.ShowBoard(WithPurifiedCleared(step));
                yield return Wait(_emptyHold, step.Depth);

                // 4. 재충전 결과. 새로 들어온 심볼이 다음 단계의 재료다.
                _board.ShowBoard(step.BoardAfter);
                bool hasNext = i + 1 < resolution.Steps.Length;
                yield return Wait(hasNext ? _refillHold : _readPause, step.Depth);
            }

            if (resolution.CascadeCapReached)
            {
                CurrentCause = $"연쇄 상한 {resolution.ChainDepth}회 도달 — 보호 종료";
                yield return Wait(_readPause, 1);
            }

            _board.ShowBoard(resolution.FinalBoard);
            _board.ClearHighlights();
            _markers?.Clear();
            _board.DrivenExternally = false;
            CurrentDepth = 0;
            CurrentCause = string.Empty;
            _playing = null;
        }

        /// <summary>
        /// 세 통관이 순차로 선다. **각 상태가 정지 화면에서 갈려야 한다**(`UP-FIX-20`).
        ///
        /// 판만 밀어 넣던 예전 구현에는 상태가 둘뿐이었다 — 심볼이 있거나(열림) 없거나(빈칸).
        /// 그런데 「빈칸」은 정화로 비워진 칸도 쓰는 표현이라, 캡처 한 장에서 「아직 안 열림」과
        /// 「방금 정화됨」이 **같은 픽셀**이었다. 셔터 표식이 그 둘을 가른다.
        ///
        /// 닫힌 판을 <see cref="_sealedHold"/>만큼 먼저 보여주는 이유: 예전에는 빈 판을
        /// 세우자마자 같은 프레임에 첫 열을 채워, **어떤 프레임에도 「아무 열도 안 열린 판」이
        /// 존재하지 않았다.** 순차 공개가 1/3에서 시작하면 그건 순차가 아니라 도약이다.
        /// </summary>
        private IEnumerator RevealColumns(SpinBoard board)
        {
            var partial = default(SpinBoard);
            _board.ShowBoard(partial);
            SetRevealed(0);
            yield return Wait(_sealedHold, 1);

            for (int column = 0; column < SpinBoard.Columns; column++)
            {
                for (int row = 0; row < SpinBoard.Rows; row++)
                    partial[column, row] = board[column, row];
                _board.ShowBoard(partial);
                SetRevealed(column + 1);
                yield return Wait(_columnRevealInterval, 1);
            }

            SetRevealed(RevealComplete);
        }

        /// <summary>공개 진행도를 한 곳에서만 바꾼다 — 표식과 판이 어긋나지 않게.</summary>
        private void SetRevealed(int revealedColumns)
        {
            RevealedColumns = revealedColumns;
            _markers?.ShowReveal(revealedColumns);
        }

        /// <summary>
        /// 통관 하나가 놓인 단계. **순수 함수다** — 씬도 코루틴도 필요 없어 헤드리스로 검사된다
        /// (`Scripts/View/Tests/OverharvestStageTests.cs`). 이 규칙의 구현은 여기 하나뿐이고
        /// <see cref="PurifyMarkerView"/>도 이것을 부른다.
        /// </summary>
        public static RevealStage StageOfColumn(int column, int revealedColumns)
        {
            if (column < 0 || column >= SpinBoard.Columns) return RevealStage.Open;

            int revealed = Mathf.Clamp(revealedColumns, 0, RevealComplete);
            if (revealed >= RevealComplete) return RevealStage.Open;
            if (column < revealed - 1) return RevealStage.Open;
            return column == revealed - 1 ? RevealStage.Opening : RevealStage.Sealed;
        }

        /// <summary>칸 인덱스로 묻는 같은 질문. 좌표 변환은 <see cref="SpinBoard"/>가 소유한다.</summary>
        public static RevealStage StageOfCell(int cellIndex, int revealedColumns)
        {
            if (cellIndex < 0 || cellIndex >= SpinBoard.Cells) return RevealStage.Open;
            return StageOfColumn(SpinBoard.ColumnOf(cellIndex), revealedColumns);
        }

        /// <summary>
        /// 정화 칸을 맥동시킨다. 패턴 등급에 따라 맥동 횟수를 다르게 해 "왜 터졌는가"가
        /// 같은 이펙트로 뭉치지 않게 한다(visual-criteria B-2.6):
        ///   개수 정화 1회 · 직선 2회 · 연결 3회 · 잭팟 4회.
        /// </summary>
        private IEnumerator PulseCells(CascadeStep step, int depth)
        {
            int pulses = 1;
            foreach (PurifyEvent purify in step.Purifies)
                pulses = Mathf.Max(pulses, PulseCountFor(purify.Pattern));

            float duration = _purifyPulse * TempoScale(depth);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float phase = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, duration));
                float wave = Mathf.Abs(Mathf.Sin(phase * Mathf.PI * pulses));

                // 맥동은 "어느 칸이" 터졌는지, 표식은 "왜" 터졌는지를 말한다.
                // 맥동만 있으면 정지 화면에서 개수·직선·연결이 전부 같아 보인다(B-2.6).
                _markers?.Begin();
                foreach (PurifyEvent purify in step.Purifies)
                {
                    if (purify.Cells != null)
                        foreach (int cell in purify.Cells) _board.SetHighlight(cell, wave);
                    _markers?.Add(in purify, wave);
                }
                _markers?.End();
                yield return null;
            }

            _markers?.Clear();
        }

        private static int PulseCountFor(PatternKind pattern)
        {
            switch (pattern)
            {
                case PatternKind.Line:      return 2;
                case PatternKind.Cluster:   return 3;
                case PatternKind.FullBoard: return 4;
                default:                    return 1;
            }
        }

        /// <summary>정화된 칸만 비운 판. 엔진의 BoardAfter는 이미 재충전까지 끝난 상태라
        /// 중간 단계를 여기서 다시 만든다 — 판정이 아니라 표시용 복원이다.</summary>
        private static SpinBoard WithPurifiedCleared(CascadeStep step)
        {
            SpinBoard board = step.BoardBefore;
            for (int i = 0; i < SpinBoard.Cells; i++)
                if (board[i] == SymbolKind.NormalSoul) board[i] = SymbolKind.Empty;

            if (step.Purifies != null)
                foreach (PurifyEvent purify in step.Purifies)
                {
                    if (purify.Cells == null) continue;
                    foreach (int cell in purify.Cells) board[cell] = SymbolKind.Empty;
                }
            return board;
        }

        private static string DescribeStep(CascadeStep step)
        {
            var parts = new List<string>(step.Purifies.Length);
            foreach (PurifyEvent purify in step.Purifies)
                parts.Add($"{purify.Kind.DisplayName()} {purify.Pattern.DisplayName()} " +
                          $"{purify.Cells?.Length ?? 0}칸 ×{purify.PatternMultiplier:0.##}");
            return string.Join("  ·  ", parts);
        }

        private IEnumerator Wait(float seconds, int depth)
        {
            float scaled = seconds * TempoScale(depth);
            if (scaled <= 0f) yield break;
            yield return new WaitForSeconds(scaled);
        }

        /// <summary>연쇄가 길수록 빨라지되 바닥이 있다. 20연쇄가 순식간에 지나가면
        /// "무한 루프 방지가 걸렸다"는 사실 자체를 못 본다.</summary>
        private float TempoScale(int depth)
            => Mathf.Max(_minTempoScale, Mathf.Pow(_chainSpeedup, Mathf.Max(0, depth - 1)));
    }
}
