# 그레이박스 UI 계획 — 층수 표시 + 계약 패널

> 범위: 층수 표현과 계약 패널 물리 인터페이스 위치·배선
> 최종 모델링·재질은 나중

---

## 1. 이미 존재하는 것 vs 진짜 빠진 것

### ✅ 이미 구현된 부분

**계기판 (`InstrumentPanelView`)**:
- 계약 명판 3장: 색으로만 구분 (미리보기·확정·미선택)
- 계약 문구 라벨: 위치는 있지만 **씬에서 `_contractLabel: {fileID: 0}`** — 죽은 경로

**레버 위 탑 (`AscentColumnView`)**:
- 남은 스핀 수치 + 슬러그 시각화
- 상승 층수 (+N 또는 0)
- 전력 탱크 (채움 높이로 0~300% 표시)
- 전력 수치 + 달성률

### ❌ 진짜 빠진 것

**UP-DEVICE-08 — 층수 표시** (PRD §4.1 필수)
- 현재: 계기판에만 작은 텍스트로 표시 ("3층 / 10")
- 필요: 엘리베이터 입구 위 또는 옆에 **큰 사이즈로**, 플레이어가 층을 올라갈 때마다 변하는 것을 한눈에 볼 수 있는 위치

**UP-DEVICE-07 — 계약 패널 물리 인터페이스** (PRD §4.1 필수)
- 현재: 계약 선택은 명판과 명판 라벨로만 표시
- 필요: 플레이어가 **클릭/상호작용할 수 있는 물리 오브젝트** (벽면이나 스탠드)

---

## 2. 층수 표현 그레이박스

### 위치
- **추측**: `ReferenceRoom/AscentColumn` 바로 위, 약 world (0, 2.0, 2.60)
- **크기**: 0.4 m H × 0.6 m W × 0.05 m D (세로 중심)
- **부모**: `ReferenceRoom` 아래 신규 자식 `FloorNumberDisplay`
- **배선 대상**: `floor.Plan.Floor` (정수, 1~10)
- **형태**: 검은 판 위 흰 텍스트 TMP (플레이스홀더)

### 근거
- 기존 계기판 `InstrumentPanelView`는 "3층 / 10"을 계기판 좌상단에만 표시
- 레버 위 탑은 전력·스핀만 담당하고 층수는 빼 있음
- 출입구(`GrayboxWorld/Door`) 위 좌표는 (0, 2.4, 1.0)로 카메라 시야에서 먼 쪽
- **제안 위치는 탑 바로 위라 시선이 자연스럽게 흐름** (레버 → 탱크 → 층수)

### 배선
```csharp
// 런타임 접근
FloorSession floor = runSession.Current;
int currentFloor = floor.Plan.Floor;  // 1~10
```

---

## 3. 계약 패널 그레이박스

### 위치
- **추측**: 오른쪽 벽 (x ≈ 2.2), 플레이어 눈높이 (y ≈ 1.60), 앞쪽 z ≈ 2.40
- **크기**: 0.4 m H × 0.6 m W × 0.1 m D (돌출된 패널 느낌)
- **부모**: `ReferenceRoom` 아래 신규 `ContractPanelInteractable`
- **배선 대상**: 
  - 선택지 표시: `floor.Plan.ContractChoices` (ResistanceContract[] 배열)
  - 현재 선택: `floor.SelectedContract` (선택 중일 때)
- **형태**: 직육면체 또는 원통형 버튼 3개 (선택지별), 클릭 감지용 Collider + `Interactable` 스크립트

### 근거
- 백로그 UP-DEVICE-07: "계약은 벽면 계약 패널, 인쇄된 계약서, 봉인된 표식 등"
- 현재 `InstrumentPanelView._plaqueLabels[3]` = 명판 라벨이 이미 계약 3장의 조건을 표시
- 명판(위치: 계기판 상단)은 색만 바뀌므로 **명판 자체가 버튼일 수도, 별개 패널일 수도 있음**
- 제안: 명판을 선택지 표시로 유지하되, 계약 패널(별도)을 오른쪽 벽에 배치해 플레이어가 "패널을 누르면 계약을 선택한다"는 인터페이스 명확화

### 배선
```csharp
// 런타임 접근
FloorSession floor = runSession.Current;
ResistanceContract[] choices = floor.Plan.ContractChoices;  // 배열
ResistanceContract current = floor.SelectedContract;  // 선택된 것

// 각 선택지 정보
if (choices.Length > 0) {
    string label = choices[0].Label;              // "흡수체 계약"
    string preview = choices[0].Preview();        // "출현률↑ 정화보상↑ 잔류대가↑"
}
```

---

## 4. 배선할 데이터의 정확한 접근 경로

### FloorSession (현재 층 상태)
```csharp
using Ascend.Prototype.Run;

FloorSession floor = runSession.Current;

// 층수 (정수, 1~10)
int floorNum = floor.Plan.Floor;

// 계약 선택지 배열
ResistanceContract[] contractChoices = floor.Plan.ContractChoices;

// 확정된 계약 (단일 객체)
ResistanceContract selectedContract = floor.SelectedContract;

// 선택 단계인지 확인
bool isSelecting = (floor.Phase == FloorPhase.ContractSelection);
```

### ResistanceContract (계약 데이터)
```csharp
// 계약 이름
string label = contract.Label;  // "흡수체 계약" 등

// 조건 미리보기 (4줄 문구)
string preview = contract.Preview();  // "출현률 +20% ... 잔류대가 ..."

// 적재와의 시너지 (부가 정보)
string synergy = contract.SynergyWith(loadout);
```

### RunSession 접근
```csharp
using Ascend.Prototype.Run;

RunSession run = runSessionBehaviour.Session;
FloorSession current = run.Current;  // null이면 런 종료

if (current != null) {
    // 위의 FloorSession 접근과 동일
}
```

---

## 5. 검증 방법

### 층수 표시 검증
1. **Play 모드**에서 레버를 당겨 다음 층으로 진행
2. 층수 표시가 "1층 / 10" → "2층 / 10" 등으로 변경되는지 확인
3. 캡처: `Captures/TenFloor/` 에 각 층 단계별 층수 표시 이미지

### 계약 패널 검증
1. 계약 선택 단계에서 플레이어가 패널과 상호작용 가능한지 확인
2. 패널의 세 버튼이 계약 선택지와 동기화되는지 확인
   - 미리보기 중인 버튼 = 하이라이트
   - 확정된 버튼 = 다른 색
3. `Logs/tenfloor_playmode.txt`에서 "계약 선택" 로그 확인
4. 캡처: 계약 선택 화면에서 패널 가시성 확인

### 데이터 동기화 검증
- `InstrumentPanelView` 계기판에 표시되는 층수와 새 층수 표시가 **항상 동일**한지 확인
- 계약 선택 중 명판의 명판 라벨과 계약 패널의 선택지가 **항상 동기화**

---

## 미정 사항 및 충돌 검토

**충돌 없음**: 제안 좌표는 기존 콘텐츠와 중복 없음
- 계기판: x ≈ 0, y ≈ 1.3
- 탑: x ≈ 0, y ≈ 1.5~2.0
- **층수 표시 (제안)**: x ≈ 0, y ≈ 2.0 (탑 바로 위, 수직 배치)
- **계약 패널 (제안)**: x ≈ 2.2 (오른쪽 벽)

**미정**: 계약 패널이 기존 명판을 대체할지, 별도 추가 UI일지 → 사용자 판단 필요
