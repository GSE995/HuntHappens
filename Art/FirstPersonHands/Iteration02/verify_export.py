import bpy,os,json
from mathutils.bvhtree import BVHTree
OUT=os.environ['HANDS_OUTPUT']
bpy.ops.wm.open_mainfile(filepath=os.path.join(OUT,'CartoonHands_v02.blend'))
rig=bpy.data.objects['Hands_Rig']; checks={}
for clip,frame in [('Hands_Idle',1),('Hands_Grab',22),('Hands_Hold',1),('Hands_Weapon',1),('Hands_Throw',15),('Hands_Trap',22),('Hands_Shoot',4)]:
 rig.animation_data.action=bpy.data.actions[clip];bpy.context.scene.frame_set(frame);deps=bpy.context.evaluated_depsgraph_get()
 checks[clip]={}
 for o in [ob for ob in bpy.context.scene.objects if ob.type=='MESH']:
  eo=o.evaluated_get(deps);me=eo.to_mesh();me.calc_loop_triangles()
  verts=[v.co.copy() for v in me.vertices];tris=[tuple(t.vertices) for t in me.loop_triangles]
  tree=BVHTree.FromPolygons(verts,tris,all_triangles=True,epsilon=0)
  pairs=tree.overlap(tree)
  hits=sum(1 for a,b in pairs if a<b and not set(tris[a]).intersection(tris[b]))
  checks[clip][o.name]={'nonadjacent_triangle_intersections':hits}
  eo.to_mesh_clear()
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(OUT,'CartoonHands_Rigged.fbx'))
arms=[o for o in bpy.context.scene.objects if o.type=='ARMATURE'];meshes=[o for o in bpy.context.scene.objects if o.type=='MESH']
result={'pose_intersections':checks,'fbx':{'armatures':len(arms),'meshes':len(meshes),'bones':len(arms[0].data.bones),'actions':[a.name for a in bpy.data.actions],'mesh_sizes_m':{o.name:list(o.dimensions) for o in meshes},'rig_scale':list(arms[0].scale)}}
assert len(arms)==1 and len(meshes)==2 and len(bpy.data.actions)==7
arm=arms[0];result['fbx']['animation_motion_m']={}
for action in bpy.data.actions:
 arm.animation_data.action=action
 samples=[]
 for frame in [1,4,10,15,22,30]:
  bpy.context.scene.frame_set(frame);deps=bpy.context.evaluated_depsgraph_get();eo=meshes[0].evaluated_get(deps);me=eo.to_mesh();samples.append([v.co.copy() for v in me.vertices]);eo.to_mesh_clear()
 movement=max((a-b).length for sample in samples[1:] for a,b in zip(samples[0],sample))
 result['fbx']['animation_motion_m'][action.name]=movement
 assert movement>1e-5,'Animation does not move exported mesh'
result['fbx']['max_vertex_influences']=max(len(v.groups) for o in meshes for v in o.data.vertices)
with open(os.path.join(OUT,'export_verification.json'),'w') as f:json.dump(result,f,indent=2)
print(json.dumps(result))
