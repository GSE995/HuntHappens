"""Run with Blender --background --python Tools/Art/RigSemenych.py -- <project-root>.
Reuse the Hero armature, fit its rest joints to MouseCatcher, and bind the mesh.
Coordinates below are Blender metres (Z up, character faces -Y).
"""
import bpy
import json
import sys
from pathlib import Path
from mathutils import Vector

ROOT = Path(sys.argv[sys.argv.index('--') + 1]) if '--' in sys.argv else Path.cwd()
OUT = ROOT / 'Assets/Art/MouseCatcher'
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(ROOT / 'Assets/Art/Hero/Meshy_AI_camper_wide_rigged_biped_Animation_Walking_frame_rate_30.fbx'))
rig = next(o for o in bpy.context.selected_objects if o.type == 'ARMATURE')
for obj in list(bpy.data.objects):
    if obj != rig:
        bpy.data.objects.remove(obj, do_unlink=True)
rig.animation_data_clear()
for p in rig.pose.bones:
    p.matrix_basis.identity()
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT / 'Hero/mousCatcher/Meshy_AI_skinny_camper_character_20261022_image-to-3d-texture.fbx'))
mesh = next(o for o in bpy.context.selected_objects if o.type == 'MESH')
mesh.name = 'SemenychBody'
bpy.context.view_layer.objects.active = mesh
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
min_z = min(v.co.z for v in mesh.data.vertices)
max_z = max(v.co.z for v in mesh.data.vertices)
scale = 2.25 / (max_z - min_z)
for v in mesh.data.vertices:
    v.co = Vector((v.co.x * scale, v.co.y * scale, (v.co.z-min_z) * scale))
# The source has 639k vertices; keep the textured silhouette at a practical skinning cost.
reduction = mesh.modifiers.new('Animation mesh reduction', 'DECIMATE')
reduction.ratio = min(1.0, 100000 / len(mesh.data.polygons))
bpy.ops.object.modifier_apply(modifier=reduction.name)
# Keep the player's exact bone hierarchy and rolls; move joint centres to the new anatomy.
joints = {
 'Hips': ((0,0,1.02),(0,0,1.16)),
 'Spine02': ((0,0,1.16),(0,0,1.32)),
 'Spine01': ((0,0,1.32),(0,0,1.49)),
 'Spine': ((0,0,1.49),(0,0,1.64)),
 'neck': ((0,0,1.64),(0,-0.015,1.80)),
 'Head': ((0,-0.015,1.80),(0,-0.015,2.12)),
 'head_end': ((0,-0.015,2.12),(0,-0.015,2.25)),
 'headfront': ((0,-0.015,1.80),(0,-0.19,1.80)),
}
for side, sign in [('Left',1),('Right',-1)]:
 def p(x,y,z): return (sign*x,y,z)
 joints.update({
  side+'UpLeg': (p(.115,0,1.03),p(.12,-.025,.59)),
  side+'Leg': (p(.12,-.025,.59),p(.125,0,.16)),
  side+'Foot': (p(.125,0,.16),p(.125,-.19,.07)),
  side+'ToeBase': (p(.125,-.19,.07),p(.125,-.30,.07)),
  side+'Shoulder': (p(.04,0,1.60),p(.205,0,1.59)),
  side+'Arm': (p(.205,0,1.59),p(.365,-.015,1.30)),
  side+'ForeArm': (p(.365,-.015,1.30),p(.50,-.035,1.075)),
  side+'Hand': (p(.50,-.035,1.075),p(.565,-.045,.93)),
 })
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True)
bpy.context.view_layer.objects.active = rig
bpy.ops.object.mode_set(mode='EDIT')
for bone in rig.data.edit_bones:
    bone.use_connect = False
    bone.head, bone.tail = joints[bone.name]
    bone.use_deform = bone.name not in ('head_end','headfront')
bpy.ops.object.mode_set(mode='OBJECT')
mesh.select_set(True)
bpy.ops.object.parent_set(type='ARMATURE_AUTO')
# Validate heat weights before writing anything.
missing = [v.index for v in mesh.data.vertices if sum(g.weight for g in v.groups) < .001]
if missing:
    raise RuntimeError(f'Automatic binding left {len(missing)} vertices unweighted')
# Backpack and bedroll sit behind the torso; keep them rigid on the chest.
# A soft transition on the back surface avoids a seam through the shirt.
chest = mesh.vertex_groups.get('Spine')
for v in mesh.data.vertices:
    x,y,z = v.co
    if y > .13 and z > 1.00:
        blend = min(1.0, max(0.0,(y-.13)/.06))
        old = {g.group:g.weight for g in v.groups}
        for group in mesh.vertex_groups:
            w = old.get(group.index,0)*(1-blend) + (blend if group == chest else 0)
            if w > .00001: group.add([v.index],w,'REPLACE')
            elif group.index in old: group.remove([v.index])
# Limit skinning to Unity's four strongest influences and normalize.
bpy.context.view_layer.objects.active = mesh
bpy.ops.object.vertex_group_limit_total(limit=4)
bpy.ops.object.vertex_group_normalize_all(lock_active=False)
rig.name = 'SemenychRig'
mesh.data.name = 'SemenychSkinnedMesh'
bpy.data.orphans_purge(do_recursive=True)
bpy.ops.wm.save_as_mainfile(filepath=str(ROOT / 'Hero/mousCatcher/SemenychRig.blend'), compress=True)
bpy.ops.object.select_all(action='DESELECT')
rig.select_set(True); mesh.select_set(True)
bpy.ops.export_scene.fbx(filepath=str(OUT / 'SemenychRig.fbx'), use_selection=True,
 object_types={'ARMATURE','MESH'}, add_leaf_bones=False, bake_anim=False,
 axis_forward='-Z', axis_up='Y', use_mesh_modifiers=True)
print('RIG_REPORT '+json.dumps({'bones':len(rig.data.bones),'vertices':len(mesh.data.vertices),
 'unweighted':len(missing),'height':2.25,'source':'Hero armature','output':str(OUT/'SemenychRig.fbx')}))
