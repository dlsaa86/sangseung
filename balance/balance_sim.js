// =============================================================
// 상승(Ascent) 5x3 슬롯 덱빌딩 — 밸런스 검증 시뮬레이터 v1.0
// 사용법(Node/REPL 공용): eval(fs.readFileSync('balance_sim.js','utf8'))
//   SIM.evalDeck('BATT:5,CAP:1', 1, 2000, 42)
//   SIM.placementGap('BATT:6,SOLAR:2,TRANS:3,CAP:1', 5, 30000, 7)
// 규칙 버전: RULESET v2.0 (기존 구조 전체 반영판 — v2 규칙은 balance_sim_v2.js)
// =============================================================
globalThis.SIM = (function () {
  // ---- 튜닝 다이얼 (아이템 시트와 동일 값이어야 함) ----
  var T = {
    battBase: 2,
    solarBase: 2, solarAux: 1, solarCap: 2,      // 인접 빈칸당 +1W, 최대 +2W
    coalBase: 7, coalCharges: 3,                  // 3회 발동 후 소멸
    capAdd: 0.5,
    transPer: 0.3,                                // 인접 GEN 1개당 +0.3x
    ampLow: 0.2, ampHigh: 0.5, ampCount: 3,       // 보드 위 안테나 레벨합 3 이상시 강화
    coreMul: 2.0, coreThreshold: 12,              // 기초합 임계 이상시 x2
    heaterBase: 1, heaterAux: 2.5,                // 인접 눈 1개당 +2.5W
    plowBase: 1, plowPerTurn: 1,                  // 턴 종료시 눈 1개 제거
    recyBase: 1, recyGain: 2, recyMinGen: 3,      // 인접 최저 GEN 파괴, 영구 +2W
    snowBasePenalty: 1,                           // 인접 눈 존재 시 기초 -1W 고정 (HEAT 면역)
    snowMultFactor: 0.5                           // 눈 인접 '인접조건' 배율(TRANS)만 x0.5
  };

  var SYMS = {
    BATT:   { cls: 'gen',  tags: ['GEN','BATTERY'] },
    SOLAR:  { cls: 'gen',  tags: ['GEN','SOLAR'] },
    COAL:   { cls: 'gen',  tags: ['GEN','BURN'] },
    CAP:    { cls: 'mult', tags: ['CIRCUIT'] },
    TRANS:  { cls: 'mult', tags: ['CIRCUIT'] },
    AMP:    { cls: 'mult', tags: ['CIRCUIT','ANTENNA'] },
    CORE:   { cls: 'mul2', tags: ['CORE'] },
    HEATER: { cls: 'gen',  tags: ['GEN','HEAT'] },
    PLOW:   { cls: 'gen',  tags: ['MACHINE'] },
    RECY:   { cls: 'gen',  tags: ['MACHINE'] }
  };

  // ---- 그리드 (row0=상단) ----
  var N = 15;
  var ADJ = [];
  for (var i = 0; i < N; i++) {
    var r = Math.floor(i / 5), c = i % 5, a = [];
    if (r > 0) a.push(i - 5);
    if (r < 2) a.push(i + 5);
    if (c > 0) a.push(i - 1);
    if (c < 4) a.push(i + 1);
    ADJ.push(a);
  }

  // ---- 스테이지 (Stage Data Table와 1:1) ----
  var STAGES = [
    { id: 1,  target: 100,  turns: 5, snow: [] },
    { id: 2,  target: 200,  turns: 5, snow: [] },
    { id: 3,  target: 300,  turns: 5, snow: [0] },
    { id: 4,  target: 450,  turns: 6, snow: [0, 14] },
    { id: 5,  target: 650,  turns: 6, snow: [0, 5, 10] },
    { id: 6,  target: 700,  turns: 6, snow: [0, 4, 10, 14] },
    { id: 7,  target: 2400, turns: 7, snow: [2, 6, 7, 8, 12] },
    { id: 8,  target: 2600, turns: 7, snow: [1, 6, 11, 3, 8, 13] },
    { id: 9,  target: 3000, turns: 8, snow: [0, 1, 2, 3, 4, 5, 9] },
    { id: 10, target: 7500, turns: 8, snow: [0, 1, 2, 3, 4, 5, 7, 9] }
  ];

  function mulberry32(a) {
    return function () {
      a |= 0; a = (a + 0x6D2B79F5) | 0;
      var t = Math.imul(a ^ (a >>> 15), 1 | a);
      t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
      return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
  }
  function shuffle(arr, rng) {
    for (var i = arr.length - 1; i > 0; i--) {
      var j = Math.floor(rng() * (i + 1));
      var t2 = arr[i]; arr[i] = arr[j]; arr[j] = t2;
    }
    return arr;
  }
  function parseDeck(spec) {
    // 'BATT:5,CAP@2:1' -> 인스턴스 배열 (@N = 합성 레벨, 기본 1)
    var out = [];
    spec.split(',').forEach(function (kv) {
      var p = kv.trim().split(':');
      var key = p[0], n = parseInt(p[1], 10);
      var k = key, lv = 1;
      if (key.indexOf('@') >= 0) { var q = key.split('@'); k = q[0]; lv = parseInt(q[1], 10); }
      for (var i = 0; i < n; i++) out.push({ k: k, lv: lv, charges: k === 'COAL' ? T.coalCharges : -1, stacks: 0 });
    });
    return out;
  }

  // ---- 1턴 전력 계산: (기초합) x (1 + 합연산) x (곱연산) ----
  function computeTurn(board) {
    function isSnow(i) { return board[i] === 'S'; }
    function isEmpty(i) { return board[i] === null; }
    function inst(i) { return (board[i] && board[i] !== 'S') ? board[i] : null; }
    function adjCount(i, pred) {
      var n = 0; for (var j = 0; j < ADJ[i].length; j++) if (pred(ADJ[i][j])) n++;
      return n;
    }
    var baseSum = 0, ampN = 0, i, s, d;
    for (i = 0; i < N; i++) { s = inst(i); if (s && s.k === 'AMP') ampN += (s.lv || 1); }
    // pass1: 기초
    for (i = 0; i < N; i++) {
      s = inst(i); if (!s) continue; d = SYMS[s.k]; if (d.cls !== 'gen') continue;
      var sc = Math.pow(2, (s.lv || 1) - 1); // 합성 레벨: 수치 x2/레벨
      var v = 0;
      if (s.k === 'BATT') v = T.battBase * sc;
      else if (s.k === 'SOLAR') v = T.solarBase * sc + Math.min(T.solarCap * sc, adjCount(i, isEmpty) * T.solarAux * sc);
      else if (s.k === 'COAL') v = T.coalBase * sc;
      else if (s.k === 'HEATER') v = T.heaterBase * sc + adjCount(i, isSnow) * T.heaterAux * sc;
      else if (s.k === 'PLOW') v = T.plowBase * sc;
      else if (s.k === 'RECY') v = T.recyBase * sc + s.stacks * T.recyGain * sc;
      var sn = adjCount(i, isSnow);
      if (sn > 0 && d.tags.indexOf('HEAT') < 0) v = Math.max(0, v - T.snowBasePenalty);
      s._v = v; baseSum += v;
    }
    // pass2: 1차 합연산 배율
    var m1 = 1;
    for (i = 0; i < N; i++) {
      s = inst(i); if (!s) continue; d = SYMS[s.k]; if (d.cls !== 'mult') continue;
      var sc2 = Math.pow(2, (s.lv || 1) - 1);
      var ctb = 0;
      if (s.k === 'CAP') ctb = T.capAdd * sc2;
      else if (s.k === 'TRANS') ctb = T.transPer * sc2 * adjCount(i, function (j) {
        var o = inst(j); return o && SYMS[o.k].tags.indexOf('GEN') >= 0;
      });
      else if (s.k === 'AMP') ctb = ((ampN >= T.ampCount) ? T.ampHigh : T.ampLow) * sc2;
      // 눈 감쇠는 '인접 조건' 배율(TRANS)에만 적용 — 평면 배율(CAP/AMP)은 위치 무관 정체성 유지
      if (s.k === 'TRANS' && adjCount(i, isSnow) > 0) ctb *= T.snowMultFactor;
      m1 += ctb;
    }
    // pass3: 2차 곱연산 (조건부)
    var m2 = 1, coreOn = 0, coreCnt = 0;
    for (i = 0; i < N; i++) {
      s = inst(i); if (!s || s.k !== 'CORE') continue;
      coreCnt++;
      var cmul = T.coreMul + 0.5 * ((s.lv || 1) - 1); // 코어는 레벨당 +0.5x (곱연산 폭주 방지)
      if (baseSum >= T.coreThreshold) { m2 *= cmul; coreOn++; }
    }
    return { p: baseSum * m1 * m2, baseSum: baseSum, m1: m1, m2: m2, coreOn: coreOn, coreCnt: coreCnt };
  }

  // ---- 스테이지 1회 실행 ----
  function runStageOnce(deck, stage, rng) {
    var snow = stage.snow.slice();
    var cum = 0, coreOnSum = 0, coreCntSum = 0, turnPs = [];
    for (var t = 0; t < stage.turns; t++) {
      var free = [];
      for (var i = 0; i < N; i++) if (snow.indexOf(i) < 0) free.push(i);
      var pool = shuffle(deck.slice(), rng);
      var chosen = pool.slice(0, Math.min(pool.length, free.length));
      var cells = shuffle(free.slice(), rng);
      var board = new Array(N).fill(null);
      snow.forEach(function (si) { board[si] = 'S'; });
      chosen.forEach(function (s2, j) { board[cells[j]] = s2; });
      var res = computeTurn(board);
      cum += res.p; turnPs.push(res.p);
      coreOnSum += res.coreOn; coreCntSum += res.coreCnt;
      // 턴 종료 효과
      chosen.forEach(function (s3) {
        if (s3.k === 'COAL') {
          s3.charges--;
          if (s3.charges <= 0) { var ix = deck.indexOf(s3); if (ix >= 0) deck.splice(ix, 1); }
        }
      });
      chosen.forEach(function (s4, j) {
        if (s4.k === 'PLOW') {
          for (var pi = 0; pi < (s4.lv || 1) && snow.length > 0; pi++) snow.splice(Math.floor(rng() * snow.length), 1);
        }
        if (s4.k === 'RECY') {
          var pos = cells[j];
          var genCount = deck.filter(function (x) { return SYMS[x.k].tags.indexOf('GEN') >= 0; }).length;
          if (genCount > T.recyMinGen) {
            var best = null, bestV = 1e9;
            ADJ[pos].forEach(function (aj) {
              var o = board[aj];
              if (o && o !== 'S' && SYMS[o.k].tags.indexOf('GEN') >= 0 && o._v < bestV) { best = o; bestV = o._v; }
            });
            if (best) { var ix2 = deck.indexOf(best); if (ix2 >= 0) { deck.splice(ix2, 1); s4.stacks++; } }
          }
        }
      });
    }
    return { cum: cum, coreOnRate: coreCntSum ? coreOnSum / coreCntSum : 0, turnPs: turnPs };
  }

  // ---- 다회 평가 ----
  function evalDeck(spec, stageId, trials, seed) {
    var stage = STAGES[stageId - 1];
    var wins = 0, evSum = 0, coreSum = 0, cums = [];
    for (var tr = 0; tr < trials; tr++) {
      var rng = mulberry32(seed * 100003 + tr);
      var deck = parseDeck(spec);
      var r = runStageOnce(deck, stage, rng);
      if (r.cum >= stage.target) wins++;
      evSum += r.cum; coreSum += r.coreOnRate; cums.push(r.cum);
    }
    cums.sort(function (a, b) { return a - b; });
    var K = stage.snow.length;
    var ev = evSum / trials;
    return {
      stage: stageId, target: stage.target, K: K, deck: spec, n: trials,
      winRate: +(wins / trials * 100).toFixed(1),
      ev: +ev.toFixed(0),
      p5: +cums[Math.floor(trials * 0.05)].toFixed(0),
      p95: +cums[Math.floor(trials * 0.95)].toFixed(0),
      achievedBPI: +(ev / (stage.turns * (15 - K) * 2)).toFixed(2),
      reqBPI: +(stage.target / (stage.turns * (15 - K) * 2)).toFixed(2),
      coreOnRate: +(coreSum / trials * 100).toFixed(0)
    };
  }

  // ---- 배치 격차 검증: 동일 심볼 세트, 배치만 변경, 1턴 출력 분포 ----
  function placementGap(spec, stageId, nSample, seed) {
    var stage = STAGES[stageId - 1];
    var snow = stage.snow;
    var free = [];
    for (var i = 0; i < N; i++) if (snow.indexOf(i) < 0) free.push(i);
    var baseDeck = parseDeck(spec);
    if (baseDeck.length > free.length) baseDeck = baseDeck.slice(0, free.length);
    var rng = mulberry32(seed);
    var ps = [];
    for (var s5 = 0; s5 < nSample; s5++) {
      var cells = shuffle(free.slice(), rng);
      var board = new Array(N).fill(null);
      snow.forEach(function (si) { board[si] = 'S'; });
      baseDeck.forEach(function (sym, j) { board[cells[j]] = sym; });
      ps.push(computeTurn(board).p);
    }
    ps.sort(function (a, b) { return a - b; });
    var mn = ps[0], mx = ps[ps.length - 1];
    return {
      stage: stageId, K: snow.length, set: spec, n: nSample,
      min: +mn.toFixed(1), max: +mx.toFixed(1),
      mean: +(ps.reduce(function (a, b) { return a + b; }, 0) / ps.length).toFixed(1),
      p5: +ps[Math.floor(nSample * 0.05)].toFixed(1),
      p95: +ps[Math.floor(nSample * 0.95)].toFixed(1),
      maxOverMin: +(mx / Math.max(0.001, mn)).toFixed(2),
      p95OverP5: +(ps[Math.floor(nSample * 0.95)] / Math.max(0.001, ps[Math.floor(nSample * 0.05)])).toFixed(2)
    };
  }

  function setTargets(arr) { arr.forEach(function (t, i) { STAGES[i].target = t; }); }
  function setTune(patch) { Object.keys(patch).forEach(function (k) { T[k] = patch[k]; }); }

  return { T: T, STAGES: STAGES, evalDeck: evalDeck, placementGap: placementGap, setTargets: setTargets, setTune: setTune, parseDeck: parseDeck };
})();
