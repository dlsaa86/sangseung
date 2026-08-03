# -*- coding: utf-8 -*-
"""
ELV_CABIN — 엘리베이터 카 내부 셸 절차적 조립기 (Blender)

레퍼런스: docs/references/elevator/mood_20260804_user_cabin.png
치수 정본: Assets/Prototype_Elevator/Scripts/Art/ReferenceRoomSpec.cs
           InteriorWidth 4.0 / InteriorDepth 4.6 / InteriorHeight 2.9 / ShellThickness 0.16
           GateOpeningWidth 2.1 / GateOpeningHeight 2.35

여기서 치수를 임의로 바꾸지 않는다 — 바꾸면 Unity 배치와 어긋난다.

좌표계 (Blender, Z-up) — export 시 Y-up/−Z-forward 로 나가므로 Unity 와 1:1 이다.
    x  좌우 폭      −2.00 … +2.00   (Unity x 그대로)
    y  앞뒤 깊이    −2.30 … +2.30   (Unity z 그대로. +y = 장치 벽)
    z  높이         0 … 2.90        (Unity y 그대로)

왜 지오메트리로 만드나:
    직전 Unity 씬은 평면 쿼드에 타일링 텍스처만 붙어 있어 그림자가 생기지 않았다.
    레퍼런스의 인상은 「오목한 패널 + 돌출 스타일 + 리벳 띠」가 만드는 실제 음영이다.
    텍스처로는 그 음영이 안 나온다. 그래서 면 분할을 진짜 형상으로 만든다.

실행:
    exec(open(r"B:\\PROJECT_NEW_BORN\\Upandup_DDD\\tools\\blender\\build_cabin.py",
              encoding="utf-8").read())
    build_all()
    export()
"""

import bpy
import bmesh
import math
from mathutils import Vector, Matrix

# ══════════════════════════════════════════════════════════════════════════
# 스펙 — ReferenceRoomSpec.cs 미러 (변경 금지)
# ══════════════════════════════════════════════════════════════════════════
IW = 4.0            # InteriorWidth
ID = 4.6            # InteriorDepth
IH = 2.9            # InteriorHeight
TH = 0.16           # ShellThickness

HX = IW * 0.5       # ±2.00  좌우 벽 안쪽 면
HY = ID * 0.5       # ±2.30  앞뒤 벽 안쪽 면

GATE_W = 2.1        # GateOpeningWidth
GATE_H = 2.35       # GateOpeningHeight

MACHINE_X = -0.35   # MachineCenterX
MACHINE_W = 1.876   # 캐비닛 폭 (build_ovenharvest 의 W)
MACHINE_H = 1.800   # 캐비닛 높이 (build_ovenharvest 의 H)
MACHINE_BOTTOM = 0.20   # MachineBottomGap

COLL_NAME = "ELV_CABIN"

# ══════════════════════════════════════════════════════════════════════════
# 벽면 입면 — 레퍼런스의 수평 3분할
# ══════════════════════════════════════════════════════════════════════════
# 레퍼런스 벽은 아래에서 위로: 걸레받이 → 낮은 패널 띠 → 허리 레일 →
# 큰 패널 띠(눈높이) → 어깨 레일 → 상부 패널 띠 → 코니스.
# 돌출(proud)은 실내 쪽으로 나오는 양, 오목(recess)은 벽 속으로 들어가는 양이다.

KICK_H     = 0.145      # 걸레받이 높이
KICK_PROUD = 0.030

BAND1_TOP  = 1.020      # 낮은 패널 띠 상단
RAIL1_H    = 0.115      # 허리 레일 높이
BAND2_TOP  = 2.170      # 큰 패널 띠 상단  (허리레일 위 ~1.0m — 눈높이 1.62 를 감싼다)
RAIL2_H    = 0.105      # 어깨 레일 높이
CORNICE_H  = 0.055      # 코니스 높이

RAIL_PROUD    = 0.028
CORNICE_PROUD = 0.034

# 스타일은 「목공 몰딩」이 아니라 「철판 사이의 이음 스트랩」이다 — 얇고 낮게 둔다.
STILE_W      = 0.072    # 패널 사이 돌출 스타일 폭
STILE_PROUD  = 0.009
PANEL_RECESS = 0.020    # 패널 바닥이 벽 안쪽 면보다 들어간 깊이
PANEL_BEVEL  = 0.018    # 오목 패널의 경사 테두리 폭

RIVET_R      = 0.017
RIVET_PROUD  = 0.011
RIVET_PITCH  = 0.165
RIVET_SIDES  = 6

CORNER_POST  = 0.105    # 모서리 앵글 한 변
CORNER_PROUD = 0.020

# 바닥
FLOOR_BORDER = 0.55     # 가장자리 테두리판 폭
FLOOR_LIP    = 0.028    # 테두리판이 중앙판보다 높은 양
STUD_PITCH   = 0.115    # 중앙 트레드판 돌기 간격
STUD_R       = 0.030
STUD_H       = 0.011
STUD_SIDES   = 6

# 천장
BEAM_DROP    = 0.135    # 천장에서 내려온 보의 깊이
BEAM_W       = 0.175

# 장치 베이 — 레퍼런스의 「무거운 문틀에 박힌 3×3」을 만드는 오목 벽감
# 베이가 커지면 후면 벽의 패널 필드가 좌우 좁은 띠로 쪼그라든다.
# 장치 자체는 명세가 벽 폭의 45~52% 로 못박고 있으므로(ReferenceRoomSpec),
# 줄일 수 있는 건 여백과 프레임뿐이다.
BAY_MARGIN   = 0.105    # 캐비닛 둘레 여백
BAY_DEPTH    = 0.105    # 벽 속으로 파인 깊이
BAY_FRAME    = 0.105    # 벽감 둘레 돌출 프레임 폭
BAY_FRAME_PROUD = 0.042

# UV 타일 밀도. 1.0 이면 텍스처 한 장이 1m 를 덮는다.
# 0.62 → 약 1.6m 마다 한 번 반복 → 결로 읽히고 패턴으로 읽히지 않는다.
UV_PER_METER = 0.62

MAT_IRON   = "ELV_Iron"        # 주 재질 — 산화 철 패널
MAT_DARK   = "ELV_IronDark"    # 그림자 면 · 오목 바닥 · 천장
MAT_TREAD  = "ELV_Tread"       # 바닥 트레드
MAT_TRIM   = "ELV_Trim"        # 레일 · 스타일 · 프레임 (약간 밝은 마모 철)
MAT_GLASS  = "ELV_LampGlass"   # 램프 유리 (발광)
MAT_BRASS  = "ELV_Brass"       # 램프 케이지 · 국소 액센트

TAU = math.tau


# ══════════════════════════════════════════════════════════════════════════
# 메시 빌더
# ══════════════════════════════════════════════════════════════════════════
class MB:
    """하나의 오브젝트가 될 bmesh 를 쌓는다. 면마다 재질 슬롯을 지정한다."""

    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []

    def slot(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def box(self, center, size, mat=MAT_IRON):
        cx, cy, cz = center
        hx, hy, hz = size[0] * 0.5, size[1] * 0.5, size[2] * 0.5
        if hx <= 0 or hy <= 0 or hz <= 0:
            return
        idx = self.slot(mat)
        o = [(-1, -1, -1), (1, -1, -1), (1, 1, -1), (-1, 1, -1),
             (-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)]
        vs = [self.bm.verts.new((cx + dx * hx, cy + dy * hy, cz + dz * hz))
              for dx, dy, dz in o]
        for quad in [(0, 1, 2, 3), (4, 5, 6, 7), (0, 1, 5, 4),
                     (1, 2, 6, 5), (2, 3, 7, 6), (3, 0, 4, 7)]:
            f = self.bm.faces.new([vs[i] for i in quad])
            f.material_index = idx

    def frustum(self, center, r0, r1, h, axis='Z', sides=8, mat=MAT_IRON,
                cap_lo=True, cap_hi=True, phase=0.0):
        """r0(하단) → r1(상단) 절두체. 원통은 r0==r1 로 부른다."""
        idx = self.slot(mat)
        cx, cy, cz = center
        lo, hi = -h * 0.5, h * 0.5

        def pt(ang, r, t):
            a, b = math.cos(ang) * r, math.sin(ang) * r
            if axis == 'Z':
                return (cx + a, cy + b, cz + t)
            if axis == 'Y':
                return (cx + a, cy + t, cz + b)
            return (cx + t, cy + a, cz + b)

        ring_lo, ring_hi = [], []
        for i in range(sides):
            ang = phase + TAU * i / sides
            ring_lo.append(self.bm.verts.new(pt(ang, r0, lo)))
            ring_hi.append(self.bm.verts.new(pt(ang, r1, hi)))
        for i in range(sides):
            j = (i + 1) % sides
            f = self.bm.faces.new([ring_lo[i], ring_lo[j], ring_hi[j], ring_hi[i]])
            f.material_index = idx
        if cap_lo and r0 > 1e-6:
            f = self.bm.faces.new(ring_lo)
            f.material_index = idx
        if cap_hi and r1 > 1e-6:
            f = self.bm.faces.new(ring_hi)
            f.material_index = idx

    def box_uvs(self, scale=UV_PER_METER):
        """
        월드 좌표 큐브 투영. 축 정렬 상자만 쓰므로 이음매가 생기지 않는다.
        텍스처는 **구조가 아니라 결(rust grain)만** 담당한다 — 리벳·패널은
        이미 형상으로 있으므로 텍스처에 또 들어 있으면 두 번 겹쳐 지저분해진다.
        """
        uvl = self.bm.loops.layers.uv.new("UVMap")
        for f in self.bm.faces:
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for lp in f.loops:
                co = lp.vert.co
                if ax == 0:
                    u, v = co.y, co.z
                elif ax == 1:
                    u, v = co.x, co.z
                else:
                    u, v = co.x, co.y
                lp[uvl].uv = (u * scale, v * scale)

    def finish(self, name, coll):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        self.box_uvs()
        me = bpy.data.meshes.new(name)
        self.bm.to_mesh(me)
        self.bm.free()
        for mname in self.mats:
            me.materials.append(get_mat(mname))
        me.shade_flat()
        ob = bpy.data.objects.new(name, me)
        coll.objects.link(ob)
        return ob


class Plane:
    """
    벽 국소 좌표 (u, v, d) → 월드 (x, y, z).
        u  벽을 따라가는 방향
        v  위쪽
        d  실내에서 벽 속으로 들어가는 방향 (양수 = 벽 속, 음수 = 실내로 돌출)
    """

    def __init__(self, origin, u_axis, d_axis):
        self.o = Vector(origin)
        self.U = Vector(u_axis)
        self.V = Vector((0, 0, 1))
        self.D = Vector(d_axis)

    def pt(self, u, v, d):
        return self.o + self.U * u + self.V * v + self.D * d

    def box(self, mb, u0, u1, v0, v1, d0, d1, mat=MAT_IRON):
        a = self.pt(u0, v0, d0)
        b = self.pt(u1, v1, d1)
        center = ((a.x + b.x) * .5, (a.y + b.y) * .5, (a.z + b.z) * .5)
        size = (abs(b.x - a.x), abs(b.y - a.y), abs(b.z - a.z))
        # 벽 두께 방향의 축은 size 가 0 이 되지 않게 d0!=d1 로 부른다
        mb.box(center, size, mat)

    def rivet(self, mb, u, v, mat=MAT_TRIM):
        c = self.pt(u, v, -RIVET_PROUD * 0.5)
        axis = 'X' if abs(self.D.x) > 0.5 else 'Y'
        mb.frustum(c, RIVET_R, RIVET_R * 0.72, RIVET_PROUD, axis=axis,
                   sides=RIVET_SIDES, mat=mat, phase=math.pi / RIVET_SIDES)

    def rivet_row(self, mb, u0, u1, v, mat=MAT_TRIM):
        span = u1 - u0
        n = max(2, int(span / RIVET_PITCH))
        step = span / n
        for i in range(n + 1):
            self.rivet(mb, u0 + step * i, v, mat)


# ══════════════════════════════════════════════════════════════════════════
# 재질
# ══════════════════════════════════════════════════════════════════════════
# 값 폭을 좁게 잡는다. 레퍼런스의 모든 면은 서로 몇 % 안쪽이고, 면 분할은
# 밝기 차가 아니라 **음영**으로만 읽힌다. 직전 판본은 TRIM 이 IRON 보다 40% 밝아
# 스타일이 나무 몰딩처럼 도드라졌다 — 산업용 철판 셸이 아니라 실내 목공이 됐다.
_MAT_BASE = {
    MAT_IRON:  (0.075, 0.066, 0.056, 0.88),
    MAT_DARK:  (0.045, 0.041, 0.038, 0.93),
    MAT_TREAD: (0.058, 0.051, 0.044, 0.82),
    MAT_TRIM:  (0.088, 0.077, 0.063, 0.80),
    MAT_BRASS: (0.185, 0.140, 0.078, 0.58),
    MAT_GLASS: (0.900, 0.720, 0.430, 0.35),
}


def get_mat(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    r, g, b, rough = _MAT_BASE.get(name, (0.1, 0.1, 0.1, 0.8))
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (r, g, b, 1.0)
        bsdf.inputs["Roughness"].default_value = rough
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 0.0 if name == MAT_GLASS else 0.85
        if name == MAT_GLASS and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (1.0, 0.78, 0.45, 1.0)
            bsdf.inputs["Emission Strength"].default_value = 6.0
    return m


# ══════════════════════════════════════════════════════════════════════════
# 패널 필드 — 레퍼런스의 오목 패널 + 돌출 스타일
# ══════════════════════════════════════════════════════════════════════════
def panel_field(mb, pl, u0, u1, v0, v1, target_panel_w=0.66):
    """
    [u0,u1] × [v0,v1] 구간을 세로 오목 패널로 채운다.
    스타일(패널 사이 돌출 부재)이 먼저 서고, 그 사이가 파인다.
    """
    span = u1 - u0
    if span <= STILE_W * 2.2 or (v1 - v0) <= PANEL_BEVEL * 2.5:
        # 너무 좁으면 통짜 판으로 둔다
        pl.box(mb, u0, u1, v0, v1, 0.0, TH, MAT_IRON)
        return
    n = max(1, int(round((span - STILE_W) / (target_panel_w + STILE_W))))
    pw = (span - STILE_W * (n + 1)) / n
    if pw < 0.14:
        n = max(1, n - 1)
        pw = (span - STILE_W * (n + 1)) / n

    # 벽 본체 (패널 바닥까지)
    pl.box(mb, u0, u1, v0, v1, PANEL_RECESS, TH, MAT_DARK)

    for i in range(n + 1):
        su = u0 + i * (pw + STILE_W)
        # 돌출 스타일
        pl.box(mb, su, su + STILE_W, v0, v1, -STILE_PROUD, PANEL_RECESS, MAT_TRIM)
        if i < n:
            pu0, pu1 = su + STILE_W, su + STILE_W + pw
            # 패널 상하 경사 테두리 — 오목 패널의 「액자」
            pl.box(mb, pu0, pu1, v0, v0 + PANEL_BEVEL, 0.0, PANEL_RECESS, MAT_IRON)
            pl.box(mb, pu0, pu1, v1 - PANEL_BEVEL, v1, 0.0, PANEL_RECESS, MAT_IRON)


def wall_elevation(mb, pl, u0, u1, v_top=IH, panel_w=0.66, rivets=True):
    """바닥 v=0 부터 v_top 까지 레퍼런스의 수평 3분할 입면을 세운다."""
    # 걸레받이
    pl.box(mb, u0, u1, 0.0, KICK_H, -KICK_PROUD, TH, MAT_TRIM)
    if rivets:
        pl.rivet_row(mb, u0 + 0.10, u1 - 0.10, KICK_H * 0.52)

    b1_top = min(BAND1_TOP, v_top)
    if b1_top > KICK_H + 0.05:
        panel_field(mb, pl, u0, u1, KICK_H, b1_top, panel_w)
    if v_top <= BAND1_TOP:
        return

    # 허리 레일
    r1_top = min(BAND1_TOP + RAIL1_H, v_top)
    pl.box(mb, u0, u1, BAND1_TOP, r1_top, -RAIL_PROUD, TH, MAT_TRIM)
    if rivets:
        pl.rivet_row(mb, u0 + 0.10, u1 - 0.10, BAND1_TOP + RAIL1_H * 0.5)
    if v_top <= BAND1_TOP + RAIL1_H:
        return

    # 큰 패널 띠 (눈높이)
    b2_top = min(BAND2_TOP, v_top)
    if b2_top > r1_top + 0.05:
        panel_field(mb, pl, u0, u1, r1_top, b2_top, panel_w)
    if v_top <= BAND2_TOP:
        return

    # 어깨 레일
    r2_top = min(BAND2_TOP + RAIL2_H, v_top)
    pl.box(mb, u0, u1, BAND2_TOP, r2_top, -RAIL_PROUD, TH, MAT_TRIM)
    if rivets:
        pl.rivet_row(mb, u0 + 0.10, u1 - 0.10, BAND2_TOP + RAIL2_H * 0.5)
    if v_top <= BAND2_TOP + RAIL2_H:
        return

    # 상부 패널 띠 + 코니스
    corn_lo = max(r2_top, v_top - CORNICE_H)
    if corn_lo > r2_top + 0.04:
        panel_field(mb, pl, u0, u1, r2_top, corn_lo, panel_w * 0.92)
    pl.box(mb, u0, u1, corn_lo, v_top, -CORNICE_PROUD, TH, MAT_TRIM)


# ══════════════════════════════════════════════════════════════════════════
# 모듈
# ══════════════════════════════════════════════════════════════════════════
def build_wall_rear(coll):
    """장치 벽 (+y). 중앙 왼쪽에 장치가 박히는 오목 베이를 판다."""
    mb = MB()
    pl = Plane((0, HY, 0), (1, 0, 0), (0, 1, 0))

    bay_w = MACHINE_W + BAY_MARGIN * 2
    bay_h = MACHINE_H + BAY_MARGIN * 2
    bu0 = MACHINE_X - bay_w * 0.5
    bu1 = MACHINE_X + bay_w * 0.5
    bv0 = MACHINE_BOTTOM - BAY_MARGIN
    bv1 = bv0 + bay_h

    # 베이 좌·우 남는 폭
    wall_elevation(mb, pl, -HX, bu0 - BAY_FRAME, IH, panel_w=0.60)
    wall_elevation(mb, pl, bu1 + BAY_FRAME, HX, IH, panel_w=0.60)
    # 베이 아래 · 위
    wall_elevation(mb, pl, bu0 - BAY_FRAME, bu1 + BAY_FRAME, bv0 - BAY_FRAME,
                   panel_w=0.60, rivets=False)
    panel_field(mb, pl, bu0 - BAY_FRAME, bu1 + BAY_FRAME, bv1 + BAY_FRAME, IH - CORNICE_H, 0.60)
    pl.box(mb, bu0 - BAY_FRAME, bu1 + BAY_FRAME, IH - CORNICE_H, IH,
           -CORNICE_PROUD, TH, MAT_TRIM)

    # 오목 베이 바닥판
    pl.box(mb, bu0, bu1, bv0, bv1, BAY_DEPTH, TH, MAT_DARK)
    # 베이 둘레 돌출 프레임 — 레퍼런스의 「무거운 문틀」
    for (fu0, fu1, fv0, fv1) in [
        (bu0 - BAY_FRAME, bu1 + BAY_FRAME, bv0 - BAY_FRAME, bv0),
        (bu0 - BAY_FRAME, bu1 + BAY_FRAME, bv1, bv1 + BAY_FRAME),
        (bu0 - BAY_FRAME, bu0, bv0, bv1),
        (bu1, bu1 + BAY_FRAME, bv0, bv1),
    ]:
        pl.box(mb, fu0, fu1, fv0, fv1, -BAY_FRAME_PROUD, TH, MAT_TRIM)
    # 프레임 리벳
    pl.rivet_row(mb, bu0 - BAY_FRAME * 0.5, bu1 + BAY_FRAME * 0.5, bv0 - BAY_FRAME * 0.5)
    pl.rivet_row(mb, bu0 - BAY_FRAME * 0.5, bu1 + BAY_FRAME * 0.5, bv1 + BAY_FRAME * 0.5)
    for v in _span(bv0 + 0.10, bv1 - 0.10, 0.30):
        pl.rivet(mb, bu0 - BAY_FRAME * 0.5, v)
        pl.rivet(mb, bu1 + BAY_FRAME * 0.5, v)

    return mb.finish("ELV_Wall_Rear", coll)


def build_wall_front(coll):
    """플레이어 뒤쪽 벽 (−y). 개구부가 없으므로 통짜 입면."""
    mb = MB()
    pl = Plane((0, -HY, 0), (1, 0, 0), (0, -1, 0))
    wall_elevation(mb, pl, -HX, HX, IH, panel_w=0.66)
    return mb.finish("ELV_Wall_Front", coll)


def build_wall_right(coll):
    """벤치가 붙는 벽 (+x)."""
    mb = MB()
    pl = Plane((HX, 0, 0), (0, 1, 0), (1, 0, 0))
    wall_elevation(mb, pl, -HY, HY, IH, panel_w=0.68)
    return mb.finish("ELV_Wall_Right", coll)


def build_wall_left(coll):
    """가위문이 있는 벽 (−x). 개구부 2.1 × 2.35 를 비운다."""
    mb = MB()
    pl = Plane((-HX, 0, 0), (0, 1, 0), (-1, 0, 0))
    gh = GATE_W * 0.5

    wall_elevation(mb, pl, -HY, -gh, IH, panel_w=0.58)
    wall_elevation(mb, pl, gh, HY, IH, panel_w=0.58)

    # 개구부 위 인방
    panel_field(mb, pl, -gh, gh, GATE_H + 0.12, IH - CORNICE_H, 0.58)
    pl.box(mb, -gh, gh, GATE_H, GATE_H + 0.12, -RAIL_PROUD, TH, MAT_TRIM)
    pl.rivet_row(mb, -gh + 0.10, gh - 0.10, GATE_H + 0.06)
    pl.box(mb, -gh, gh, IH - CORNICE_H, IH, -CORNICE_PROUD, TH, MAT_TRIM)

    # 개구부 좌·우 문설주 (실내로 살짝 돌출)
    for su in (-gh, gh - 0.075):
        pl.box(mb, su, su + 0.075, 0.0, GATE_H + 0.12, -0.055, TH, MAT_TRIM)
    for v in _span(0.16, GATE_H - 0.06, 0.28):
        pl.rivet(mb, -gh + 0.038, v)
        pl.rivet(mb, gh - 0.038, v)

    return mb.finish("ELV_Wall_Left", coll)


def build_corner_posts(coll):
    """네 모서리 앵글 — 셸이 조립품임을 말한다."""
    mb = MB()
    p = CORNER_POST
    for sx in (-1, 1):
        for sy in (-1, 1):
            x = sx * HX
            y = sy * HY
            mb.box((x - sx * p * 0.5, y + sy * CORNER_PROUD * 0.5,
                    IH * 0.5), (p, CORNER_PROUD, IH), MAT_TRIM)
            mb.box((x - sx * CORNER_PROUD * 0.5, y - sy * p * 0.5,
                    IH * 0.5), (CORNER_PROUD, p, IH), MAT_TRIM)
    return mb.finish("ELV_CornerPosts", coll)


def build_floor(coll):
    """테두리판 + 돌기 트레드 중앙판. 레퍼런스 바닥의 핵심은 이 대비다."""
    mb = MB()
    cu = HX - FLOOR_BORDER
    cv = HY - FLOOR_BORDER

    # 셸 바닥 슬래브
    mb.box((0, 0, -TH * 0.5), (IW, ID, TH), MAT_DARK)

    # 테두리판 4장
    for (x0, x1, y0, y1) in [
        (-HX, HX, cv, HY), (-HX, HX, -HY, -cv),
        (-HX, -cu, -cv, cv), (cu, HX, -cv, cv),
    ]:
        mb.box(((x0 + x1) * .5, (y0 + y1) * .5, FLOOR_LIP * 0.5),
               (x1 - x0, y1 - y0, FLOOR_LIP), MAT_TRIM)

    # 테두리판 리벳
    for x in _span(-HX + 0.22, HX - 0.22, 0.34):
        for y in (HY - FLOOR_BORDER * 0.5, -HY + FLOOR_BORDER * 0.5):
            mb.frustum((x, y, FLOOR_LIP), RIVET_R * 1.15, RIVET_R * 0.8,
                       RIVET_PROUD, axis='Z', sides=RIVET_SIDES, mat=MAT_TRIM)
    for y in _span(-cv + 0.22, cv - 0.22, 0.34):
        for x in (HX - FLOOR_BORDER * 0.5, -HX + FLOOR_BORDER * 0.5):
            mb.frustum((x, y, FLOOR_LIP), RIVET_R * 1.15, RIVET_R * 0.8,
                       RIVET_PROUD, axis='Z', sides=RIVET_SIDES, mat=MAT_TRIM)

    # 중앙 트레드판 + 돌기
    mb.box((0, 0, 0.004), (cu * 2, cv * 2, 0.008), MAT_TREAD)
    nx = int((cu * 2 - STUD_PITCH) / STUD_PITCH)
    ny = int((cv * 2 - STUD_PITCH) / STUD_PITCH)
    x0 = -(nx - 1) * STUD_PITCH * 0.5
    y0 = -(ny - 1) * STUD_PITCH * 0.5
    for i in range(nx):
        for j in range(ny):
            # 엇갈림 배치 — 격자보다 트레드처럼 읽힌다
            ox = (STUD_PITCH * 0.5) if (j % 2) else 0.0
            x = x0 + i * STUD_PITCH + ox
            if x > cu - 0.05:
                continue
            mb.frustum((x, y0 + j * STUD_PITCH, 0.008 + STUD_H * 0.5),
                       STUD_R, STUD_R * 0.62, STUD_H, axis='Z',
                       sides=STUD_SIDES, mat=MAT_TREAD, cap_lo=False)
    return mb.finish("ELV_Floor", coll)


def build_ceiling(coll):
    """어두운 천장판 + 횡보 3개 + 램프 마운트."""
    mb = MB()
    mb.box((0, 0, IH + TH * 0.5), (IW, ID, TH), MAT_DARK)
    for y in (-1.30, 0.0, 1.30):
        mb.box((0, y, IH - BEAM_DROP * 0.5), (IW, BEAM_W, BEAM_DROP), MAT_DARK)
        # 보 아래면 리벳 띠
        for x in _span(-HX + 0.24, HX - 0.24, 0.30):
            mb.frustum((x, y, IH - BEAM_DROP), RIVET_R, RIVET_R * 0.7,
                       RIVET_PROUD, axis='Z', sides=RIVET_SIDES, mat=MAT_TRIM)
    # 램프 마운트판
    mb.box((0, 0, IH - BEAM_DROP - 0.018), (0.34, 0.34, 0.036), MAT_TRIM)
    return mb.finish("ELV_Ceiling", coll)


def build_ceiling_lamp(coll):
    """
    레퍼런스의 케이지 램프. 원점이 천장 부착점이 되도록 짓는다 —
    Unity 에서 (0, IH-BEAM_DROP, 0) 에 놓으면 맞는다.
    """
    mb = MB()
    top = 0.0
    stem_h = 0.085
    mb.frustum((0, 0, top - stem_h * 0.5), 0.030, 0.030, stem_h,
               sides=8, mat=MAT_TRIM)
    # 갓 (원뿔대)
    hood_h = 0.115
    hz = top - stem_h - hood_h * 0.5
    mb.frustum((0, 0, hz), 0.052, 0.148, hood_h, sides=10, mat=MAT_BRASS,
               cap_lo=False, cap_hi=False)
    # 유리 원통
    glass_h = 0.165
    gz = hz - hood_h * 0.5 - glass_h * 0.5
    mb.frustum((0, 0, gz), 0.072, 0.072, glass_h, sides=10, mat=MAT_GLASS)
    # 하단 캡
    mb.frustum((0, 0, gz - glass_h * 0.5 - 0.016), 0.078, 0.050, 0.032,
               sides=10, mat=MAT_BRASS)
    # 케이지 세로살 6개
    cage_r = 0.098
    cage_h = hood_h + glass_h + 0.06
    cz = hz - hood_h * 0.5 - cage_h * 0.5 + 0.02
    for i in range(6):
        a = TAU * i / 6
        mb.box((math.cos(a) * cage_r, math.sin(a) * cage_r, cz),
               (0.014, 0.014, cage_h), MAT_BRASS)
    # 케이지 링 2개
    for rz in (cz + cage_h * 0.42, cz - cage_h * 0.42):
        for i in range(12):
            a0 = TAU * i / 12
            a1 = TAU * (i + 1) / 12
            xm = (math.cos(a0) + math.cos(a1)) * 0.5 * cage_r
            ym = (math.sin(a0) + math.sin(a1)) * 0.5 * cage_r
            seg = cage_r * TAU / 12
            ang = math.atan2(math.sin(a1) - math.sin(a0), math.cos(a1) - math.cos(a0))
            mb.box((xm, ym, rz), (seg, 0.013, 0.013), MAT_BRASS)
            # 회전 없이 근사 — 12각 링은 축소 화면에서 원으로 읽힌다
            _ = ang
    return mb.finish("ELV_CeilingLamp", coll)


def build_bench(coll):
    """
    레퍼런스 우측의 2단 선반 벤치. 원점이 우측 벽 안쪽 면에 오도록 짓는다 —
    Unity 에서 (HX, 0, 0) 에 놓는다. −x 가 실내 방향이다.
    """
    mb = MB()
    depth = 0.56
    top_z = 0.92
    mid_z = 0.44
    length = 3.05
    cx = -depth * 0.5

    mb.box((cx, 0, top_z), (depth, length, 0.055), MAT_TRIM)
    mb.box((cx - 0.008, 0, top_z - 0.055), (depth * 0.12, length, 0.075), MAT_TRIM)
    mb.box((cx, 0, mid_z), (depth * 0.92, length * 0.985, 0.036), MAT_IRON)
    mb.box((cx, 0, 0.115), (depth * 0.86, length * 0.97, 0.030), MAT_IRON)

    for y in (-length * 0.5 + 0.12, -length * 0.17, length * 0.17, length * 0.5 - 0.12):
        for dx in (-depth + 0.075, -0.075):
            mb.box((dx, y, top_z * 0.5 - 0.03), (0.052, 0.052, top_z - 0.06), MAT_TRIM)
    # 벽 브래킷
    for y in (-length * 0.34, length * 0.34):
        mb.box((-0.10, y, top_z - 0.16), (0.20, 0.030, 0.22), MAT_TRIM)
    return mb.finish("ELV_Bench", coll)


# ══════════════════════════════════════════════════════════════════════════
# 마모와 비대칭
# ══════════════════════════════════════════════════════════════════════════
# 금지 9항 「깨끗하고 대칭적인 쇼룸 구성」. 절차적으로 지은 셸은 기본이 완전 대칭이라
# 이 금지에 **자동으로 걸린다** — Notion §12.2 「장식보다 기능과 마모가 먼저 보임」이
# 무너지고 "사용된 적 없는 기계"로 읽힌다.
#
# 그래서 넣는 것은 장식이 아니라 **기능의 흔적**이다. 덧댄 수리판, 등에 전기를
# 보내는 배선관, 화물 결박 고리. 금지 6항(과도한 파이프)에 걸리지 않으려면
# 배선관은 **실제로 무언가를 잇는 하나**여야 한다 — 천장등에서 분전함까지.
#
# 시드를 고정한다. 이 저장소는 결정론이 규율이고, 돌릴 때마다 달라지는 마모는
# 캡처 비교를 불가능하게 만든다.

WEAR_SEED = 20260804


def build_wear(coll):
    """덧댄 수리판 · 배선관 · 결박 고리. 좌우 비대칭으로 배치한다."""
    import random
    rnd = random.Random(WEAR_SEED)
    mb = MB()

    # ── 덧댄 수리판 — 패널 위에 볼트로 때운 자국 ──
    # 위치를 손으로 고르지 않고 시드로 뽑되, 각 벽에서 서로 다른 높이 대역을 쓴다.
    patches = [
        # (plane 이름, u 중심, v 중심, 폭, 높이)
        ("right", -1.42, 1.55, 0.62, 0.46),
        ("right",  0.86, 0.63, 0.44, 0.38),
        ("front",  1.05, 1.72, 0.55, 0.40),
        ("left",  -1.88, 0.72, 0.40, 0.34),
    ]
    planes = {
        "right": Plane((HX, 0, 0), (0, 1, 0), (1, 0, 0)),
        "front": Plane((0, -HY, 0), (1, 0, 0), (0, -1, 0)),
        "left":  Plane((-HX, 0, 0), (0, 1, 0), (-1, 0, 0)),
    }
    for key, cu, cv, w, h in patches:
        pl = planes[key]
        jitter = (rnd.random() - 0.5) * 0.06
        u0, u1 = cu - w * 0.5 + jitter, cu + w * 0.5 + jitter
        v0, v1 = cv - h * 0.5, cv + h * 0.5
        pl.box(mb, u0, u1, v0, v1, -0.014, 0.004, MAT_TRIM)
        # 네 귀퉁이 볼트
        for bu in (u0 + 0.045, u1 - 0.045):
            for bv in (v0 + 0.045, v1 - 0.045):
                pl.rivet(mb, bu, bv)

    # ── 배선관 — 천장등 마운트에서 우측 벽 분전함까지. 기능적 연결 하나뿐이다 ──
    box_u, box_v = 1.62, 1.78          # 우측 벽 분전함 위치
    plr = planes["right"]
    plr.box(mb, box_u - 0.16, box_u + 0.16, box_v - 0.21, box_v + 0.21,
            -0.105, 0.004, MAT_TRIM)
    plr.box(mb, box_u - 0.13, box_u + 0.13, box_v - 0.18, box_v + 0.18,
            -0.118, -0.100, MAT_DARK)
    for bu in (box_u - 0.13, box_u + 0.13):
        for bv in (box_v - 0.175, box_v + 0.175):
            plr.rivet(mb, bu, bv)

    # 분전함 → 천장 (수직 구간), 우측 벽면을 따라 올라간다
    cx = HX - 0.055
    mb.frustum((cx, box_u, (box_v + 0.21 + IH) * 0.5), 0.026, 0.026,
               IH - (box_v + 0.21), axis='Z', sides=6, mat=MAT_TRIM)
    # 천장을 가로질러 램프까지 — 두 구간이 **실제로 만나야** 「잇는 관」으로 읽힌다.
    # (직전 판본은 가로 구간을 y = box_u*0.5 에 두어 세로 구간과 끊겨 있었다)
    mb.frustum((cx * 0.5, box_u, IH - 0.055), 0.026, 0.026,
               cx, axis='X', sides=6, mat=MAT_TRIM)          # x: 0 → 1.945, y = box_u
    mb.frustum((0.0, box_u * 0.5, IH - 0.055), 0.026, 0.026,
               box_u, axis='Y', sides=6, mat=MAT_TRIM)       # y: 0 → box_u, x = 0
    # 두 구간이 만나는 모서리 — 직각 조인트
    mb.box((0.0, box_u, IH - 0.055), (0.052, 0.052, 0.052), MAT_TRIM)
    # 관 고정 클램프
    for t in (0.30, 0.62, 0.88):
        mb.box((cx, box_u, (box_v + 0.21) + (IH - box_v - 0.21) * t),
               (0.052, 0.052, 0.022), MAT_TRIM)

    # ── 화물 결박 고리 — 벽 하단. 좌우 개수를 다르게 둔다 (비대칭) ──
    for (key, u, v) in [("right", -0.95, 0.52), ("right", 0.35, 0.52),
                        ("front", -0.62, 0.52)]:
        pl = planes[key]
        pl.box(mb, u - 0.055, u + 0.055, v - 0.048, v + 0.048, -0.032, 0.004, MAT_TRIM)
        axis = 'X' if key in ("right", "left") else 'Y'
        c = pl.pt(u, v, -0.052)
        mb.frustum((c.x, c.y, c.z), 0.030, 0.030, 0.016, axis=axis,
                   sides=8, mat=MAT_TRIM)

    return mb.finish("ELV_Wear", coll)


# ══════════════════════════════════════════════════════════════════════════
# 승강로 밖 통로 — 별도 컬렉션 · 별도 FBX
# ══════════════════════════════════════════════════════════════════════════
# 근거: DEVICE_DESIGN_SPEC §2.6 — Notion 은 「문 밖 깊이 3~5m」를 요구하는데
# 현재 씬 Lobby 는 1.4m 다. **미달로 이미 판정된 실질 결함 2건 중 하나다.**
# 레퍼런스에서도 가위문 너머로 물러나는 어둠과 먼 등 하나가 보이고,
# 그 깊이가 폐쇄감의 상당 부분을 만든다.
#
# 「짧은 탐색용 무대」이지 방이 아니다 — 03번이 "핵심 룰렛 루프를 과도하게
# 지연시키지 않는다"를 함께 요구하므로 3m 가 하한이자 적정선이다.

SHAFT_COLL = "ELV_SHAFT"
SHAFT_DEPTH = 3.60      # 벽 바깥 면에서 막힌 끝까지
SHAFT_W = 2.70          # 개구부(2.1)보다 넓게 — 문틀이 프레임으로 읽힌다
SHAFT_H = 2.55


def build_shaft(coll):
    """가위문 너머 통로. 카 좌측(−x) 으로 뻗는다."""
    mb = MB()
    x_near = -HX - TH               # −2.16  카 바깥 면
    x_far = x_near - SHAFT_DEPTH    # −5.76
    hw = SHAFT_W * 0.5
    t = 0.18

    # 바닥 · 천장
    mb.box(((x_near + x_far) * .5, 0, -t * .5), (SHAFT_DEPTH, SHAFT_W, t), MAT_DARK)
    mb.box(((x_near + x_far) * .5, 0, SHAFT_H + t * .5), (SHAFT_DEPTH, SHAFT_W, t), MAT_DARK)
    # 좌우 벽
    for sy in (-1, 1):
        mb.box(((x_near + x_far) * .5, sy * (hw + t * .5), SHAFT_H * .5),
               (SHAFT_DEPTH, t, SHAFT_H), MAT_DARK)
    # 막힌 끝
    mb.box((x_far - t * .5, 0, SHAFT_H * .5), (t, SHAFT_W + t * 2, SHAFT_H), MAT_DARK)

    # 끝벽 패널 — 통로 끝이 판독되게 최소한의 면 분할만 준다
    pl_end = Plane((x_far, 0, 0), (0, 1, 0), (-1, 0, 0))
    panel_field(mb, pl_end, -hw, hw, 0.30, SHAFT_H - 0.30, 0.62)
    pl_end.box(mb, -hw, hw, 0.0, 0.30, -0.022, t, MAT_TRIM)
    pl_end.rivet_row(mb, -hw + 0.12, hw - 0.12, 0.16)

    # 횡보 3개 — 물러나는 깊이를 리듬으로 읽히게 한다
    for i in range(3):
        x = x_near - 0.85 * (i + 1)
        mb.box((x, 0, SHAFT_H - 0.105), (0.16, SHAFT_W, 0.21), MAT_DARK)

    # 벽 등 — 레퍼런스의 「먼 등 하나」. 이게 없으면 통로가 그냥 검은 구멍이다
    lx, ly = x_near - 2.35, hw - 0.05
    mb.box((lx, ly + 0.055, 1.92), (0.16, 0.11, 0.30), MAT_TRIM)
    mb.frustum((lx, ly - 0.055, 1.90), 0.062, 0.062, 0.185, axis='Y',
               sides=8, mat=MAT_GLASS)
    for i in range(4):
        a = TAU * i / 4 + math.pi / 4
        mb.box((lx + math.cos(a) * 0.075, ly - 0.055, 1.90 + math.sin(a) * 0.075),
               (0.012, 0.20, 0.012), MAT_BRASS)

    # 배관 1줄 — 기능적 연결을 설명하는 선만 쓴다 (금지 6항: 과도한 파이프 금지)
    mb.frustum(((x_near + x_far) * .5, -hw + 0.14, 2.18), 0.048, 0.048,
               SHAFT_DEPTH * 0.92, axis='X', sides=8, mat=MAT_TRIM)

    return mb.finish("ELV_Shaft", coll)


def build_shaft_all():
    old = bpy.data.collections.get(SHAFT_COLL)
    if old:
        for ob in list(old.objects):
            bpy.data.objects.remove(ob, do_unlink=True)
        bpy.data.collections.remove(old)
    coll = bpy.data.collections.new(SHAFT_COLL)
    bpy.context.scene.collection.children.link(coll)
    ob = build_shaft(coll)
    me = ob.data
    tris = sum(max(0, len(p.vertices) - 2) for p in me.polygons)
    print(f"ELV_SHAFT 조립 완료  verts={len(me.vertices)} tris={tris}")
    return {"verts": len(me.vertices), "tris": tris}


def export_shaft(path=None):
    import os
    path = path or (r"B:\PROJECT_NEW_BORN\Upandup_DDD\Assets\Prototype_Elevator"
                    r"\Art\Models\ELV_Shaft.fbx")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    coll = bpy.data.collections.get(SHAFT_COLL)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in coll.objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = next(iter(coll.objects))
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_unit_scale=True,
        global_scale=1.0, axis_forward='-Z', axis_up='Y',
        object_types={'MESH'}, use_mesh_modifiers=True,
        mesh_smooth_type='FACE', bake_space_transform=False,
        path_mode='STRIP')
    return path


def _span(a, b, step):
    """a..b 를 step 이하 간격으로 균등 분할한 좌표들."""
    if b <= a:
        return []
    n = max(1, int((b - a) / step))
    s = (b - a) / n
    return [a + s * i for i in range(n + 1)]


# ══════════════════════════════════════════════════════════════════════════
# 조립 · 내보내기
# ══════════════════════════════════════════════════════════════════════════
def _fresh_collection():
    old = bpy.data.collections.get(COLL_NAME)
    if old:
        for ob in list(old.objects):
            bpy.data.objects.remove(ob, do_unlink=True)
        bpy.data.collections.remove(old)
    coll = bpy.data.collections.new(COLL_NAME)
    bpy.context.scene.collection.children.link(coll)
    return coll


# 국소 원점으로 지은 모듈의 최종 배치. 여기서 자리를 잡아 두면 Unity 는
# 루트를 원점에 놓기만 하면 된다 — 배치 수치가 두 곳에 흩어지지 않는다.
PLACEMENT = {
    "ELV_Bench":       (HX, 0.0, 0.0),
    "ELV_CeilingLamp": (0.0, 0.0, IH - BEAM_DROP),
}


def build_all():
    coll = _fresh_collection()
    builders = [
        build_floor, build_ceiling, build_wall_rear, build_wall_front,
        build_wall_right, build_wall_left, build_corner_posts,
        build_ceiling_lamp, build_bench, build_wear,
    ]
    report = []
    total_v = total_t = 0
    for fn in builders:
        ob = fn(coll)
        if ob.name in PLACEMENT:
            ob.location = PLACEMENT[ob.name]
        me = ob.data
        tris = sum(max(0, len(p.vertices) - 2) for p in me.polygons)
        total_v += len(me.vertices)
        total_t += tris
        report.append(f"  {ob.name:22s} verts={len(me.vertices):6d} tris={tris:6d} "
                      f"mats={len(me.materials)}")
    report.append(f"  {'TOTAL':22s} verts={total_v:6d} tris={total_t:6d}")
    print("ELV_CABIN 조립 완료")
    print("\n".join(report))
    return {"objects": len(coll.objects), "verts": total_v, "tris": total_t,
            "report": report}


def preview(tag="", exposure=0.0, ambient=0.35, energy=26.0):
    """
    레퍼런스와 같은 조건의 미리보기 — 케이지 램프 하나 + 아주 낮은 대기광.
    exposure 를 올리면 형상 확인용으로 밝아진다 (분위기 판정에는 0 을 쓴다).
    """
    import os
    sc = bpy.context.scene
    coll = bpy.data.collections[COLL_NAME]
    keep = {ob.name for ob in coll.objects}
    for ob in bpy.data.objects:
        ob.hide_render = ob.name not in keep and ob.type == 'MESH'

    for n in ("CAB_Lamp", "CAB_Cam"):
        ob = bpy.data.objects.get(n)
        if ob:
            bpy.data.objects.remove(ob, do_unlink=True)

    ld = bpy.data.lights.new("CAB_LampData", 'POINT')
    ld.energy = energy
    ld.color = (1.0, 0.80, 0.55)
    ld.shadow_soft_size = 0.09
    lamp = bpy.data.objects.new("CAB_Lamp", ld)
    lamp.location = (0.0, 0.0, IH - BEAM_DROP - 0.34)
    sc.collection.objects.link(lamp)

    world = sc.world or bpy.data.worlds.new("W")
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes["Background"]
    bg.inputs[0].default_value = (0.020, 0.023, 0.028, 1.0)
    bg.inputs[1].default_value = ambient

    cd = bpy.data.cameras.new("CAB_CamData")
    cd.lens_unit = 'FOV'
    cd.angle = math.radians(70)
    cam = bpy.data.objects.new("CAB_Cam", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam

    for eng in ('BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE', 'CYCLES'):
        try:
            sc.render.engine = eng
            break
        except Exception:
            continue
    sc.view_settings.view_transform = 'AgX'
    sc.view_settings.exposure = exposure
    sc.render.resolution_x, sc.render.resolution_y = 1280, 720
    sc.render.image_settings.file_format = 'PNG'

    def look_at(ob, tgt):
        d = Vector(tgt) - ob.location
        ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()

    outdir = r"B:\PROJECT_NEW_BORN\Upandup_DDD\Captures\blender_cabin"
    os.makedirs(outdir, exist_ok=True)
    views = {
        "A_entry_to_machine": ((0.30, -1.90, 1.62), (-0.35, 2.28, 1.30)),
        "C_toward_gate":      ((0.55, 0.30, 1.62), (-2.00, -0.10, 1.25)),
        "E_corner_wide":      ((1.55, -1.95, 1.72), (-1.10, 1.90, 1.10)),
    }
    made = []
    for k, (loc, tgt) in views.items():
        cam.location = loc
        look_at(cam, tgt)
        sc.render.filepath = os.path.join(outdir, f"cab_{k}{tag}.png")
        bpy.ops.render.render(write_still=True)
        made.append(k)
    return made


def export(path=None):
    path = path or (r"B:\PROJECT_NEW_BORN\Upandup_DDD\Assets\Prototype_Elevator"
                    r"\Art\Models\ELV_Cabin.fbx")
    import os
    os.makedirs(os.path.dirname(path), exist_ok=True)
    coll = bpy.data.collections.get(COLL_NAME)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in coll.objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = next(iter(coll.objects))
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_unit_scale=True,
        global_scale=1.0, axis_forward='-Z', axis_up='Y',
        object_types={'MESH'}, use_mesh_modifiers=True,
        mesh_smooth_type='FACE', bake_space_transform=False,
        path_mode='STRIP')
    return path
