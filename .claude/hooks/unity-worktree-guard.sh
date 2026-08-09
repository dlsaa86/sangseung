#!/usr/bin/env bash
# PreToolUse gate on anything that rewrites Unity YAML on disk behind the editor's back.
#
# Why: Unity watches the files backing the open scene. When one changes underneath it,
# the next asset refresh (which fires on focus regain, so it lands right when automation
# hands control back) throws a modal — "The open scene(s) have been modified externally,
# Reload / Ignore". A modal blocks Unity's main thread, so every subsequent MCP call sits
# until its 120s timeout and the session stalls waiting on a click nobody is there to make.
# unity-preflight.sh cannot catch this: the process is alive and healthy, just blocked.
#
# So the modal has to be prevented, not detected. Two things raise it in this repo:
#   1. git commands that rewrite the working tree (checkout/restore/stash/reset/rebase/...)
#   2. writing *.unity / *.prefab / *.mat as text instead of going through the editor
#
# Both are only dangerous while an editor owns the project — with Unity closed they are
# ordinary file edits, so the guard is a no-op then.
#
# ── 2026-08-04 복원 시 좁힌 것 ────────────────────────────────────────────────
# 직전 판본은 명령 문자열에 "git checkout" 이 **포함되기만 해도** 막았다. 그래서
# 이 저장소의 문서를 쓰는 것 자체가 막혔다 — CLAUDE.md 와 진행 기록에는 복구 절차로
# `git checkout HEAD -- <파일>` 이 그대로 적혀 있기 때문이다. heredoc 으로 그 문장을
# 쓰려는 순간 훅이 실제 체크아웃으로 오인했다. 방향은 옳았고 판정이 거칠었다.
#
# 이제 **실제로 실행되는 git 호출만** 본다. 판정 전에 두 가지를 지운다.
#   · heredoc 본문 — 파일에 쓰는 텍스트지 실행되는 명령이 아니다
#   · 따옴표 안 — echo "git reset --hard" 는 문자열이다
# 그리고 세그먼트의 **첫 토큰이 git 일 때만** 위험 동사를 찾는다. `echo git checkout`
# 은 첫 토큰이 echo 라 걸리지 않는다.
#
# Escape hatch: SKIP_UNITY_GUARD=1 in the command, for when the editor is deliberately
# parked (scene closed, or you will press Reload yourself).
#
# Exit 0 with no output = allow. Exit 0 with JSON = deny.

set -uo pipefail

ROOT="${CLAUDE_PROJECT_DIR:-$(pwd)}"
INSTANCE="$ROOT/Library/EditorInstance.json"

INPUT="$(cat)"

deny() {
  MSG="$1" node -e '
    process.stdout.write(JSON.stringify({
      hookSpecificOutput: {
        hookEventName: "PreToolUse",
        permissionDecision: "deny",
        permissionDecisionReason: process.env.MSG
      }
    }));
  '
  exit 0
}

field() {
  MSG="$INPUT" KEY="$1" node -e '
    try {
      const j = JSON.parse(process.env.MSG || "{}");
      const path = process.env.KEY.split(".");
      let v = j;
      for (const k of path) v = (v == null ? v : v[k]);
      process.stdout.write(v == null ? "" : String(v));
    } catch (e) { process.stdout.write(""); }
  '
}

# --- Is an editor actually holding this project open? -------------------------------
# Same reasoning as unity-preflight.sh: EditorInstance.json survives a crash, so only a
# live PID proves ownership. Duplicated rather than sourced because that hook denies.
[ -s "$INSTANCE" ] || exit 0
PID="$(grep -oE '"process_id"[[:space:]]*:[[:space:]]*[0-9]+' "$INSTANCE" | grep -oE '[0-9]+$' | head -1)"
[ -n "${PID:-}" ] || exit 0

if command -v tasklist >/dev/null 2>&1; then
  tasklist //FI "PID eq $PID" 2>/dev/null | grep -qE "[[:space:]]$PID[[:space:]]" || exit 0
elif command -v ps >/dev/null 2>&1; then
  ps -p "$PID" >/dev/null 2>&1 || exit 0
fi

TOOL="$(field tool_name)"

# --- Direct text edits of scene-backing YAML ----------------------------------------
case "$TOOL" in
  Write|Edit|MultiEdit)
    TARGET="$(field tool_input.file_path)"
    case "$TARGET" in
      *.unity|*.prefab|*.mat)
        deny "Unity(PID $PID)가 이 프로젝트를 열고 있는 동안 $TOOL 로 '$TARGET' 을 직접 고칠 수 없다.

씬·프리팹·머티리얼은 fileID 로 서로를 참조하는 YAML 이라 텍스트로 고치면 참조가 조용히 어긋나고, 파일이 바뀐 것을 에디터가 감지하면 '외부에서 수정됨' 모달이 떠서 메인 스레드가 막힌다. 그 시점부터 MCP 호출은 전부 120초 타임아웃까지 매달린다.

대신 에디터 안에서 바꿀 것:
  mcp__unity-mcp__Unity_RunCommand 로 EditorSceneManager / SerializedObject 를 거쳐 수정 후 SaveScene

에디터를 닫고 작업할 거라면 Unity 를 종료한 뒤 재시도한다."
        ;;
    esac
    exit 0
    ;;
  Bash) ;;
  *) exit 0 ;;
esac

CMD="$(field tool_input.command)"
case "$CMD" in
  *SKIP_UNITY_GUARD=1*) exit 0 ;;
esac

# --- 실제로 실행되는 git 호출만 골라낸다 ---------------------------------------------
# 출력: 위험 동사 하나 (없으면 빈 문자열).
VERB="$(CMD="$CMD" node -e '
  const raw = process.env.CMD || "";

  // ① heredoc 본문 제거 — 디스크에 쓰이는 텍스트지 실행되는 명령이 아니다.
  //    이 저장소의 문서에는 복구 절차로 git 명령이 그대로 적혀 있다.
  let s = raw;
  const hd = /<<-?\s*(["\x27]?)([A-Za-z_][A-Za-z0-9_]*)\1/g;
  let m, cuts = [];
  while ((m = hd.exec(raw)) !== null) {
    const tag = m[2];
    const term = new RegExp("^\\s*" + tag + "\\s*$", "m");
    const rest = raw.slice(hd.lastIndex);
    const t = rest.search(term);
    cuts.push([hd.lastIndex, t === -1 ? raw.length : hd.lastIndex + t]);
  }
  for (const [a, b] of cuts.reverse()) s = s.slice(0, a) + " ".repeat(b - a) + s.slice(b);

  // ② 따옴표 안 제거 — echo "git reset --hard" 는 문자열이다.
  //    자리를 공백으로 채워 세그먼트 구분자는 보존한다.
  let t2 = "", q = null;
  for (const c of s) {
    if (q) { t2 += " "; if (c === q) q = null; continue; }
    if (c === "\"" || c === "\x27") { q = c; t2 += " "; continue; }
    t2 += c;
  }

  // ③ 명령 구분자로 쪼개 각 세그먼트의 첫 토큰을 본다.
  const RISKY = {
    checkout: 1, restore: 1, switch: 1, clean: 1, revert: 1,
    merge: 1, rebase: 1, pull: 1, "cherry-pick": 1, apply: 1, am: 1,
    reset: 1, stash: 1
  };

  for (let seg of t2.split(/;|&&|\|\||\||\n/)) {
    seg = seg.trim().replace(/^(?:[A-Za-z_][A-Za-z0-9_]*=\S*\s+)+/, "");
    const tok = seg.split(/\s+/).filter(Boolean);
    if (!tok.length) continue;

    const head = tok[0].replace(/\\/g, "/").split("/").pop().replace(/\.exe$/i, "");
    if (head !== "git") continue;   // echo / printf / cat 등은 여기서 걸러진다

    // 전역 옵션을 건너뛰고 서브커맨드를 찾는다 (git -C <path> checkout ...)
    let i = 1, sub = null;
    while (i < tok.length) {
      const x = tok[i];
      if (x === "-C" || x === "-c" || x === "--git-dir" || x === "--work-tree") { i += 2; continue; }
      if (x.startsWith("-")) { i += 1; continue; }
      sub = x; i += 1; break;
    }
    if (!sub || !RISKY[sub]) continue;
    const rest = tok.slice(i);

    // 워킹 트리를 건드리지 않는 형태는 통과시킨다.
    if (sub === "checkout" && (rest.includes("-b") || rest.includes("-B"))) continue;
    if (sub === "stash" && (rest[0] === "list" || rest[0] === "show")) continue;
    if (sub === "reset" && !rest.some(a => a === "--hard" || a === "--merge" || a === "--keep")) continue;

    process.stdout.write(sub);
    process.exit(0);
  }
  process.stdout.write("");
')"

[ -n "$VERB" ] || exit 0

# A pathspec-limited command only matters if the paths can reach Unity YAML. This is what
# keeps font-atlas-guard.sh's `git checkout HEAD -- <*.asset>` recovery working unblocked.
if [[ "$CMD" == *" -- "* ]]; then
  # Tokenized in node rather than by shell word splitting: Unity asset paths routinely
  # contain spaces ("Noto Sans SDF.asset"), and splitting one of those yields a bare
  # "Noto" that looks like a directory, denying a recovery that was perfectly safe.
  RISKY="$(PATHS="${CMD#* -- }" node -e '
    const raw = process.env.PATHS || "";
    const tokens = raw.match(/"[^"]*"|'"'"'[^'"'"']*'"'"'|\S+/g) || [];
    const risky = tokens
      .map(t => t.replace(/^["'"'"']|["'"'"']$/g, ""))
      .filter(t => t.length > 0)
      .some(t =>
        /\.(unity|prefab|mat)$/i.test(t) ||   // scene-backing YAML
        !/\.[A-Za-z0-9]+$/.test(t)            // a directory or glob: could sweep in anything
      );
    process.stdout.write(risky ? "1" : "0");
  ')"
  [ "$RISKY" = "0" ] && exit 0
fi

deny "Unity(PID $PID)가 이 프로젝트를 열고 있는 동안 'git $VERB' 로 워킹 트리를 되돌릴 수 없다.

씬 파일이 디스크에서 바뀌면 다음 에셋 리프레시에 '외부에서 수정됨(Reload/Ignore)' 모달이 뜨고, 모달이 Unity 메인 스레드를 잡는 순간 이후 MCP 호출은 전부 120초 타임아웃까지 매달린다. 세션이 거기서 멈춘다.

셋 중 하나를 고를 것:
  1. 씬을 건드리지 않는 되돌리기라면 경로를 명시한다:  git $VERB HEAD -- <파일>
  2. 에디터 안에서 되돌린다 (Unity_RunCommand → 수정 → SaveScene)
  3. Unity 를 종료하고 실행한 뒤 다시 연다

에디터가 파킹돼 있어 모달이 뜰 수 없는 상황이면 명령 앞에 SKIP_UNITY_GUARD=1 을 붙인다."
