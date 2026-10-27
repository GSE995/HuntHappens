"""Convert the approved 1044-triangle Meshy latte cup into a Unity FBX, without remeshing."""
import bpy, os, math
from mathutils import Vector
root=os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
out=os.path.join(root,'Assets/Art/SmallGame/MeshyLatte')
source=os.path.join(root,'meshy_output/20261027_171915_latte-cup_01a17dba/latte.glb')
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=source)
objects=[o for o in bpy.context.scene.objects if o.type=='MESH']
for o in objects:
 o.select_set(True)
bpy.context.view_layer.objects.active=objects[0]
bpy.ops.object.join();o=bpy.context.object;o.name='Meshy Latte'
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
# Orient the longest horizontal axis along Blender Y (Unity Z).
if o.dimensions.x>o.dimensions.y:
 o.rotation_euler.z=math.pi/2
 bpy.ops.object.transform_apply(location=False,rotation=True,scale=False)
scale=.14/o.dimensions.z
o.scale*=scale;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
points=[o.matrix_world@Vector(v) for v in o.bound_box]
o.location-=Vector(((min(v.x for v in points)+max(v.x for v in points))/2,(min(v.y for v in points)+max(v.y for v in points))/2,min(v.z for v in points)))
bpy.ops.object.transform_apply(location=True,rotation=False,scale=False)
# Extract original embedded images by material role rather than exporter-specific names.
import json, struct
with open(source,'rb') as stream:
 stream.read(12); size,kind=struct.unpack('<II',stream.read(8)); doc=json.loads(stream.read(size))
 size,kind=struct.unpack('<II',stream.read(8)); binary=stream.read(size)
mat=doc['materials'][0]
roles={'BaseColor':mat['pbrMetallicRoughness']['baseColorTexture']['index'],'Normal':mat['normalTexture']['index'],'MetallicRoughness':mat['pbrMetallicRoughness']['metallicRoughnessTexture']['index']}
for name,index in roles.items():
 image=doc['images'][doc['textures'][index]['source']]; view=doc['bufferViews'][image['bufferView']];offset=view.get('byteOffset',0)
 with open(os.path.join(out,name+'.png'),'wb') as target:target.write(binary[offset:offset+view['byteLength']])
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(root,'Tools/Art/MeshyLatte.blend'))
bpy.ops.export_scene.fbx(filepath=os.path.join(out,'Latte.fbx'),use_selection=True,object_types={'MESH'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=False,path_mode='STRIP',use_mesh_modifiers=True)
print('MESHY_LATTE',len(o.data.polygons),'faces',tuple(o.dimensions))
