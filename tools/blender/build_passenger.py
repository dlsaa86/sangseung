# -*- coding: utf-8 -*-
"""
PSG_MINER — 승객 1호 「광부」 절차적 조립기 (Blender)

레퍼런스: 사용자 제공 T-포즈 모델 시트 (2026-08-04)
    안전모 + 전방 헤드램프 · 박스형 작업 재킷 · 벨트에 매단 신분증 4장 ·
    통 넓은 작업 바지 · 각진 워크부츠 · 얼룩덜룩한 픽셀 위장 텍스처

스타일 정본: docs/VISUAL_SPEC.md §1
    「단순하고 각진 로우 폴리 실루엣」 · 「PS1 및 초기 PS2 생존 호러의 저해상도 감각」

─────────────────────────────────────────────────────────────────────────
결과물은 **오브젝트 하나**다 (`PSG_Miner`)
─────────────────────────────────────────────────────────────────────────
부위별로 조립하지만 마지막에 합친다. 이유 셋:

  1. 나중에 리깅하면 스킨드 메시는 하나여야 한다. 파츠를 나누면 어깨·무릎에서
     **이음매가 벌어진다** — 각 파츠가 자기 본만 따라가기 때문이다.
  2. 파츠 8개는 드로우콜 8개다. 승객이 여럿 타는 게임에서 그대로 곱해진다.
  3. PS1 게임이 파츠를 나눈 건 당시 하드웨어가 스키닝을 못 해서지 스타일이
     아니다. 그 제약을 흉내 낼 이유가 없다.

재질 슬롯은 합쳐도 유지되므로 부위별 색은 그대로 살아 있다.

─────────────────────────────────────────────────────────────────────────
단면은 사각이 아니라 **모따기 8각**이다
─────────────────────────────────────────────────────────────────────────
v1 은 전부 사각 단면(744 tri)이라 팔다리가 각목처럼 보였다. 8각 모따기 링은
「상자인데 모서리가 깎인」 형태 — 산업용 로우폴리의 그 느낌을 정확히 낸다.
타원(정n각형)을 쓰지 않는 이유: 옷과 부츠는 둥근 게 아니라 **각진 것이 깎인** 것이다.

좌표계 (Blender, Z-up)
    x  좌우      (+x = 캐릭터의 왼쪽)
    y  앞뒤      (**−y = 캐릭터가 바라보는 방향**)
    z  높이      0 … 1.752   (바닥 = 발바닥)

축 규약은 `build_cabin.py` 와 같다. `axis_forward='-Z', axis_up='Y'` 로 내보내면

    블렌더 (x, y, z)  →  유니티 (−x, z, −y)

이므로 여기서 −y 를 보게 지으면 유니티에서 +z 를 본다. 카 안에서 +z 는
장치 벽(`WallRearZ = +2.30`)이다. 즉 기본 자세는 「장치를 마주 본 승객」이다.
x 가 뒤집혀 **유니티에서는 좌우가 거울상**이 되지만 이 캐릭터는 대칭이라 무해하다.
나중에 한쪽에만 다는 물건(공구 가방·완장)을 붙일 때만 기억하면 된다.

포즈
    "idle"   기본값. 팔을 몸 옆으로 내린 대기 자세 — 게임에 그대로 세울 수 있다.
    "tpose"  레퍼런스와 같은 T-포즈. 리깅 전 대조용이며 게임에 세우지 않는다.
             T-포즈를 씬에 세우면 「고장난 캐릭터」로 읽힌다.

실행:
    exec(open(r"B:\\PROJECT_NEW_BORN\\Upandup_DDD\\tools\\blender\\build_passenger.py",
              encoding="utf-8").read())
    build_all()          # idle, 하나로 합쳐진 PSG_Miner
    preview()
    export()
"""

import bpy
import bmesh
import math
from mathutils import Vector

COLL_NAME = "PSG_MINER"
OBJ_NAME = "PSG_Miner"
TAU = math.tau


# ══════════════════════════════════════════════════════════════════════════
# 치수 정본 — 총신장 1.752 m (안전모 꼭대기)
# ══════════════════════════════════════════════════════════════════════════
# 플레이어 눈높이가 1.70 m(씬 실측)이므로 승객의 눈(1.645)은 그보다 살짝 아래다.
# 마주 섰을 때 플레이어가 미세하게 내려다본다 — 의도한 관계다.

Z_SOLE       = 0.000
Z_ANKLE      = 0.105
Z_CALF       = 0.300
Z_KNEE       = 0.475
Z_THIGH      = 0.680
Z_CROTCH     = 0.855
Z_HIP        = 0.950
# 🔴 v2 교정 0.985 → 0.918. 레퍼런스의 재킷은 벨트에서 한 뼘 더 내려와
# **허리 아래 치마단**을 만든다. 6cm 밖에 안 내려오면 벨트가 밑단 위에 얹힌
# 것처럼 보이고 상의가 짧아 보인다.
Z_HEM        = 0.918      # 재킷 밑단
Z_BELT       = 1.045
Z_WAIST      = 1.150
Z_CHEST      = 1.280
Z_DELTOID    = 1.380      # 어깨 최대폭 — 어깨마루보다 아래다
Z_SHOULDER   = 1.435
Z_NECK       = 1.495
Z_CHIN       = 1.565
Z_CHEEK      = 1.610
Z_EYE        = 1.648
Z_BROW       = 1.672
Z_SKULL      = 1.705
Z_HELMET_TOP = 1.752

# 몸통 반폭(x) · 반깊이(y) · 모따기
W_HIP,   D_HIP   = 0.168, 0.108
W_WAIST, D_WAIST = 0.158, 0.102
W_CHEST, D_CHEST = 0.196, 0.118
W_SHLDR, D_SHLDR = 0.212, 0.112
W_NECK,  D_NECK  = 0.062, 0.062

COAT_PAD  = 0.022         # 옷 한 겹 두께 — 「옷을 입은 사람」을 만드는 여유
HEM_FLARE = 0.026
CH_BODY   = 0.042         # 몸통 모따기
CH_LIMB   = 0.018         # 팔다리 모따기

# 다리
W_THIGH, D_THIGH = 0.098, 0.104
W_KNEE,  D_KNEE  = 0.082, 0.092
W_CALF,  D_CALF  = 0.088, 0.096
W_ANKLE, D_ANKLE = 0.060, 0.070
LEG_X            = 0.092

# 부츠
BOOT_LEN_F = 0.168
BOOT_LEN_B = 0.088
BOOT_W     = 0.080
BOOT_TOP   = 0.212
SOLE_H     = 0.030
SOLE_LIP   = 0.012

# 팔 — 삼각근 안에서 나오게 뿌리를 안쪽에 둔다
ARM_ROOT_X = 0.168
W_UPPER    = 0.062
W_ELBOW    = 0.053
W_WRIST    = 0.044
L_UPPER    = 0.290
L_FORE     = 0.235
PALM_L     = 0.098
FING_L     = 0.068
HAND_W     = 0.050
HAND_T     = 0.032

# 머리
W_HEAD, D_HEAD = 0.086, 0.096

# 안전모
HELM_R      = 0.116
HELM_BRIM   = 0.026
HELM_BRIM_T = 0.014
HELM_SIDES  = 12
LAMP_R      = 0.029
LAMP_D      = 0.028

# 벨트 · 신분증
BELT_T     = 0.028
CARD_W     = 0.050
CARD_H     = 0.070
CARD_T     = 0.006
CARD_COUNT = 4
CARD_PITCH = 0.056
POUCH_W    = 0.086
POUCH_H    = 0.074
POUCH_D    = 0.038


# ══════════════════════════════════════════════════════════════════════════
# 재질 — 레퍼런스의 색조. 위장 무늬는 텍스처가 맡는다
# ══════════════════════════════════════════════════════════════════════════
# ⚠ 얼굴 이목구비·수염·위장 얼룩은 **형상이 아니라 텍스처의 일이다.**
#    레퍼런스도 그렇게 만들어져 있다. 여기서 눈두덩을 깎으면 폴리곤만 늘고
#    PS1 감각에서 멀어진다. 지오메트리는 실루엣과 큰 면 분할까지만 한다.

MAT_COAT    = "PSG_Coat"
MAT_PANTS   = "PSG_Pants"
MAT_SKIN    = "PSG_Skin"
MAT_SHIRT   = "PSG_Shirt"
MAT_HELMET  = "PSG_Helmet"
MAT_LAMP    = "PSG_Lamp"
MAT_BOOT    = "PSG_Boot"
MAT_LEATHER = "PSG_Leather"
MAT_CARD    = "PSG_Card"
MAT_METAL   = "PSG_Metal"      # 버클 · 아일릿 · 램프 브래킷

#                      R      G      B     rough
_MAT_BASE = {
    MAT_COAT:    (0.246, 0.208, 0.145, 0.90),
    MAT_PANTS:   (0.196, 0.166, 0.118, 0.92),
    MAT_SKIN:    (0.430, 0.312, 0.240, 0.78),
    MAT_SHIRT:   (0.078, 0.070, 0.058, 0.94),
    MAT_HELMET:  (0.300, 0.244, 0.152, 0.62),
    MAT_LAMP:    (0.960, 0.900, 0.700, 0.22),
    MAT_BOOT:    (0.132, 0.108, 0.084, 0.80),
    MAT_LEATHER: (0.176, 0.140, 0.100, 0.76),
    MAT_CARD:    (0.660, 0.632, 0.560, 0.92),
    MAT_METAL:   (0.240, 0.224, 0.196, 0.45),
}


def get_mat(name):
    m = bpy.data.materials.get(name)
    if m:
        return m
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = m.node_tree.nodes.get("Principled BSDF")
    r, g, b, rough = _MAT_BASE.get(name, (0.2, 0.2, 0.2, 0.9))
    if bsdf:
        bsdf.inputs["Base Color"].default_value = (r, g, b, 1.0)
        bsdf.inputs["Roughness"].default_value = rough
        if "Metallic" in bsdf.inputs:
            bsdf.inputs["Metallic"].default_value = 0.55 if name == MAT_METAL else 0.0
        if name == MAT_LAMP and "Emission Color" in bsdf.inputs:
            bsdf.inputs["Emission Color"].default_value = (1.0, 0.93, 0.72, 1.0)
            bsdf.inputs["Emission Strength"].default_value = 5.0
    return m


# ══════════════════════════════════════════════════════════════════════════
# 메시 빌더
# ══════════════════════════════════════════════════════════════════════════
class MB:
    def __init__(self):
        self.bm = bmesh.new()
        self.mats = []

    def slot(self, name):
        if name not in self.mats:
            self.mats.append(name)
        return self.mats.index(name)

    def _face(self, pts, idx):
        vs = [self.bm.verts.new(p) for p in pts]
        try:
            f = self.bm.faces.new(vs)
        except ValueError:
            return
        f.material_index = idx

    def loft(self, rings, mat, cap_start=True, cap_end=True):
        """정점 수가 같은 고리들을 이어 붙인다. 고리는 바깥에서 봤을 때 반시계."""
        idx = self.slot(mat)
        n = len(rings[0])
        for a, b in zip(rings[:-1], rings[1:]):
            for i in range(n):
                j = (i + 1) % n
                self._face([a[i], a[j], b[j], b[i]], idx)
        if cap_start:
            self._face(list(reversed(rings[0])), idx)
        if cap_end:
            self._face(list(rings[-1]), idx)

    def box(self, center, size, mat, chamfer=0.0):
        cx, cy, cz = center
        hx, hy, hz = size[0] * 0.5, size[1] * 0.5, size[2] * 0.5
        if min(hx, hy, hz) <= 0:
            return
        c = min(chamfer, hx * 0.8, hy * 0.8)
        self.loft([chamfer_ring(cx, cy, cz - hz, hx, hy, c),
                   chamfer_ring(cx, cy, cz + hz, hx, hy, c)], mat)

    def mirror_x(self):
        """x 를 뒤집는다. 감김이 뒤집히지만 `finish` 의 법선 재계산이 되돌린다.

        왜 필요한가: 팔은 방향 벡터의 외적으로 단면 축을 잡는데, 외적은
        **거울 대칭이 아니다.** 좌우를 따로 계산하면 엄지가 한쪽은 바깥,
        한쪽은 안쪽으로 붙는다(v1 실측 26mm 어긋남). 한쪽만 짓고 뒤집는다.
        """
        for v in self.bm.verts:
            v.co.x = -v.co.x
        return self

    def finish(self, name, coll):
        me = bpy.data.meshes.new(name)
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts[:], dist=1e-5)
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces[:])
        self.bm.to_mesh(me)
        self.bm.free()
        ob = bpy.data.objects.new(name, me)
        for mname in self.mats:
            me.materials.append(get_mat(mname))
        for p in me.polygons:
            p.use_smooth = False          # PS1 감각 — 면마다 평평하게
        coll.objects.link(ob)
        return ob


# ── 링 생성기 ──────────────────────────────────────────────────────────────
def chamfer_ring(cx, cy, cz, hw, hd, c):
    """모따기 8각 고리 (수평). 위에서 봤을 때 반시계.

    「상자인데 모서리가 깎인」 단면. 타원보다 각지고 사각보다 부드럽다 —
    작업복·부츠·안전모가 실제로 그렇게 생겼다.
    """
    c = max(0.0, min(c, hw * 0.9, hd * 0.9))
    return [(cx + hw - c, cy - hd, cz), (cx + hw, cy - hd + c, cz),
            (cx + hw, cy + hd - c, cz), (cx + hw - c, cy + hd, cz),
            (cx - hw + c, cy + hd, cz), (cx - hw, cy + hd - c, cz),
            (cx - hw, cy - hd + c, cz), (cx - hw + c, cy - hd, cz)]


def ngon_ring(cx, cy, cz, rx, ry, sides, phase=0.0):
    return [(cx + rx * math.cos(phase + TAU * i / sides),
             cy + ry * math.sin(phase + TAU * i / sides), cz)
            for i in range(sides)]


def oriented_chamfer(origin, right, up, hw, hd, c):
    """임의 방향 사지용 모따기 8각 고리."""
    o, r, u = Vector(origin), Vector(right), Vector(up)
    c = max(0.0, min(c, hw * 0.9, hd * 0.9))
    pat = [(hw - c, -hd), (hw, -hd + c), (hw, hd - c), (hw - c, hd),
           (-hw + c, hd), (-hw, hd - c), (-hw, -hd + c), (-hw + c, -hd)]
    return [tuple(o + r * a + u * b) for a, b in pat]


def oriented_ngon(origin, right, up, rx, ry, sides, phase=0.0):
    o, r, u = Vector(origin), Vector(right), Vector(up)
    return [tuple(o + r * (rx * math.cos(phase + TAU * i / sides))
                    + u * (ry * math.sin(phase + TAU * i / sides)))
            for i in range(sides)]


def _axes(d):
    """방향 d 와 직교하는 단면 축 둘. d 가 수직에 가까워도 안정적이다."""
    d = Vector(d).normalized()
    ref = Vector((0, 1, 0)) if abs(d.y) < 0.9 else Vector((1, 0, 0))
    a = d.cross(ref).normalized()
    b = d.cross(a).normalized()
    return a, b


# ══════════════════════════════════════════════════════════════════════════
# 포즈
# ══════════════════════════════════════════════════════════════════════════
POSES = {
    "tpose": {"upper": (1.000, 0.000,  0.000), "fore": (1.000, 0.000,  0.000)},
    # 🔴 v2 교정: 아래팔 앞으로 기울기 −0.150 → −0.055. 측면에서 **손이 허벅지
    # 앞으로 한 뼘 나가** 무언가를 잡으려는 자세로 읽혔다. 대기 자세의 손은
    # 허벅지 옆선에 거의 붙는다.
    "idle":  {"upper": (0.215, 0.030, -0.976), "fore": (0.120, -0.055, -0.991)},
}


def _arm_dirs(pose):
    """항상 **왼팔(+x)** 방향을 준다. 오른팔은 다 짓고 x 를 뒤집어 만든다."""
    p = POSES[pose]
    return Vector(p["upper"]).normalized(), Vector(p["fore"]).normalized()


# ══════════════════════════════════════════════════════════════════════════
# 몸통 — 재킷
# ══════════════════════════════════════════════════════════════════════════
def build_torso(coll):
    mb = MB()
    P = COAT_PAD
    C = CH_BODY

    # 재킷 몸통. 최대폭(삼각근)을 어깨마루보다 5.5cm 아래에 둔다 —
    # 맨 위를 최대폭으로 평평하게 덮으면 **옷걸이처럼** 보인다(v1 실패).
    mb.loft([
        chamfer_ring(0, 0, Z_HEM,             W_HIP + P + HEM_FLARE,
                                              D_HIP + P + HEM_FLARE * 0.6, C),
        chamfer_ring(0, 0, Z_BELT,            W_WAIST + P, D_WAIST + P, C),
        chamfer_ring(0, 0, Z_WAIST,           W_WAIST + P + 0.012,
                                              D_WAIST + P + 0.006, C),
        chamfer_ring(0, 0, Z_CHEST,           W_CHEST + P, D_CHEST + P, C),
        chamfer_ring(0, 0, Z_DELTOID,         W_SHLDR + P, D_SHLDR + P, C),
        chamfer_ring(0, 0, Z_SHOULDER + 0.012, W_SHLDR * 0.62,
                                              D_SHLDR * 0.84, C * 0.7),
    ], MAT_COAT)

    # 재킷 아래로 드러나는 엉덩이
    mb.loft([
        chamfer_ring(0, 0, Z_CROTCH,       W_HIP * 0.96, D_HIP * 0.96, C * 0.7),
        chamfer_ring(0, 0, Z_HIP,          W_HIP,        D_HIP,        C * 0.7),
        chamfer_ring(0, 0, Z_HEM + 0.004,  W_HIP,        D_HIP,        C * 0.7),
    ], MAT_PANTS, cap_end=False)

    # 목
    mb.loft([
        chamfer_ring(0, 0.004, Z_SHOULDER - 0.010, W_NECK * 1.32, D_NECK * 1.30, 0.016),
        chamfer_ring(0, 0.002, Z_NECK,             W_NECK,        D_NECK,        0.016),
    ], MAT_SKIN, cap_start=False)

    yf = -(D_CHEST + P)

    # 앞섶 — 목 아래에서 끝나는 좁은 틈. 크게 만들면 가슴에 흰 V 가 뜬다(v1 실패)
    mb.loft([
        chamfer_ring(0, yf - 0.001, Z_SHOULDER - 0.024, 0.032, 0.005, 0.008),
        chamfer_ring(0, yf - 0.001, Z_CHEST + 0.028,    0.013, 0.005, 0.004),
    ], MAT_SHIRT)

    # 옷깃 좌우 — 얇고 짧게, 실루엣에 각만 준다
    for s in (-1, 1):
        mb.loft([
            oriented_chamfer((s * 0.034, yf - 0.006, Z_SHOULDER - 0.008),
                             (1, 0, 0), (0, 0, 1), 0.030, 0.006, 0.004),
            oriented_chamfer((s * 0.062, yf - 0.013, Z_CHEST + 0.034),
                             (1, 0, 0), (0, 0, 1), 0.021, 0.006, 0.004),
        ], MAT_COAT)

    # 앞여밈 단추줄 — 가슴 중앙 세로 띠
    mb.box((0.0, yf - 0.007, (Z_CHEST + Z_BELT) * 0.5),
           (0.044, 0.012, Z_CHEST - Z_BELT + 0.050), MAT_COAT, 0.006)
    for i in range(3):
        z = Z_BELT + 0.055 + i * 0.075
        mb.box((0.0, yf - 0.015, z), (0.017, 0.008, 0.017), MAT_METAL, 0.004)

    # 가슴 주머니 좌우 — 몸통 + 덮개. 레퍼런스 상의의 핵심 디테일
    for s in (-1, 1):
        px = s * 0.108
        pz = Z_CHEST - 0.030
        mb.box((px, yf - 0.011, pz), (0.086, 0.020, 0.092), MAT_COAT, 0.010)
        mb.box((px, yf - 0.016, pz + 0.052), (0.092, 0.028, 0.024), MAT_COAT, 0.006)
        mb.box((px, yf - 0.028, pz + 0.044), (0.014, 0.008, 0.014), MAT_METAL, 0.003)

    # 어깨 이음 견장 — 삼각근 위를 덮는 얇은 판
    #
    # 🔴 v2 교정: 어깨 바깥까지 나가게 뒀더니 정면에서 **네모난 날개 두 장**으로
    # 읽혔다. 레퍼런스의 어깨는 밖으로 튀지 않는다 — 끝을 삼각근 안(0.150)에서
    # 끊고 아래로 눕혀 이음선만 남긴다.
    for s in (-1, 1):
        mb.loft([
            oriented_chamfer((s * 0.078, 0.0, Z_SHOULDER + 0.010),
                             (1, 0, 0), (0, 1, 0), 0.046, D_SHLDR * 0.76, 0.014),
            oriented_chamfer((s * 0.150, 0.0, Z_DELTOID + 0.018),
                             (1, 0, 0), (0, 1, 0), 0.040, D_SHLDR * 0.82, 0.014),
        ], MAT_COAT)

    return mb.finish("PSG_Torso", coll)


# ══════════════════════════════════════════════════════════════════════════
# 벨트 · 신분증 — 레퍼런스에서 가장 눈에 띄는 식별 요소
# ══════════════════════════════════════════════════════════════════════════
def build_belt(coll):
    mb = MB()
    P = COAT_PAD
    hw = W_WAIST + P + 0.010
    hd = D_WAIST + P + 0.010

    mb.loft([
        chamfer_ring(0, 0, Z_BELT - BELT_T * 0.5, hw, hd, CH_BODY),
        chamfer_ring(0, 0, Z_BELT + BELT_T * 0.5, hw, hd, CH_BODY),
    ], MAT_LEATHER)

    yf = -hd - 0.004

    # 버클 — 틀 + 혀
    mb.box((0.0, yf - 0.009, Z_BELT), (0.066, 0.018, 0.042), MAT_METAL, 0.008)
    mb.box((0.0, yf - 0.016, Z_BELT), (0.020, 0.008, 0.026), MAT_METAL, 0.004)

    # 신분증 — 벨트 앞면에 나란히. 카드마다 조금씩 다르게 매달려 줄 세운 티를 없앤다
    x0 = -(CARD_COUNT - 1) * CARD_PITCH * 0.5
    for i in range(CARD_COUNT):
        x = x0 + i * CARD_PITCH
        drop = 0.005 * ((i % 3) - 1)
        cz = Z_BELT - 0.016 - CARD_H * 0.5 + drop
        mb.box((x, yf - 0.012, cz), (CARD_W, CARD_T, CARD_H), MAT_CARD, 0.005)
        # 사진 칸
        mb.box((x - CARD_W * 0.19, yf - 0.017, cz + CARD_H * 0.17),
               (CARD_W * 0.42, CARD_T * 0.7, CARD_H * 0.34), MAT_LEATHER, 0.003)
        # 집게
        mb.box((x, yf - 0.013, cz + CARD_H * 0.5 + 0.006),
               (CARD_W * 0.34, CARD_T * 1.6, 0.012), MAT_METAL, 0.003)

    # 옆구리 파우치 — 몸통 + 덮개 + 버클
    for s in (-1, 1):
        px = s * (hw + POUCH_D * 0.5 - 0.008)
        pz = Z_BELT - 0.020 - POUCH_H * 0.5
        mb.box((px, -hd * 0.28, pz), (POUCH_D, POUCH_W, POUCH_H), MAT_LEATHER, 0.010)
        mb.box((px, -hd * 0.28, pz + POUCH_H * 0.5 + 0.006),
               (POUCH_D + 0.008, POUCH_W + 0.008, 0.018), MAT_LEATHER, 0.006)
        mb.box((px - s * (POUCH_D * 0.5 + 0.004), -hd * 0.28, pz + POUCH_H * 0.22),
               (0.008, 0.016, 0.020), MAT_METAL, 0.003)

    # 🔴 v2: 멜빵 한 쌍을 뺐다. **레퍼런스에 없다.** 가슴에 어두운 세로 막대
    # 두 개가 생겨 주머니·앞여밈과 경쟁했고, 실루엣이 「군용 하네스」쪽으로 밀렸다.
    # 레퍼런스의 상반신은 재킷 + 벨트 + 신분증 셋으로만 읽힌다.

    # 벨트 고리 — 멜빵을 뺀 자리에 벨트가 재킷에 물려 있다는 것만 남긴다
    for s in (-1, 1):
        mb.box((s * 0.062, yf - 0.004, Z_BELT),
               (0.016, 0.012, BELT_T + 0.016), MAT_LEATHER, 0.004)

    return mb.finish("PSG_Belt", coll)


# ══════════════════════════════════════════════════════════════════════════
# 다리 · 부츠
# ══════════════════════════════════════════════════════════════════════════
def build_leg(coll, side):
    mb = MB()
    x = side * LEG_X
    C = CH_LIMB

    # 바지 — 발목에서 사타구니까지. 무릎에서 한 번 좁아졌다 허벅지로 벌어진다
    mb.loft([
        chamfer_ring(x, 0.004, Z_ANKLE,        W_ANKLE, D_ANKLE, C),
        chamfer_ring(x, 0.002, Z_CALF,         W_CALF,  D_CALF,  C),
        chamfer_ring(x, 0.000, Z_KNEE,         W_KNEE,  D_KNEE,  C),
        chamfer_ring(x, 0.000, Z_THIGH,        W_THIGH, D_THIGH, C),
        chamfer_ring(x, 0.000, Z_CROTCH,       W_THIGH * 1.06, D_THIGH * 1.04, C),
    ], MAT_PANTS)

    # 무릎 보강 패치 — 앞면에 덧댄 판
    mb.loft([
        oriented_chamfer((x, -D_KNEE - 0.004, Z_KNEE - 0.060),
                         (1, 0, 0), (0, 0, 1), W_KNEE * 0.82, 0.058, 0.014),
        oriented_chamfer((x, -D_KNEE - 0.010, Z_KNEE + 0.010),
                         (1, 0, 0), (0, 0, 1), W_KNEE * 0.88, 0.062, 0.014),
    ], MAT_PANTS)

    # 바짓단 — 부츠 위로 덮이며 벌어진다
    mb.loft([
        chamfer_ring(x, 0.004, Z_ANKLE + 0.100, W_ANKLE * 1.14, D_ANKLE * 1.10, C),
        chamfer_ring(x, 0.004, Z_ANKLE + 0.030, W_ANKLE * 1.30, D_ANKLE * 1.22, C),
    ], MAT_PANTS)

    # ── 부츠 ────────────────────────────────────────────────────────────
    sz = Z_SOLE + SOLE_H

    # 목
    mb.loft([
        chamfer_ring(x, 0.006, Z_ANKLE - 0.010, W_ANKLE * 1.14, D_ANKLE * 1.08, C),
        chamfer_ring(x, 0.004, BOOT_TOP,        W_ANKLE * 1.20, D_ANKLE * 1.12, C),
    ], MAT_BOOT)

    # 발등 — 뒤꿈치에서 발끝으로 낮아지며 좁아진다.
    #
    # 🔴 v2 교정: 수평 링(`chamfer_ring`)을 y·z 가 다른 채로 이어 붙였더니
    # **얇은 판 세 장이 비스듬히 걸린** 꼴이 됐고 측면에서 발 아래에 삼각형
    # 빈틈이 보였다. 발은 y 를 따라 쓸어 나가는 형상이므로 단면을 XZ 로 세운다.
    def foot_ring(y, hw, z_center, half_h):
        return oriented_chamfer((x, y, z_center), (1, 0, 0), (0, 0, 1),
                                hw, half_h, 0.014)

    mb.loft([
        foot_ring(BOOT_LEN_B,        BOOT_W * 0.86, sz + 0.048, 0.048),
        foot_ring(0.030,             BOOT_W,        sz + 0.056, 0.056),
        foot_ring(-BOOT_LEN_F * 0.55, BOOT_W * 0.96, sz + 0.044, 0.044),
        foot_ring(-BOOT_LEN_F,       BOOT_W * 0.78, sz + 0.026, 0.026),
    ], MAT_BOOT)

    # 앞코 보강
    mb.box((x, -BOOT_LEN_F + 0.032, sz + 0.020),
           (BOOT_W * 1.78, 0.062, 0.042), MAT_BOOT, 0.014)

    # 밑창 — 바닥에 닿는 판 + 옆으로 나온 립
    mb.loft([
        chamfer_ring(x, (BOOT_LEN_B - BOOT_LEN_F) * 0.5, Z_SOLE,
                     BOOT_W + SOLE_LIP * 0.5, (BOOT_LEN_B + BOOT_LEN_F) * 0.5, 0.026),
        chamfer_ring(x, (BOOT_LEN_B - BOOT_LEN_F) * 0.5, Z_SOLE + SOLE_H * 0.55,
                     BOOT_W + SOLE_LIP, (BOOT_LEN_B + BOOT_LEN_F) * 0.5 + SOLE_LIP, 0.026),
        chamfer_ring(x, (BOOT_LEN_B - BOOT_LEN_F) * 0.5, sz,
                     BOOT_W + SOLE_LIP * 0.3, (BOOT_LEN_B + BOOT_LEN_F) * 0.5 - 0.004, 0.026),
    ], MAT_BOOT)

    # 뒤꿈치 굽. 🔴 v2: 중심을 0.45·SOLE_H 로 올렸다 — 0.40 이면 밑면이 z=−0.0015 로
    # **바닥을 1.5mm 뚫는다.** 유니티에서 발이 바닥에 박힌 것으로 보이는 크기는
    # 아니지만, 원점이 곧 발바닥이라는 규약이 깨지면 배치가 전부 그 오차를 안는다.
    mb.box((x, BOOT_LEN_B - 0.032, Z_SOLE + SOLE_H * 0.45),
           (BOOT_W * 1.9, 0.062, SOLE_H * 0.9), MAT_BOOT, 0.010)

    # 끈 — 발등 위 가로 띠 셋 + 아일릿
    for i in range(3):
        lz = sz + 0.062 + i * 0.040
        ly = -0.006 + i * 0.016
        mb.box((x, ly, lz), (BOOT_W * 1.72, 0.020, 0.011), MAT_LEATHER, 0.004)
        for s2 in (-1, 1):
            mb.box((x + s2 * BOOT_W * 0.84, ly, lz), (0.011, 0.011, 0.011),
                   MAT_METAL, 0.003)

    return mb.finish("PSG_Leg_%s" % ("L" if side > 0 else "R"), coll)


# ══════════════════════════════════════════════════════════════════════════
# 팔 · 손
# ══════════════════════════════════════════════════════════════════════════
def build_arm(coll, side, pose):
    mb = MB()
    C = CH_LIMB
    up_d, fore_d = _arm_dirs(pose)
    root = Vector((ARM_ROOT_X, 0.0, Z_SHOULDER - 0.026))

    a1, b1 = _axes(up_d)
    elbow = root + up_d * L_UPPER
    a2, b2 = _axes(fore_d)
    wrist = elbow + fore_d * L_FORE

    # 소매 — 어깨에서 팔꿈치로. 팔꿈치에서 한 번 접힌다
    mb.loft([
        oriented_chamfer(root, a1, b1, W_UPPER * 1.30, W_UPPER * 1.24, C),
        oriented_chamfer(root + up_d * (L_UPPER * 0.42), a1, b1,
                         W_UPPER, W_UPPER * 0.96, C),
        oriented_chamfer(elbow, a1, b1, W_ELBOW * 1.10, W_ELBOW * 1.06, C),
    ], MAT_COAT)

    mb.loft([
        oriented_chamfer(elbow, a2, b2, W_ELBOW * 1.10, W_ELBOW * 1.06, C),
        oriented_chamfer(elbow + fore_d * (L_FORE * 0.55), a2, b2,
                         W_ELBOW * 0.94, W_ELBOW * 0.90, C),
        oriented_chamfer(wrist, a2, b2, W_WRIST, W_WRIST * 0.94, C),
    ], MAT_COAT)

    # 소맷부리 — 손목을 감싸는 띠. 소매와 손의 경계를 만든다
    mb.loft([
        oriented_chamfer(wrist - fore_d * 0.030, a2, b2,
                         W_WRIST * 1.16, W_WRIST * 1.10, C),
        oriented_chamfer(wrist + fore_d * 0.008, a2, b2,
                         W_WRIST * 1.20, W_WRIST * 1.14, C),
    ], MAT_COAT)

    # 손바닥
    palm_end = wrist + fore_d * PALM_L
    mb.loft([
        oriented_chamfer(wrist + fore_d * 0.004, a2, b2, HAND_W, HAND_T, 0.010),
        oriented_chamfer(wrist + fore_d * (PALM_L * 0.55), a2, b2,
                         HAND_W * 1.12, HAND_T, 0.010),
        oriented_chamfer(palm_end, a2, b2, HAND_W * 1.06, HAND_T * 0.92, 0.010),
    ], MAT_SKIN)

    # 손가락 넷 — 개별 블록. PS1 에서도 이 정도는 형상으로 낸다
    for i in range(4):
        off = (i - 1.5) * (HAND_W * 2.0 / 4.0)
        fl = FING_L * (1.0 - abs(i - 1.2) * 0.09)
        base = palm_end + a2 * off
        mb.loft([
            oriented_chamfer(base, a2, b2, HAND_W * 0.23, HAND_T * 0.88, 0.005),
            oriented_chamfer(base + fore_d * fl * 0.6 + b2 * (-0.004), a2, b2,
                             HAND_W * 0.21, HAND_T * 0.80, 0.005),
            oriented_chamfer(base + fore_d * fl + b2 * (-0.010), a2, b2,
                             HAND_W * 0.17, HAND_T * 0.62, 0.005),
        ], MAT_SKIN)

    # 엄지
    thumb_o = wrist + fore_d * (PALM_L * 0.28) + a2 * (HAND_W * 1.02)
    mb.loft([
        oriented_chamfer(thumb_o, fore_d, b2, 0.026, HAND_T * 0.82, 0.006),
        oriented_chamfer(thumb_o + a2 * 0.022 + fore_d * 0.030, fore_d, b2,
                         0.022, HAND_T * 0.70, 0.006),
        oriented_chamfer(thumb_o + a2 * 0.030 + fore_d * 0.058, fore_d, b2,
                         0.017, HAND_T * 0.56, 0.006),
    ], MAT_SKIN)

    if side < 0:
        mb.mirror_x()
    return mb.finish("PSG_Arm_%s" % ("L" if side > 0 else "R"), coll)


# ══════════════════════════════════════════════════════════════════════════
# 머리 — 각진 상자. 이목구비는 텍스처가 맡되 코와 눈두덩만 형상으로 준다
# ══════════════════════════════════════════════════════════════════════════
def build_head(coll):
    mb = MB()
    C = 0.024
    mb.loft([
        chamfer_ring(0, 0.008, Z_NECK - 0.008, W_HEAD * 0.74, D_HEAD * 0.74, C),
        chamfer_ring(0, 0.006, Z_CHIN,         W_HEAD * 0.90, D_HEAD * 0.94, C),
        chamfer_ring(0, 0.002, Z_CHEEK,        W_HEAD * 0.99, D_HEAD * 1.00, C),
        chamfer_ring(0, 0.000, Z_EYE,          W_HEAD,        D_HEAD,        C),
        chamfer_ring(0, 0.002, Z_BROW,         W_HEAD * 0.98, D_HEAD * 0.97, C),
        chamfer_ring(0, 0.006, Z_SKULL,        W_HEAD * 0.84, D_HEAD * 0.86, C),
    ], MAT_SKIN)

    # 코 — 눈높이에서 코끝까지 짧은 쐐기
    yf = -D_HEAD
    mb.loft([
        oriented_chamfer((0.0, yf - 0.004, Z_BROW - 0.006),
                         (1, 0, 0), (0, 0, 1), 0.013, 0.008, 0.004),
        oriented_chamfer((0.0, yf - 0.017, Z_EYE - 0.022),
                         (1, 0, 0), (0, 0, 1), 0.016, 0.010, 0.004),
    ], MAT_SKIN)

    # 눈두덩 — 이마 아래 가로 능선. 그림자를 만들어 얼굴을 읽히게 한다
    mb.box((0.0, yf - 0.006, Z_BROW + 0.006), (W_HEAD * 1.62, 0.014, 0.016),
           MAT_SKIN, 0.005)

    # 귀 — 안전모 아래로 살짝 보인다
    for s in (-1, 1):
        mb.box((s * (W_HEAD + 0.007), 0.014, Z_EYE - 0.010),
               (0.015, 0.028, 0.042), MAT_SKIN, 0.006)

    return mb.finish("PSG_Head", coll)


# ══════════════════════════════════════════════════════════════════════════
# 안전모 + 헤드램프 — 이 실루엣 하나가 「광부」를 읽히게 한다
# ══════════════════════════════════════════════════════════════════════════
def build_helmet(coll):
    mb = MB()
    n = HELM_SIDES
    z0 = Z_SKULL - 0.050
    ph = TAU / (n * 2)                # 정면에 꼭짓점이 아니라 면이 오게 돌린다

    # 돔 — 세 단으로 끊어 각을 남긴다
    mb.loft([
        ngon_ring(0, 0, z0,                   HELM_R,        HELM_R * 1.03, n, ph),
        ngon_ring(0, 0, z0 + 0.042,           HELM_R * 0.97, HELM_R * 1.00, n, ph),
        ngon_ring(0, 0, Z_HELMET_TOP - 0.016, HELM_R * 0.68, HELM_R * 0.70, n, ph),
        ngon_ring(0, 0, Z_HELMET_TOP,         HELM_R * 0.30, HELM_R * 0.31, n, ph),
    ], MAT_HELMET, cap_start=False)

    # 챙 — 둘러 나온 띠. 앞쪽이 더 나온다
    brim_lo = ngon_ring(0, -0.006, z0 - HELM_BRIM_T,
                        HELM_R + HELM_BRIM, HELM_R + HELM_BRIM * 1.9, n, ph)
    brim_hi = ngon_ring(0, -0.006, z0,
                        HELM_R + HELM_BRIM, HELM_R + HELM_BRIM * 1.9, n, ph)
    mb.loft([brim_lo, brim_hi], MAT_HELMET)

    # 마루 능선 — 세로 보강 리브. 앞→위→뒤
    for s in (0, -1, 1):
        xo = s * HELM_R * 0.52
        w = 0.013 if s == 0 else 0.010
        h = 0.014 if s == 0 else 0.010
        shrink = 1.0 if s == 0 else 0.80
        mb.loft([
            oriented_chamfer((xo, -HELM_R * 0.84 * shrink, z0 + 0.050),
                             (1, 0, 0), (0, 0, 1), w, h, 0.004),
            oriented_chamfer((xo, 0.0, Z_HELMET_TOP - 0.002 - abs(s) * 0.016),
                             (1, 0, 0), (0, 0, 1), w, h, 0.004),
            oriented_chamfer((xo, HELM_R * 0.84 * shrink, z0 + 0.050),
                             (1, 0, 0), (0, 0, 1), w, h, 0.004),
        ], MAT_HELMET)

    # 턱끈 — 챙 양옆에서 턱 아래로
    for s in (-1, 1):
        mb.loft([
            oriented_chamfer((s * (HELM_R * 0.92), 0.008, z0 - 0.006),
                             (0, 1, 0), (0, 0, 1), 0.012, 0.007, 0.004),
            oriented_chamfer((s * (W_HEAD + 0.012), 0.012, Z_CHIN + 0.020),
                             (0, 1, 0), (0, 0, 1), 0.011, 0.006, 0.004),
        ], MAT_LEATHER)
    mb.box((0.0, 0.014, Z_CHIN - 0.004), (W_HEAD * 2.0, 0.020, 0.011),
           MAT_LEATHER, 0.004)

    # ── 헤드램프 ────────────────────────────────────────────────────────
    # 램프는 **앞(−y)을 본다.** `ngon_ring` 은 XY 평면 고리라 여기 쓸 수 없다 —
    # 고리를 XZ 평면으로 세우고 −y 로 밀어 통을 만든다.
    lz = z0 + 0.036
    ly = -(HELM_R * 0.90)

    def lamp_ring(depth, r):
        return [(r * math.cos(TAU / 16 + TAU * i / 8), ly - depth,
                 lz + r * math.sin(TAU / 16 + TAU * i / 8)) for i in range(8)]

    mb.box((0.0, ly + 0.014, lz), (0.052, 0.026, 0.038), MAT_METAL, 0.008)  # 브래킷
    mb.loft([lamp_ring(-0.012, LAMP_R * 1.24),
             lamp_ring(LAMP_D * 0.60, LAMP_R * 1.20)], MAT_HELMET, cap_end=False)
    mb.loft([lamp_ring(LAMP_D * 0.60, LAMP_R * 1.24),
             lamp_ring(LAMP_D * 0.76, LAMP_R * 1.24)], MAT_METAL,
            cap_start=False, cap_end=False)                                  # 베젤
    mb.loft([lamp_ring(LAMP_D * 0.72, LAMP_R * 1.04),
             lamp_ring(LAMP_D, LAMP_R * 0.82)], MAT_LAMP, cap_start=False)   # 렌즈

    # 램프 케이블 — 안전모 옆을 타고 뒤로
    mb.loft([
        oriented_chamfer((0.030, ly + 0.020, lz - 0.014), (1, 0, 0), (0, 0, 1),
                         0.008, 0.008, 0.003),
        oriented_chamfer((HELM_R * 0.80, -HELM_R * 0.30, z0 + 0.012),
                         (1, 0, 0), (0, 0, 1), 0.008, 0.008, 0.003),
        oriented_chamfer((HELM_R * 0.70, HELM_R * 0.62, z0 + 0.004),
                         (1, 0, 0), (0, 0, 1), 0.008, 0.008, 0.003),
    ], MAT_LEATHER)

    return mb.finish("PSG_Helmet", coll)


# ══════════════════════════════════════════════════════════════════════════
# 조립 · 병합 · 프리뷰 · 내보내기
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


def _ensure_object_mode(fallback):
    """오브젝트 모드 + 활성 오브젝트를 보장한다.

    `_fresh_collection` 이 직전 실행의 `PSG_Miner`(= 활성 오브젝트)를 지우면
    활성이 None 이 되고, 그 상태에서 `bpy.ops.object.select_all` 의 poll 이
    실패한다(v2 실측: "context is incorrect"). 조립기는 몇 번을 다시 돌려도
    같은 결과를 내야 하므로 여기서 문맥을 되살린다.
    """
    vl = bpy.context.view_layer
    if vl.objects.active is None or vl.objects.active.name not in vl.objects:
        vl.objects.active = fallback
    if bpy.context.object is not None and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')


def _join(coll):
    """부위를 오브젝트 하나로 합친다. 재질 슬롯은 유지된다."""
    parts = list(coll.objects)
    if len(parts) <= 1:
        if parts:
            parts[0].name = OBJ_NAME
        return parts[0] if parts else None
    _ensure_object_mode(parts[0])
    bpy.ops.object.select_all(action='DESELECT')
    for ob in parts:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    bpy.ops.object.join()
    merged = bpy.context.view_layer.objects.active
    merged.name = OBJ_NAME
    merged.data.name = OBJ_NAME
    return merged


def _unwrap(ob, angle_deg=66.0, margin=0.02):
    """UV 를 편다. 위장 무늬·얼굴·수염은 전부 텍스처의 일이라 UV 가 먼저 있어야 한다.

    스마트 프로젝트를 쓴다 — 각진 로우폴리는 면마다 법선이 확연히 갈려서
    자동 분할이 손으로 자른 것과 거의 같게 나온다.
    """
    _ensure_object_mode(ob)
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(angle_deg),
                             island_margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT')
    return len(ob.data.uv_layers)


def build_all(pose="idle"):
    if pose not in POSES:
        raise ValueError("pose 는 %s 중 하나여야 한다" % list(POSES))
    coll = _fresh_collection()
    build_torso(coll)
    build_belt(coll)
    build_head(coll)
    build_helmet(coll)
    for s in (-1, 1):
        build_leg(coll, s)
        build_arm(coll, s, pose)
    ob = _join(coll)
    _unwrap(ob)
    return {"pose": pose, "object": ob.name, "uv_layers": len(ob.data.uv_layers),
            "tris": sum(max(0, len(p.vertices) - 2) for p in ob.data.polygons),
            "verts": len(ob.data.vertices),
            "material_slots": [m.name for m in ob.data.materials]}


def stats():
    coll = bpy.data.collections.get(COLL_NAME)
    if not coll:
        return {}
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    tris = verts = 0
    for ob in coll.objects:
        for v in ob.data.vertices:
            w = ob.matrix_world @ v.co
            lo = Vector((min(lo.x, w.x), min(lo.y, w.y), min(lo.z, w.z)))
            hi = Vector((max(hi.x, w.x), max(hi.y, w.y), max(hi.z, w.z)))
        verts += len(ob.data.vertices)
        tris += sum(max(0, len(p.vertices) - 2) for p in ob.data.polygons)
    return {"min": tuple(round(v, 4) for v in lo),
            "max": tuple(round(v, 4) for v in hi),
            "size": tuple(round(hi[i] - lo[i], 4) for i in range(3)),
            "verts": verts, "tris": tris,
            "objects": [o.name for o in coll.objects]}


def preview(tag="", outdir=None, key_energy=22.0, fill_energy=6.5):
    """정면·측면·3/4 를 같은 조건으로 렌더한다. 레퍼런스와 나란히 놓고 보기 위한 것.

    직교 투영을 쓴다 — 원근이 섞이면 비율을 눈으로 못 잰다.

    ⚠ 기본 광량은 **실측으로 정했다.** 90W/26W 로 찍었더니 헬멧·손·바지가
    sRGB 1.0 에 붙어 재질 색이 전부 크림색으로 날아갔다(v2 실측:
    coat 0.863 / helmet 1.000 / hand 1.000). 4분의 1 로 내려야 알베도가 읽힌다.
    """
    import os
    outdir = outdir or r"B:\PROJECT_NEW_BORN\Upandup_DDD\Captures\passenger"
    os.makedirs(outdir, exist_ok=True)
    sc = bpy.context.scene

    for ob in list(bpy.data.objects):
        if ob.type in {'CAMERA', 'LIGHT'} and ob.name.startswith("PSG_PV"):
            bpy.data.objects.remove(ob, do_unlink=True)

    # ⚠ 이 .blend 는 ELV_CABIN · ELV_SHAFT · OVENHARVEST 와 같이 산다.
    # 그쪽 조명(`OHLight_Key` 1400W · `OHLight_Rim` 700W · `OHLight_Fill` 420W)이
    # 켜진 채로 렌더되어 **승객이 통째로 하얗게 탔다**(v2 실측: 헬멧·손 sRGB 1.000).
    # 광량을 낮추는 것으로는 못 고친다 — 남의 조명을 꺼야 한다.
    # 렌더가 끝나면 원상 복구한다. 남의 컬렉션 상태를 바꿔 놓고 나오지 않는다.
    _saved = {}
    keep = set()
    _coll = bpy.data.collections.get(COLL_NAME)
    if _coll:
        keep.update(o.name for o in _coll.objects)
    for ob in bpy.data.objects:
        if ob.name in keep or ob.name.startswith("PSG_PV"):
            continue
        _saved[ob.name] = ob.hide_render
        ob.hide_render = True

    cam_d = bpy.data.cameras.new("PSG_PVCam")
    cam_d.type = 'ORTHO'
    cam_d.ortho_scale = 2.0
    cam = bpy.data.objects.new("PSG_PVCam", cam_d)
    sc.collection.objects.link(cam)
    sc.camera = cam

    # ⚠ 뷰 트랜스폼을 반드시 지정한다. 블렌더 기본값 AgX 는 하이라이트를 강하게
    # 탈색시켜 **재질 색이 전부 흰색으로 날아간다.** v1 첫 프리뷰가 정확히 그랬다.
    sc.view_settings.view_transform = 'Standard'
    sc.view_settings.look = 'None'
    sc.view_settings.exposure = 0.0

    key_d = bpy.data.lights.new("PSG_PVKey", type='AREA')
    key_d.energy, key_d.size = key_energy, 2.4
    key = bpy.data.objects.new("PSG_PVKey", key_d)
    key.location = (-1.9, -2.4, 2.9)
    key.rotation_euler = (math.radians(52), 0.0, math.radians(-38))
    sc.collection.objects.link(key)

    fill_d = bpy.data.lights.new("PSG_PVFill", type='AREA')
    fill_d.energy, fill_d.size = fill_energy, 3.0
    fill = bpy.data.objects.new("PSG_PVFill", fill_d)
    fill.location = (2.6, -1.6, 1.6)
    fill.rotation_euler = (math.radians(78), 0.0, math.radians(58))
    sc.collection.objects.link(fill)

    engines = {i.identifier for i in
               bpy.types.RenderSettings.bl_rna.properties['engine'].enum_items}
    sc.render.engine = ('BLENDER_EEVEE_NEXT' if 'BLENDER_EEVEE_NEXT' in engines
                        else 'BLENDER_EEVEE')
    sc.render.resolution_x, sc.render.resolution_y = 620, 940
    sc.render.film_transparent = False
    sc.world = sc.world or bpy.data.worlds.new("PSG_PVWorld")
    sc.world.use_nodes = True
    bg = sc.world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs[0].default_value = (0.02, 0.02, 0.024, 1.0)
        bg.inputs[1].default_value = 0.55

    cz, dist = 0.88, 4.0
    views = {
        "front": (0.0, -dist, cz, math.radians(90), 0.0, 0.0),
        "side":  (dist, 0.0, cz, math.radians(90), 0.0, math.radians(90)),
        "three": (-dist * 0.72, -dist * 0.72, cz + 0.35,
                  math.radians(83), 0.0, math.radians(-45)),
    }
    made = []
    try:
        for name, (x, y, z, rx, ry, rz) in views.items():
            cam.location = (x, y, z)
            cam.rotation_euler = (rx, ry, rz)
            sc.render.filepath = os.path.join(outdir, "psg_%s%s.png" % (name, tag))
            bpy.ops.render.render(write_still=True)
            made.append(sc.render.filepath)
    finally:
        for n, v in _saved.items():
            ob = bpy.data.objects.get(n)
            if ob:
                ob.hide_render = v
    return made


def export(path=None):
    path = path or (r"B:\PROJECT_NEW_BORN\Upandup_DDD\Assets\Prototype_Elevator"
                    r"\Art\Models\PSG_Miner.fbx")
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
