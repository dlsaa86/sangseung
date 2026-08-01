# VISUAL VERDICT — 독립 시각 평가 판정

> **구현자가 이 파일을 쓰지 않는다.** 구현자와 분리된 평가자(`visual-critic` /
> `visual-director` 또는 `visual-verify` 스킬의 블라인드 평가자)만 판정을 기록한다.
> `tools/verify-topdown.ps1` C10 이 마지막 `VERDICT:` 줄 하나만 읽는다.
>
> **판정 파일이 캡처 매니페스트보다 오래되면 검증기가 거부한다.** 캡처를 다시 뽑았으면
> 평가도 다시 받아야 한다 — 옛 ACCEPT 를 새 캡처에 재사용할 수 없다.

## 형식

각 평가는 아래 블록 하나를 **파일 끝에 추가**한다. 이전 기록을 지우지 않는다.

```
## <YYYY-MM-DD> — <대상 캡처 세트>
- 평가자: <구현자와 분리된 주체>
- 대상: Captures/<set>/manifest.txt  (machineFingerprint: <값>)
- 방식: 블라인드 쌍대비교 / 루브릭 채점
VERDICT: ACCEPT | REJECT | PENDING
- 근거: <한 문단>
- 지적 → 백로그 전환: <UP-FIX-NN 목록>
```

`VERDICT:` 는 **줄 전체가 정확히** `VERDICT: ACCEPT` 형태여야 한다 (앞뒤 공백 허용).

---

## 2026-08-01 — Captures/TenFloor (18장)

- 평가자: 독립 시각 평가자 (구현 세션과 분리)
- 대상: `Captures/TenFloor/manifest.txt`
  (machineFingerprint: `Windows|Direct3D12|NVIDIA GeForce RTX 3070|6000.5.5f1`)
- 방식: 루브릭 대조 + 항목별 지적

VERDICT: REJECT

- 근거: 최우선 지적은 `01_entry` 가 이 게임의 공간을 보여주지 못한다는 것이다 —
  **높이를 보여주는 프레임이 0장.** PRD §12.2 는 "좁고 높고 박스형"을 공간 미학
  목표로 두는데, 세트 전체에서 그 높이가 읽히는 장면이 없다.
  더해 `16_risk_collapse` 가 실제 Collapse 가 아니었고(매니페스트가 스스로 "실제 단계
  Warning / 실패 False" 라고 적고 있었다), 임계점 눈금 숫자 라벨을 넣으려던 수정이
  계기판 본문 4번째 줄 위에 겹쳐 렌더돼 **직전보다 나빠진 순 후퇴**였다. 되돌렸다.
- 지적 → 백로그 전환: `UP-FIX-01`, `UP-FIX-02`, `UP-FIX-03`, `UP-FIX-04`, `UP-FIX-05`, `UP-FIX-06`

> 이 REJECT 는 **작업 종료 사유가 아니다.** 지적은 백로그 §5 수정 백로그로 전환됐고,
> 다음 작업은 미구현 Required 범위로 이동한다 (`CLAUDE.md` 실행 규칙 2).
</content>
