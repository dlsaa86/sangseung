// =============================================================
// 상승 밸런스 시뮬레이터 v2 — 기존 게임 구조 전체 반영판
// (승객 23 · 무게 · 동적 강설 · 종 게이지+인터폰 거래 · 연쇄 · 설비 로드아웃)
// 선행: balance_sim.js 로드 필요 (SIM 전역)
// =============================================================
globalThis.SIM2 = (function () {
  var T = SIM.T, N = 15;
  var ADJ = [];
  for (var i = 0; i < N; i++) {
    var r = Math.floor(i / 5), c = i % 5, a = [];
    if (r > 0) a.push(i - 5); if (r < 2) a.push(i + 5);
    if (c > 0) a.push(i - 1); if (c < 4) a.push(i + 1);
    ADJ.push(a);
  }
  var V2 = {
    snowfallBase: 0.08, snowfallPerStage: 0.02, snowfallFrom: 4, snowCapExtra: 4,
    weightTax: 0.05,           // 무게 1당 목표 +5%
    gaugeNeed0: 3, gaugeNeedMax: 7, gaugeGrowth: 1,
    dealLegacyPerRing: 0.25, ringsPerStagePast: 1.0, // 과거 층 거래 누적 근사 (M1 환산)
    dealAcceptPolicy: ['d5', 'd10'] // 이 둘은 거절 (옛 시뮬 정책 계승)
  };

  // ── 승객 23 (시뮬 필드 번역) ──
  var PAX = {
    quar: { nm:'검역관', w:2, wcMul:0.65, quest:2 },
    mine: { nm:'폐광의 광부', w:3, snowfall:-0.20, quest:3 },
    mort: { nm:'장의사', w:4, allBaseX:2, quest:5 },
    metr: { nm:'전력 검침원', w:3, m1Add:1.5, quest:4 },
    swep: { nm:'굴뚝 청소부', w:2, m1Add:0.5, quest:99 },
    clrk: { nm:'명부 서기', w:1, quest:99 },
    plum: { nm:'배관공', w:2, vPairAdd:1.5, compound:{src:'synergy',v:0.012} },
    bee:  { nm:'검은 벌의 양봉업자', w:2, redoLowest:1 },
    gamb: { nm:'손가락 없는 도박사', w:3, luck:2, heaterAux:0.5 },
    endr: { nm:'7734행 수금원', w:3, allBaseAdd:0.5, compound:{src:'deliv',v:0.09} },
    pilg: { nm:'눈먼 순례자', w:2, heaterAux:0.75, chain:{on:'snowfall',fx:'pow',v:0.12}, compound:{src:'eyes',v:0.07} },
    lamp: { nm:'점등원', w:2, snowfall:0.15, compound:{src:'turns',v:0.022} },
    mend: { nm:'수선공', w:2, luck:1 },
    tail: { nm:'재단사', w:1, luck:0, unsim:'각인 적립 미시뮬' },
    wnch: { nm:'권양기 기사', w:4, symAdd:{COAL:2}, carry:12 },
    glaz: { nm:'유리공', w:2, chainA:{on:'snowRemoved',fx:'out',v:0.1}, chainB:{on:'synergy',fx:'purge',cap:2} },
    scri: { nm:'필경사', w:1, dudLuck:1 },
    insp: { nm:'승강기 검사원', w:3, m1Add:2, carry:60 },
    line: { nm:'고압선 전공', w:2, hPairAdd:1.5 },
    seal: { nm:'인장 관리인', w:2, heaterAux:0.5, carry:40 },
    ngwd: { nm:'야간 경비', w:2, snowfall:-0.20 },
    stok: { nm:'소각로 화부', w:3, purgeOnStart:1, m1Add:-0.5 },
    wrck: { nm:'해체공', w:4, hiBaseX:2, compound:{src:'weight',v:0.032} }
  };

  // 복리(compound) 체크포인트 근사: 탑승 층수 = min(4, stage-1)
  function compoundM1(pax, stageId, K, totalW) {
    var est = { synergy: 3, deliv: 2, eyes: K, turns: SIM.STAGES[stageId-1].turns, weight: totalW };
    var s = 0;
    pax.forEach(function (p) {
      if (!p.compound) return;
      var rid = Math.min(4, Math.max(0, stageId - 1));
      s += p.compound.v * (est[p.compound.src] || 0) * rid;
    });
    return s;
  }

  function aggregate(paxIds, equip) {
    var A = { w:0, wcMul:1, m1Add:0, m2Mul:1, allBaseX:1, hiBaseX:1, allBaseAdd:0, symAdd:{},
      hPairAdd:0, vPairAdd:0, heaterAux:0, snowfall:0, luck:0, dudLuck:0, purgeOnStart:0,
      redoLowest:0, chains:[], targetMul:1, pax:[] };
    paxIds.forEach(function (id) {
      var p = PAX[id]; if (!p) return;
      A.pax.push(p);
      A.w += p.w || 0; if (p.wcMul) A.wcMul = Math.min(A.wcMul, p.wcMul);
      A.m1Add += p.m1Add || 0; A.allBaseX *= p.allBaseX || 1; A.hiBaseX *= p.hiBaseX || 1;
      A.allBaseAdd += p.allBaseAdd || 0;
      for (var k in (p.symAdd || {})) A.symAdd[k] = (A.symAdd[k] || 0) + p.symAdd[k];
      A.hPairAdd += p.hPairAdd || 0; A.vPairAdd += p.vPairAdd || 0;
      A.heaterAux += p.heaterAux || 0; A.snowfall += p.snowfall || 0;
      A.luck += p.luck || 0; A.dudLuck += p.dudLuck || 0; A.purgeOnStart += p.purgeOnStart || 0;
      A.redoLowest += p.redoLowest || 0;
      if (p.chain) A.chains.push(p.chain);
      if (p.chainA) A.chains.push(p.chainA);
      if (p.chainB) A.chains.push(p.chainB);
    });
    (equip || {}).m1Add && (A.m1Add += equip.m1Add);
    (equip || {}).m2Mul && (A.m2Mul *= equip.m2Mul);
    if (equip) {
      A.w += equip.w || 0; A.snowfall += equip.snowfall || 0; A.luck += equip.luck || 0;
      A.heaterAux += equip.heaterAux || 0; A.purgeOnStart += equip.purgeOnStart || 0;
      A.targetMul *= equip.targetMul || 1; A.allBaseAdd += equip.allBaseAdd || 0;
      A.hPairAdd += equip.hPairAdd || 0; A.vPairAdd += equip.vPairAdd || 0;
      for (var k2 in (equip.symAdd || {})) A.symAdd[k2] = (A.symAdd[k2] || 0) + equip.symAdd[k2];
      (equip.chains || []).forEach(function (c) { A.chains.push(c); });
    }
    return A;
  }

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

  // 보드 1턴 계산 (v1 로직 + 승객/설비 보정)
  function computeTurnV2(board, A) {
    function isSnow(i) { return board[i] === 'S'; }
    function isEmpty(i) { return board[i] === null; }
    function inst(i) { return (board[i] && board[i] !== 'S') ? board[i] : null; }
    function adjCount(i, pred) {
      var n = 0; for (var j = 0; j < ADJ[i].length; j++) if (pred(ADJ[i][j])) n++;
      return n;
    }
    var SY = { BATT:1, SOLAR:1, COAL:1, HEATER:1, PLOW:1, RECY:1 };
    var baseSum = 0, ampN = 0, i, s;
    for (i = 0; i < N; i++) { s = inst(i); if (s && s.k === 'AMP') ampN += (s.lv || 1); }
    var vals = [];
    for (i = 0; i < N; i++) {
      s = inst(i); if (!s || !SY[s.k]) { if (s) s._v = 0; continue; }
      var sc = Math.pow(2, (s.lv || 1) - 1), v = 0;
      if (s.k === 'BATT') v = T.battBase * sc;
      else if (s.k === 'SOLAR') v = T.solarBase * sc + Math.min(T.solarCap * sc, adjCount(i, isEmpty) * T.solarAux * sc);
      else if (s.k === 'COAL') v = T.coalBase * sc;
      else if (s.k === 'HEATER') v = (T.heaterBase * sc) + adjCount(i, isSnow) * (T.heaterAux * sc + A.heaterAux);
      else if (s.k === 'PLOW') v = T.plowBase * sc;
      else if (s.k === 'RECY') v = T.recyBase * sc + s.stacks * T.recyGain * sc;
      v += (A.symAdd[s.k] || 0) + A.allBaseAdd;
      if ((s.k === 'COAL' || s.k === 'HEATER' || s.k === 'RECY')) v *= A.hiBaseX;
      v *= A.allBaseX;
      var sn = adjCount(i, isSnow);
      if (sn > 0 && s.k !== 'HEATER') v = Math.max(0, v - T.snowBasePenalty);
      s._v = v; vals.push(v); baseSum += v;
    }
    // 방향 인접쌍 보너스 (배관공/고압선)
    if (A.hPairAdd || A.vPairAdd) {
      for (i = 0; i < N; i++) {
        var s2 = inst(i); if (!s2 || !SY[s2.k]) continue;
        if (A.hPairAdd && (i % 5) < 4) { var rr = inst(i + 1); if (rr && SY[rr.k]) baseSum += A.hPairAdd; }
        if (A.vPairAdd && i < 10) { var dd = inst(i + 5); if (dd && SY[dd.k]) baseSum += A.vPairAdd; }
      }
    }
    if (A.redoLowest && vals.length) baseSum += Math.min.apply(null, vals) * A.redoLowest;
    var m1 = 1 + A.m1Add, transSum = 0;
    for (i = 0; i < N; i++) {
      s = inst(i); if (!s) continue;
      var sc2 = Math.pow(2, (s.lv || 1) - 1), ctb = 0;
      if (s.k === 'CAP') ctb = T.capAdd * sc2;
      else if (s.k === 'TRANS') {
        ctb = T.transPer * sc2 * adjCount(i, function (j) { var o = inst(j); return o && SY[o.k]; });
        if (adjCount(i, isSnow) > 0) ctb *= T.snowMultFactor;
        transSum += ctb;
      }
      else if (s.k === 'AMP') ctb = ((ampN >= T.ampCount) ? T.ampHigh : T.ampLow) * sc2;
      m1 += ctb;
    }
    var m2 = A.m2Mul, coreOn = 0, coreCnt = 0;
    for (i = 0; i < N; i++) {
      s = inst(i); if (!s || s.k !== 'CORE') continue;
      coreCnt++;
      var cmul = T.coreMul + 0.5 * ((s.lv || 1) - 1);
      if (baseSum >= T.coreThreshold) { m2 *= cmul; coreOn++; }
    }
    return { p: baseSum * m1 * m2, baseSum: baseSum, m1: m1, m2: m2, coreOn: coreOn, coreCnt: coreCnt, transSum: transSum, ampSet: ampN >= T.ampCount };
  }

  // 운(luck): 기여 최저 심볼을 최적 빈칸으로 L회 재배치 (그리디)
  function applyLuck(board, L, A) {
    for (var t = 0; t < L; t++) {
      var worstI = -1, worstV = 1e9, bestE = -1;
      for (var i = 0; i < N; i++) {
        var s = board[i];
        if (s && s !== 'S' && s._v !== undefined && s._v < worstV) { worstV = s._v; worstI = i; }
      }
      if (worstI < 0) break;
      // 운 = 맞교환: 원작 "운은 뽑힌 것들을 자리에 앉혀 준다" — 심볼 1개를 최적 칸과 스왑 (빈칸 불요)
      var sym = board[worstI];
      var bestGain = 0;
      for (var e2 = 0; e2 < N; e2++) {
        if (e2 === worstI || board[e2] === 'S') continue;
        var snowAdj = 0;
        ADJ[e2].forEach(function (j) { if (board[j] === 'S') snowAdj++; });
        var here = 0;
        ADJ[worstI].forEach(function (j) { if (board[j] === 'S') here++; });
        var gain = (sym.k === 'HEATER') ? (snowAdj - here) * (T.heaterAux + A.heaterAux)
                                        : (here - snowAdj) * 1.0;
        if (gain > bestGain) { bestGain = gain; bestE = e2; }
      }
      if (bestE >= 0) { var tmp = board[bestE]; board[bestE] = board[worstI]; board[worstI] = tmp; }
    }
  }

  // ── 스테이지 1회 (v2 전체 규칙) ──
  function runStageOnceV2(deck, stage, A, rng, dealsOn) {
    var snow = stage.snow.slice();
    for (var pu = 0; pu < A.purgeOnStart && snow.length; pu++) snow.splice(Math.floor(rng() * snow.length), 1);
    var target = stage.target * (1 + V2.weightTax * A.w * A.wcMul) * A.targetMul;
    var snowCap = stage.snow.length + V2.snowCapExtra;
    var cum = 0, gauge = 0, rings = 0, dealM1 = 0, dealM2 = 1, dealSnowNext = 0, luckBonus = 0;
    var coreOnSum = 0, coreCntSum = 0, dealLog = [];
    var sfP = (stage.id >= V2.snowfallFrom)
      ? Math.max(0, Math.min(0.6, V2.snowfallBase + V2.snowfallPerStage * (stage.id - V2.snowfallFrom) + A.snowfall))
      : Math.max(0, A.snowfall);
    for (var t = 0; t < stage.turns; t++) {
      // 강설 (턴 시작)
      var snowfell = false;
      if (rng() < sfP && snow.length < snowCap) {
        var freeAll = [];
        for (var q = 0; q < N; q++) if (snow.indexOf(q) < 0) freeAll.push(q);
        if (freeAll.length > 1) { snow.push(freeAll[Math.floor(rng() * freeAll.length)]); snowfell = true; }
      }
      var free = [];
      for (var i2 = 0; i2 < N; i2++) if (snow.indexOf(i2) < 0) free.push(i2);
      var pool = shuffle(deck.slice(), rng);
      var chosen = pool.slice(0, Math.min(pool.length, free.length));
      var cells = shuffle(free.slice(), rng);
      var board = new Array(N).fill(null);
      snow.forEach(function (si) { board[si] = 'S'; });
      chosen.forEach(function (s3, j) { board[cells[j]] = s3; });
      var pre = computeTurnV2(board, A); // 1차 계산 (운 판단용 _v 채움)
      var L = A.luck + luckBonus + ((A.dudLuck && pre.coreOn === 0) ? A.dudLuck : 0);
      if (L > 0) applyLuck(board, L, A);
      var res = computeTurnV2(board, A);
      res.m1 += dealM1; // 거래 영구 배율
      var powBonus = 1;
      var synergy = (res.coreOn > 0) || (res.transSum >= 1.0) || res.ampSet;
      var snowRemovedThisTurn = 0;
      // 연쇄 (듣기 → 효과)
      A.chains.forEach(function (ch) {
        var fire = (ch.on === 'snowfall' && snowfell) || (ch.on === 'synergy' && synergy) ||
                   (ch.on === 'snowRemoved' && snowRemovedThisTurn > 0);
        if (!fire) return;
        if (ch.fx === 'pow') powBonus += ch.v;
        else if (ch.fx === 'purge' && snow.length) { if ((ch._used||0) < (ch.cap||99)) { ch._used=(ch._used||0)+1; snow.splice(Math.floor(rng() * snow.length), 1); snowRemovedThisTurn++; } }
        else if (ch.fx === 'out') dealM1 += ch.v;
        else if (ch.fx === 'bellg') gauge += ch.v;
      });
      cum += res.baseSum * res.m1 * res.m2 * dealM2 * powBonus;
      coreOnSum += res.coreOn; coreCntSum += res.coreCnt;
      // 턴 종료: 석탄/제설/압축 (v1 규칙)
      chosen.forEach(function (s4) {
        if (s4.k === 'COAL') { s4.charges--; if (s4.charges <= 0) { var ix = deck.indexOf(s4); if (ix >= 0) deck.splice(ix, 1); } }
      });
      chosen.forEach(function (s5, j) {
        if (s5.k === 'PLOW') for (var pi = 0; pi < (s5.lv || 1) && snow.length > 0; pi++) { snow.splice(Math.floor(rng() * snow.length), 1); snowRemovedThisTurn++; }
        if (s5.k === 'RECY') {
          var pos = cells[j];
          var genCount = deck.filter(function (x) { return ['BATT','SOLAR','COAL','HEATER'].indexOf(x.k) >= 0; }).length;
          if (genCount > T.recyMinGen) {
            var best = null, bestV = 1e9;
            ADJ[pos].forEach(function (aj) {
              var o = board[aj];
              if (o && o !== 'S' && ['BATT','SOLAR','COAL','HEATER'].indexOf(o.k) >= 0 && o._v < bestV) { best = o; bestV = o._v; }
            });
            if (best) { var ix2 = deck.indexOf(best); if (ix2 >= 0) { deck.splice(ix2, 1); s5.stacks++; } }
          }
        }
      });
      // 종 게이지 적립 (결정론 이벤트)
      var add = 0;
      if (res.coreOn > 0) add++;
      if (snowRemovedThisTurn > 0) add++;
      if (res.ampSet) add++;
      gauge += add;
      var need = Math.min(V2.gaugeNeedMax, V2.gaugeNeed0 + rings * V2.gaugeGrowth);
      if (dealsOn && gauge >= need) {
        gauge -= need; rings++;
        var pool2 = (add >= 2) ? 'WELL' : (rings >= 4 ? 'GRAND' : (add >= 3 ? 'RED' : 'NORMAL'));
        var deal = drawDeal(pool2, rng);
        dealLog.push(pool2 + ':' + deal.id);
        // 효과 적용 (수락 정책: d5/d10 거절)
        if (V2.dealAcceptPolicy.indexOf(deal.id) < 0) {
          if (deal.m1) dealM1 += deal.m1;
          if (deal.m2) dealM2 *= deal.m2;
          if (deal.snowNow) for (var sn2 = 0; sn2 < deal.snowNow && snow.length < snowCap; sn2++) {
            var fa = []; for (var q2 = 0; q2 < N; q2++) if (snow.indexOf(q2) < 0) fa.push(q2);
            if (fa.length > 1) snow.push(fa[Math.floor(rng() * fa.length)]);
          }
          if (deal.purge) for (var pg = 0; pg < deal.purge && snow.length; pg++) snow.splice(Math.floor(rng() * snow.length), 1);
          if (deal.luck) luckBonus += deal.luck;
          if (deal.targetMul) target *= deal.targetMul; // 다음 층 대신 즉시 근사
        }
      }
    }
    return { cum: cum, target: target, win: cum >= target, rings: rings,
      coreOnRate: coreCntSum ? coreOnSum / coreCntSum : 0, deals: dealLog };
  }

  // 거래 풀 (기계 효과 번역판 — 07_deals.csv와 1:1)
  var DEAL_POOLS = {
    NORMAL: [
      { id:'d1', m1:1, snowNow:2 }, { id:'d2', targetMul:0.7 }, { id:'d3' },
      { id:'d4', targetMul:0.8 }, { id:'d5' }, { id:'d6', luck:2, snowNow:1 },
      { id:'d7', m1:0.5, snowNow:1 }, { id:'d8' }, { id:'d9', snowNow:1 }, { id:'d10' }
    ],
    WELL: [ { id:'w1', m1:1 }, { id:'w2' }, { id:'w3', luck:2 }, { id:'w4', luck:3 } ],
    GRAND: [ { id:'x1', m2:1.4 }, { id:'x2', m1:0.6 }, { id:'x3' }, { id:'x4' } ],
    RED: [ { id:'r1', m2:1.5, snowNow:3 }, { id:'r2', targetMul:0.5, snowNow:4 }, { id:'r3', snowNow:2 }, { id:'r4', m2:1.3 } ]
  };
  function drawDeal(pool, rng) {
    var arr = DEAL_POOLS[pool] || DEAL_POOLS.NORMAL;
    return arr[Math.floor(rng() * arr.length)];
  }

  function evalV2(deckSpec, stageId, paxIds, equip, trials, seed, dealsOn) {
    if (dealsOn === undefined) dealsOn = true;
    var stage = SIM.STAGES[stageId - 1];
    var wins = 0, evSum = 0, tgtSum = 0, ringSum = 0, cums = [];
    var A0 = aggregate(paxIds, equip);
    A0.m1Add += compoundM1(A0.pax, stageId, stage.snow.length, A0.w);
    if (dealsOn) A0.m1Add += V2.dealLegacyPerRing * V2.ringsPerStagePast * Math.max(0, stageId - 1);
    for (var tr = 0; tr < trials; tr++) {
      var rng = mulberry32(seed * 100003 + tr);
      var deck = SIM.parseDeck(deckSpec);
      var A = JSON.parse(JSON.stringify(A0)); A.pax = A0.pax;
      var r = runStageOnceV2(deck, stage, A, rng, dealsOn);
      if (r.win) wins++;
      evSum += r.cum; tgtSum += r.target; ringSum += r.rings; cums.push(r.cum);
    }
    cums.sort(function (a, b) { return a - b; });
    return {
      stage: stageId, deck: deckSpec, pax: paxIds.join('+') || '-', n: trials,
      winRate: +(wins / trials * 100).toFixed(1),
      ev: +(evSum / trials).toFixed(0),
      effTarget: +(tgtSum / trials).toFixed(0),
      p5: +cums[Math.floor(trials * 0.05)].toFixed(0),
      p95: +cums[Math.floor(trials * 0.95)].toFixed(0),
      rings: +(ringSum / trials).toFixed(1)
    };
  }

  return { V2: V2, PAX: PAX, evalV2: evalV2, aggregate: aggregate, DEAL_POOLS: DEAL_POOLS };
})();
