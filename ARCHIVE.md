# 아카이브 — 해븐스 헝거 v1 (슬롯 시대) 전량 보관

작성: 2026-08-26
사유: `dlsaa86/HEAVEN-S-HUNGER` 를 **거울 결투 코어**로 완전 리뉴얼하면서, 그 이전 작업물 전체를 이 저장소로 옮겼다.

> 원칙: **동결이지 삭제가 아니다.** 재시작 문서 §5 — "v2를 삭제하지 말고 별도 브랜치/저장소로 동결한다. 재시작 후 거울이 손에서 실패하면 돌아올 자리가 필요하다."

---

## 1. 무엇이 어디에 있나

### 1.1 브랜치 아카이브 (`archive/hh-v1/*`)

리뉴얼 직전 `HEAVEN-S-HUNGER` 의 **모든 브랜치를 커밋 해시 그대로** 옮겨왔다. 히스토리 전체가 살아 있다.

| 브랜치 | 커밋 | 내용 |
|---|---|---|
| `archive/hh-v1/pre-renewal-20260826` | `1a5518a` | **리뉴얼 직전 최종 상태.** 미커밋 작업 444건까지 포함한 진짜 마지막 스냅샷 |
| `archive/hh-v1/main` | `96b0060` | 원격 main |
| `archive/hh-v1/agent/autonomous-polish-20260809` | `28539bc` | 로컬 작업 브랜치 |
| `archive/hh-v1/agent/phase2-full-prototype` | `96b0060` | 풀 프로토타입 |
| `archive/hh-v1/agent/phase1-hero-slice` | `0125b92` | 히어로 슬라이스 |
| `archive/hh-v1/agent/freeze-ai-development-docs` | `10a63a4` | 문서 동결 |
| `archive/hh-v1/agent/opus-sol-agent-policy` | `bbed396` | 에이전트 정책 |
| `archive/hh-v1/agent/windows-handoff` | `58e862d` | 윈도우 인계 |
| `archive/hh-v1/prototype/overnight` | `63176a5` | 야간 자동 작업 |

보관 규모: 추적 파일 **3,222개** (Unity 프로젝트 일체 — Assets 2,460 · docs 110 · tools 31 · ProjectSettings 27 · .claude 221).

### 1.2 웹 프로토타입 (이 브랜치 `main`)

- `sangseung_proto.html` — v1 합산 슬롯 게임 본체
- `slot_proto.html` — **v2 슬롯 네이티브** (5×3 덱빌딩, 라인 배당 + 릴 스트립 + 장전). **손 테스트 미실시 상태로 동결됨**
- `balance/` — RULESET 2.0 수치 정본 (심볼 10 · 승객 23 · 설비 161 · 인터폰 거래 24 · 7734층 진행)
- `index.html` — 모바일 래퍼 / GitHub Pages 진입점

이 페이지들은 계속 살아 있다: https://dlsaa86.github.io/sangseung/

### 1.3 아카이브되지 **않은** 것 (로컬 디스크에만 존재)

git 추적 대상이 아니라 이 저장소에 없다. `~/Documents/GitHub/Upandup_DDD` 로컬 폴더에 그대로 남아 있다.

| 경로 | 크기 | 성격 |
|---|---|---|
| `Library/` | ~4GB | Unity 임포트 캐시 — 재생성 가능 |
| `Logs/` | 832MB | 에디터 로그 — 노이즈 |
| `Build/` | 309MB | 빌드 산출물 — 재생성 가능 |
| `Captures/` | 62MB | 캡처 하네스 스크린샷 — 런 증거, 재생성 가능 |

---

## 2. 되돌아가는 방법

```bash
# 리뉴얼 직전 상태를 통째로 꺼내기
git clone git@github.com:dlsaa86/sangseung.git hh-v1
cd hh-v1
git checkout archive/hh-v1/pre-renewal-20260826
# → Unity 프로젝트 전체가 그대로 나온다 (Library는 첫 오픈 시 재생성)
```

특정 파일만 필요할 때:

```bash
git show archive/hh-v1/pre-renewal-20260826:docs/GAMEPLAY_CORE_V7.md
git checkout archive/hh-v1/pre-renewal-20260826 -- Assets/Prototype_Elevator/Art
```

---

## 3. 재활용 예정 자산 (재시작 문서 §6)

새 코어로 옮겨올 것들. 지금은 아카이브에만 있고, 필요해지는 시점에 꺼내 쓴다.

**재활용**
- 엘리베이터 셸 모델 / 머티리얼 / 셰이더 / 텍스처 (`Assets/Prototype_Elevator/Art`, `Materials`)
- 층 이동 연출, 조명, 포스트프로세싱
- UI 프레임 · 폰트 · 컬러 토큰
- 프로젝트 아키텍처 골격(DDD), 세이브/설정 시스템
- 텔레메트리 · 로깅 뼈대 ← **거울 AI가 이것 위에 올라간다**
- 사운드, 앰비언스

**동결 (삭제 아님)**
- 슬롯 릴 / 심볼 / 라인 배당 로직 (`Scripts/Spin`, `Scripts/Roulette`)
- 백빌딩 · 스트립 배치 시스템
- 장전 / 리스핀 자원 시스템 ← 개념은 결투로 이식됨 (총알 = 장전, 막기 = 리스핀의 후예)

**폐기**
- 합산 점수(v1) 관련 잔여 코드

---

## 4. 왜 이렇게 했는가 — 정직한 기록

이번이 **세 번째 코어 변경**이다.

1. v1 합산 슬롯 → 폐기. 근거: "슬롯을 돌리는 감각이 없다"
2. v2 슬롯 네이티브 → 제작 완료, **손 테스트 미실시**
3. 거울 결투 → 수학 검증 완료, 프로토타입 미제작 ← **새 코어**

v2를 죽이는 근거는 아직 없다. 거울이 더 흥미로워서일 뿐이다. **증거 기반 폐기가 아니라 흥분 기반 전환이다.**
그래서 삭제하지 않고 여기에 동결한다. 거울이 손에서 실패하면 `slot_proto.html` 로 돌아온다.

다음 전환 유혹이 올 때 이 문단을 읽을 것.
