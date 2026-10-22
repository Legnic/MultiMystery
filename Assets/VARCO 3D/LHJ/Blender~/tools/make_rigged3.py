import bpy, sys, json, struct, mathutils, os
# 사용법: blender -b --python make_rigged3.py -- 이름 원본.glb 최종.obj 출력.fbx 동작1=파일1=돌릴각도 동작2=파일2=0 ...
# VARCO 리깅 메쉬에 최종 모양(눈 다듬기)을 옮기고, 여러 동작을 한 FBX로 묶는다.
argv = sys.argv[sys.argv.index("--") + 1:]
name, rawglb, objpath, out = argv[:4]
specs = [s.split('=') for s in argv[4:]]
def glb_pos(path):
    b = open(path, 'rb').read()
    jl = struct.unpack_from('<I', b, 12)[0]; js = json.loads(b[20:20+jl]); bin0 = 20 + jl + 8
    a = js['accessors'][js['meshes'][0]['primitives'][0]['attributes']['POSITION']]; v = js['bufferViews'][a['bufferView']]
    f = struct.unpack_from('<%df' % (a['count']*3), b, bin0 + v.get('byteOffset', 0) + a.get('byteOffset', 0))
    return [f[i:i+3] for i in range(0, len(f), 3)]
def obj_pos(path):
    out = []
    for line in open(path, encoding='utf-8'):
        if line.startswith('v '): p = line.split(); out.append((float(p[1]), float(p[2]), float(p[3])))
        elif line.startswith('vt'): break
    return out
raw = glb_pos(rawglb); fin = obj_pos(objpath)
ax = lambda p: mathutils.Vector((p[0], -p[2], p[1]))
bpy.ops.wm.read_factory_settings(use_empty=True)
def imp(path):
    before = set(bpy.data.objects); acts = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=path)
    return [o for o in bpy.data.objects if o not in before], [a for a in bpy.data.actions if a not in acts]
arm = rig = None; actions = []
for act_name, path, deg in specs:
    objs, acts = imp(path)
    if arm is None:
        arm = [o for o in objs if o.type == 'ARMATURE'][0]; rig = [o for o in objs if o.type == 'MESH'][0]
    else:
        for o in objs: bpy.data.objects.remove(o, do_unlink=True)
    acts[0].name = act_name; acts[0].use_fake_user = True
    actions.append((acts[0], float(deg)))
# 최종 모양 옮기기 (원본 정점과 가장 가까운 정점에 같은 변화량을 더함)
rv = [v.co.copy() for v in rig.data.vertices]; rawv = [ax(p) for p in raw]
def bb(vs): return [min(p[i] for p in vs) for i in range(3)], [max(p[i] for p in vs) for i in range(3)]
(r0, r1), (f0, f1) = bb(rv), bb(rawv)
off = mathutils.Vector(((r0[0]+r1[0])/2-(f0[0]+f1[0])/2, (r0[1]+r1[1])/2-(f0[1]+f1[1])/2, r0[2]-f0[2]))
kd = mathutils.kdtree.KDTree(len(rawv))
for i, p in enumerate(rawv): kd.insert(p + off, i)
kd.balance()
match = []
for v in rig.data.vertices:
    co, i, d = kd.find(v.co); v.co = v.co + (ax(fin[i]) - ax(raw[i])); match.append(i)
# 이음새(같은 자리에 겹쳐 있는 점들)가 움직일 때 벌어지지 않도록, 겹친 점들의 뼈 무게를 똑같이 맞추고 부드럽게 다듬는다
# 팔 뼈가 배·몸통 살까지 잡고 있으면 팔을 휘두를 때 배가 끌려가 뭉개진다.
# 팔이 몸에서 나오는 어깨 관절보다 안쪽(몸통 폭 안)에 있는 점은 팔 쪽 무게를 지우고 몸통 뼈만 따르게 한다.
def fix_leak():
    bw = {b.name: arm.matrix_world @ b.head_local for b in arm.data.bones}
    cx = (bw['LeftArm'].x + bw['RightArm'].x) / 2; inner = abs(bw['LeftArm'].x - bw['RightArm'].x) / 2 * 0.85
    names = [g.name for g in rig.vertex_groups]
    arm_groups = {i for i, n in enumerate(names) if any(k in n for k in ('Arm', 'Hand'))}
    mw = rig.matrix_world; fixed = 0
    for v in rig.data.vertices:
        if not any(g.group in arm_groups and g.weight > 0 for g in v.groups): continue
        p = mw @ v.co
        if abs(p.x - cx) < inner:
            for gi in [g.group for g in v.groups if g.group in arm_groups]: rig.vertex_groups[gi].remove([v.index])
            tot = sum(g.weight for g in v.groups)
            if tot > 0:
                for g in v.groups: g.weight /= tot
            else:
                rig.vertex_groups['Spine1'].add([v.index], 1.0, 'REPLACE')
            fixed += 1
    print("LEAK fixed", fixed, "inner", round(inner, 3))
if os.environ.get('FIX_LEAK') == '1': fix_leak()
# 몸통(배) 살이 머리·어깨·팔·다리 뼈를 따라가지 않도록, 몸통 폭 안쪽 점들은
# 높이에 따라 엉덩이~목 사이 몸통 뼈들에만 무게를 나눠 준다 (경계 근처는 원래 무게와 섞어 이음새가 생기지 않게)
def fix_torso():
    bh = {b.name: arm.matrix_world @ b.head_local for b in arm.data.bones}
    # 몸 가운데는 뼈 위치가 파일마다 조금씩 달라서, 양 팔 관절의 한가운데로 잡는다
    cx = (bh['LeftArm'].x + bh['RightArm'].x) / 2; inner = abs(bh['LeftArm'].x - bh['RightArm'].x) / 2 * 0.85
    chain = [n for n in ('Hips', 'Spine', 'Spine1', 'Spine2', 'Neck') if n in bh]
    zs = [bh[n].z for n in chain]
    z_lo = max(bh['LeftUpLeg'].z, bh['RightUpLeg'].z) - float(os.environ.get('TORSO_DROP', '0.06')); z_hi = bh[chain[-1]].z + float(os.environ.get('TORSO_TOP', '0'))
    leg_r = float(os.environ.get('LEG_R', '0'))
    limb_r = float(os.environ.get('LIMB_R', '0')); leg_r2 = float(os.environ.get('LEG_R2', '0.06'))
    arm_segs = [(s + 'Arm', s + 'ForeArm') for s in ('Left', 'Right')] + [(s + 'ForeArm', s + 'Hand') for s in ('Left', 'Right')]
    leg_segs = [(s + 'UpLeg', s + 'Leg') for s in ('Left', 'Right')] + [(s + 'Leg', s + 'Foot') for s in ('Left', 'Right')]
    def seg_dist(p, a, b):
        ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9))); return (p - (a + ab * t)).length
    band = 0.06; band_lo = float(os.environ.get('BAND_LO', '0.10'))  # 경계(옆구리·엉덩이)는 넓게 섞어서 꺾이는 선이 생기지 않게
    gidx = {g.name: g.index for g in rig.vertex_groups}
    for n in chain:
        if n not in gidx: gidx[n] = rig.vertex_groups.new(name=n).index
    mw = rig.matrix_world; fixed = 0
    for v in rig.data.vertices:
        p = mw @ v.co
        # 몸통 안쪽일수록 1, 경계(옆·위·아래)로 갈수록 0
        if limb_r > 0:
            # 팔·다리 기둥 둘레(limb_r) 안의 점만 팔·다리를 따라가고, 그 밖의 몸통은 옆구리까지 전부 몸통 뼈를 따라간다
            da = min(seg_dist(p, bh[a], bh[b]) for a, b in arm_segs)
            dl = min(seg_dist(p, bh[a], bh[b]) for a, b in leg_segs)
            k = min((da - limb_r) / band, (dl - leg_r2) / band, (z_hi - p.z) / band, (p.z - z_lo) / band_lo, 1.0)
        else:
            k = min((inner - abs(p.x - cx)) / band, (p.z - z_lo) / band_lo, (z_hi - p.z) / band, 1.0)
        # 허벅지 기둥(다리 뼈 둘레) 안의 점은 다리를 따라가야 하므로 몸통에서 뺀다
        if leg_r > 0 and k > 0:
            dl = min(seg_dist(p, bh[a], bh[b]) for a, b in (('LeftUpLeg', 'LeftLeg'), ('RightUpLeg', 'RightLeg')))
            k = min(k, max(0.0, (dl - leg_r) / band))
        if k <= 0: continue
        z = min(max(p.z, zs[0]), zs[-1]); new = {}
        for i in range(len(zs) - 1):
            if zs[i] <= z <= zs[i + 1]:
                t = (z - zs[i]) / max(zs[i + 1] - zs[i], 1e-6); new = {chain[i]: 1 - t, chain[i + 1]: t}; break
        old = {rig.vertex_groups[g.group].name: g.weight for g in v.groups}
        mix = {n: old.get(n, 0) * (1 - k) + new.get(n, 0) * k for n in set(old) | set(new)}
        for gi in [g.group for g in v.groups]: rig.vertex_groups[gi].remove([v.index])
        tot = sum(mix.values()) or 1
        for n, w in mix.items():
            if w / tot > 0.001: rig.vertex_groups[n].add([v.index], w / tot, 'REPLACE')
        fixed += 1
    print("TORSO fixed", fixed, "z", round(z_lo, 3), round(z_hi, 3), "inner", round(inner, 3), "cx", round(cx, 3), "zs", [round(z, 3) for z in zs])
    if os.environ.get('DEBUG_V'):
        i = int(os.environ['DEBUG_V']); v = rig.data.vertices[i]
        print("DBG", i, tuple(round(x, 3) for x in (mw @ v.co)), {rig.vertex_groups[g.group].name: round(g.weight, 2) for g in v.groups})
dup_sets = []
if os.environ.get('FIX_SEAMS') == '1':
    smooth_n = int(os.environ.get('WEIGHT_SMOOTH', '0'))
    if smooth_n > 0:
        # 먼저 무게를 부드럽게 다듬고, 그다음 겹친 점들을 똑같이 맞춘다 (다듬기가 이음새를 다시 벌리지 않게)
        for o in bpy.data.objects: o.select_set(False)
        bpy.context.view_layer.objects.active = rig; rig.select_set(True)
        bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
        bpy.ops.object.vertex_group_smooth(group_select_mode='ALL', factor=0.5, repeat=smooth_n)
        # 유니티 기본 설정(점 하나에 뼈 4개까지)에서도 똑같이 보이도록 미리 4개로 줄이고 합을 1로 맞춘다
        bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
        bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
        bpy.ops.object.mode_set(mode='OBJECT')
        print("WEIGHTS smoothed", smooth_n)
        # 다듬기가 팔 무게를 다시 배로 번지게 하므로 한 번 더 정리
        if os.environ.get('FIX_LEAK') == '1': fix_leak()
        if os.environ.get('FIX_TORSO') == '1':
            fix_torso()
            # 고친 몸통과 팔·다리 사이를 한 번 더 살짝 다듬어 경계를 매끈하게
            bpy.ops.object.mode_set(mode='WEIGHT_PAINT')
            bpy.ops.object.vertex_group_smooth(group_select_mode='ALL', factor=0.5, repeat=int(os.environ.get('TORSO_SMOOTH', '4')))
            bpy.ops.object.vertex_group_limit_total(group_select_mode='ALL', limit=4)
            bpy.ops.object.vertex_group_normalize_all(group_select_mode='ALL', lock_active=False)
            bpy.ops.object.mode_set(mode='OBJECT')
    me = rig.data; kd2 = mathutils.kdtree.KDTree(len(me.vertices))
    for v in me.vertices: kd2.insert(v.co, v.index)
    kd2.balance()
    seen = set(); groups_n = 0
    for v in me.vertices:
        if v.index in seen: continue
        same = [i for (_, i, _) in kd2.find_range(v.co, 1e-5)]
        for i in same: seen.add(i)
        if len(same) < 2: continue
        groups_n += 1; acc = {}; dup_sets.append(same)
        for i in same:
            for g in me.vertices[i].groups: acc[g.group] = acc.get(g.group, 0) + g.weight / len(same)
        for i in same:
            for gi, w in acc.items(): rig.vertex_groups[gi].add([i], w, 'REPLACE')
    print("SEAMS unified", groups_n)
# 서 있는 캐릭터(최종 OBJ)의 매끈한 노멀을 그대로 입혀, 몸의 잔주름(금 간 듯한 선)이 안 보이게 한다
if os.environ.get('OBJ_NORMALS') == '1':
    vn = [tuple(float(x) for x in line.split()[1:4]) for line in open(objpath, encoding='utf-8') if line.startswith('vn ')]
    norms = [ax(vn[i]).normalized() for i in match]
    for same in dup_sets:
        avg = sum((norms[i] for i in same), mathutils.Vector()).normalized()
        for i in same: norms[i] = avg
    rig.data.normals_split_custom_set_from_vertices(norms)
    print("OBJ normals applied", len(vn))
rig.name = name; arm.name = name + "_Armature"
# 몸의 잔주름(금 간 듯한 선)이 보이지 않도록, 아주 매끈하게 만든 복사본의 빛 방향(노멀)을 옮겨 입힌다 (모양은 그대로)
SMOOTH_NORMALS = int(os.environ.get('SMOOTH_NORMALS', '0'))
if SMOOTH_NORMALS > 0:
    smooth = rig.copy(); smooth.data = rig.data.copy(); smooth.parent = None
    for m in list(smooth.modifiers): smooth.modifiers.remove(m)
    bpy.context.scene.collection.objects.link(smooth)
    sm = smooth.modifiers.new("smooth", 'SMOOTH'); sm.factor = 1.0; sm.iterations = SMOOTH_NORMALS
    for o in bpy.data.objects: o.select_set(False)
    bpy.context.view_layer.objects.active = smooth; smooth.select_set(True); bpy.ops.object.modifier_apply(modifier="smooth")
    dt = rig.modifiers.new("normals", 'DATA_TRANSFER'); dt.object = smooth
    dt.use_loop_data = True; dt.data_types_loops = {'CUSTOM_NORMAL'}; dt.loop_mapping = 'POLYINTERP_NEAREST'
    bpy.context.view_layer.objects.active = rig; rig.select_set(True)
    while rig.modifiers[0].name != "normals": bpy.ops.object.modifier_move_up(modifier="normals")
    bpy.ops.object.modifier_apply(modifier="normals")
    bpy.data.objects.remove(smooth, do_unlink=True)
    print("NORMALS smoothed", SMOOTH_NORMALS)
sc = bpy.context.scene; root = arm.pose.bones['Root']
def has_root_keys(act):
    try: fcs = list(act.fcurves)
    except AttributeError: fcs = [fc for l in act.layers for s in l.strips for cb in s.channelbags for fc in cb.fcurves]
    return any(fc.data_path.startswith('pose.bones["Root"]') for fc in fcs)
for act, deg in actions:
    arm.animation_data.action = act
    for pb in arm.pose.bones: pb.matrix_basis = mathutils.Matrix.Identity(4)
    a0, a1 = (int(x) for x in act.frame_range)
    if deg != 0:
        # 옆을 보는 동작은 몸 전체(Root)를 위쪽 축 기준으로 돌려 앞을 보게 한다
        turn = mathutils.Matrix.Rotation(deg * 3.14159265 / 180, 4, 'Z'); orig = {}
        for f in range(a0, a1 + 1): sc.frame_set(f); orig[f] = root.matrix.copy()
        for f in range(a0, a1 + 1):
            root.matrix = turn @ orig[f]
            root.keyframe_insert('location', frame=f); root.keyframe_insert('rotation_quaternion', frame=f)
    elif not has_root_keys(act):
        # Root 키가 없는 동작에 다른 동작의 돌린 자세가 남지 않도록 제자리로 고정
        root.matrix_basis = mathutils.Matrix.Identity(4)
        for f in (a0, a1): root.keyframe_insert('location', frame=f); root.keyframe_insert('rotation_quaternion', frame=f)
    # 동작하는 동안 몸이 점점 떠오르거나 가라앉지 않도록, 처음과 끝 높이 차이를 고르게 빼 준다
    hips = arm.pose.bones['Hips']
    up = (arm.matrix_world.inverted().to_3x3() @ mathutils.Vector((0, 0, 1))).normalized()
    sc.frame_set(a0); z0 = hips.matrix.translation.dot(up)
    sc.frame_set(a1); z1 = hips.matrix.translation.dot(up)
    if abs(z1 - z0) > 0.01:
        mats = {}
        for f in range(a0, a1 + 1): sc.frame_set(f); mats[f] = root.matrix.copy()
        for f in range(a0, a1 + 1):
            m = mats[f].copy(); m.translation -= up * ((z1 - z0) * (f - a0) / (a1 - a0))
            root.matrix = m; root.keyframe_insert('location', frame=f); root.keyframe_insert('rotation_quaternion', frame=f)
        print("DRIFT fixed", act.name, round(z1 - z0, 3))
    print("ACTION", act.name, a0, a1, "turn", deg)
# 배가 접히지 않게: 허리(Spine) 굽힘을 줄이고, 팔을 몸 바깥쪽으로 조금 벌려 손이 배를 파고들지 않게 한다
import os
SPINE_KEEP = float(os.environ.get('SPINE_KEEP', '1'))   # 허리 굽힘을 이만큼만 남김 (1=그대로)
ARM_OUT = float(os.environ.get('ARM_OUT', '0'))          # 팔을 바깥으로 벌리는 각도(도)
if SPINE_KEEP != 1 or ARM_OUT != 0:
    spines = [arm.pose.bones[n] for n in ('Spine', 'Spine1', 'Spine2')]
    arms = [(arm.pose.bones['LeftArm'], -1), (arm.pose.bones['RightArm'], 1)]  # 음수 회전이 왼팔을 바깥(+X)으로 벌림
    ident = mathutils.Quaternion()
    for act, deg in actions:
        arm.animation_data.action = act
        a0, a1 = (int(x) for x in act.frame_range)
        out_deg = float(os.environ.get('ARM_OUT_' + act.name, ARM_OUT))  # 동작별로 팔 벌림 각도를 다르게 줄 수 있음
        # POSE_KEEP_<동작>: 그 동작의 모든 굽힘(웅크리기 등)을 이만큼만 남겨 얕게 만든다 (1=그대로)
        pose_keep = float(os.environ.get('POSE_KEEP_' + act.name, '1'))
        for f in range(a0, a1 + 1):
            sc.frame_set(f)
            if pose_keep != 1:
                for pb in arm.pose.bones:
                    if pb.name in ('Root',) or 'Arm' in pb.name or 'Hand' in pb.name: continue
                    pb.rotation_mode = 'QUATERNION'
                    pb.rotation_quaternion = ident.slerp(pb.rotation_quaternion, pose_keep)
                    pb.keyframe_insert('rotation_quaternion', frame=f)
                hp = arm.pose.bones['Hips']; hp.location = hp.location * pose_keep; hp.keyframe_insert('location', frame=f)
            spine_keep = float(os.environ.get('SPINE_KEEP_' + act.name, SPINE_KEEP))  # 동작별 허리 굽힘
            for pb in spines:
                pb.rotation_mode = 'QUATERNION'
                pb.rotation_quaternion = ident.slerp(pb.rotation_quaternion, spine_keep)
                pb.keyframe_insert('rotation_quaternion', frame=f)
            bpy.context.view_layer.update()
            for pb, side in arms:
                head = pb.matrix.translation.copy()
                rot = mathutils.Matrix.Rotation(side * out_deg * 3.14159265 / 180, 4, 'Y')
                m = mathutils.Matrix.Translation(head) @ rot @ mathutils.Matrix.Translation(-head) @ pb.matrix
                pb.matrix = m; bpy.context.view_layer.update()
                pb.keyframe_insert('rotation_quaternion', frame=f)
        print("SOFTEN", act.name)
for o in bpy.data.objects: o.select_set(o in (arm, rig))
# 플레이 전 기본 모습이 서 있는 자세가 되도록 'Stand' 동작을 현재 동작으로 두고 내보낸다
stand = bpy.data.actions.new("Stand"); stand.use_fake_user = True
arm.animation_data.action = stand
for pb in arm.pose.bones:
    pb.matrix_basis = mathutils.Matrix.Identity(4)
    for f in (0, 1):
        pb.keyframe_insert('location', frame=f); pb.keyframe_insert('rotation_quaternion', frame=f); pb.keyframe_insert('scale', frame=f)
sc.frame_set(0); bpy.context.view_layer.update()
bpy.ops.export_scene.fbx(filepath=out, use_selection=True, add_leaf_bones=False, bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False)
print("EXPORTED", out)
