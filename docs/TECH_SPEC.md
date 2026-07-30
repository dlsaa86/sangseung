# Unity 프로토타입 구조·테스트 명세

> 상태: AI 구현 기술 기준 스냅샷  
> 원본: https://app.notion.com/p/3ada30cad9c58160a5f8ce347273d843  
> 동결일: 2026-07-30

이 문서는 클래스, 데이터, 상태, 테스트, 성능 구조를 정의한다. 제품 범위와 완료 판단은 `MASTER_PRD.md`가 우선한다.

---

# 1. 고정 개발 환경

- Unity: **6000.5.5f1**
- URP: **17.5.0**
- 대상 플랫폼: Windows x86-64
- 기준 해상도: 1920×1080
- 입력: Unity Input System
- 게임 코드 네임스페이스: `Ascend.Prototype`
- 캡처 하네스 네임스페이스: `Ascend.CaptureHarness`
- 현재 작업 씬: `Assets/Prototype_Elevator/Scenes/Prototype_Elevator.unity`
- 주요 데이터: `Assets/Prototype_Elevator/Data/PrototypeConfig.asset`
- 캡처 하네스: `Assets/CaptureHarness/`

Unity, URP, Input System 및 패키지 버전을 에이전트가 임의로 변경하지 않는다. `Packages/packages-lock.json`은 항상 재현 가능한 상태로 커밋한다.

# 2. 기술 원칙

- 게임 로직과 연출 로직을 분리한다.
- 핵심 규칙을 `MonoBehaviour`에 직접 하드코딩하지 않는다.
- 심볼, 계약, 승객, 부품, 층 수치, 위험 프리셋은 `ScriptableObject` 또는 직렬화 가능한 데이터 객체로 관리한다.
- 난수는 `UnityEngine.Random` 전역 상태에 의존하지 않는다.
- 모든 스핀은 명시적인 시드 기반 RNG 서비스로 재현 가능해야 한다.
- Scene 오브젝트를 이름 검색으로 찾지 않는다.
- 필수 의존성은 Inspector 참조 또는 명시적 초기화로 주입한다.
- null 상태에서 조용히 실패하지 않는다. 개발 빌드와 에디터에서는 원인과 경로를 명확히 출력한다.
- 화면 연출을 제거해도 룰렛 판정 테스트가 가능해야 한다.
- 기존에 정상 동작하는 시스템은 근거 없이 재작성하지 않는다.

# 3. 현재 저장소 구조

```text
Assets/Prototype_Elevator/
  Scenes/Prototype_Elevator.unity
  Scripts/
    Core/
    Data/
    Effects/
    Roulette/
    UI/
  Data/PrototypeConfig.asset
Assets/CaptureHarness/
Assets/Plans/
.claude/visual-criteria.md
Captures/
```

현재 asmdef이 없으므로 모든 게임 스크립트는 `Assembly-CSharp`에 들어간다. 작은 스크립트 수정도 전체 재컴파일과 Play Mode 종료를 유발할 수 있다. asmdef 도입은 별도 결정 없이 진행하지 않는다.

# 4. 권장 런타임 구조

현재 코드와 충돌하지 않는 범위에서 다음 책임을 분리한다.

```text
RuntimeSystems
├── RunController
├── FloorController
├── SpinController
├── RouletteResolver
├── CascadeResolver
├── PowerController
├── ResidualController
├── RiskStateController
├── PassengerController
├── PartController
├── AccidentRecorder
└── TelemetryLogger
```

## 책임

- `RunController`: 1회 런의 시작, 층 진행, 종료
- `FloorController`: 요구 전력, 남은 스핀, 계약, 층 결과
- `SpinController`: 입력 잠금, 추첨 요청, 연출 순서, 결과 확정
- `RouletteResolver`: 초기 보드, 정화, 직선, 연결 판정
- `CascadeResolver`: 제거, 재충전, 재판정, 하드 캡
- `PowerController`: 기본 전력, 보너스, 임계점, 확정
- `ResidualController`: 미정화 흡수체·증식체 잔류 효과
- `RiskStateController`: Stable/Warning/Critical/Collapse 상태 산출
- `PassengerController`·`PartController`: 빌드 효과와 무게
- `AccidentRecorder`: 실패와 런 결과의 설명 가능한 기록
- `TelemetryLogger`: 재현 가능한 이벤트 로그와 플레이테스트 지표

한 클래스가 입력, 룰렛 판정, 점수, UI, 연출을 동시에 처리하지 않는다.

# 5. 상태 모델

## 5.1 RunState

한 번의 플레이 전체 상태를 보유한다.

- `CurrentFloor`
- `Money`
- `StoredPower`
- 현재 승객 목록
- 현재 부품 목록
- `TotalWeight`
- `WeightCapacity`
- `RunSeed`
- 런 종료 여부와 종료 사유

## 5.2 FloorState

현재 층에 한정된 상태다.

- 층 번호
- 요구 전력
- 현재 층 전력
- 남은 스핀
- 선택된 계약
- 현재 흡수체 잔류량
- 현재 증식체 가중치 보정
- 현재 캐스케이드 배수
- 전력 확정 가능 여부
- 과수확 횟수
- 현재 위험 상태
- 층 종료 결과

## 5.3 SpinResult

한 번의 스핀 결과는 반드시 불변에 가까운 데이터 객체로 보존한다.

- 스핀 인덱스
- 사용 시드
- 초기 9칸 결과
- 각 캐스케이드 단계의 보드
- 정상 영혼 기본 전력
- 저항체별 개수
- 발생한 정화 목록
- 발생한 패턴 목록
- 각 단계별 획득 전력
- 승객·부품 발동 목록
- 잔류 효과
- 위험 상태 변화
- 최종 획득 전력
- 최종 보드

UI와 연출은 `SpinResult`를 소비하며, 판정 중 임의로 결과를 수정하지 않는다.

# 6. 데이터 정의

## 6.1 SymbolDefinition

필수 속성:

- `Id`
- 표시 이름
- 심볼 유형: `NormalSoul` 또는 `Resistance`
- 저항체 하위 유형
- 기본 가중치
- 기본 전력값
- 정화 시 기본 전력값
- 표시 프리팹
- 아이콘
- 결과 공개 사운드
- 색상·발광 식별값

초기 심볼:

- `normal_soul`: 즉시 기본 전력, 정화 대상 아님, 잔류 없음
- `absorber`: 미정화 시 현재 층 전력 감소
- `replicator`: 미정화 시 다음 스핀 등장 가중치 증가

정확한 밸런스 값은 `PrototypeConfig.asset` 또는 별도 프로파일에 둔다. 코드 상수로 분산하지 않는다.

## 6.2 ContractDefinition

- `Id`
- 대상 저항체
- 등장 가중치 보정
- 정화 보상 보정
- 잔류 대가 보정
- 계약 표시 데이터
- 설명 텍스트

계약은 보상만 높이지 않는다. 위험과 보상을 동시에 변경해야 한다.

## 6.3 PassengerDefinition / PartDefinition

- `Id`
- 표시 데이터
- 무게
- 적용 시점
- 효과 규칙
- 발동 조건
- 발동 연출 키
- 충돌 태그 또는 배타 조건

초기 4종은 실제 룰렛 판단 또는 운영 판단을 바꾸는 효과를 가져야 한다.

## 6.4 RiskProfile

- 위험 단계
- 진입·이탈 임계값
- 조명 프리셋
- 오디오 믹스 프리셋
- 진동 프리셋
- 파티클 강도
- UI 경고 수준
- 승객 반응 키

# 7. 결정론적 RNG

- 런 시작 시 `RunSeed`를 생성하거나 입력받는다.
- 각 층과 스핀은 `RunSeed`, 층 번호, 스핀 인덱스에서 파생한 시드를 사용한다.
- 동일 버전, 동일 데이터, 동일 시드는 동일한 초기 보드와 캐스케이드 결과를 생성해야 한다.
- 시드 파생 규칙은 한 곳에 정의한다.
- 로그에는 실제 사용 시드를 저장한다.
- 연출용 난수는 판정용 RNG와 분리한다.

# 8. 룰렛 판정 파이프라인

1. 계약, 승객, 부품, 잔류 효과를 반영한 가중치 테이블 생성
2. 명시적 시드 RNG로 3×3 초기 보드 생성
3. 정상 영혼 기본 전력 계산
4. 저항체별 총개수 계산
5. 같은 저항체 3개 이상 기본 정화
6. 직선 3개 판정
7. 직교 연결 4개 이상 판정
8. 적용 가능한 가장 높은 보너스 규칙 결정
9. 제거 대상 확정
10. 제거된 칸을 명시적 순서로 재충전
11. 캐스케이드 재판정
12. 승객·부품·전력 임계점 발동
13. 미정화 저항 잔류 효과 적용
14. 최종 `SpinResult` 생성

판정 로직은 애니메이션 시간, 코루틴, 프레임레이트에 의존하지 않는다.

# 9. 캐스케이드

- 제거 대상, 빈칸, 신규 심볼 유입을 단계별 데이터로 보존한다.
- 신규 심볼 유입 순서는 고정하고 테스트한다.
- 하드 캡은 20회다.
- 하드 캡 도달은 정상적인 보호 종료로 처리한다.
- 캡 도달 시 시드, 초기 보드, 마지막 보드, 단계별 발동을 로그에 남긴다.
- 10회 연쇄까지 시각 판독성을 유지해야 한다.

# 10. 입력과 상태 전이

권장 스핀 상태:

```text
Idle
→ ContractRequired
→ Ready
→ Spinning
→ Revealing
→ Resolving
→ Cascading
→ ApplyingEffects
→ AwaitingDecision
→ FloorComplete 또는 Ready
```

- 스핀 중 중복 입력을 차단한다.
- 계약 미선택 시 실행 레버를 비활성화한다.
- 결과 공개와 판정이 끝난 뒤에만 입력을 복구한다.
- 요구 전력 달성 시 일반 스핀 흐름이 아니라 확정 또는 과수확 선택으로 전이한다.
- 상태 전이는 로그로 추적 가능해야 한다.

# 11. EditMode 테스트

반드시 자동화한다.

- 동일 시드에서 동일 결과 생성
- 다른 시드에서 결과가 고정되지 않음
- 가중치 합산과 선택 경계 정확성
- 같은 저항체 3개 정화
- 같은 저항체 2개 이하 미정화
- 가로, 세로, 대각선 직선 판정
- 직교 연결 4개 이상 판정
- 대각선 연결이 직교 연결로 잘못 판정되지 않음
- 제거 대상 정확성
- 캐스케이드 재충전 순서
- 캐스케이드 하드 캡
- 흡수체 잔류 전력 감소
- 증식체 잔류 가중치 증가
- 계약 보정 적용
- 승객·부품 효과의 적용 시점
- 요구 전력 달성 판정
- 과적과 위험 상태 계산
- `SpinResult` 직렬화 또는 로그 재현

# 12. PlayMode 테스트

- 레버 입력 후 스핀 완료
- 스핀 중 중복 입력 차단
- 계약 미선택 시 레버 비활성
- 결과 공개 후 입력 복구
- 요구 전력 달성 후 확정 UI 활성
- 과수확 레버 잠금·해제
- 층 종료 후 다음 층 전환
- 1층부터 10층까지 진행 불가 상태 없음
- 위험 상태와 조명·오디오·UI 동기화
- 사고 기록기 출력

시각 연출의 품질 자체는 PlayMode 테스트만으로 통과시키지 않는다. 캡처 루브릭을 별도로 사용한다.

# 13. 성능 기준

- 기준 해상도: 1920×1080
- 일반 플레이 목표: 90 FPS
- 최악 장면 허용 하한: 지속 60 FPS 이상
- 워밍업 이후 일반 렌더·게임플레이 루프: 프레임당 0 B GC Alloc 목표
- 스핀 시작과 층 전환의 비정기 할당은 원인과 양을 기록
- 룰렛 판정 중 체감 가능한 프레임 스파이크 금지
- 캐스케이드 10회까지 정상 동작과 판독성 유지
- 기준 PC는 `TargetHardwareProfile`로 기록하고, 미지정 상태에서는 성능 완료를 선언하지 않는다.

현재 사용자 기준 PC 후보는 Ryzen 7 5700 / RTX 3070이다. RAM, OS 빌드, 그래픽 API가 확정되지 않았다면 `ASSUMPTION_LOG.md`에 기록한다.

# 14. 캡처와 검증

- 화면에 보이는 변경은 `Assets/CaptureHarness`와 `.claude/visual-criteria.md`를 사용해 검증한다.
- 캡처 조건은 해상도, FOV, 카메라 위치, 품질 프리셋, 시드로 고정한다.
- 구현자가 자신의 결과를 최종 평가하지 않는다.
- 가능한 경우 평가자는 신규/기존 빌드를 모르는 상태에서 비교한다.
- 직전 승인 빌드보다 나빠진 변경은 채택하지 않는다.
- 베이스라인은 기기 종속이므로 `machineFingerprint`가 달라지면 새 기준을 만든다.

# 15. 로그와 산출물

- 에디터 로그: `Logs/Editor.log`
- 런 로그: 시드, 상태 전이, 판정 단계, 오류, 성능 이벤트 포함
- 캡처: `Captures/`에 생성하되 기기 종속 파일은 Git에 포함하지 않는다.
- 완료 보고: 빌드, 테스트, 프로파일링, 캡처, 영상, 재현 시드, 변경 파일, 미해결 항목 포함

# 16. 실패 대응

- 동일 오류를 세 번 이상 같은 방식으로 수정하지 않는다.
- 반복 실패 시 최소 재현 테스트를 만든다.
- 해결되지 않는 비차단 기능은 인터페이스 뒤 플레이스홀더로 격리하고 계속 진행한다.
- 빌드 또는 핵심 테스트가 깨진 상태에서 새 기능을 추가하지 않는다.
- 패키지 추가, Unity 버전 변경, 렌더 파이프라인 변경은 사용자 승인 없이 진행하지 않는다.
- 유료 에셋, 외부 유료 API, 라이선스가 불명확한 파일을 추가하지 않는다.
- 실패한 테스트를 삭제하거나 무시 처리해 통과시키지 않는다.

# 17. Unity 자동화 주의사항

- MCP 콘솔이 비어 있어도 `Logs/Editor.log`를 확인한다.
- `Unity_RunCommand`에서 Reflection이 차단될 수 있으므로 직접 타입 참조 또는 허용된 우회 방식을 사용한다.
- 에디터 포커스 상실로 Play Mode가 멈추면 `runInBackground` 상태를 먼저 확인한다.
- Scene을 열기 전에 참조되지 않은 ScriptableObject에서 필요한 값을 읽어둔다.
- Play Mode 진입은 도메인 리로드로 오래 걸릴 수 있으므로 과도하게 폴링하지 않는다.
- 동시에 두 에이전트가 Unity 씬을 열거나 같은 YAML 에셋을 수정하지 않는다.
