import bpy, bmesh, mathutils
bpy.ops.wm.read_factory_settings(use_empty=True)
def seg(p, a, b):
    ab = b - a; t = max(0.0, min(1.0, (p - a).dot(ab) / max(ab.length_squared, 1e-9))); return (p - (a + ab * t)).length
for n, x in (("Blue", -0.6), ("Orange", 0.6)):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=r"C:\Users\mbc\Desktop\MultiMystery\Assets\VARCO 3D\LHJ\Char_%s\Char_%s_Rigged.fbx" % (n, n))
    new = set(bpy.data.objects) - before
    arm = [o for o in new if o.type == 'ARMATURE'][0]; rig = [o for o in new if o.type == 'MESH'][0]
    bh = {b.name: arm.matrix_world @ b.head_local for b in arm.data.bones}
    z_lo = max(bh['LeftUpLeg'].z, bh['RightUpLeg'].z) - 0.03; z_hi = bh['Neck'].z + 0.02
    arms = [(s + a, s + b) for s in ('Left', 'Right') for a, b in (('Arm', 'ForeArm'), ('ForeArm', 'Hand'))]
    legs = [(s + a, s + b) for s in ('Left', 'Right') for a, b in (('UpLeg', 'Leg'), ('Leg', 'Foot'))]
    me = rig.data; cnt = 0
    for v in me.vertices:
        p = rig.matrix_world @ v.co
        ok = z_lo < p.z < z_hi and min(seg(p, bh[a], bh[b]) for a, b in arms) > 0.055 and min(seg(p, bh[a], bh[b]) for a, b in legs) > 0.055
        v.select = ok; cnt += ok
    for e in me.edges: e.select = all(me.vertices[i].select for i in e.vertices)
    for f in me.polygons: f.select = all(me.vertices[i].select for i in f.vertices)
    me.use_paint_mask_vertex = True
    print("BODY", n, cnt, "of", len(me.vertices))
    jump = [a for a in bpy.data.actions if a.name.endswith('Jump') and arm.name in a.name]
    if jump: arm.animation_data.action = jump[0]
    img = bpy.data.images.load(r"C:\Users\mbc\Desktop\MultiMystery\Assets\VARCO 3D\LHJ\Char_%s\Char_%s_Albedo.png" % (n, n), check_existing=True)
    img.pack()
    m = bpy.data.materials.new("Char_%s_Mat" % n); m.use_nodes = True
    t = m.node_tree.nodes.new('ShaderNodeTexImage'); t.image = img
    m.node_tree.links.new(t.outputs['Color'], m.node_tree.nodes['Principled BSDF'].inputs['Base Color'])
    rig.data.materials.clear(); rig.data.materials.append(m)
    e = bpy.data.objects.new('Place_' + n, None); bpy.context.scene.collection.objects.link(e); e.location.x = x; arm.parent = e
sc = bpy.context.scene; sc.frame_start = 45; sc.frame_end = 105; sc.frame_set(57)
bpy.ops.wm.save_as_mainfile(filepath=r"C:\Users\mbc\Desktop\MultiMystery\Assets\VARCO 3D\LHJ\Blender~\캐릭터_몸통다듬기.blend")
print("SAVED")
