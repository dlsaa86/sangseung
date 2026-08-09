using System;
using System.Collections.Generic;
using Ascend.Prototype.Build;
using Ascend.Prototype.Data.Profiles;
using Ascend.Prototype.Events;
using Ascend.Prototype.Spin;

namespace Ascend.Prototype.Run
{
    /// <summary>
    /// Pure-C# state machine for one floor. It owns boarding, contract selection,
    /// spins, residual carry-over, the push-your-luck decision, and final ascent data.
    /// </summary>
    public sealed class FloorSession
    {
        // ── 과적 규칙 3종의 코드 프리셋 (`UP-TECH-09` ⑤) ──────────────────────
        //
        // **`PrototypeConfig` 의 기본값이 아니다.** 오래된 주석이 그렇게 적혀 있었으나
        // 실측은 다르다 — 그쪽 `allowedWeight` 는 `682bbd0` 에서 8로 내려갔고(승객 수
        // 단위였던 T-04·T-06 경로), 여기 100 은 kg 단위인 10층 경로의 값이다.
        // 두 숫자는 어긋난 게 아니라 **다른 양**이다. 자세한 근거는
        // <see cref="Data.Profiles.WeightProfile"/> 의 주석에 있다.
        //
        // 이 상수들은 이제 **폴백 프리셋**이다. 값의 정본은
        // <see cref="Data.Profiles.WeightSnapshot"/> 이고, 에셋이 없으면 이 수로 돌아온다.
        // `WeightProfile.Default*` 와 같은 수여야 하며 테스트가 그 일치를 대조한다.
        public const float WeightPowerFactor = 2f;

        /// <summary>짐꾼 계열의 보너스를 더하기 전 기본 허용 중량.</summary>
        public const float AllowedWeight = 100f;
        public const float OverloadRequiredPowerMultiplier = 1.5f;

        /// <summary>적재 대가 곡선의 곡률 폴백. <see cref="WeightProfile.DefaultExcessCurvature"/> 와 같은 수여야 한다.</summary>
        public const float ExcessCurvature = 1f;

        /// <summary>과적 완충 폭 폴백. <see cref="WeightProfile.DefaultOverloadRampWidth"/> 와 같은 수여야 한다.</summary>
        public const float OverloadRampWidth = 0.30f;
        public const float DefaultAnteRatio = 0.12f;
        public const float DefaultAnteEscalation = 0.35f;

        /// <summary>적재 층에서 한 번에 제시하는 후보 수.</summary>
        public const int BuildOfferCount = 3;

        private readonly SpinEngine _engine;
        private readonly PowerThresholds _thresholds;
        private readonly List<SpinResolution> _history = new List<SpinResolution>();
        private readonly BuildLoadout _loadout;
        private float _baseWeight;
        private BuildItem[] _offers = Array.Empty<BuildItem>();
        private float _carriedWeight;
        private float _requiredPower;
        private SpinRuleSet _rules;
        private ResistanceContract _contract;
        private ResidualState _residual;
        private FloorResult _result;
        private float _resolvedCapacity;

        /// <summary>
        /// 과수확 수치 9종의 값 사본 (`UP-POWER-07`). <see cref="OverharvestSnapshot"/>은
        /// 순수 구조체라 이 파일이 지키는 「UnityEngine·ScriptableObject 비의존」이 유지된다 —
        /// 에셋을 든 쪽과 안 든 쪽이 **같은 구조체**를 넘기고, 판정은 여기 한 곳에서만 한다.
        ///
        /// 직전 판본은 판돈 두 값만 float 로 받고 해금 임계·추가 스핀 상한은 아예 몰랐다.
        /// 그래서 프로파일의 <c>IsUnlocked</c>·<c>EffectiveExtraSpinLimit</c> 가 만들어진 채
        /// **호출자 0곳**이었다 — 값을 바꿔도 게임이 그대로인 상태의 정확한 원인이다.
        /// </summary>
        private readonly OverharvestSnapshot _overharvest;

        /// <summary>
        /// 과적 규칙 3종의 값 사본 (`UP-TECH-09` ⑤). 에셋이 없으면 코드 프리셋이고
        /// 그 사실이 <see cref="WeightSnapshot.SourceName"/> 에 남는다.
        /// </summary>
        private readonly WeightSnapshot _weight;

        /// <summary>
        /// 스핀 밸런스 10종의 값 사본 (`UP-TECH-09` ①③). 층이 규칙 다발을 만들 때마다
        /// 쓰이므로 층이 들고 있어야 한다 — 계약 선택 뒤 `BuildRules` 가 다시 돈다.
        /// </summary>
        private readonly SpinBalanceSnapshot _balance;
        private float _totalAnte;
        private float _extraSpinNetPower;

        /// <summary>
        /// 남은 스핀 정산 수치 (`T-05` 2026-08-02). 에셋이 없으면 코드 프리셋을 쓴다.
        ///
        /// `readonly` 로 둔다 — 생성자 밖에서 대입할 곳이 없다는 것이 곧
        /// 「층이 도는 중에 정산율이 바뀌지 않는다」이고, 그 성질이 없으면 결정론이 깨진다.
        /// </summary>
        private readonly Data.Profiles.RemainingSpinSettlementSnapshot _settlement;

        /// <summary>
        /// **과수확을 한 번이라도 선택하면 참이 되고 다시 거짓이 되지 않는다.**
        ///
        /// `T-05` — 「과수확 선택 시 해당 층의 정산 권리 소멸」. `ExtraSpinsTaken > 0`
        /// 으로 대신 판정하지 않는 이유: 추가 스핀이 **실행되기 전에** 앤티를 이미
        /// 냈고 선택은 그 시점에 끝났다. 둘을 같은 것으로 보면 「과수확을 골랐는데
        /// 스핀이 실패해서 카운터가 안 오른」 경계에서 정산이 되살아난다.
        /// </summary>
        private bool _settlementForfeited;
        private float _lastAnte;
        private bool _activeSpinIsExtra;

        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight)
            : this(plan, engine, thresholds, carriedWeight, ResidualState.Empty)
        {
        }

        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight, ResidualState carriedResidual)
            : this(plan, engine, thresholds, carriedWeight, carriedResidual,
                DefaultAnteRatio, DefaultAnteEscalation)
        {
        }

        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight,
            float anteRatio, float anteEscalation)
            : this(plan, engine, thresholds, carriedWeight, ResidualState.Empty,
                anteRatio, anteEscalation)
        {
        }

        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight, ResidualState carriedResidual,
            float anteRatio, float anteEscalation)
            : this(plan, engine, thresholds, carriedWeight, carriedResidual,
                anteRatio, anteEscalation, null)
        {
        }

        /// <summary>
        /// <paramref name="loadout"/>는 런이 소유하며 층을 건너 살아남는다. null이면
        /// 적재 없는 층으로 동작한다(Phase 1 Hero Slice 경로가 이렇다).
        /// </summary>
        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight, ResidualState carriedResidual,
            float anteRatio, float anteEscalation, BuildLoadout loadout)
            : this(plan, engine, thresholds, carriedWeight, carriedResidual,
                WithAnte(OverharvestProfile.DefaultSnapshot, anteRatio, anteEscalation), loadout)
        {
        }

        /// <summary>
        /// 판돈 두 값만 받던 옛 경로를 스냅샷으로 올린다. 나머지 일곱 값은 코드 기본값이라
        /// **동작이 바뀌지 않는다** — 옛 호출자(테스트·밸런스 심)를 고치지 않고 남기기 위한 것이다.
        /// </summary>
        private static OverharvestSnapshot WithAnte(OverharvestSnapshot s, float anteRatio, float anteEscalation)
        {
            return new OverharvestSnapshot(
                Math.Max(0f, anteRatio), Math.Max(0f, anteEscalation), s.UnlockThreshold,
                s.ApproachMachineDuckScale, s.MinSilenceSeconds, s.MaxSilenceSeconds,
                s.PassengerGazeDelaySeconds, s.ResumeFadeSeconds, s.MaxExtraSpins);
        }

        /// <summary>
        /// <paramref name="overharvest"/>는 `OverharvestProfile.asset` 에서 온 값 사본이거나
        /// 에셋이 없으면 코드 기본값이다. 어느 쪽이든 판정식은 하나다.
        /// </summary>
        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight, ResidualState carriedResidual,
            OverharvestSnapshot overharvest, BuildLoadout loadout)
            : this(plan, engine, thresholds, carriedWeight, carriedResidual,
                overharvest, WeightProfile.DefaultSnapshot, loadout)
        {
        }

        /// <summary>
        /// <paramref name="weight"/>는 `WeightProfile.asset` 에서 온 값 사본이거나 에셋이
        /// 없으면 코드 프리셋이다. 어느 쪽이든 과적 판정식은 하나다
        /// (<see cref="WeightSnapshot.RequiredPowerFor"/>).
        /// </summary>
        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight, ResidualState carriedResidual,
            OverharvestSnapshot overharvest, WeightSnapshot weight, BuildLoadout loadout)
            : this(plan, engine, thresholds, carriedWeight, carriedResidual,
                overharvest, weight, SpinBalanceProfile.DefaultSnapshot, loadout)
        {
        }

        /// <summary>
        /// <paramref name="balance"/>는 `SpinBalanceProfile.asset` 에서 온 값 사본이거나
        /// 에셋이 없으면 코드 프리셋이다. 심볼 가중치·패턴 배수·연쇄 증분이 여기서 온다.
        /// </summary>
        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight, ResidualState carriedResidual,
            OverharvestSnapshot overharvest, WeightSnapshot weight,
            SpinBalanceSnapshot balance, BuildLoadout loadout)
            : this(plan, engine, thresholds, carriedWeight, carriedResidual, overharvest,
                weight, balance,
                Data.Profiles.RemainingSpinSettlementProfile.DefaultSnapshot, loadout)
        {
        }

        /// <summary>
        /// <paramref name="settlement"/>는 `RemainingSpinSettlementProfile.asset` 에서 온
        /// 값 사본이거나 에셋이 없으면 코드 프리셋이다.
        ///
        /// **이 인자가 없던 것이 결함이었다.** `_settlement` 는 필드 초기화로 코드 프리셋을
        /// 들고 있었고 대입하는 코드가 어디에도 없었다 — 즉 `T-05` 가 「값은 임시이므로
        /// 플레이테스트가 교체할 수 있게 `ScriptableObject` 로 뺀다」고 적어 둔 바로 그
        /// 교체 경로가 **연결된 적이 없다.** 프로파일을 만들고 배선해도 한 자리도 안 바뀐다.
        ///
        /// 이 저장소는 같은 부류를 이미 겪었다 — `RemainingSpinSettlementProfile` 자신의
        /// 주석이 「'배선했다'와 '그 값이 쓰였다'는 다르고, 프로파일 8종이 여덟 세션 동안
        /// 죽어 있는 것을 못 봤다」고 적고 있다. 그 목록에 자기 자신이 들어 있었다.
        /// </summary>
        public FloorSession(FloorPlan plan, SpinEngine engine,
            PowerThresholds thresholds, float carriedWeight, ResidualState carriedResidual,
            OverharvestSnapshot overharvest, WeightSnapshot weight,
            SpinBalanceSnapshot balance,
            Data.Profiles.RemainingSpinSettlementSnapshot settlement, BuildLoadout loadout)
        {
            _settlement = settlement;
            if (engine == null) throw new ArgumentNullException(nameof(engine));
            if (plan.Spins <= 0) throw new ArgumentOutOfRangeException(nameof(plan), "A floor needs at least one spin.");

            Plan = plan;
            _engine = engine;
            _thresholds = thresholds;
            _loadout = loadout;
            _baseWeight = Math.Max(0f, carriedWeight);
            // 과적 수치는 `RecomputeLoad` 보다 **먼저** 들어와야 한다. 그 안의
            // `Capacity`·`ComputeRequiredPower` 가 이 값을 읽으므로, 순서를 뒤집으면
            // 첫 계산만 허용 중량 0(기본값 구조체)으로 돌아 층이 시작부터 과적이 된다.
            _weight = weight;
            // 규칙 다발은 이 생성자 끝에서 만들어질 수 있다. 그 전에 들어와야 한다.
            _balance = balance;
            // 앞선 층에서 실은 것이 그대로 따라온다. 여기서 기본 무게만 쓰면 2층 이후
            // 적재가 요구 전력에 반영되지 않는다.
            RecomputeLoad();
            _residual = carriedResidual;
            _overharvest = overharvest;
            _contract = ResistanceContract.None;
            _rules = null;

            // 적재가 요구 전력과 허용 중량을 바꾸므로 계약보다 먼저 끝나야 한다.
            if (plan.OffersBuildReward && _loadout != null)
            {
                _offers = BuildCatalog.OffersFor(engine.RunSeed, plan.Floor, _loadout, BuildOfferCount);
                if (_offers.Length > 0)
                {
                    Phase = FloorPhase.Boarding;
                    return;
                }
            }

            Phase = plan.ContractChoices != null && plan.ContractChoices.Length > 0
                ? FloorPhase.ContractSelection : FloorPhase.Spinning;

            if (Phase == FloorPhase.Spinning)
                BuildRules(_contract);
        }

        public FloorPlan Plan { get; }
        public FloorPhase Phase { get; private set; }
        public float CarriedWeight => _carriedWeight;

        /// <summary>
        /// 사건을 내보낼 곳. 런이 넣어 준다. null이면 아무 데도 보내지 않는다 —
        /// 헤드리스 단위 테스트는 버스 없이 층 하나만 돌릴 수 있어야 한다.
        /// </summary>
        public GameEventBus Events { get; set; }

        /// <summary>
        /// 이미 알린 임계점(%). 같은 층에서 100%를 두 번 알리지 않는다.
        /// 잔류 피해로 전력이 내려갔다 다시 올라가는 일이 실제로 있기 때문에
        /// "지금 넘었는가"가 아니라 "한 번이라도 넘었는가"로 판정한다.
        /// </summary>
        private int _announcedThreshold;

        /// <summary>
        /// 기본 허용 중량 + 짐꾼 보너스. 이 값을 넘으면 과적이다.
        ///
        /// **확정 뒤에는 확정 시점 값으로 얼어붙는다.** 계산 프로퍼티였을 때 무슨 일이
        /// 있었나: `_carriedWeight`와 `_requiredPower`는 확정 시 얼리는데(`RunSession`의
        /// `OnLoadoutChanged` 가드) 이 프로퍼티만 런이 소유한 살아 있는 `_loadout`을
        /// 매번 다시 읽었다. `CompleteFloor`가 화물 포기와 하차를 실행한 **뒤에**
        /// 사고 기록기가 한 프레임 늦게 기록하므로, 떠난 층의 레코드에 얼어붙은 무게와
        /// 하차 이후의 허용 중량이 한 줄에 섞여 들어갔다.
        /// </summary>
        public float Capacity => _result != null
            ? _resolvedCapacity
            : _weight.CapacityWith(_loadout != null ? _loadout.TotalCapacityBonus : 0f);

        /// <summary>
        /// 과적 수치가 어디서 왔는가. 「배선됐는가」가 아니라 「읽혔는가」를 묻는 검사가
        /// 이 값을 본다 — 에셋의 기본값이 코드 프리셋과 같으면 값 비교로는 두 경로를
        /// 구분할 수 없기 때문이다.
        /// </summary>
        public WeightSnapshot Weight => _weight;

        /// <summary>스핀 밸런스 수치의 출처와 값. 하네스가 「에셋이 읽혔는가」를 이걸로 묻는다.</summary>
        public SpinBalanceSnapshot Balance => _balance;

        public bool IsOverloaded => _carriedWeight > Capacity;

        /// <summary>지금 이 층에 실려 있는 것. 층이 소유하지 않는다 — 런의 것을 빌려 본다.</summary>
        public BuildLoadout Loadout => _loadout;

        /// <summary>
        /// 확정 시점의 적재 목록 문자열. `Loadout`은 런의 것이라 하차·포기 뒤에 달라진다.
        /// 기록은 **그 층에 실제로 실려 있던 것**을 적어야 한다.
        /// null 이면 아직 확정 전이므로 호출자가 `Loadout`을 직접 읽는다.
        /// </summary>
        public string ResolvedLoadoutShort { get; private set; }
        public string ResolvedLoadoutDetail { get; private set; }

        /// <summary>적재 단계에서 제시된 후보. 다른 단계에서는 비어 있다.</summary>
        public IReadOnlyList<BuildItem> BuildOffers => _offers;

        /// <summary>
        /// 후보 하나를 싣는다. 자리가 없거나 이미 실었으면 거부한다.
        /// 싣는 즉시 무게와 요구 전력이 갱신된다 — 문을 닫기 전에 대가를 보여줘야 선택이 된다.
        /// </summary>
        public bool TakeOffer(int index)
        {
            if (Phase != FloorPhase.Boarding || _loadout == null) return false;
            if (index < 0 || index >= _offers.Length) return false;

            BuildItem item = _offers[index];
            if (item == null || !_loadout.Add(item)) return false;

            var remaining = new List<BuildItem>(_offers.Length - 1);
            for (int i = 0; i < _offers.Length; i++)
                if (i != index) remaining.Add(_offers[i]);
            _offers = remaining.ToArray();

            RecomputeLoad();
            Events?.Publish(GameEventKind.ItemBoarded, Plan.Floor, -1,
                _loadout.Count, item.Weight, item.Label);
            return true;
        }

        /// <summary>문을 닫는다. 아무것도 싣지 않아도 진행할 수 있어야 한다(진행 불가 방지).</summary>
        public bool FinishBoarding()
        {
            if (Phase != FloorPhase.Boarding) return false;

            _offers = Array.Empty<BuildItem>();
            RecomputeLoad();
            Phase = Plan.ContractChoices != null && Plan.ContractChoices.Length > 0
                ? FloorPhase.ContractSelection : FloorPhase.Spinning;

            if (Phase == FloorPhase.Spinning)
                BuildRules(_contract);

            Events?.Publish(GameEventKind.BoardingFinished, Plan.Floor, -1,
                _loadout != null ? _loadout.Count : 0, _carriedWeight,
                IsOverloaded ? "과적" : null);
            return true;
        }

        /// <summary>
        /// 적재가 층 바깥에서 바뀌었을 때 무게와 요구 전력을 다시 계산한다.
        ///
        /// 필요한 이유: `_carriedWeight`와 `_requiredPower`는 층이 만들어질 때 한 번,
        /// 그리고 적재 단계에서 갱신된다. `RunSession.AddWeight`처럼 층이 이미 존재하는
        /// 상태에서 무게가 바뀌면 층은 그 사실을 모른 채 옛 요구 전력을 들고 있고,
        /// `IsOverloaded`도 거짓으로 남는다. 실제로 캡처 리그가 과적 218/130 상태에서
        /// 위험 단계 Stable을 찍었다 — 무게는 늘었는데 층이 모르고 있었다.
        /// </summary>
        /// <param name="baseWeight">
        /// 런이 들고 있는 기본 무게. 층의 사본이 아니라 **런의 현재 값**을 받아야 한다 —
        /// 인자 없이 자기 사본으로 다시 계산하면 아무것도 바뀌지 않는다.
        /// 처음에 인자 없는 형태로 만들었다가 테스트가 "층 무게가 0 (기대 200)"으로 잡았다.
        /// </param>
        public void RefreshLoad(float baseWeight)
        {
            _baseWeight = Math.Max(0f, baseWeight);
            RecomputeLoad();
            ReopenSpinningIfRequirementOutgrewPower();
        }

        /// <summary>
        /// 🔴 **`CE-1` — 확정 단계의 영구 교착.** 요구 전력이 오르면 단계도 다시 판정한다.
        ///
        /// ## 무엇이 있었나
        ///
        /// <see cref="FloorPhase.Decision"/> 은 <see cref="Spin"/> 이 `CanBank` 를 보고
        /// **한 번 걸어 두는 값**이었다. 그 뒤에 적재가 늘어 요구 전력이 오르면
        /// (실측: 215 → 700.5, 시드 1337·1338·1339 재현) `CanBank` 가 거짓이 되는데
        /// 단계는 `Decision` 에 남는다. 그 순간 **네 출구가 동시에 닫힌다.**
        ///
        /// | 출구 | 조건 | 왜 닫히는가 |
        /// |---|---|---|
        /// | <see cref="Bank"/>          | `Decision && CanBank`             | `CanBank` 가 거짓이 됐다 |
        /// | <see cref="ForceResolve"/>  | `Decision && SpinsRemaining == 0` | 스핀이 남아 있다 |
        /// | <see cref="PushYourLuck"/>  | `CanTakeExtraSpin` → 해금 임계     | 달성률이 1.0 아래로 떨어져 잠겼다 |
        /// | <see cref="Spin"/>          | `Phase == Spinning`               | 단계가 `Decision` 이다 |
        ///
        /// 층이 끝나지도, 진행되지도 않는다. 프로젝트 **자신의** 캡처 리그가
        /// (`TenFloorCaptureRig.cs:532`) 이 경로를 쓴다.
        ///
        /// ## 왜 이렇게 고치나
        ///
        /// 「스핀이 남았는데 요구 전력에 못 미친다」는 상태의 이름은 이미 있다 —
        /// <see cref="FloorPhase.Spinning"/> 이다. 요구 전력을 얼려서 고치는 방법도
        /// 있지만 그건 **밸런스 결정**이다(적재의 대가가 그 층에서 사라진다).
        /// 여기서 필요한 것은 값을 바꾸는 것이 아니라 **단계를 상태의 함수로 되돌리는 것**이고,
        /// 그러면 경제는 한 자리도 안 바뀐 채 출구만 다시 열린다.
        ///
        /// `_spinsRemainingAtFirstReach` 는 되돌리지 않는다 — Mercy 등급의 정의가
        /// 「**처음** 채운 순간」이고, 요구 전력이 나중에 오른 것이 그 사실을 지우지 않는다.
        /// </summary>
        private void ReopenSpinningIfRequirementOutgrewPower()
        {
            if (Phase != FloorPhase.Decision || _result != null) return;
            if (CanBank || SpinsRemaining <= 0 || _rules == null) return;

            // **사건을 발행하지 않는다.** 이 전이에 맞는 사건이 없고, 뜻이 다른 것을
            // 빌려 쓰면 소리·승객 반응·텔레메트리가 일어나지 않은 일에 반응한다.
            // 대신 카운터로 남긴다 — 「일어나지 않았어야 할 일이 몇 번 일어났는가」는
            // 조용히 정상 동작하는 것보다 세는 편이 낫다.
            Phase = FloorPhase.Spinning;
            RequirementReopenCount++;
        }

        /// <summary>
        /// 요구 전력 상승으로 확정 단계가 다시 열린 횟수. 정상 플레이에서는 0이다 —
        /// 적재는 <see cref="FloorPhase.Boarding"/> 에서만 바뀌기 때문이다.
        /// 0이 아니면 층 도중에 무게를 바꾸는 경로(캡처 리그·프로브)가 돌았다는 뜻이고,
        /// 그 사실이 보고서에 남아야 「왜 이 층만 스핀을 더 썼는가」를 되짚을 수 있다.
        /// </summary>
        public int RequirementReopenCount { get; private set; }

        private void RecomputeLoad()
        {
            _carriedWeight = _baseWeight + (_loadout != null ? _loadout.TotalWeight : 0f);
            _requiredPower = ComputeRequiredPower(Plan, _carriedWeight, Capacity);
        }
        public float Power { get; private set; }
        public float RequiredPower => _requiredPower;
        public int SpinsUsed { get; private set; }
        public int SpinsRemaining => Math.Max(0, Plan.Spins - SpinsUsed);

        private int _spinsRemainingAtFirstReach = -1;

        /// <summary>
        /// 요구 전력을 **처음** 채운 순간의 남은 스핀. 아직 못 채웠으면 −1 이다.
        ///
        /// Mercy 등급의 유일한 입력이다(`MercyHunger.MercyFor`). −1 과 0 은 다르다 —
        /// −1 은 「끝내 못 채웠다」, 0 은 「마지막 스핀에 겨우 채웠다」이고
        /// 앞은 <see cref="MercyGrade.None"/>, 뒤는 절제로는 최하위지만 **달성**이다.
        /// </summary>
        public int SpinsRemainingAtFirstReach => _spinsRemainingAtFirstReach;

        /// <summary>초과 전력 비율. 계기·기록기가 등급 대신 이 연속값을 쓴다.</summary>
        public float ExcessRatio => MercyHunger.ExcessRatio(Power, RequiredPower);
        public ResidualState Residual => _residual;
        public PowerBand CurrentBand => _thresholds.BandFor(Power, RequiredPower);
        public bool CanBank => Power >= RequiredPower;
        public float AnteRatio => _overharvest.AnteRatio;
        public float AnteEscalation => _overharvest.AnteEscalation;
        public int ExtraSpinsTaken { get; private set; }

        /// <summary>
        /// 과수확 잠금이 풀렸는가 (PRD §7.1 「요구 전력 100% 달성 시」).
        ///
        /// **`CanBank` 과 일부러 분리했다.** 기본 임계 1.00 에서 둘은 같은 값이지만
        /// 같은 것이 아니다 — 확정(브레이크)은 규칙이고 과수확 해금은 **조정 가능한 수치**다.
        /// 하나로 묶어 두면 `UnlockThreshold` 를 바꿔도 아무 일도 일어나지 않는다.
        ///
        /// 🔴 **2026-08-09 — 전력 달성만으로는 더 이상 부족하다.** 사용자 지시: 「과수확도
        /// 그냥 가능하게 하는 게 아니라 아이템이나 승객 조건으로 해 두는 게 좋을 것 같아.
        /// 아니면 계약으로. 우선 과수확은 기본 옵션이 아니게 해 줘」. 전력 달성률은 이제
        /// **필요조건일 뿐**이고 <see cref="HasOverharvestKey"/> 도 함께 참이어야 한다
        /// (AND — 어느 한쪽만으로는 안 열린다, 아래 두 프로퍼티 참고).
        /// <see cref="Data.Profiles.OverharvestSnapshot.UnlockThreshold"/> 는 건드리지
        /// 않았다 — 그건 다른 축이고, 같이 움직이면 무엇이 게이트를 걸었는지 분리할 수 없다
        /// (팀 지시). 자세한 설계 근거는 `docs/runtime/OVERHARVEST_GATE_NOTES.md`.
        ///
        /// 🔴 **2026-08-09 오후 — 과수확 자체가 보류됐다.** 사용자 지시: 「일단 과수확
        /// 컨셉은 제거하자 이건 보류해야 할 것 같으니까」. <see cref="PrototypeFeatures.Overharvest"/>
        /// 가 꺼져 있으면 아래 두 조건을 **보지도 않고** 거짓이다.
        ///
        /// 스위치를 여기 하나에만 건 이유: 이 프로퍼티가 과수확 전체의 **단일 관문**이다.
        /// 추가 스핀 · 레버 · 해금 이벤트 · 단계 타임라인 · 접근 정적 · 승객 응시가 전부
        /// <see cref="CanTakeExtraSpin"/> 을 통해 여기 매달려 있다. 104개 파일 1,300곳을
        /// 지우는 대신 한 곳을 막았다 — 되살리는 비용도 같은 한 곳이다.
        /// </summary>
        public bool IsOverharvestUnlocked =>
            PrototypeFeatures.Overharvest
            && _overharvest.IsUnlocked(Power, RequiredPower) && HasOverharvestKey;

        /// <summary>
        /// 과수확 레버를 쓸 「열쇠」가 적재에 있는가.
        /// <see cref="BuildEffectKind.OverharvestUnlock"/> 효과를 가진 품목이
        /// 실려 있으면 참이다. 지금 카탈로그 기준 두 갈래다 — 부품 「과수확 변압기」
        /// (런 내내 유지)와 승객 「짐꾼」(하차 층까지만, `BuildAxis.Load`). **열쇠가
        /// 하나뿐이면 지배적 선택이 된다**는 이 저장소가 반복해서 겪은 문제라 최소
        /// 두 갈래로 열었다 — 근거는 `docs/runtime/OVERHARVEST_GATE_NOTES.md`.
        ///
        /// `SpinRuleSet` 을 거치지 않고 <c>_loadout</c> 을 직접 스캔한다 —
        /// <see cref="BuildLoadout.HasEffect"/> 주석에 이유가 있다(요약: 이 게이트는
        /// 스핀 판정 규칙이 아니라 층 진행 층위의 개념이라 `SpinRuleSet` 이 몰라도 된다).
        ///
        /// **`_loadout == null` 이면 참으로 둔다 — 게이트를 느슨하게 하는 선택이 아니다.**
        /// 실제 런(<see cref="RunSession"/>)은 `_loadout` 필드를 <c>new BuildLoadout()</c>
        /// 으로 초기화해 두므로(`RunSession.cs`) **실제 플레이에서는 이 값이 절대 null 이
        /// 아니다** — null 분기는 살아 있는 게임에서 한 번도 타지 않는다. null 이 실제로
        /// 지나가는 경로는 적재 시스템 자체가 없는 옛 경로(이 파일 생성자 주석의 "Phase 1
        /// Hero Slice") 하나뿐이고, 그 경로를 그대로 쓰는 기존 테스트가 대량으로 있다.
        ///
        /// 반대(=null 이면 항상 잠금)를 골랐다면: 적재 시스템이 아예 없어 열쇠를 실을
        /// 수단 자체가 없는 경로에 게이트를 거는 모순이 되고, 그 경로를 쓰는 기존
        /// 테스트들이 — 이 게이트가 지키려는 것(적재가 있는데 열쇠가 없으면 잠긴다)과는
        /// 무관하게 — 무더기로 깨진다. 그래서 **적재 시스템이 존재할 때만 게이트가
        /// 적용된다**로 정했다.
        /// </summary>
        public bool HasOverharvestKey =>
            _loadout == null || _loadout.HasEffect(BuildEffectKind.OverharvestUnlock);

        /// <summary>남은 스핀과 프로파일 상한 중 작은 쪽. 둘 다 지켜야 한다.</summary>
        public int ExtraSpinLimit => _overharvest.EffectiveExtraSpinLimit(SpinsRemaining);

        /// <summary>한 번 더 당길 수 있는가. 상한에 닿으면 레버가 아무것도 하지 않는다.</summary>
        public bool CanTakeExtraSpin => IsOverharvestUnlocked && SpinsRemaining > 0
            && ExtraSpinsTaken < _overharvest.MaxExtraSpins;

        public float PendingAnte => CanTakeExtraSpin && Phase == FloorPhase.Decision
            ? Power * AnteRatioForNextSpin : 0f;
        public float TotalAnte => _totalAnte;
        public float TotalStakedPower => _totalAnte;
        public float ExtraSpinNetPower => _extraSpinNetPower;
        public float NetProfit => _extraSpinNetPower - _totalAnte;

        // ── 남은 스핀 정산 (`T-05`) ──────────────────────────────────────────

        /// <summary>정산 권리가 남아 있는가. 과수확을 고르면 영구히 거짓이 된다.</summary>
        public bool CanSettleRemainingSpins => !_settlementForfeited;

        /// <summary>
        /// 지금 안전 확정하면 받을 정산 금액. **`T-05` 완료 조건이 요구하는
        /// 「조기 달성 시 예상 정산 가치」가 이 값이다** — 선택 전에 보여야 한다.
        /// </summary>
        public float PendingSettlementMoney =>
            _settlementForfeited ? 0f : _settlement.SettlementMoney(SpinsRemaining, RequiredPower);

        /// <summary>정산이 층당 상한에 걸렸는가.</summary>
        public bool SettlementCapped =>
            !_settlementForfeited && _settlement.IsCapped(SpinsRemaining, RequiredPower);

        /// <summary>정산 수치의 출처. 「배선했다」와 「그 값이 쓰였다」를 가른다.</summary>
        public string SettlementSource => _settlement.Source;

        /// <summary>
        /// **지금 확정하면 어떤 상승이 나오는가.** 계기가 「몇 층 오르는가」를 그리려면
        /// 이 값이 필요하다 (`MASTER_PRD` §7 「다음 초과 전력 임계점과 보상」).
        ///
        /// ⚠ **새 계산식을 만들지 않는다.** <see cref="Resolve"/> 가 실제로 부르는 것과
        /// **같은 한 줄**이다 — 인자도 같다. 화면과 실제 상승이 갈라지는 것이
        /// 이 저장소가 가장 두려워하는 종류의 결함이고, 식을 복제하면 반드시 갈라진다.
        /// 확정 뒤에는 이미 확정된 결과를 그대로 돌려준다(다시 계산하지 않는다).
        /// </summary>
        public AscendResult PreviewAscent()
            => _result != null ? _result.Ascent
                               : AscendResult.Calculate(Power, RequiredPower, _thresholds);

        /// <summary>
        /// 임의의 전력·요구로 같은 판정을 돌린다. **검증과 캡처 스윕 전용**이다 —
        /// 「화면이 그리는 층수가 실제 상승과 같은가」를 여러 전력값에서 대조하려면
        /// 세션의 현재 값이 아닌 값을 넣어 볼 수 있어야 한다.
        /// 식은 위와 **완전히 같다** (같은 `_thresholds`, 같은 `Calculate`).
        /// </summary>
        public AscendResult PreviewAscent(float power, float required)
            => AscendResult.Calculate(power, required, _thresholds);
        public float LastAnte => _lastAnte;
        public ResistanceContract SelectedContract => _contract;
        public SpinRuleSet Rules => _rules;
        public IReadOnlyList<SpinResolution> History => _history;
        public FloorResult Result => _result;

        public bool SelectContract(int choiceIndex)
        {
            if (Phase != FloorPhase.ContractSelection) return false;
            if (Plan.ContractChoices == null || choiceIndex < 0 ||
                choiceIndex >= Plan.ContractChoices.Length) return false;

            _contract = Plan.ContractChoices[choiceIndex];
            BuildRules(_contract);
            Phase = FloorPhase.Spinning;
            Events?.Publish(GameEventKind.ContractSelected, Plan.Floor, -1,
                choiceIndex, 0f, _contract.Label);
            return true;
        }

        /// <summary>
        /// Re-enters spinning after the player declines to bank. This explicit
        /// transition keeps Decision meaningful while preserving a state-gated Spin().
        /// </summary>
        public bool PushYourLuck()
        {
            // `CanTakeExtraSpin` 이 해금 임계와 추가 스핀 상한을 **둘 다** 본다.
            // 직전 판본은 `CanBank && SpinsRemaining > 0` 이라 프로파일의 상한이
            // 존재하되 아무것도 막지 않았다 — 상한을 0 으로 내려도 계속 당겨졌다.
            if (Phase != FloorPhase.Decision || !CanTakeExtraSpin || _result != null)
                return false;

            // The ante is paid at choice time, before the engine consumes another
            // random result. PendingAnte is therefore the exact amount removed here.
            // **여기서 정산 권리가 소멸한다** (`T-05`). 스핀 결과가 아니라 **선택**이
            // 권리를 버리는 것이므로, 앤티를 내는 이 순간에 함께 처리한다.
            _settlementForfeited = true;

            float ante = PendingAnte;
            Power -= ante;
            _totalAnte += ante;
            _lastAnte = ante;
            ExtraSpinsTaken++;
            _activeSpinIsExtra = true;
            Phase = FloorPhase.Spinning;
            Events?.Publish(GameEventKind.OverharvestPulled, Plan.Floor, SpinsUsed,
                ExtraSpinsTaken, ante);
            Events?.Publish(GameEventKind.ExtraSpinTaken, Plan.Floor, SpinsUsed,
                ExtraSpinsTaken, Power);
            return true;
        }

        public bool ContinueSpinning() => PushYourLuck();

        public SpinResolution Spin()
        {
            // A rejected action is represented by default(SpinResolution), never an
            // exception; callers can also use TrySpin when they need the bool.
            if (Phase != FloorPhase.Spinning || SpinsRemaining <= 0 || _rules == null)
                return default(SpinResolution);

            // 시드는 순차 스트림이 아니라 (런 시드, 층, 스핀 인덱스) 좌표에서 파생한다.
            // 그래야 "이 층 이 스핀"을 앞선 진행과 무관하게 단독 재현할 수 있다 —
            // TECH_SPEC §7, 파생 규칙의 단일 출처는 SpinSeed다.
            int spinSeed = SpinSeed.Derive(_engine.RunSeed, Plan.Floor, SpinsUsed);
            Events?.Publish(GameEventKind.SpinStarted, Plan.Floor, SpinsUsed,
                SpinsRemaining, Power, _activeSpinIsExtra ? "추가 스핀" : null);

            SpinResolution resolution = _engine.SpinWithSeed(
                spinSeed, _rules, in _contract, in _residual, Plan.Floor, SpinsUsed);
            _history.Add(resolution);
            _residual = resolution.Residual;
            Power += resolution.NetPower;
            if (_activeSpinIsExtra)
            {
                _extraSpinNetPower += resolution.NetPower;
                _activeSpinIsExtra = false;
            }
            SpinsUsed++;

            PublishSpinDetail(in resolution);

            // 🔴 **첫 달성 시점을 여기서만 붙잡는다** (Notion §6.2 / `D-20260805-01`).
            //
            // Mercy 등급은 「첫 달성 시점의 남은 스핀」으로 고정된다. 나중에 결과에서
            // 되계산할 수 없다 — 그때는 이미 더 돌렸는지 여부밖에 남지 않고, 몇 스핀
            // 남기고 처음 채웠는지는 사라진다. 그래서 **그 순간에** 적는다.
            if (_spinsRemainingAtFirstReach < 0 && CanBank)
                _spinsRemainingAtFirstReach = SpinsRemaining;

            if (CanBank || SpinsRemaining == 0)
                Phase = FloorPhase.Decision;
            return resolution;
        }

        /// <summary>
        /// 스핀 하나를 사건으로 쪼갠다. 사운드·승객 반응·텔레메트리가 이 흐름만 보고
        /// 각자 반응하며, 아무도 <see cref="SpinResolution"/>을 다시 해석하지 않는다.
        ///
        /// 순서가 곧 연출 순서다 — 열 공개 → 단계별(영혼·정화) → 잔류 → 임계점 → 종합.
        /// `MASTER_PRD.md` §6이 "시각적으로 생략하지 않는다"를 요구하므로 단계를 접지 않는다.
        /// </summary>
        private void PublishSpinDetail(in SpinResolution resolution)
        {
            GameEventBus bus = Events;
            if (bus == null) return;

            int floor = Plan.Floor;
            int idx = resolution.SpinIndex;

            for (int column = 0; column < SpinBoard.Columns; column++)
                bus.Publish(GameEventKind.ColumnRevealed, floor, idx, column);

            if (resolution.Steps != null)
            {
                foreach (CascadeStep step in resolution.Steps)
                {
                    if (step.NormalSoulsHarvested > 0)
                        bus.Publish(GameEventKind.NormalSoulHarvested, floor, idx,
                            step.NormalSoulsHarvested, step.NormalSoulPower);

                    if (step.Purifies != null)
                    {
                        foreach (PurifyEvent p in step.Purifies)
                        {
                            GameEventKind kind;
                            switch (p.Pattern)
                            {
                                case PatternKind.Line:      kind = GameEventKind.PurifyLine; break;
                                case PatternKind.Cluster:
                                case PatternKind.FullBoard: kind = GameEventKind.PurifyCluster; break;
                                default:                    kind = GameEventKind.PurifyScattered; break;
                            }
                            bus.Publish(kind, floor, idx,
                                p.Cells != null ? p.Cells.Length : 0, p.Power, p.Kind.DisplayName());
                        }
                    }

                    bus.Publish(GameEventKind.CascadeStep, floor, idx, step.Depth, step.StepPower);
                }
            }

            if (resolution.CascadeCapReached)
                bus.Publish(GameEventKind.CascadeCapReached, floor, idx, resolution.ChainDepth);

            if (resolution.Residual.StoredPowerLoss > 0f)
                bus.Publish(GameEventKind.ResidualDamage, floor, idx,
                    resolution.Residual.AbsorberCount, resolution.Residual.StoredPowerLoss,
                    resolution.Residual.Describe());

            AnnounceThresholds();

            bus.Publish(GameEventKind.SpinResolved, floor, idx,
                resolution.ChainDepth, resolution.NetPower, null, resolution);
        }

        /// <summary>
        /// 임계점 100 · 170 · 300%를 처음 넘는 순간에만 알린다.
        /// `MASTER_PRD.md` §7이 요구하는 과수확 잠금 해제도 100% 통과와 같은 사건이다.
        /// </summary>
        private void AnnounceThresholds()
        {
            if (RequiredPower <= 0f) return;
            int percent = (int)(Power / RequiredPower * 100f);

            foreach (int gate in ThresholdGates)
            {
                if (percent < gate || _announcedThreshold >= gate) continue;
                _announcedThreshold = gate;
                Events?.Publish(GameEventKind.PowerThresholdCrossed, Plan.Floor, SpinsUsed - 1,
                    gate, Power);
            }

            // 해금은 임계점 100 과 **같은 사건이 아니다.** 직전 판본은 `gate == 100` 안에서
            // 발행해 `UnlockThreshold` 를 1.20 으로 올려도 100% 에서 해금 신호가 나갔다 —
            // 소리·승객 반응·연출이 전부 이 이벤트를 듣기 때문에 값이 조용히 무시됐다.
            if (!_announcedOverharvestUnlock && IsOverharvestUnlocked)
            {
                _announcedOverharvestUnlock = true;
                Events?.Publish(GameEventKind.OverharvestUnlocked, Plan.Floor, SpinsUsed - 1,
                    (int)Math.Round(_overharvest.UnlockThreshold * 100f), Power);
            }
        }

        private bool _announcedOverharvestUnlock;

        /// <summary>승객이 반응하는 임계점(`MASTER_PRD.md` §9.2 목록의 세 지점).</summary>
        private static readonly int[] ThresholdGates = { 100, 170, 300 };

        // 식을 여기서 다시 쓰지 않고 스냅샷의 것을 부른다. 두 벌로 두면 한쪽만 고쳐도
        // 컴파일이 통과하고, 그때 판돈 표시와 실제 차감이 조용히 갈라진다.
        private float AnteRatioForNextSpin => _overharvest.AnteRatioForPull(ExtraSpinsTaken);

        public bool TrySpin(out SpinResolution resolution)
        {
            if (Phase != FloorPhase.Spinning || SpinsRemaining <= 0 || _rules == null)
            {
                resolution = default(SpinResolution);
                return false;
            }
            resolution = Spin();
            return true;
        }

        public FloorResult Bank()
        {
            if (Phase != FloorPhase.Decision || !CanBank)
                return null;
            return Resolve();
        }

        public FloorResult ForceResolve()
        {
            if (Phase != FloorPhase.Decision || SpinsRemaining > 0)
                return null;
            return Resolve();
        }

        private FloorResult Resolve()
        {
            if (_result != null) return _result;

            // 확정 순간의 적재 상태를 뜬다. `_result` 를 세우기 **전에** 떠야 한다 —
            // `Capacity` 가 `_result != null` 로 분기하므로 순서를 뒤집으면
            // 아직 채우지 않은 `_resolvedCapacity`(0)를 읽는다.
            _resolvedCapacity = _weight.CapacityWith(_loadout != null ? _loadout.TotalCapacityBonus : 0f);
            ResolvedLoadoutShort = _loadout != null ? _loadout.DescribeShort() : null;
            ResolvedLoadoutDetail = _loadout != null ? _loadout.Describe() : null;

            // ── 남은 스핀 정산 (`T-05`) ──
            // **전력에 더하지 않는다.** 정산은 「캐스케이드·임계점·승객·부품 효과를
            // 발동하지 않는다」가 규격이고, 전력에 더하면 임계점을 넘겨 상승 층수를
            // 바꿔 버린다 — 그 순간 「안 돌린 스핀」이 「돌린 스핀」과 같아져
            // 두 선택을 겨루게 만든다는 이 규칙의 목적 자체가 사라진다.
            int settledSpins = _settlementForfeited ? 0 : SpinsRemaining;
            float settlementMoney = _settlementForfeited
                ? 0f : _settlement.SettlementMoney(settledSpins, RequiredPower);
            bool settlementCapped = !_settlementForfeited
                && _settlement.IsCapped(settledSpins, RequiredPower);

            AscendResult ascent = AscendResult.Calculate(Power, RequiredPower, _thresholds);
            _result = new FloorResult(ascent, _totalAnte, ExtraSpinsTaken,
                _extraSpinNetPower, NetProfit,
                settlementMoney, settledSpins, settlementCapped);
            Phase = FloorPhase.Resolved;

            Events?.Publish(GameEventKind.PowerBanked, Plan.Floor, SpinsUsed - 1,
                (int)_result.Band, Power, _result.Band.DisplayName());
            if (!_result.Succeeded)
                Events?.Publish(GameEventKind.CollapseBegan, Plan.Floor, SpinsUsed - 1,
                    (int)_result.Band, Power, _result.FailureReason);
            Events?.Publish(GameEventKind.FloorResolved, Plan.Floor, SpinsUsed - 1,
                _result.FloorsAscended, Power, _result.ToString());
            return _result;
        }

        private void BuildRules(in ResistanceContract contract)
        {
            // FloorPlan owns pool filtering and resistance scaling. Keep this as
            // the single runtime call site, then apply the selected contract once.
            FloorPlan plan = Plan;
            _rules = PrototypeCurriculum.BuildRules(in plan, _balance);
            // 🔴 적재를 넘긴다 (PD-29 안 C). 계약이 **읽기만** 한다 — 적재 효과는 여전히
            //    아래 `ApplyTo` 에서만 규칙에 들어간다. 넘기지 않으면 계약의 조건이
            //    항상 0개를 세어 개편 전과 같아진다.
            _rules.Apply(in contract, _loadout);

            // 발동 순서: 기본값 → 층 규칙 → 계약 → 승객·부품(`SpinRuleSet` 주석).
            // 승객이 계약보다 뒤인 이유는 계약의 곱셈이 승객의 가산 위에 얹히면
            // 같은 조합이 다른 값을 내기 때문이다.
            _loadout?.ApplyTo(_rules);
        }

        /// <summary>
        /// 판정식을 <see cref="WeightSnapshot.RequiredPowerFor"/> 로 넘긴다.
        ///
        /// 여기서 상수를 직접 읽던 판본은 값만 데이터로 옮겨도 소용이 없었다 —
        /// 식이 옛 상수를 계속 보므로 「에셋을 바꿔도 게임이 그대로」가 유지된다.
        /// `static` 을 뗀 것이 그 때문이다. 식은 값을 든 쪽에 있어야 한다.
        /// </summary>
        private float ComputeRequiredPower(in FloorPlan plan, float weight, float capacity)
        {
            return _weight.RequiredPowerFor(plan.RequiredPower, weight, capacity);
        }
    }
}
