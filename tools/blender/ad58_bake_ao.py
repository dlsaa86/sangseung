# AD58 — 캐빈 오브젝트의 앰비언트 오클루전을 구워 Unity 로 내보낸다.
#
# 왜 AO 만 굽는가
# ---------------
# 독립 평가의 1순위 지적이 「드리운 그림자가 없다」였고, 인계 문서는 `AD53_BAKEALL` 의
# `use_pass_direct/indirect` 를 켜는 것을 가장 싼 해법으로 제안했다. **그건 못 쓴다.**
# 빛을 알베도에 구우면 Unity 가 그 위에 런타임 조명을 또 곱한다. 이중 조명을 피하려면
# 런타임 점광을 줄여야 하는데, `AscendStylized.shader` 주석이 명시한다 —
# 「추가 광원이 위험 단계를 나른다」. 그걸 건드려 실패한 기록이 세 번 있다.
#
# **AO 는 조명과 무관하다.** 곱해도 이중 조명이 되지 않고, 런타임 점광이 그대로 산다.
# 접촉 음영과 접지감만 얻고 위험 단계 연출은 손대지 않는다.
# (2026-08-08 사용자 결정: 「그냥 SSAO 정도만 구워라, 광원 그림자는 굽지 마라」)
#
# `AD53_BAKEALL` 과 다른 점
# -------------------------
#   bake_type      DIFFUSE(순수 알베도) → AO
#   samples        1 → SAMPLES          AO 는 광선을 쏘므로 1 샘플이면 순수 노이즈다
#   colorspace     sRGB → Non-Color     AO 는 색이 아니라 데이터다
#   AO 거리        씬 기본 10.0 → AO_DIST
#                  캐빈이 3 m 인데 광선이 10 m 를 날면 모든 면이 방 전체를 보게 되어
#                  접촉 음영 없이 균일하게 탁해진다. 접지감은 짧은 거리에서만 나온다.
#
# ⚠⚠ 블렌더가 `~/Documents` 를 건드리게 하지 않는다 — 메인 스레드가 통째로 멈춘다
# ------------------------------------------------------------------------------
# 2026-08-08 에 이걸로 블렌더를 완전히 잠갔다. 증상과 원인:
#
#   · `exec(open("~/Documents/GitHub/Upandup_DDD/tools/blender/ad58_bake_ao.py").read())`
#   · 블렌더 CPU 0.0%, 브리지 무응답, AppleScript `activate` 조차 2분간 매달림
#   · `sample <pid>` 로 뜬 메인 스레드 스택:
#         py_timer_execute → builtin_exec → _io_open → open() → __open()  ← 커널에서 정지
#
# macOS TCC 가 「Blender 가 Documents 폴더에 접근하려 합니다」 대화상자를 띄웠고,
# 블렌더가 백그라운드라 그게 안 보인 채 `open()` 이 사람의 클릭을 기다린 것이다.
# **Unity 모달 잠금과 정확히 같은 구조다** — 프로세스는 멀쩡히 살아 있고 그냥 막혀 있다.
#
# 그래서 이 파이프라인은 TCC 를 아예 안 건드린다.
#   ① 이 파일을 블렌더가 열지 않는다. 호출자가 **내용을 읽어 소켓으로 보낸다.**
#   ② 굽기 결과도 `~/Documents` 에 쓰지 않는다. **임시 폴더에 쓰고 호출자가 옮긴다.**
#
# ⚠ Windows 절대경로도 하드코딩하지 않는다
# ----------------------------------------
# `AD48_EXPORT` 와 `AD53_BAKEALL` 은 `DST = r"B:\PROJECT_NEW_BORN\..."` 를 박아 두어
# Mac 에서 그대로 돌리면 실패한다(인계 문서 3번). 여기서는 호출자가 경로를 넘긴다.

import bpy
import os
import tempfile
import time

# 호출자가 `set_out_dir()` 로 덮어쓴다. 기본값은 어느 기기에서나 쓸 수 있는 임시 폴더다.
OUT_DIR = os.path.join(tempfile.gettempdir(), "ad58_ao")

SAMPLES = 64        # M5 GPU 기준. 노이즈가 남으면 올린다.
AO_DIST = 0.6       # m. 접촉 음영용. 씬 기본 10.0 은 이 크기의 방에 맞지 않는다.
MARGIN = 8          # AD53 이 실측으로 고른 값(0/2/4/8/16 중 4·8 이 동률, 8 이 밉에 유리).


def set_out_dir(path):
    """굽기 결과를 쓸 폴더를 정한다. **`~/Documents` 아래를 넘기지 않는다** (위 경고 참조)."""
    global OUT_DIR
    OUT_DIR = path
    return dst_dir()


def dst_dir():
    os.makedirs(OUT_DIR, exist_ok=True)
    return OUT_DIR


def setup(samples=SAMPLES, ao_dist=AO_DIST):
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    try:
        sc.cycles.device = 'GPU'
    except Exception:
        pass
    sc.cycles.samples = samples
    sc.cycles.bake_type = 'AO'

    # 노이즈를 줄인다 — AO 는 반구 샘플링이라 저샘플에서 얼룩진다.
    try:
        sc.cycles.use_denoising = True
    except Exception:
        pass

    # **이 한 줄이 결과를 좌우한다.** 거리가 길면 접촉 음영이 사라지고 방이 통째로 탁해진다.
    sc.world.light_settings.distance = ao_dist

    bk = sc.render.bake
    bk.use_selected_to_active = False
    bk.use_clear = True
    bk.margin = margin_value()


def margin_value():
    return MARGIN


def bake_one(objname, res):
    """오브젝트 하나의 AO 를 구워 PNG 로 저장하고 (경로, 초) 를 돌려준다.

    UV 활성 상태와 임시 노드는 반드시 원래대로 돌려놓는다 — `AD53_BAKEALL` 이
    같은 이유로 `pa`/`pr` 을 저장해 두고 복원한다. 사용자의 저작 UV 를 덮어써서
    블렌더 쪽을 깨뜨린 사고가 이미 한 번 있었다(커밋 207a80e).
    """
    t0 = time.time()
    o = bpy.data.objects[objname]
    me = o.data
    uvs = me.uv_layers

    prev_active = uvs.active.name
    prev_render = next((u.name for u in uvs if u.active_render), None)
    uvs.active = uvs["UVBake"]
    uvs["UVBake"].active_render = True

    img = bpy.data.images.new("__ao", res, res, alpha=False, float_buffer=False)
    img.colorspace_settings.name = 'Non-Color'      # AO 는 색이 아니라 데이터다.

    added = []
    for m in me.materials:
        if not m or not m.use_nodes:
            continue
        n = m.node_tree.nodes.new('ShaderNodeTexImage')
        n.image = img
        n.select = True
        m.node_tree.nodes.active = n
        added.append((m, n))

    for ob in bpy.context.view_layer.objects:
        ob.select_set(False)
    o.hide_set(False)
    o.select_set(True)
    bpy.context.view_layer.objects.active = o

    bpy.ops.object.bake(type='AO')

    out = os.path.join(dst_dir(), "AO_" + objname.replace(".", "_") + ".png")
    img.filepath_raw = out
    img.file_format = 'PNG'
    img.save()

    for m, n in added:
        m.node_tree.nodes.remove(n)
    bpy.data.images.remove(img)
    o.select_set(False)

    uvs.active = uvs[prev_active]
    if prev_render:
        uvs[prev_render].active_render = True

    return out, time.time() - t0
