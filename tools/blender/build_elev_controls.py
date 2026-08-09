# -*- coding: utf-8 -*-
"""엘리베이터 조작부 하드서피스 — 좌측 3버튼 콘솔 + 상단 층 표시 하우징.

## 왜 이 파일이 저장소에 있는가

블렌더 **저작 원본은 저장소 밖**(iCloudDrive)에 있고, 이 스크립트를 돌린 시점에
그 파일에는 사용자의 **미저장 변경이 있었다**(`bpy.data.is_dirty == True`).
그래서 사용자의 .blend 를 열지도 저장하지도 않고, 임시 컬렉션에 만들어 FBX 만
내보낸 뒤 흔적을 지웠다. 그 결과 **모델을 다시 만들 근거가 어디에도 안 남는다** —
그걸 막는 것이 이 파일이다.

## 쓰는 법

블렌더에서 이 파일을 Text Editor 로 열어 실행하거나,

    exec(open(r"<repo>/tools/blender/build_elev_controls.py", encoding="utf-8").read())

`TMP_AD_EXPORT` 컬렉션에 다섯 개를 만들고 `EXPORT_PATH` 로 FBX 를 쓴다.
`CLEANUP = True` 면 내보낸 뒤 임시 객체를 지운다 — 남의 파일에서 돌릴 때의 기본값이다.

## 좌표 약속

- X 폭 · Z 높이 · Y 깊이. **앞면은 −Y 를 본다.**
- 단위는 미터. 유니티 FBX 임포트는 `-Z forward / Y up` 로 내보낸다.
- 심볼 파이프라인(`AscendSlotSymbolSwap`)의 `MeshToMetres = 100` · `Rx(270)` 규약을
  **따르지 않는다.** 그쪽은 옛 저작물의 단위를 보정하는 값이고, 여기는 처음부터
  실척으로 만들었다.

## 왜 불리언을 안 썼나

창구와 버튼 자리를 파내는 대신 **테두리를 얹어** 만들었다. 불리언은 저폴리에서
n-gon 과 얇은 조각을 남기고, 그 조각이 FBX 왕복에서 법선을 뒤집는다.
더하기만 쓰면 결과가 결정론적이다.
"""

import math
import bpy
import bmesh
from mathutils import Matrix, Vector

TMP = "TMP_AD_EXPORT"
EXPORT_PATH = r"B:\PROJECT_NEW_BORN\Upandup_DDD\Assets\Prototype_Elevator\Art\Models\SM_ElevControls.fbx"
CLEANUP = True


def _fresh_collection():
    old = bpy.data.collections.get(TMP)
    if old:
        for o in list(old.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(old)
    coll = bpy.data.collections.new(TMP)
    bpy.context.scene.collection.children.link(coll)
    return coll


def _merge(bm, tmp):
    me = bpy.data.meshes.new("_t")
    tmp.to_mesh(me)
    tmp.free()
    bm.from_mesh(me)
    bpy.data.meshes.remove(me)


def box(bm, sx, sy, sz, cx=0.0, cy=0.0, cz=0.0, bev=0.0035, seg=2):
    """축 정렬 상자. bev>0 이면 모서리를 깎는다 — 이 아트 방향은 면이 아니라
    **모서리 하이라이트**로 형태를 읽히게 한다. 깎지 않으면 전부 검은 덩어리다."""
    tmp = bmesh.new()
    bmesh.ops.create_cube(tmp, size=1.0)
    bmesh.ops.scale(tmp, vec=Vector((sx, sy, sz)), verts=tmp.verts)
    if bev > 0.0:
        bmesh.ops.bevel(tmp, geom=tmp.verts[:] + tmp.edges[:], offset=bev,
                        segments=seg, profile=0.5, affect='EDGES')
    bmesh.ops.translate(tmp, vec=Vector((cx, cy, cz)), verts=tmp.verts)
    _merge(bm, tmp)


def rivet(bm, r, h, cx, cy, cz, n=10):
    """리벳. 축은 Y(앞뒤). 끝을 살짝 좁혀 둥근 머리처럼 읽히게 한다."""
    tmp = bmesh.new()
    bmesh.ops.create_cone(tmp, cap_ends=True, cap_tris=False, segments=n,
                          radius1=r, radius2=r * 0.78, depth=h)
    bmesh.ops.rotate(tmp, verts=tmp.verts, matrix=Matrix.Rotation(math.radians(90), 3, 'X'))
    bmesh.ops.translate(tmp, vec=Vector((cx, cy, cz)), verts=tmp.verts)
    _merge(bm, tmp)


def prism(bm, pts, depth, cy):
    """XZ 평면 다각형을 −Y 로 밀어낸다. 버튼 글리프(삼각형)에 쓴다."""
    tmp = bmesh.new()
    vs = [tmp.verts.new((x, cy, z)) for (x, z) in pts]
    tmp.faces.new(vs)
    tmp.verts.ensure_lookup_table()
    bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces[:])
    r = bmesh.ops.extrude_face_region(tmp, geom=tmp.faces[:])
    moved = [e for e in r["geom"] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(tmp, vec=Vector((0, -depth, 0)), verts=moved)
    bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces[:])
    bmesh.ops.bevel(tmp, geom=tmp.verts[:] + tmp.edges[:], offset=0.0015,
                    segments=1, profile=0.5, affect='EDGES')
    _merge(bm, tmp)


def ring(bm, ro, ri, depth, cy, n=20):
    """확인 버튼의 ○ 글리프. 체크 표시는 저폴리에서 뭉개져 읽히지 않는다."""
    tmp = bmesh.new()
    outer = [tmp.verts.new((ro * math.cos(2 * math.pi * i / n), cy,
                            ro * math.sin(2 * math.pi * i / n))) for i in range(n)]
    inner = [tmp.verts.new((ri * math.cos(2 * math.pi * i / n), cy,
                            ri * math.sin(2 * math.pi * i / n))) for i in range(n)]
    for i in range(n):
        j = (i + 1) % n
        tmp.faces.new([outer[i], outer[j], inner[j], inner[i]])
    bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces[:])
    r = bmesh.ops.extrude_face_region(tmp, geom=tmp.faces[:])
    mv = [e for e in r["geom"] if isinstance(e, bmesh.types.BMVert)]
    bmesh.ops.translate(tmp, vec=Vector((0, -depth, 0)), verts=mv)
    bmesh.ops.recalc_face_normals(tmp, faces=tmp.faces[:])
    _merge(bm, tmp)


def _object(coll, bm, name, loc=(0.0, 0.0, 0.0)):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    ob.location = loc
    coll.objects.link(ob)
    return ob


# ─────────────────────────────────────────────────────────────
PANEL_W, PANEL_H, PANEL_D = 0.360, 0.920, 0.055
PANEL_FRONT = -PANEL_D / 2
BTN_Z = (0.270, 0.000, -0.270)        # 위 · 확인 · 아래


def build_panel(coll):
    bm = bmesh.new()
    box(bm, PANEL_W, PANEL_D, PANEL_H, bev=0.005, seg=2)
    rail = 0.030
    box(bm, PANEL_W, 0.020, rail, cy=PANEL_FRONT - 0.007, cz=(PANEL_H - rail) / 2)
    box(bm, PANEL_W, 0.020, rail, cy=PANEL_FRONT - 0.007, cz=-(PANEL_H - rail) / 2)
    box(bm, rail, 0.020, PANEL_H, cy=PANEL_FRONT - 0.007, cx=(PANEL_W - rail) / 2)
    box(bm, rail, 0.020, PANEL_H, cy=PANEL_FRONT - 0.007, cx=-(PANEL_W - rail) / 2)
    for sx in (-1, 1):
        for sz in (-1, 0, 1):
            box(bm, 0.014, 0.012, 0.014, bev=0.002, seg=1,
                cx=sx * (PANEL_W / 2 - 0.015), cy=PANEL_FRONT - 0.020,
                cz=sz * (PANEL_H / 2 - 0.030))
    for z in BTN_Z:
        box(bm, 0.252, 0.014, 0.172, bev=0.004, seg=2, cy=PANEL_FRONT - 0.004, cz=z)
    return _object(coll, bm, "SM_FloorPanel")


def build_button(coll, name, z, glyph):
    bm = bmesh.new()
    box(bm, 0.215, 0.038, 0.135, bev=0.006, seg=3, cy=PANEL_FRONT - 0.030)
    gy = PANEL_FRONT - 0.049
    if glyph in ("up", "down"):
        s = 1.0 if glyph == "up" else -1.0
        prism(bm, [(-0.040, -0.026 * s), (0.040, -0.026 * s), (0.0, 0.030 * s)], 0.007, gy)
    else:
        ring(bm, 0.036, 0.021, 0.007, gy)
    return _object(coll, bm, name, loc=(0.0, 0.0, z))


SIGN_W, SIGN_H, SIGN_D = 1.200, 0.320, 0.050
SIGN_FRONT = -SIGN_D / 2


def build_sign(coll):
    bm = bmesh.new()
    box(bm, SIGN_W, SIGN_D, SIGN_H, bev=0.005, seg=2)
    bar_z, bar_x = 0.055, 0.060
    box(bm, SIGN_W, 0.030, bar_z, cy=SIGN_FRONT - 0.012, cz=(SIGN_H - bar_z) / 2)
    box(bm, SIGN_W, 0.030, bar_z, cy=SIGN_FRONT - 0.012, cz=-(SIGN_H - bar_z) / 2)
    box(bm, bar_x, 0.030, SIGN_H, cy=SIGN_FRONT - 0.012, cx=(SIGN_W - bar_x) / 2)
    box(bm, bar_x, 0.030, SIGN_H, cy=SIGN_FRONT - 0.012, cx=-(SIGN_W - bar_x) / 2)
    # 차양 — 위에서 오는 캐빈 등을 창면에서 끊는다. 없으면 글자 대비가 씻긴다.
    box(bm, SIGN_W + 0.050, 0.110, 0.022, bev=0.004, seg=2,
        cy=SIGN_FRONT - 0.045, cz=SIGN_H / 2 + 0.022)
    for i in range(9):
        x = -SIGN_W / 2 + 0.075 + i * (SIGN_W - 0.150) / 8.0
        rivet(bm, 0.010, 0.014, x, SIGN_FRONT - 0.032, (SIGN_H - bar_z) / 2)
        rivet(bm, 0.010, 0.014, x, SIGN_FRONT - 0.032, -(SIGN_H - bar_z) / 2)
    for s in (-1, 1):
        box(bm, 0.045, 0.075, 0.130, bev=0.004, seg=2,
            cx=s * (SIGN_W / 2 + 0.020), cy=0.0, cz=SIGN_H / 2 - 0.010)
        box(bm, 0.030, 0.030, 0.150, bev=0.003, seg=1,
            cx=s * (SIGN_W / 2 + 0.020), cy=0.0, cz=SIGN_H / 2 + 0.100)
    return _object(coll, bm, "SM_FloorSign")


def export(coll, path):
    import os
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for o in coll.objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = coll.objects[0]
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, global_scale=1.0,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_NONE',
        axis_forward='-Z', axis_up='Y', object_types={'MESH'},
        use_mesh_modifiers=True, mesh_smooth_type='FACE',
        bake_space_transform=False, add_leaf_bones=False, path_mode='AUTO')
    bpy.ops.object.select_all(action='DESELECT')


def main():
    coll = _fresh_collection()
    build_panel(coll)
    build_button(coll, "SM_FloorBtn_Up", BTN_Z[0], "up")
    build_button(coll, "SM_FloorBtn_Confirm", BTN_Z[1], "ok")
    build_button(coll, "SM_FloorBtn_Down", BTN_Z[2], "down")
    build_sign(coll)
    export(coll, EXPORT_PATH)
    if CLEANUP:
        for o in list(coll.objects):
            bpy.data.objects.remove(o, do_unlink=True)
        bpy.data.collections.remove(coll)


if __name__ == "__main__":
    main()
