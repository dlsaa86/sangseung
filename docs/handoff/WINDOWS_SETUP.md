# Windows PC 인수인계

> 작성: 2026-07-31 / 기준 커밋: `main` @ `5bd1ca1` (PR #3 머지)
> 직전 작업 기기: Apple M5 / Metal / macOS 26.5.2 — **이 문서의 모든 측정치는 그 기기 것이다**

macOS에서 진행하던 Phase 1을 Windows PC로 옮겨 이어가기 위한 문서다.
새 세션은 `CLAUDE.md` → `docs/README.md` 순서를 먼저 읽고 이 문서를 본다.

---

# 1. 현재 상태

`CURRENT_PHASE.md`(Phase 1 전체 + Phase 2의 1층 Hero Slice)의 **Gate A~D 통과 조건을
모두 충족**했고 `main`에 머지되어 있다.

| 항목 | 값 |
|---|---|
| EditMode | 54 PASS / 0 FAIL |
| PlayMode | 48 PASS / 0 FAIL / 콘솔 오류 0 |
| 플레이 가능 범위 | 계약 선택 → 실행 레버 → 3×3 → 정화·패턴·캐스케이드 → 전력 → 확정 또는 과수확 → 결과 |
| 고정 시드 | 1337(기준) · 7(Critical) · 12(깊은 연쇄) · 1(직선 패턴) |

경위와 측정표는 `docs/runtime/ProgressLog.md`, 감사 결과는 `docs/runtime/GapAnalysis.md`.

---

# 2. Windows PC 세팅 — 순서대로

## 2.1 Unity

- **Unity 6000.5.5f1 정확히.** `ProjectSettings/ProjectVersion.txt`에 고정돼 있다.
  다른 버전으로 열면 조용히 업그레이드되고 되돌리기 어렵다.
- 설치 시 **Windows Build Support (IL2CPP)** 모듈을 반드시 포함한다.
  macOS 기기에는 이게 없어서 명세 대상 빌드가 막혀 있었다 — 이번 이동의 핵심 목적이다.

## 2.2 Git

```
git clone https://github.com/dlsaa86/Upandup_DDD.git
```

`.gitattributes`가 `* text=auto` + Unity YAML `eol=lf` + 명시적 binary 목록으로 잡혀 있어
**클론 직후 "전 파일 수정됨"이 뜨지 않는다.** 별도 조치 불필요.

단, **UnityYAMLMerge는 기기마다 수동 등록**해야 한다(경로가 설치마다 다름):

```
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver '"C:/Program Files/Unity/Hub/Editor/6000.5.5f1/Editor/Data/Tools/UnityYAMLMerge.exe" merge -p "$BASE" "$REMOTE" "$LOCAL" "$MERGED"'
```

안 하면 씬 충돌 시 병합 드라이버가 없어 YAML이 깨진다.

## 2.3 에이전트 훅 — 지금은 없다

**2026-08-03에 훅을 전부 걷어냈다(`9723ffb`).** `.claude/settings.json`은 비어 있고
`.claude/hooks/`도 없다. 이 절에서 준비할 것은 없다.

예전에는 `.claude/settings.json`이 `.claude/hooks/*.sh`를 `bash`로 호출했고, 그래서
Windows에서는 **Git Bash가 PATH에** 있어야 했다(`font-atlas-guard.sh`는 추가로 **Node**).
`855c724`에서 훅을 되살린다면 그 두 가지가 다시 필요하고, 없으면 훅이 조용히 동작하지
않는다 — 특히 폰트 아틀라스 보호가 풀린다.

## 2.4 첫 오픈

`Library/`가 gitignore 대상이라 전체 재임포트가 돌아간다. 오래 걸리는 게 정상이다.

---

# 3. Windows에서 **반드시 다시** 해야 하는 것

`Captures/`와 `Logs/`는 gitignore라 넘어오지 않는다. 그리고 `CLAUDE.md`가 못박는다 —
**"베이스라인은 기기에 종속된다. OS·GPU·그래픽 API가 다르면 렌더 결과가 비트 단위로 다르므로,
기기를 옮기면 그 기기에서 베이스라인을 새로 세운다."**

| 메뉴 | 산출물 | 왜 |
|---|---|---|
| `Ascend/Capture Hero Slice Set` | `Captures/HeroSlice/` 11장 + manifest | 캡처 베이스라인을 이 기기 기준으로 새로 수립 |
| `Ascend/Measure Hero Slice Performance` | `Logs/heroslice_perf.txt` | 기준 PC에서 재면 참고치가 아니라 **기준**이 된다 |
| Windows x86-64 빌드 | 실행 파일 | DoD 증거 산출물 |

## 이 셋이 "막혀 있던 것" 3가지를 전부 푼다

1. **Windows 빌드** — macOS엔 모듈이 없었다. (빌드 씬 목록은 이미 교정돼 있다 —
   예전엔 `SampleScene`을 가리켜 프로토타입이 안 들어간 실행 파일이 나왔다.)
2. **성능 완료 선언** — `TECH_SPEC.md` §13이 기준 PC 미지정 시 선언을 금지한다.
   `ASSUMPTION_LOG` A-20260730-01이 가정한 Ryzen 7 5700 / RTX 3070이 이 PC라면
   그 가정을 Confirmed로 바꾸고 성능 완료를 판정할 수 있다.
3. **"프레임당 0 B GC" 판정** — macOS 에디터에서는 **에디터 바닥이 8,773 B/프레임**이라
   게임 코드 몫(80 B)보다 커서 판정 자체가 불가능했다. 빌드 프로파일링으로만 답이 나온다.

### 참고: macOS에서 잰 값 (그대로 믿지 말 것)

| 항목 | 값 |
|---|---|
| 판정 순수 비용 | 스핀당 7.7 µs / 336 B |
| 프레임당 GC — 게임 코드 | 80 B |
| 프레임당 GC — 에디터 바닥 | 8,773 B |
| 프레임 스파이크 | **게임 루프가 원인 아님** (에디터 GC 일시정지로 규명) |

스파이크 규명 과정은 `ProgressLog.md`에 있다. 하네스가 그 구분(게임 코드 / 에디터 바닥 /
GC 일시정지)을 그대로 다시 낸다.

---

# 4. 알려진 위험 — 이 PC에서 실제로 일어난 일

`KNOWN_ISSUES.md` A-4: **Unity 에디터가 D3D12 디바이스 소실로 두 번 크래시했다.**
NVIDIA 드라이버 `32.0.15.9186`. 스크립트나 씬 문제가 아니라 GPU/드라이버 레벨이다.

장시간 자율 작업을 돌릴 거면 **먼저 조치할 것**:

- 드라이버 버전을 다른 버전으로 롤백 또는 업데이트
- 증상이 계속되면 Unity를 `-force-d3d11`로 실행해 D3D12 우회
- 어느 쪽이든, 복구 가능한 단위마다 커밋하는 습관을 유지(방치하면 임의 시점에 끊긴다)

그 외 환경 주의사항은 `CLAUDE.md` "환경 주의사항" 절을 그대로 따른다
(에디터 로그는 `Logs/Editor.log`, `Unity_RunCommand`는 Reflection 차단, 포커스 상실 시 Play 정지 등).

---

# 5. 저장소 지도 — 이번 세션에 새로 생긴 것

## 검증·측정 하네스 (Windows에서 바로 쓸 것)

| 메뉴 | 내용 |
|---|---|
| `Ascend/Run All EditMode Tests` (`Ctrl+Shift+T`) | Spin 27 + Run 16 + Risk 11 = 54 |
| `Ascend/Run PlayMode Hero Slice Check` | 물체만 써서 1층 완주. 결과는 `Logs/heroslice_playmode.txt` |
| `Ascend/Capture Hero Slice Set` | 고정 시점 11장. 1920×1080 / FOV 60 / 전용 카메라 RT |
| `Ascend/Measure Hero Slice Performance` | 프레임타임·GC·스파이크 정황·대조군 |
| `Ascend/Build Hero Slice Scene Objects` | 씬 오브젝트 재생성(멱등) |

**PlayMode·캡처·성능은 Play 모드 진입을 동반하고 끝나면 자동 종료한다.** 도메인 리로드로
수십 초 걸리므로 폴링하지 말고 결과 파일이 생기는지만 기다린다.

## 신규 런타임 컴포넌트

```
Spin/SpinSeed.cs                   시드 파생 단일 출처 (런시드, 층, 스핀인덱스)
Risk/{RiskLevel,RiskEvaluator,RiskProfile,RiskStateView}.cs   위험 상태 (히스테리시스)
Run/{FloorRecord,AccidentRecorder}.cs                         사고 기록기
Run/ISpinPresentation.cs           연출자 인터페이스 (입력 잠금 창구)
View/SpinPresenter.cs              캐스케이드를 시간축에 재생
View/PurifyMarkerView.cs           정화 원인 형태 표식 (직선 막대 / 연결봉)
View/InstrumentPanelView.cs        벽면 계기판 구동
View/OverharvestUnlockEffect.cs    과수확 해제 순간 연출 (조명·소리·기계·진동)
UI/GameHudView.cs                  화면 UI (안내 한 줄 / 연쇄 배너 / 층 결과)
UI/DebugPanelView.cs               디버그 패널 (기본 꺼짐, F1)
Player/InteractableOverharvestLever.cs                        과수확 레버
```

## 조작

게임 조작은 **전부 엘리베이터 안의 물체로만** 한다(Gate B). 키보드는 조사 도구뿐이다.

```
[F1] 디버그 패널   [R] 같은 시드 재시작   [T] 시드 입력   [L] 마지막 스핀 로그
```

---

# 6. 하지 말 것

- **`Ascend/Build Hero Slice Scene Objects`를 습관적으로 돌리지 말 것.**
  씬은 이미 완성 상태로 커밋돼 있다. 돌리면 재생성 후 저장돼 불필요한 diff가 생긴다.
  씬 오브젝트를 실제로 고칠 때만 쓴다.
- **폰트 에셋(`NanumGothic SDF.asset`)을 무심코 커밋하지 말 것.**
  플레이 중 동적 아틀라스가 자라면 4 MB짜리 리비전이 생긴다.
  예전에는 `font-atlas-guard.sh` 훅이 글리프가 **줄어드는** 경우를 막아 줬지만
  (한글 렌더링 깨짐 방지) **2026-08-03에 지웠다 — 지금은 아무도 안 막는다.**
  커밋 전에 이 파일이 diff 에 들어 있는지 직접 본다.
- **패키지·Unity·URP 버전을 승인 없이 바꾸지 말 것** (`TECH_SPEC.md` §1).
- **폐기 코드를 정리한다며 손대지 말 것** — `Core/` `Roulette/` `Effects/` `Data/Ball*` `Sim/`
  약 5,000줄은 컴파일에만 참여한다. 제거는 Gate에 기여하지 않고 위험만 크다.

---

# 7. 승인 대기 항목

| 항목 | 현재 | 교체 위치 |
|---|---|---|
| **패키지 버전** | `com.unity.ai.assistant` 2.16→2.17 (에디터가 자체 변경) | `Packages/manifest.json`, `packages-lock.json` revert |
| 공포 표현 강도 | `RiskIntensity.Standard` | `RiskStateView._intensity` (3종 프리셋) |
| 연출 템포 | `SpinPresenter.Tempo.Standard` | 인스펙터 (3종 프리셋) |
| 밸런스 | 요구 전력 460 (400시드 측정) | `PrototypeCurriculum.HeroSlice.RequiredPower` |
| 기준 하드웨어 | 미확정 | `ASSUMPTION_LOG` A-20260730-01 |

상세는 `docs/ASSUMPTION_LOG.md` A-20260730-05 ~ 10.

---

# 8. 다음에 할 수 있는 것

**Gate B가 통과했으므로 `CURRENT_PHASE.md` §6 중단 규칙상 10층 확장 착수가 가능하다.**

권장 순서:

1. **위 §3의 재측정 3종 먼저.** 막힌 항목 3개를 닫고 Phase 1을 완전히 마감한다.
2. 그 다음 Phase 2 — 10층 흐름. `PrototypeCurriculum.TenFloors`가 이미 있고
   `IFloorPlanSource`로 갈라져 있어 `RunSessionBehaviour._mode`를 `TenFloor`로 바꾸면 바로 돈다.
   다만 층별 통과율은 아직 측정하지 않았다.
3. 밸런스 미해결 항목: **계약 없음이 열등 선택지**
   (5스핀 중앙 무계약 767 vs 흡수체 1716 / 증식체 1591).
   `MASTER_PRD.md` 검증 가설 4번이 현 구성에서는 성립하지 않는다. 잔류 대가가
   출현률·보상 증가를 상쇄하지 못한다.

---

# 9. 새 세션 첫 5분 체크리스트

```
□ git pull (main @ 5bd1ca1 이후)
□ Unity 6000.5.5f1 로 열림 확인 (업그레이드 프롬프트가 뜨면 중단)
□ 콘솔 컴파일 오류 0 확인
□ Ascend/Run All EditMode Tests → 54 PASS / 0 FAIL
□ Ascend/Run PlayMode Hero Slice Check → 48 PASS / 0 FAIL
□ 위 둘이 그린이면 §3 재측정으로 진행
```

둘 중 하나라도 빨간불이면 **먼저 그것부터** 해결한다. 빌드나 핵심 테스트가 깨진 상태에서
새 기능을 추가하지 않는다(`CLAUDE.md`).
