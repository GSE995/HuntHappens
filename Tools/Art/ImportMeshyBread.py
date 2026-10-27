"""Convert the approved 1541-triangle Meshy loaf into a Unity FBX, without remeshing."""
import bpy, os, math
from mathutils import Vector
root=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
out=os.path.join(root,'Assets/Art/SmallGame/MeshyBread')
source=os.path.join(root,'meshy_output/20261027_165546_bread-loaf_01a17da5/bread-loaf-1500.glb')
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=source)
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in objects:
 o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.join();o=bpy.context.object;o.name='Meshy Bread Loaf'
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
# Orient the longest horizontal axis along Blender Y (Unity Z).
if o.dimensions.x>o.dimensions.y:
 o.rotation_euler.z=math.pi/2
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=False)
scale=.22/max(o.dimensions.x,o.dimensions.y)
o.scale*=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
points=[o.matrix_world@Vector(v) for v in o.bound_box]
o.location-=Vector(((min(v.x for v in points)+max(v.x for v in points))/2,(min(v.y for v in points)+max(v.y for v in points))/2,min(v.z for v in points)))
bpy.ops.object.transform_apply(location=True,rotation=False,scale=False)
for image in bpy.data.images:
 if image.type!='IMAGE' or image.name=='Render Result':continue
 name='BaseColor' if image.name.startswith('texture_0') and 'metallic' not in image.name else 'Normal' if image.name.startswith('normal') else 'MetallicRoughness'
 image.filepath_raw=os.path.join(out,name+'.png');image.file_format='PNG';image.save()
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root,'Tools/Art/MeshyBread.blend'))
bpy.ops.export_scene.fbx(filepath=os.path.join(out,'BreadLoaf.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_mesh_modifiers=True)
print('MESHY_BREAD',len(o.data.polygons),'faces',tuple(o.dimensions))
