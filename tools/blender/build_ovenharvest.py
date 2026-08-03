# -*- coding: utf-8 -*-
"""
OVENHARVEST EXTRACTION SYSTEM — 절차적 조립기 (Blender)

치수는 전부 `Assets/Prototype_Elevator/Scripts/Art/ReferenceRoomSpec.cs` 의
확정 상수에서 온다. 여기서 임의로 바꾸지 않는다 — 바꾸면 Unity 의 배치와 어긋난다.

좌표계 (Blender, Z-up):
    x  폭        −W/2 … +W/2
    z  높이      0 (캐비닛 바닥) … H
    y  깊이      0 (캐비닛 전면, 실내 쪽) … +DEPTH (벽 쪽)
    즉 −y 가 관찰자 방향이다. Blender Numpad1 정면뷰와 일치한다.

실행:  exec(open(r"...\\build_ovenharvest.py", encoding="utf-8").read())
       build_all()
"""

import bpy
import bmesh
import math
from mathutils import Vector

TAU = math.tau
R = math.radians

# ══════════════════════════════════════════════════════════════════════════
# 스펙 — ReferenceRoomSpec.cs 미러
# ══════════════════════════════════════════════════════════════════════════
OUTER = 0.090          # OuterFrameBand
RIB = 0.068            # BankRibWidth
BULK = 0.030           # BulkheadHeight
SHAFT = 0.120          # ShaftHousingHeight
DW = 0.52              # ChamberDoorWidth
DH = 0.48              # ChamberDoorHeight
DEPTH = 0.26           # MachineDepth
FACE_T = 0.028         # CabinetFaceThickness
BACK_T = 0.024         # CabinetBackThickness

W = OUTER * 2 + DW * 3 + RIB * 2                 # 1.876
H = OUTER * 2 + DH * 3 + BULK * 2 + SHAFT        # 1.800

PITCH_X = DW + RIB                                # 0.588
COL_X = [(i - 1) * PITCH_X for i in range(3)]
ROW_Z = [OUTER + DH * 0.5 + i * (DH + BULK) for i in range(3)]   # .33 .84 1.35

RING_D = 0.38          # WindowRingDiameter
GLASS_D = 0.29         # WindowGlassDiameter
PROTRUDE = 0.060       # WindowProtrusion
GLASS_INSET = 0.045    # WindowGlassInset
GLASS_T = 0.018        # WindowGlassThickness
SIDES = 12             # WindowSilhouetteSides — 12각. 완전한 원을 쓰지 않는다
BOLTS = 8              # WindowBoltCount
BOLT_R = (GLASS_D + RING_D) * 0.25                # 0.1675
SOUL_R = 0.058         # SoulRadius
SOUL_DEPTH = 0.100     # SoulDepthFromDoorFace

APER = 0.29            # 도어 사각 개구부 한 변 (DoorApertureHalf*2)
WSEAT = RING_D * 0.5 + 0.022    # WindowSeatRadius 0.212 — 링이 앉는 단

# 세로 원통 뱅크의 곡률 반경(m). 도어 폭 520mm 에서 크라운이 55mm 볼록해진다.
# 캐비닛 깊이 260mm 에 온전한 지름 520mm 원통은 들어가지 않으므로 얕은 배럴로 세운다.
# 방(정육면체 챔버)과 관(통관)의 치수.
# 행 피치 510mm 안에서 링 지름 380mm 이 들어갈 방과, 눈에 보일 만큼의 관 구간을
# 둘 다 확보해야 한다 → 방 400mm + 관 110mm.
CHAMBER_W = 0.44
CHAMBER_H = 0.40
CHAMBER_PROUD = 0.085  # 방이 캐비닛 면에서 앞으로 나온 깊이
# 통관 반지름.
# 🔴 사용자 지적: 「통관이 너무 작다 — 구슬보다 커야 하는데 구슬보다 작아 보인다.」
# 구슬이 이 관을 **지나다니는** 물건이므로 이건 비례가 아니라 **논리**의 문제다.
# 구슬 핵 지름 116mm, 헤일로까지 200mm. 관 지름 320mm 면 둘 다 여유롭게 통과한다.
# 방 폭 440mm 보다는 작아야 「관」으로 읽히므로 그 사이에 둔다.
PIPE_R = 0.160

# 돌출 위계 — 90mm@20 > 68mm@12 > 30mm@6. 굵은 것이 앞에 온다
PROUD_OUTER = 0.020
PROUD_RIB = 0.012
PROUD_BULK = 0.006
DOOR_PROUD = 0.010

SILL_H = 0.090         # SillHeight
SILL_D = 0.140         # SillDepth

# 레버 컬럼 — 룸 좌표를 캐비닛 로컬(z = roomY − 0.2)로 옮긴 값
COL_W = 0.34           # LeverColumnWidth
COL_BOTTOM = 0.22      # LeverColumnBottomY 0.42 − MachineBottomY 0.2
PIVOT_Z = 1.05         # LeverPivotY 1.25 − 0.2
GRIP_D = 0.045
GRIP_LEN = 0.18
# 모멘트 암(m). 진행 문서의 지적 「260~320mm, 정지 자세는 수평에서 35~45° 아래」.
# 380mm(LeverHandleLength)는 손잡이 전체 길이이고 회전 반경이 아니다
ARM_LEN = 0.28
LEVER_SWING = 55.0     # LeverSwingDegrees

COLL_NAME = "OVENHARVEST"


# ══════════════════════════════════════════════════════════════════════════
# 저수준 지오메트리 — bmesh 에 직접 쌓는다
# ══════════════════════════════════════════════════════════════════════════
def box(bm, center, size):
    """축 정렬 박스. center/size 는 (x, y, z)."""
    cx, cy, cz = center
    hx, hy, hz = size[0] * 0.5, size[1] * 0.5, size[2] * 0.5
    co = [(cx - hx, cy - hy, cz - hz), (cx + hx, cy - hy, cz - hz),
          (cx + hx, cy + hy, cz - hz), (cx - hx, cy + hy, cz - hz),
          (cx - hx, cy - hy, cz + hz), (cx + hx, cy - hy, cz + hz),
          (cx + hx, cy + hy, cz + hz), (cx - hx, cy + hy, cz + hz)]
    v = [bm.verts.new(c) for c in co]
    f = bm.faces.new
    f((v[0], v[3], v[2], v[1]))
    f((v[4], v[5], v[6], v[7]))
    f((v[0], v[1], v[5], v[4]))
    f((v[2], v[3], v[7], v[6]))
    f((v[1], v[2], v[6], v[5]))
    f((v[3], v[0], v[4], v[7]))


def _ring(bm, cx, cz, r, y, n, phase):
    out = []
    for i in range(n):
        a = TAU * (i / n) + phase
        out.append(bm.verts.new((cx + r * math.cos(a), y, cz + r * math.sin(a))))
    return out


def prism(bm, center_xz, r, y0, y1, n=SIDES, phase=0.0):
    """y 축 방향 n각 기둥. y0 이 앞면."""
    cx, cz = center_xz
    a = _ring(bm, cx, cz, r, y0, n, phase)
    b = _ring(bm, cx, cz, r, y1, n, phase)
    bm.faces.new(a)                 # 앞 캡 → −y
    bm.faces.new(list(reversed(b)))  # 뒤 캡 → +y
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], b[i], b[j], a[j]))


def annulus(bm, center_xz, r_out, r_in, y0, y1, n=SIDES, phase=0.0):
    """y 축 방향 n각 고리. y0 이 앞면."""
    cx, cz = center_xz
    ao = _ring(bm, cx, cz, r_out, y0, n, phase)
    ai = _ring(bm, cx, cz, r_in, y0, n, phase)
    bo = _ring(bm, cx, cz, r_out, y1, n, phase)
    bi = _ring(bm, cx, cz, r_in, y1, n, phase)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ao[i], ao[j], ai[j], ai[i]))   # 앞
        bm.faces.new((bi[i], bi[j], bo[j], bo[i]))   # 뒤
        bm.faces.new((ao[i], bo[i], bo[j], ao[j]))   # 바깥
        bm.faces.new((ai[i], ai[j], bi[j], bi[i]))   # 보어(안쪽)


def dome(bm, center_xz, r, h, y_base, n=10):
    """
    리벳 머리. −y 쪽으로 h 만큼 솟는다.

    ⚠ 첫 판본은 링 하나 + 꼭짓점, 즉 **원뿔**이었다. 베벨까지 걸리자 판 위에
    가시가 돋은 것처럼 보였다. 중간 링을 하나 넣어 버섯 머리로 만든다.
    """
    cx, cz = center_xz
    lo = _ring(bm, cx, cz, r, y_base, n, 0.0)
    mid = _ring(bm, cx, cz, r * 0.86, y_base - h * 0.55, n, 0.0)
    top = _ring(bm, cx, cz, r * 0.52, y_base - h, n, 0.0)
    for a, b in ((lo, mid), (mid, top)):
        for i in range(n):
            j = (i + 1) % n
            bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(top)
    bm.faces.new(list(reversed(lo)))


def cyl_x(bm, center, r, length, n=10):
    """x 축 정렬 원기둥 (레버 그립·핸들 바)."""
    cx, cy, cz = center
    a, b = [], []
    for i in range(n):
        t = TAU * i / n
        a.append(bm.verts.new((cx - length * 0.5, cy + r * math.cos(t), cz + r * math.sin(t))))
        b.append(bm.verts.new((cx + length * 0.5, cy + r * math.cos(t), cz + r * math.sin(t))))
    bm.faces.new(list(reversed(a)))
    bm.faces.new(b)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))


def strut(bm, p0, p1, w, t):
    """두 점을 잇는 각재. 링크·로드처럼 임의 방향으로 놓이는 부재에 쓴다."""
    p0, p1 = Vector(p0), Vector(p1)
    d = (p1 - p0)
    d.normalize()
    up = Vector((1, 0, 0)) if abs(d.x) < 0.9 else Vector((0, 1, 0))
    a = d.cross(up).normalized() * (w * 0.5)
    b = d.cross(a).normalized() * (t * 0.5)
    v = [bm.verts.new(base + a * sa + b * sb)
         for base in (p0, p1) for sa, sb in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    bm.faces.new(v[0:4])
    bm.faces.new(v[4:8])
    for k in range(4):
        bm.faces.new((v[k], v[(k + 1) % 4], v[4 + (k + 1) % 4], v[4 + k]))


def arc_band(bm, center_xz, r_out, r_in, y0, y1, a0, a1, n=18):
    """XZ 평면의 부채꼴 띠(축은 y). 뱅크 원통의 돔 상단이 판을 뚫고 보이는 아치 리브."""
    cx, cz = center_xz

    def ring(r, y):
        return [bm.verts.new((cx + r * math.cos(a0 + (a1 - a0) * k / n), y,
                              cz + r * math.sin(a0 + (a1 - a0) * k / n)))
                for k in range(n + 1)]
    ao, ai, bo, bi = ring(r_out, y0), ring(r_in, y0), ring(r_out, y1), ring(r_in, y1)
    for k in range(n):
        bm.faces.new((ao[k], ao[k + 1], ai[k + 1], ai[k]))
        bm.faces.new((bo[k], bo[k + 1], bi[k + 1], bi[k]))
        bm.faces.new((ao[k], ao[k + 1], bo[k + 1], bo[k]))
        bm.faces.new((ai[k], ai[k + 1], bi[k + 1], bi[k]))
    bm.faces.new((ao[0], ai[0], bi[0], bo[0]))
    bm.faces.new((ao[n], ai[n], bi[n], bo[n]))


def arc_band_x(bm, center_yz, r_out, r_in, x0, x1, a0, a1, n=16):
    """YZ 평면의 부채꼴 띠(축은 x). 레버가 지나는 사분면 가이드판."""
    cy, cz = center_yz

    def ring(r, x):
        return [bm.verts.new((x, cy + r * math.cos(a0 + (a1 - a0) * k / n),
                              cz + r * math.sin(a0 + (a1 - a0) * k / n)))
                for k in range(n + 1)]
    ao, ai, bo, bi = ring(r_out, x0), ring(r_in, x0), ring(r_out, x1), ring(r_in, x1)
    for k in range(n):
        bm.faces.new((ao[k], ao[k + 1], ai[k + 1], ai[k]))
        bm.faces.new((bo[k], bo[k + 1], bi[k + 1], bi[k]))
        bm.faces.new((ao[k], ao[k + 1], bo[k + 1], bo[k]))
        bm.faces.new((ai[k], ai[k + 1], bi[k + 1], bi[k]))
    bm.faces.new((ao[0], ai[0], bi[0], bo[0]))
    bm.faces.new((ao[n], ai[n], bi[n], bo[n]))


def barrel_y(x, half_w, Rb, y_off=0.0):
    """뱅크 원통 전면의 y. x=±half_w 에서 y_off, x=0 에서 가장 앞(작은 y)."""
    return y_off - (math.sqrt(max(Rb * Rb - x * x, 1e-9))
                    - math.sqrt(max(Rb * Rb - half_w * half_w, 1e-9)))


def barrel_shell(bm, cx, xs, zs, Rb, half_w, t, holes=(), y_off=0.0):
    """
    세로 원통 뱅크의 전면 셸. 격자 셀 단위로 만들고 구멍 셀만 건너뛴다.

    🔴 **이 함수가 「원통 3개가 박스에 들어 있다」를 만든다.**
    직전 판본은 도어를 평판으로 두고 그 위에 링을 얹었다. 그래서 아홉 칸이
    같은 평면에 있었고, 어떤 재질을 입혀도 「무늬가 그려진 판」으로 읽혔다.
    캐비닛 깊이가 260mm 뿐이라 온전한 원통은 들어가지 않는다 — 대신 곡률
    반경 Rb 의 **얕은 배럴 면**으로 세우면 55mm 볼록이 나오고, 그것이
    측면광에서 원통의 그림자 기울기를 만든다.
    """
    nx, nz = len(xs) - 1, len(zs) - 1
    Vf = [[bm.verts.new((cx + xs[i], barrel_y(xs[i], half_w, Rb, y_off), zs[j]))
           for j in range(nz + 1)] for i in range(nx + 1)]
    Vb = [[bm.verts.new((cx + xs[i], barrel_y(xs[i], half_w, Rb, y_off) + t, zs[j]))
           for j in range(nz + 1)] for i in range(nx + 1)]

    def solid(i, j):
        if i < 0 or j < 0 or i >= nx or j >= nz:
            return False
        xc, zc = (xs[i] + xs[i + 1]) * 0.5, (zs[j] + zs[j + 1]) * 0.5
        return not any(x0 < xc < x1 and z0 < zc < z1 for (x0, x1, z0, z1) in holes)

    for i in range(nx):
        for j in range(nz):
            if not solid(i, j):
                continue
            bm.faces.new((Vf[i][j], Vf[i + 1][j], Vf[i + 1][j + 1], Vf[i][j + 1]))
            bm.faces.new((Vb[i][j], Vb[i + 1][j], Vb[i + 1][j + 1], Vb[i][j + 1]))
            if not solid(i - 1, j):
                bm.faces.new((Vf[i][j], Vf[i][j + 1], Vb[i][j + 1], Vb[i][j]))
            if not solid(i + 1, j):
                bm.faces.new((Vf[i + 1][j], Vf[i + 1][j + 1], Vb[i + 1][j + 1], Vb[i + 1][j]))
            if not solid(i, j - 1):
                bm.faces.new((Vf[i][j], Vf[i + 1][j], Vb[i + 1][j], Vb[i][j]))
            if not solid(i, j + 1):
                bm.faces.new((Vf[i][j + 1], Vf[i + 1][j + 1], Vb[i + 1][j + 1], Vb[i][j + 1]))


def prism_z(bm, center_xy, r, z0, z1, n=16, phase=0.0):
    """세로(z축) n각 기둥. 통관과 그 이음 플랜지."""
    cx, cy = center_xy

    def ring(z):
        return [bm.verts.new((cx + r * math.cos(TAU * i / n + phase),
                              cy + r * math.sin(TAU * i / n + phase), z))
                for i in range(n)]
    a, b = ring(z0), ring(z1)
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], b[i], b[j], a[j]))


def shallow_arch(bm, cx, z_base, chord, rise, band, y0, y1, up=True, n=20):
    """현 길이와 솟음으로 정의하는 얕은 아치 리브. 반경을 적지 않고 유도한다."""
    Rr = (chord * chord) / (8.0 * rise) + rise * 0.5
    ah = math.acos(min(1.0, (chord * 0.5) / Rr))
    if up:
        arc_band(bm, (cx, z_base + rise - Rr), Rr, Rr - band, y0, y1,
                 ah, math.pi - ah, n)
    else:
        arc_band(bm, (cx, z_base - rise + Rr), Rr, Rr - band, y0, y1,
                 math.pi + ah, TAU - ah, n)


def sphere(bm, center, r, seg=12, ring=8):
    """영혼 덩어리. 저폴리 UV 구."""
    cx, cy, cz = center
    rows = []
    for k in range(1, ring):
        phi = math.pi * k / ring
        rr, yy = r * math.sin(phi), r * math.cos(phi)
        rows.append([bm.verts.new((cx + rr * math.cos(TAU * i / seg),
                                   cy + yy,
                                   cz + rr * math.sin(TAU * i / seg)))
                     for i in range(seg)])
    top = bm.verts.new((cx, cy + r, cz))
    bot = bm.verts.new((cx, cy - r, cz))
    for i in range(seg):
        j = (i + 1) % seg
        bm.faces.new((top, rows[0][j], rows[0][i]))
        bm.faces.new((bot, rows[-1][i], rows[-1][j]))
    for k in range(len(rows) - 1):
        for i in range(seg):
            j = (i + 1) % seg
            bm.faces.new((rows[k][i], rows[k][j], rows[k + 1][j], rows[k + 1][i]))


# ══════════════════════════════════════════════════════════════════════════
# 파트 컨테이너
# ══════════════════════════════════════════════════════════════════════════
class Part:
    def __init__(self, name, mat):
        self.name, self.mat = name, mat
        self.bm = bmesh.new()

    def finish(self, coll, bevel=0.0015, loc=(0, 0, 0), rot=(0, 0, 0)):
        if not self.bm.faces:          # 부재를 다 걷어낸 파트는 오브젝트를 만들지 않는다
            self.bm.free()
            return None
        # 격자 셸의 구멍 자리에 남은 고립 정점은 베벨을 어지럽힌다
        loose = [v for v in self.bm.verts if not v.link_faces]
        if loose:
            bmesh.ops.delete(self.bm, geom=loose, context='VERTS')
        # ⚠ 감는 방향을 손으로 유도하지 않는다. 부재가 스무 종류를 넘으면
        # 반드시 한둘을 뒤집고, 뒤집힌 면은 렌더에서 「구멍」으로만 보인다
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        me = bpy.data.meshes.new(self.name)
        self.bm.to_mesh(me)
        self.bm.free()
        for p in me.polygons:
            p.use_smooth = False
        ob = bpy.data.objects.new(self.name, me)
        ob.location = loc
        ob.rotation_euler = rot
        me.materials.append(self.mat)
        coll.objects.link(ob)
        if bevel:
            m = ob.modifiers.new("bev", 'BEVEL')
            m.width = bevel
            m.segments = 1
            m.limit_method = 'ANGLE'
            m.angle_limit = R(35)
            m.harden_normals = False
        return ob


# ══════════════════════════════════════════════════════════════════════════
# 머티리얼
# ══════════════════════════════════════════════════════════════════════════
def _nodes(mat):
    mat.use_nodes = True
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled")
    out.location = (400, 0)
    nt.links.new(bsdf.outputs[0], out.inputs[0])
    return nt, bsdf


def _noise(nt, scale, detail, rough, loc):
    n = nt.nodes.new("ShaderNodeTexNoise")
    n.inputs["Scale"].default_value = scale
    n.inputs["Detail"].default_value = detail
    n.inputs["Roughness"].default_value = rough
    n.location = loc
    return n


def worn_metal(name, base, rust, rough_lo, rough_hi, metal=1.0, scale=14.0,
               rust_lo=0.30, rust_hi=0.48):
    """
    녹이 **얼룩으로** 앉은 무쇠.

    ⚠ 첫 판본은 노이즈 하나를 그대로 램프에 물렸고, 그래서 녹이 표면 전체에
    균일하게 깔려 「주황색 물건」이 됐다. 레퍼런스의 금속은 어두운 무쇠가
    기본이고 녹은 **소수 면적**이다. 큰 노이즈(어디에 녹이 앉나)와 작은
    노이즈(그 안의 결)를 곱해 마스크를 만들면 면적이 저절로 줄어든다.
    """
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    nt, bsdf = _nodes(mat)
    big = _noise(nt, scale * 0.22, 4.0, 0.55, (-940, 160))
    fine = _noise(nt, scale, 8.0, 0.68, (-940, -160))
    mul = nt.nodes.new("ShaderNodeMath")
    mul.operation = 'MULTIPLY'
    mul.location = (-740, 0)
    nt.links.new(big.outputs["Fac"], mul.inputs[0])
    nt.links.new(fine.outputs["Fac"], mul.inputs[1])

    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.location = (-560, 0)
    ramp.color_ramp.elements[0].position = rust_lo
    ramp.color_ramp.elements[1].position = rust_hi
    nt.links.new(mul.outputs[0], ramp.inputs["Fac"])

    mix = nt.nodes.new("ShaderNodeMix")
    mix.data_type = 'RGBA'
    mix.location = (-300, 140)
    mix.inputs[6].default_value = (*base, 1.0)
    mix.inputs[7].default_value = (*rust, 1.0)
    nt.links.new(ramp.outputs["Color"], mix.inputs[0])
    nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])

    rr = nt.nodes.new("ShaderNodeMapRange")
    rr.location = (-300, -160)
    rr.inputs[3].default_value = rough_lo
    rr.inputs[4].default_value = rough_hi
    nt.links.new(ramp.outputs["Color"], rr.inputs[0])
    nt.links.new(rr.outputs[0], bsdf.inputs["Roughness"])

    # 녹이 앉은 곳은 금속성을 잃는다 — 이것이 없으면 녹이 「주황색 금속」이 된다
    mr = nt.nodes.new("ShaderNodeMapRange")
    mr.location = (-300, -400)
    mr.inputs[3].default_value = metal
    mr.inputs[4].default_value = metal * 0.25
    nt.links.new(ramp.outputs["Color"], mr.inputs[0])
    nt.links.new(mr.outputs[0], bsdf.inputs["Metallic"])

    bump = nt.nodes.new("ShaderNodeBump")
    bump.location = (-300, -660)
    bump.inputs["Strength"].default_value = 0.18
    nt.links.new(fine.outputs["Fac"], bump.inputs["Height"])
    nt.links.new(bump.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def soul_mat(name):
    """영혼 — 균일한 발광 구가 아니라 갈라진 잉걸. 노이즈로 발광 세기를 흔든다."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    nt, bsdf = _nodes(mat)
    n = _noise(nt, 26.0, 13.0, 0.78, (-760, 0))
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.location = (-560, 0)
    ramp.color_ramp.elements[0].position = 0.44
    ramp.color_ramp.elements[1].position = 0.58
    nt.links.new(n.outputs["Fac"], ramp.inputs["Fac"])
    # ⚠ 세기를 34 까지 올렸더니 AgX 가 전부 흰색으로 클립했다 —
    # 화면에서 「분홍색 공」이 된 원인이다. 붉은색이 남으려면 2~9 안에 있어야 한다.
    rr = nt.nodes.new("ShaderNodeMapRange")
    rr.location = (-320, 0)
    rr.inputs[3].default_value = 0.35
    rr.inputs[4].default_value = 2.6
    nt.links.new(ramp.outputs["Color"], rr.inputs[0])
    nt.links.new(rr.outputs[0], bsdf.inputs["Emission Strength"])
    bsdf.inputs["Emission Color"].default_value = (1.0, 0.038, 0.016, 1.0)
    bsdf.inputs["Base Color"].default_value = (0.055, 0.004, 0.003, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.45
    return mat


def halo_mat(name):
    """영혼을 감싼 붉은 기체. 반투명 발광 — 창 대부분을 덮되 핵을 가리지 않는다."""
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    nt, bsdf = _nodes(mat)
    n = _noise(nt, 5.5, 8.0, 0.70, (-760, 0))
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    ramp.location = (-560, 0)
    ramp.color_ramp.elements[0].position = 0.42
    ramp.color_ramp.elements[1].position = 0.78
    nt.links.new(n.outputs["Fac"], ramp.inputs["Fac"])
    er = nt.nodes.new("ShaderNodeMapRange")
    er.location = (-320, 120)
    er.inputs[3].default_value = 0.25
    er.inputs[4].default_value = 2.4
    nt.links.new(ramp.outputs["Color"], er.inputs[0])
    nt.links.new(er.outputs[0], bsdf.inputs["Emission Strength"])
    ar = nt.nodes.new("ShaderNodeMapRange")
    ar.location = (-320, -160)
    ar.inputs[3].default_value = 0.10
    ar.inputs[4].default_value = 0.78
    nt.links.new(ramp.outputs["Color"], ar.inputs[0])
    nt.links.new(ar.outputs[0], bsdf.inputs["Alpha"])
    bsdf.inputs["Emission Color"].default_value = (1.0, 0.075, 0.030, 1.0)
    bsdf.inputs["Base Color"].default_value = (0.05, 0.004, 0.003, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.9
    # ⚠ 헤일로를 알파 블렌딩으로 두면 **유리와 정렬 경쟁을 하다 통째로 사라진다** —
    # 둘 다 BLENDED 인 표면이 겹치면 EEVEE 가 하나를 버린다. 디더링은 정렬이
    # 필요 없어 유리 뒤에서도 항상 남는다.
    for attr, val in (("surface_render_method", 'DITHERED'), ("blend_method", 'HASHED'),
                      ("show_transparent_back", False), ("use_backface_culling", False)):
        if hasattr(mat, attr):
            try:
                setattr(mat, attr, val)
            except Exception:
                pass
    return mat


def flat_mat(name, color, rough=0.5, metal=0.0, emit=None, emit_str=0.0, alpha=1.0):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    nt, bsdf = _nodes(mat)
    bsdf.inputs["Base Color"].default_value = (*color, 1.0)
    bsdf.inputs["Roughness"].default_value = rough
    bsdf.inputs["Metallic"].default_value = metal
    if emit:
        bsdf.inputs["Emission Color"].default_value = (*emit, 1.0)
        bsdf.inputs["Emission Strength"].default_value = emit_str
    if alpha < 1.0:
        bsdf.inputs["Alpha"].default_value = alpha
        mat.blend_method = 'BLEND' if hasattr(mat, "blend_method") else mat.blend_method
    return mat


def glass_mat():
    """
    두꺼운 압력 유리.

    ⚠ 굴절(transmission)로 만들면 EEVEE 에서 뒤의 영혼이 스크린 스페이스 밖으로
    밀려 사라진다 — 첫 판본에서 아홉 칸이 전부 회색 원반이 된 이유다.
    알파 블렌딩 + 강한 스펙큘러가 「어두운 두꺼운 유리 뒤의 발광체」를 더 정확히
    준다. 게임에서도 이쪽이 싸다.
    """
    mat = bpy.data.materials.get("OH_Glass") or bpy.data.materials.new("OH_Glass")
    nt, bsdf = _nodes(mat)
    bsdf.inputs["Base Color"].default_value = (0.030, 0.036, 0.034, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.05
    bsdf.inputs["Metallic"].default_value = 0.0
    bsdf.inputs["IOR"].default_value = 1.52
    bsdf.inputs["Alpha"].default_value = 0.17
    for attr, val in (("surface_render_method", 'BLENDED'), ("blend_method", 'BLEND'),
                      ("use_backface_culling", False), ("show_transparent_back", False)):
        if hasattr(mat, attr):
            try:
                setattr(mat, attr, val)
            except Exception:
                pass
    return mat


def make_materials():
    m = {}
    # 무쇠 — 어두운 것이 기본, 녹은 소수 면적
    m["iron"] = worn_metal("OH_Iron", (0.0295, 0.0285, 0.0275), (0.086, 0.042, 0.021),
                           0.46, 0.84, metal=0.90, scale=9.0, rust_lo=0.30, rust_hi=0.50)
    # 금구 — 같은 계열이되 조금 밝고 매끈하다. 밝기 차가 크면 부품이 떠 보인다
    m["steel"] = worn_metal("OH_Steel", (0.082, 0.080, 0.076), (0.090, 0.048, 0.026),
                            0.22, 0.66, metal=1.0, scale=20.0, rust_lo=0.34, rust_hi=0.54)
    m["dark"] = flat_mat("OH_ChamberDark", (0.008, 0.0075, 0.0075), rough=0.95)
    m["glass"] = glass_mat()
    m["soul"] = soul_mat("OH_Soul")
    m["halo"] = halo_mat("OH_SoulHalo")
    m["red"] = worn_metal("OH_RedPaint", (0.190, 0.028, 0.020), (0.085, 0.042, 0.024),
                          0.34, 0.72, metal=0.30, scale=26.0, rust_lo=0.32, rust_hi=0.52)
    m["hazard"] = flat_mat("OH_Hazard", (0.34, 0.25, 0.035), rough=0.58, metal=0.2)
    m["screen"] = flat_mat("OH_Screen", (0.010, 0.011, 0.011), rough=0.16)
    m["led"] = flat_mat("OH_LED", (0.30, 0.015, 0.010), rough=0.4,
                        emit=(1.0, 0.10, 0.05), emit_str=14.0)
    m["ledw"] = flat_mat("OH_LEDWhite", (0.35, 0.35, 0.34), rough=0.4,
                         emit=(0.85, 0.86, 0.80), emit_str=5.0)
    # 사이렌 렌즈 — 구슬보다 밝아야 「경고등」이지만, 클립되면 흰 원통이 된다
    # ⚠ 세기를 올릴수록 AgX 가 흰색으로 클립해 「분홍 원통」이 된다. 1.5 가 상한이다
    m["siren"] = flat_mat("OH_Siren", (0.22, 0.012, 0.008), rough=0.22,
                          emit=(1.0, 0.045, 0.014), emit_str=1.5)
    return m


# ══════════════════════════════════════════════════════════════════════════
# 캐비닛
# ══════════════════════════════════════════════════════════════════════════
def build_cabinet(M, coll):
    iron = Part("OH_Cabinet", M["iron"])
    trim = Part("OH_Trim", M["steel"])
    dark = Part("OH_ChamberInterior", M["dark"])
    glass = Part("OH_Portholes_Glass", M["glass"])
    soul = Part("OH_Souls", M["soul"])
    halo = Part("OH_SoulHalos", M["halo"])
    red = Part("OH_Cabinet_Red", M["red"])

    b_iron, b_trim = iron.bm, trim.bm
    y_back = DEPTH - BACK_T

    # ── ① 셸 — 후면판 + 사방 측벽. 챔버는 실제로 비어 있다 ──────────────
    box(b_iron, (0, DEPTH - BACK_T * 0.5, H * 0.5), (W, BACK_T, H))
    for sx in (-1, 1):
        box(b_iron, (sx * (W * 0.5 - FACE_T * 0.5), DEPTH * 0.5, H * 0.5),
            (FACE_T, DEPTH, H))
    box(b_iron, (0, DEPTH * 0.5, FACE_T * 0.5), (W, DEPTH, FACE_T))
    box(b_iron, (0, DEPTH * 0.5, H - FACE_T * 0.5), (W, DEPTH, FACE_T))

    # 챔버 후면 — 유리 너머로 보이는 어두운 면
    box(dark.bm, (0, y_back - 0.002, H * 0.5), (W - FACE_T * 2, 0.004, H))

    # 내부 격벽 — 챔버 아홉을 실제로 가른다
    for x in (COL_X[0] + PITCH_X * 0.5, COL_X[1] + PITCH_X * 0.5):
        box(b_iron, (x, (FACE_T + y_back) * 0.5, H * 0.5),
            (RIB, y_back - FACE_T, H))
    for z in (ROW_Z[0] + (DH + BULK) * 0.5, ROW_Z[1] + (DH + BULK) * 0.5):
        box(b_iron, (0, (FACE_T + y_back) * 0.5, z), (W - FACE_T * 2, y_back - FACE_T, BULK))

    # ── ② 전면 격자 — 외곽대 · 뱅크 리브 · 격벽. 아홉 칸이 뚫려 있다 ────
    grid_top = OUTER + DH * 3 + BULK * 2          # 1.590
    box(b_iron, (0, FACE_T * 0.5, OUTER * 0.5), (W, FACE_T, OUTER))          # 하단대
    box(b_iron, (0, FACE_T * 0.5, H - OUTER * 0.5), (W, FACE_T, OUTER))      # 상단대
    for sx in (-1, 1):
        box(b_iron, (sx * (W - OUTER) * 0.5, FACE_T * 0.5, H * 0.5), (OUTER, FACE_T, H))
    for x in (COL_X[0] + PITCH_X * 0.5, COL_X[1] + PITCH_X * 0.5):
        box(b_iron, (x, FACE_T * 0.5, (OUTER + grid_top) * 0.5),
            (RIB, FACE_T, grid_top - OUTER))
    # 돌출 캡 — 굵기 위계를 깊이로도 준다
    box(b_iron, (0, -PROUD_OUTER * 0.5, OUTER * 0.5), (W, PROUD_OUTER, OUTER))
    box(b_iron, (0, -PROUD_OUTER * 0.5, H - OUTER * 0.5), (W, PROUD_OUTER, OUTER))
    for sx in (-1, 1):
        box(b_iron, (sx * (W - OUTER) * 0.5, -PROUD_OUTER * 0.5, H * 0.5),
            (OUTER, PROUD_OUTER, H))
    # 리브 캡은 행마다 끊는다 — 무중단 세로 채널이 생기면 릴 띠로 읽힌다
    for x in (COL_X[0] + PITCH_X * 0.5, COL_X[1] + PITCH_X * 0.5):
        for z in ROW_Z:
            box(b_iron, (x, -PROUD_RIB * 0.5, z), (RIB, PROUD_RIB, DH - 0.040))

    # ── ②-b 통관 — 원통 · 네모 · 원통 · 네모 ────────────────────────────
    #
    # 🔴 사용자 설명: 「3×3 정육면체 박스가 있고 그 박스 위아래 사이사이에 원형
    # 통관이 동그라미로 있다. 원통 네모 원통 네모 원통 네모. 그 네모에 꽤 큰
    # 원형 구멍이 뚫려 있고 유리로 막혀 있다 — 이게 구슬을 볼 구멍이다.」
    #
    # 직전 두 판본이 왜 틀렸는지 —
    #   ① 평판 격자: 아홉 칸이 한 평면이라 「무늬」였다.
    #   ② 얕은 배럴: 세로로 **하나의 통**이라 칸이 나뉘지 않았다. 통관은 연속된
    #      관이 아니라 **관과 방이 번갈아** 나오는 것이다. 관은 지나가는 곳이고
    #      방은 멈춰서 보이는 곳이다 — 그 교대가 「통과한다」를 만든다.
    #
    # 진행 문서가 이미 적어 둔 지적이기도 하다:
    # 「지금 아홉 밀폐 챔버는 통관이 아니라 **진열장**이다.」
    # 관은 방보다 **더 앞으로** 나온다. 앞면을 같은 평면에 두면(직전 판본) 방과 방
    # 사이 62mm 구간만 보여 「띠」로 읽힌다 — 둥근 것이 둥글게 보이려면 옆에 있는
    # 것보다 튀어나와 곡률이 빛을 받아야 한다
    pipe_cy = PIPE_R - (CHAMBER_PROUD + 0.045)
    hh = CHAMBER_H * 0.5
    for cx in COL_X:
        segs = [(-SILL_H + 0.030, ROW_Z[0] - hh)]               # 바닥 회수 트로프에서
        segs += [(ROW_Z[k] + hh, ROW_Z[k + 1] - hh) for k in range(2)]
        segs.append((ROW_Z[2] + hh, grid_top + SHAFT))          # 상단 투입 매니폴드로
        for z0, z1 in segs:
            prism_z(b_iron, (cx, pipe_cy), PIPE_R, z0, z1, 16, R(11.25))
            for zf in ((z0, z0 + 0.024), (z1 - 0.024, z1)):     # 이음 플랜지
                prism_z(b_trim, (cx, pipe_cy), PIPE_R + 0.026, zf[0], zf[1], 16, R(11.25))
                for k in range(6):                              # 플랜지 볼트
                    a = TAU * k / 6 + R(30)
                    prism_z(b_trim, (cx + (PIPE_R + 0.014) * math.cos(a),
                                     pipe_cy + (PIPE_R + 0.014) * math.sin(a)),
                            0.011, zf[0] - 0.007, zf[1] + 0.007, 6)

    # ── ③ 상단 공통 샤프트 하우징 ────────────────────────────────────────
    sh_c = grid_top + SHAFT * 0.5
    box(b_iron, (0, -0.030, sh_c), (W - 0.030, 0.060 + FACE_T, SHAFT))
    box(b_trim, (0, -0.052, sh_c), (W - 0.140, 0.028, 0.044))       # 노출 잠금축
    for sx in (-1, 1):                                              # 축 베어링 블록
        box(b_trim, (sx * (W * 0.5 - 0.120), -0.060, sh_c), (0.078, 0.052, 0.086))

    # ── ④ 방 아홉 — 큰 원형 구멍이 뚫린 정육면체 상자 ────────────────────
    ap = APER * 0.5
    hw = CHAMBER_W * 0.5
    f_door = -CHAMBER_PROUD
    for cx in COL_X:
        for cz in ROW_Z:
            # 상자 몸통 — 옆·위·아래 벽이 있어야 **입체**로 읽힌다.
            # 앞판만 두면 다시 「무늬」가 된다
            for s_ in (-1, 1):
                box(b_iron, (cx + s_ * (hw - 0.018), (f_door + FACE_T) * 0.5, cz),
                    (0.036, FACE_T - f_door, CHAMBER_H))
                box(b_iron, (cx, (f_door + FACE_T) * 0.5, cz + s_ * (hh - 0.018)),
                    (CHAMBER_W - 0.072, FACE_T - f_door, 0.036))
            # 앞판 — 원형 구멍 둘레 네 조각
            box(b_iron, (cx - (hw + ap) * 0.5, f_door + 0.014, cz),
                (hw - ap, 0.028, CHAMBER_H))
            box(b_iron, (cx + (hw + ap) * 0.5, f_door + 0.014, cz),
                (hw - ap, 0.028, CHAMBER_H))
            box(b_iron, (cx, f_door + 0.014, cz + (hh + ap) * 0.5),
                (ap * 2, 0.028, hh - ap))
            box(b_iron, (cx, f_door + 0.014, cz - (hh + ap) * 0.5),
                (ap * 2, 0.028, hh - ap))
            # 모서리 리벳 — 상자가 리벳으로 짜인 물건임을 말한다
            for sx in (-1, 1):
                for sz in (-1, 1):
                    dome(b_trim, (cx + sx * (hw - 0.030), cz + sz * (hh - 0.030)),
                         0.0110, 0.0068, f_door)

            # 클램프 링 — 두 단. 바깥 플랜지 35mm + 안쪽 칼라 60mm
            annulus(b_trim, (cx, cz), RING_D * 0.5, 0.155,
                    f_door - 0.035, f_door, SIDES, phase=R(15))
            annulus(b_trim, (cx, cz), 0.163, GLASS_D * 0.5,
                    f_door - PROTRUDE, f_door - 0.035, SIDES, phase=R(15))
            for i in range(BOLTS):      # 둘레 볼트 — 창마다 같은 개수·같은 위치
                a = TAU * i / BOLTS + R(22.5)
                bp = (cx + BOLT_R * math.cos(a), cz + BOLT_R * math.sin(a))
                prism(b_trim, bp, 0.0185, f_door - 0.050, f_door - 0.035, 6)
                prism(b_trim, bp, 0.0105, f_door - 0.058, f_door - 0.050, 6)

            # 두꺼운 유리 — 링 앞면에서 45mm 안쪽
            gy = f_door - PROTRUDE + GLASS_INSET
            prism(glass.bm, (cx, cz), GLASS_D * 0.5, gy, gy + GLASS_T, SIDES, phase=R(15))
            # 구슬 — 유리 뒤 100mm. 핵 + 헤일로 두 겹
            sy = f_door + SOUL_DEPTH
            sphere(soul.bm, (cx, sy, cz), SOUL_R)
            sphere(halo.bm, (cx, sy, cz), SOUL_R * 1.72, seg=20, ring=12)

    # ── ⑤ 리벳 — 외곽대와 모서리 보강판 ─────────────────────────────────
    def rivet_row(x0, x1, z, step=0.082, y=-PROUD_OUTER):
        n = max(2, int(round(abs(x1 - x0) / step)))
        for i in range(n + 1):
            dome(b_trim, (x0 + (x1 - x0) * i / n, z), 0.0125, 0.0080, y)

    def rivet_col(z0, z1, x, step=0.082, y=-PROUD_OUTER):
        n = max(2, int(round(abs(z1 - z0) / step)))
        for i in range(n + 1):
            dome(b_trim, (x, z0 + (z1 - z0) * i / n), 0.0125, 0.0080, y)

    inset = OUTER * 0.5
    rivet_row(-W * 0.5 + inset, W * 0.5 - inset, inset)
    rivet_row(-W * 0.5 + inset, W * 0.5 - inset, H - inset)
    rivet_col(inset, H - inset, -W * 0.5 + inset)
    rivet_col(inset, H - inset, W * 0.5 - inset)
    # 뱅크 리브 캡의 상·하단 리벳
    for x in (COL_X[0] + PITCH_X * 0.5, COL_X[1] + PITCH_X * 0.5):
        for z in ROW_Z:
            for s in (-1, 1):
                dome(b_trim, (x, z + s * (DH * 0.5 - 0.048)), 0.0082, 0.0048, -PROUD_RIB)

    # 모서리 거싯 — 레퍼런스의 모서리 감싸는 판. **무쇠와 같은 재질이다.**
    # 밝은 강재로 두면 네 귀퉁이에 밝은 사각형이 떠서 상자가 갈라져 보인다
    for sx in (-1, 1):
        for gz in (0.078, H - 0.078):
            box(b_iron, (sx * (W * 0.5 - 0.075), -PROUD_OUTER - 0.010, gz),
                (0.150, 0.020, 0.156))
            for dz in (-0.050, 0.050):
                for dx in (-0.044, 0.044):
                    dome(b_trim, (sx * (W * 0.5 - 0.075) + dx, gz + dz),
                         0.0125, 0.0080, -PROUD_OUTER - 0.010)

    # 측면 장착 러그 — 옆에서 봤을 때 벽에 물린 물건으로 읽히게
    for sx in (-1, 1):
        for lz in (0.30, H - 0.30):
            box(b_trim, (sx * (W * 0.5 + 0.030), 0.150, lz), (0.060, 0.100, 0.110))
            prism(b_trim, (sx * (W * 0.5 + 0.030), lz), 0.022, 0.130, 0.170, 8)

    # ── ⑥ 기초 채널 — 육각 볼트가 박힌 점검 패널 셋 ─────────────────────
    box(b_iron, (0, SILL_D * 0.5, -SILL_H * 0.5), (W, SILL_D, SILL_H))
    box(b_iron, (0, -0.012, -SILL_H * 0.5), (W - 0.020, 0.024, SILL_H - 0.014))
    for i, cx in enumerate(COL_X):
        box(b_iron, (cx, -0.026, -SILL_H * 0.5), (DW - 0.060, 0.028, SILL_H - 0.030))
        prism(b_trim, (cx, -SILL_H * 0.5), 0.030, -0.040, -0.026, 8)   # 팔각 메달리온
        prism(b_trim, (cx, -SILL_H * 0.5), 0.014, -0.050, -0.040, 6)   # 중앙 육각 볼트
        for s in (-1, 1):
            dome(b_trim, (cx + s * (DW * 0.5 - 0.048), -SILL_H * 0.5), 0.0095, 0.0055, -0.026)

    # ⑦ 상단 적색 표시등은 **뺐다.** 사용자 지적 「오른쪽 위 빨간 동그라미는
    # 뭐냐, 안 쓰는 거면 빼 달라」 — 옳다. 무엇을 알리는 등인지 형태에도
    # 게임 상태에도 근거가 없었고, 경고등 역할은 컬럼의 사이렌이 맡는다.

    iron.finish(coll)
    trim.finish(coll)
    dark.finish(coll, bevel=0)
    glass.finish(coll, bevel=0)
    soul.finish(coll, bevel=0)
    halo.finish(coll, bevel=0)
    red.finish(coll)


# ══════════════════════════════════════════════════════════════════════════
# 레버 컬럼 — 캐비닛 오른쪽에 붙는 좁은 제어 컬럼
# ══════════════════════════════════════════════════════════════════════════
def build_column(M, coll):
    """
    레버 컬럼 — 사이렌 경고등 + 세로로 내리는 레버.

    🔴 사용자 지시: 「오른쪽 레버 위에는 사이렌 경고등이 붙어 있고 그 아래에
    레버가 있다. 수직으로 내릴 수 있게 손잡이와 막대, 그리고 그 막대가 충분히
    움직일 수 있을 **막대 모양 깊이감 있는 공간(구멍)** 이 확보되어야 한다.」

    앞선 판본들이 왜 「움직일 것 같지 않았는지」의 최종 원인은 이것이다 —
    막대가 **판 위에 얹혀 있었다.** 레일을 붙이든 사분면을 붙이든, 판 위의
    물건은 갈 곳이 없다. 갈 곳은 그려 넣는 것이 아니라 **파내는 것**이다.
    그래서 앞판을 네 조각으로 끊어 진짜 개구부를 만들고, 그 뒤로 100mm 깊이의
    채널 벽과 어두운 바닥을 세운다. 막대는 그 안에 있다.
    """
    ox = W * 0.5 + COL_W * 0.5 + 0.006
    iron = Part("OH_Column", M["iron"])
    trim = Part("OH_ColumnTrim", M["steel"])
    dark = Part("OH_ColumnSlotDark", M["dark"])
    b, t = iron.bm, trim.bm

    top = H
    cz = (COL_BOTTOM + top) * 0.5
    ch = top - COL_BOTTOM

    slot_hw, slot_z0, slot_z1 = 0.058, 0.55, 1.25     # 막대가 도는 구멍
    slot_depth = 0.120
    fp0, fp1 = -0.036, 0.0                             # 앞판 두께
    px = COL_W * 0.5 - 0.008                           # 앞판 반폭

    box(b, (ox, DEPTH * 0.5, cz), (COL_W, DEPTH, ch))  # 몸통

    # 앞판 — **네 조각.** 통판으로 두면 구멍이 사라진다
    fy, ft = (fp0 + fp1) * 0.5, fp1 - fp0
    for s_ in (-1, 1):
        box(b, (ox + s_ * (px + slot_hw) * 0.5, fy, cz), (px - slot_hw, ft, ch - 0.012))
    box(b, (ox, fy, (slot_z1 + top - 0.006) * 0.5), (slot_hw * 2, ft, top - 0.006 - slot_z1))
    box(b, (ox, fy, (COL_BOTTOM + 0.006 + slot_z0) * 0.5),
        (slot_hw * 2, ft, slot_z0 - COL_BOTTOM - 0.006))

    # 채널 벽 — 깊이가 여기서 나온다
    for s_ in (-1, 1):
        box(t, (ox + s_ * (slot_hw + 0.007), (fp0 + slot_depth) * 0.5, (slot_z0 + slot_z1) * 0.5),
            (0.014, slot_depth - fp0, slot_z1 - slot_z0))
        box(t, (ox, (fp0 + slot_depth) * 0.5, slot_z0 + (slot_z1 - slot_z0) * (0 if s_ < 0 else 1)),
            (slot_hw * 2 + 0.028, slot_depth - fp0, 0.014))
    box(dark.bm, (ox, slot_depth + 0.008, (slot_z0 + slot_z1) * 0.5),
        (slot_hw * 2 + 0.020, 0.016, slot_z1 - slot_z0))

    # 테두리 스트랩 + 리벳
    for s_ in (-1, 1):
        box(b, (ox + s_ * (COL_W * 0.5 - 0.026), -0.030, cz), (0.052, 0.024, ch))
    box(b, (ox, -0.030, COL_BOTTOM + 0.030), (COL_W, 0.024, 0.060))
    box(b, (ox, -0.030, top - 0.030), (COL_W, 0.024, 0.060))
    for i in range(11):
        z = COL_BOTTOM + 0.030 + (ch - 0.060) * i / 10
        for s_ in (-1, 1):
            dome(t, (ox + s_ * (COL_W * 0.5 - 0.026), z), 0.0090, 0.0052, -0.030)

    # 디텐트 이빨 — 어디에 멈추는 물건인지 말한다
    for k in range(6):
        box(t, (ox + slot_hw + 0.026, -0.044,
                slot_z0 + 0.055 + (slot_z1 - slot_z0 - 0.11) * k / 5), (0.032, 0.020, 0.016))

    # ── 막대와 손잡이 — 채널 **안**에 있다 ────────────────────────────────
    rod_y = slot_depth * 0.32          # 막대는 홈 앞쪽에 있어야 빛을 받는다
    gl_z = slot_z0 - 0.010             # 막대가 들어가는 글랜드 높이
    z_car = 1.16                                        # 정지 위치(내리기 전, 위)
    prism_z(t, (ox, rod_y), 0.030, gl_z, z_car + 0.055, 12)      # 막대
    box(t, (ox, rod_y, z_car), (0.076, 0.070, 0.086))            # 캐리지
    for s_ in (-1, 1):                                           # 채널을 타는 롤러
        cyl_x(t, (ox + s_ * slot_hw, rod_y, z_car + 0.030), 0.016, 0.030, 10)
        cyl_x(t, (ox + s_ * slot_hw, rod_y, z_car - 0.030), 0.016, 0.030, 10)
    for zs_ in (slot_z0 + 0.030, slot_z1 - 0.030):               # 상·하 스토퍼
        box(t, (ox, rod_y + 0.020, zs_), (slot_hw * 2, 0.040, 0.026))

    # 손잡이 스템은 **구멍을 통과해** 실내로 나온다.
    #
    # 🔴 사용자 지적: 「손잡이가 너무 들어가 있어서 못 잡는 것처럼 보인다.
    # 손 들어갈 틈이 안 보인다.」 옳다 — 직전 판본은 봉 중심이 판에서 94mm 였고
    # 봉 반지름 22mm 를 빼면 **틈이 72mm** 였다. 주먹이 안 들어간다.
    # 230mm 로 빼면 봉 뒤로 200mm 가 비고, 스템을 가늘게(32mm) 두면 손이 봉을
    # 감싸는 데 걸리는 것이 없다.
    grip_y = -0.230
    box(t, (ox, (rod_y + grip_y) * 0.5, z_car), (0.032, rod_y - grip_y, 0.044))
    cyl_x(t, (ox, grip_y, z_car), 0.024, 0.058, 10)
    grip = Part("OH_LeverGrip", M["red"])
    cyl_x(grip.bm, (ox, grip_y, z_car), GRIP_D * 0.5, GRIP_LEN, 14)
    for s_ in (-1, 1):
        cyl_x(grip.bm, (ox + s_ * (GRIP_LEN * 0.5 + 0.011), grip_y, z_car),
              GRIP_D * 0.5 + 0.011, 0.026, 12)
    grip.finish(coll, bevel=0.001)

    # 막대가 들어가는 곳 — 홈 바닥의 글랜드 하우징.
    #
    # ⚠ 직전 판본은 여기에 스포크 달린 플라이휠을 뒀다. 사용자가 「저 동그란 건
    # 뭐냐, 이해할 수 없다」고 했고 옳은 지적이다 — 직선으로 내려오는 막대가
    # 왜 바퀴를 돌리는지 **형태 어디에도 근거가 없었다.** 회전 레버 판본에서
    # 크랭크로 이어져 있던 부품인데, 기구를 직선으로 바꾸면서 남겨 둔 것이다.
    # 근거 없는 부품은 빼는 것이 맞다. 막대는 그냥 하우징으로 들어간다.
    box(b, (ox, (fp0 + slot_depth) * 0.5, gl_z - 0.045), (0.170, slot_depth - fp0, 0.090))
    box(t, (ox, -0.048, gl_z - 0.030), (0.130, 0.028, 0.048))
    for s_ in (-1, 1):
        dome(t, (ox + s_ * 0.046, gl_z - 0.030), 0.0095, 0.0058, -0.048)
    prism_z(t, (ox, rod_y), 0.036, gl_z - 0.014, gl_z + 0.014, 12)   # 패킹 글랜드

    # ── 사이렌 경고등 — 레버 **위** ──────────────────────────────────────
    sy, sz = -0.098, 1.44
    box(b, (ox, -0.058, sz - 0.035), (0.150, 0.056, 0.044))       # 받침 브래킷
    for s_ in (-1, 1):
        dome(t, (ox + s_ * 0.052, sz - 0.035), 0.0095, 0.0058, -0.086)
    prism_z(t, (ox, sy), 0.080, sz - 0.014, sz + 0.018, 14)       # 베이스
    siren = Part("OH_SirenLens", M["siren"])
    prism_z(siren.bm, (ox, sy), 0.070, sz + 0.018, sz + 0.118, 14)
    siren.finish(coll, bevel=0.001)
    prism_z(t, (ox, sy), 0.090, sz + 0.118, sz + 0.150, 14)       # 갓
    for k in range(4):                                            # 보호 케이지
        a = TAU * k / 4 + R(45)
        strut(t, (ox + 0.074 * math.cos(a), sy + 0.074 * math.sin(a), sz + 0.014),
              (ox + 0.074 * math.cos(a), sy + 0.074 * math.sin(a), sz + 0.140), 0.016, 0.016)

    iron.finish(coll)
    trim.finish(coll)
    dark.finish(coll, bevel=0)
    return ox


# ══════════════════════════════════════════════════════════════════════════
# 전력 표시기 — 별개의 작은 계기 상자
# ══════════════════════════════════════════════════════════════════════════
def build_power_box(M, coll, origin):
    bw, bh, bd = 0.46, 0.36, 0.15
    iron = Part("OH_PowerBox", M["iron"])
    trim = Part("OH_PowerBoxTrim", M["steel"])
    scr = Part("OH_PowerScreen", M["screen"])
    b, t = iron.bm, trim.bm

    box(b, (0, bd * 0.5, 0), (bw, bd, bh))
    # 전면 베젤 — **판이 아니라 테두리다.** 첫 판본은 통판이라 화면을 덮었다
    sw, sh = bw - 0.150, bh - 0.150       # 화면 개구부
    for s in (-1, 1):
        box(b, (s * (sw + bw - 0.030) * 0.25, -0.014, 0),
            ((bw - 0.030 - sw) * 0.5, 0.028, bh - 0.030))
        box(b, (0, -0.014, s * (sh + bh - 0.030) * 0.25),
            (sw, 0.028, (bh - 0.030 - sh) * 0.5))
    for s in (-1, 1):                                              # 모서리 스트랩
        box(b, (s * (bw * 0.5 - 0.028), -0.026, 0), (0.056, 0.026, bh))
        box(b, (0, -0.026, s * (bh * 0.5 - 0.024)), (bw, 0.026, 0.048))
        box(t, (s * (bw * 0.5 + 0.018), bd * 0.5, 0), (0.036, 0.070, 0.090))
    for sx in (-1, 1):
        for sz in (-1, 1):
            dome(t, (sx * (bw * 0.5 - 0.028), sz * (bh * 0.5 - 0.024)), 0.0095, 0.0055, -0.026)
        for zz in (-0.055, 0.055):
            dome(t, (sx * (bw * 0.5 - 0.028), zz), 0.0095, 0.0055, -0.026)

    box(scr.bm, (0, -0.004, -0.004), (bw - 0.130, 0.012, bh - 0.130))   # 유리창

    o = Vector(origin)
    iron.finish(coll, loc=o)
    trim.finish(coll, loc=o)
    scr.finish(coll, bevel=0, loc=o)

    text(coll, "POWER", (o.x, o.y - 0.012, o.z + 0.082), 0.030, M["ledw"])
    text(coll, "014", (o.x - 0.062, o.y - 0.012, o.z + 0.006), 0.062, M["led"])
    text(coll, "/ 100", (o.x + 0.072, o.y - 0.012, o.z + 0.010), 0.042, M["ledw"])
    text(coll, "REQUIRED", (o.x, o.y - 0.012, o.z - 0.086), 0.024, M["led"])
    box_bar = Part("OH_PowerBar", M["led"])
    box(box_bar.bm, (0, -0.010, -0.052), (bw - 0.180, 0.006, 0.004))
    box_bar.finish(coll, bevel=0, loc=o)


# ══════════════════════════════════════════════════════════════════════════
def text(coll, body, loc, size, mat):
    cu = bpy.data.curves.new(f"OHT_{body}", type='FONT')
    cu.body = body
    cu.size = size
    cu.align_x = 'CENTER'
    cu.align_y = 'CENTER'
    cu.extrude = 0.0012
    cu.space_character = 1.05
    ob = bpy.data.objects.new(f"OHTxt_{body}", cu)
    ob.location = loc
    ob.rotation_euler = (R(90), 0, 0)
    cu.materials.append(mat)
    coll.objects.link(ob)
    return ob


def fresh_collection():
    old = bpy.data.collections.get(COLL_NAME)
    if old:
        for ob in list(old.objects):
            bpy.data.objects.remove(ob, do_unlink=True)
        bpy.data.collections.remove(old)
    coll = bpy.data.collections.new(COLL_NAME)
    bpy.context.scene.collection.children.link(coll)
    return coll


def build_all(with_scene=True):
    for name in ("Cube",):
        ob = bpy.data.objects.get(name)
        if ob:
            bpy.data.objects.remove(ob, do_unlink=True)
    M = make_materials()
    coll = fresh_collection()
    build_cabinet(M, coll)
    ox = build_column(M, coll)
    # 전력 계기 상자는 사용자 지시로 보류한다(`build_power_box` 는 남겨 둔다).
    # 명판·해저드 스트라이프·문자도 뺐다 — 텍스처로 들어갈 것을 지오메트리로
    # 만들면 나중에 두 벌을 관리하게 된다
    if with_scene:
        setup_scene(M)
    tris = 0
    for ob in coll.objects:
        if ob.type == 'MESH':
            tris += sum(len(p.vertices) - 2 for p in ob.data.polygons)
    return {"objects": len(coll.objects), "tris_before_bevel": tris,
            "W": W, "H": H, "D": DEPTH, "column_x": ox}


# ══════════════════════════════════════════════════════════════════════════
# 씬 — 레퍼런스 시트와 같은 회색 스튜디오
# ══════════════════════════════════════════════════════════════════════════
def setup_scene(M):
    sc = bpy.context.scene
    for n in ("Light",):
        ob = bpy.data.objects.get(n)
        if ob:
            bpy.data.objects.remove(ob, do_unlink=True)
    for ob in list(bpy.data.objects):
        if ob.name.startswith("OHLight") or ob.name == "OHCam" or ob.name == "OHBackdrop":
            bpy.data.objects.remove(ob, do_unlink=True)

    world = sc.world or bpy.data.worlds.new("World")
    sc.world = world
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.055, 0.055, 0.058, 1.0)
        bg.inputs[1].default_value = 0.55

    # 배경판
    me = bpy.data.meshes.new("OHBackdrop")
    bm = bmesh.new()
    box(bm, (0, 2.6, 0.9), (18, 0.1, 12))
    bm.to_mesh(me)
    bm.free()
    bd = bpy.data.objects.new("OHBackdrop", me)
    bd.data.materials.append(flat_mat("OH_Backdrop", (0.20, 0.20, 0.205), rough=0.9))
    sc.collection.objects.link(bd)

    def area(name, loc, rot, size, energy, color=(1, 1, 1)):
        d = bpy.data.lights.new(name, 'AREA')
        d.energy = energy
        d.size = size
        d.color = color
        ob = bpy.data.objects.new(name, d)
        ob.location = loc
        ob.rotation_euler = rot
        sc.collection.objects.link(ob)
        return ob

    area("OHLight_Key", (-2.6, -3.0, 3.2), (R(52), 0, R(-40)), 3.0, 1400)
    area("OHLight_Fill", (3.2, -2.6, 1.2), (R(80), 0, R(52)), 3.2, 420,
         color=(0.78, 0.83, 1.0))
    area("OHLight_Rim", (2.2, 2.4, 2.6), (R(120), 0, R(150)), 2.4, 700,
         color=(1.0, 0.86, 0.72))

    cam_data = bpy.data.cameras.new("OHCam")
    cam_data.lens = 78
    cam = bpy.data.objects.new("OHCam", cam_data)
    sc.collection.objects.link(cam)
    sc.camera = cam

    for eng in ('BLENDER_EEVEE_NEXT', 'BLENDER_EEVEE', 'CYCLES'):
        try:
            sc.render.engine = eng
            break
        except TypeError:
            continue
    ee = getattr(sc, "eevee", None)
    if ee is not None:
        for attr, val in (("use_raytracing", True), ("taa_render_samples", 64),
                          ("use_shadows", True), ("use_bloom", True)):
            if hasattr(ee, attr):
                try:
                    setattr(ee, attr, val)
                except Exception:
                    pass
    sc.render.resolution_x = 1000
    sc.render.resolution_y = 1180
    sc.render.film_transparent = False
    sc.view_settings.view_transform = 'AgX' if 'AgX' in [
        t.name for t in bpy.types.ColorManagedViewSettings.bl_rna.properties['view_transform'].enum_items
    ] else sc.view_settings.view_transform


def look_at(ob, target):
    d = Vector(target) - ob.location
    ob.rotation_euler = d.to_track_quat('-Z', 'Y').to_euler()


def export_fbx(path, apply_modifiers=True):
    """
    Unity 용 FBX. Y-up / −Z forward 로 내보내 유니티에서 회전 보정이 필요 없게 한다.
    베벨 모디파이어는 적용해서 나간다 — 유니티에는 그 모디파이어가 없다.
    """
    coll = bpy.data.collections.get(COLL_NAME)
    bpy.ops.object.select_all(action='DESELECT')
    for ob in coll.objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = next(iter(coll.objects))
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, apply_unit_scale=True,
        global_scale=1.0, axis_forward='-Z', axis_up='Y',
        object_types={'MESH'}, use_mesh_modifiers=apply_modifiers,
        mesh_smooth_type='FACE', bake_space_transform=False,
        path_mode='STRIP')
    return path


def render_sheet(out_dir, tag=""):
    """레퍼런스 시트와 같은 네 각도."""
    import os
    views = {
        "front": ((0.52, -7.4, 0.86), (0.52, 0.10, 0.84), (1500, 1050)),
        "hero34": ((-2.35, -3.95, 1.95), (0.02, 0.12, 0.88), (1050, 1200)),
        "right34": ((3.30, -3.55, 1.70), (0.55, 0.12, 0.86), (1050, 1200)),
        "side": ((5.20, -1.30, 1.05), (0.60, 0.12, 0.86), (900, 1200)),
    }
    made = []
    for k, (loc, tgt, res) in views.items():
        made.append(render_view(os.path.join(out_dir, f"oh_{k}{tag}.png"), loc, tgt, res))
    return made


def render_view(path, cam_loc, target=None, res=(1000, 1180)):
    sc = bpy.context.scene
    cam = bpy.data.objects.get("OHCam")
    cam.location = Vector(cam_loc)
    look_at(cam, target or (0.35, 0.10, 0.92))
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.filepath = path
    sc.render.image_settings.file_format = 'PNG'
    bpy.ops.render.render(write_still=True)
    return path
