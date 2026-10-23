import bpy,os,json,math,bmesh,numpy as np
from mathutils.bvhtree import BVHTree
out=os.environ.get('CAMPER_HANDS_OUTPUT',os.path.dirname(os.path.abspath(__file__)));bpy.ops.wm.open_mainfile(filepath=os.path.join(out,'CamperFPSHands_Rigged.blend'))
results={}
for rig in [o for o in bpy.context.scene.objects if o.type=='ARMATURE']:
 obj=next(o for o in bpy.context.scene.objects if o.type=='MESH' and o.parent==rig)
 lengths=[(obj.data.vertices[e.vertices[0]].co-obj.data.vertices[e.vertices[1]].co).length for e in obj.data.edges]
 bone_weights={name:sum(any(g.group==obj.vertex_groups[name].index and g.weight>.001 for g in v.groups) for v in obj.data.vertices) for name in obj.vertex_groups.keys() if any(n in name for n in ['Thumb_','Index_','Middle_','Ring_','Little_'])}
 assert len(bone_weights)==15 and min(bone_weights.values())>0
 checks={}
 for kind in ['Open','Grip','Point']:
  rig.animation_data.action=bpy.data.actions['Camper_'+('Left' if 'Left' in rig.name else 'Right')+'_'+kind];bpy.context.scene.frame_set(12)
  eo=obj.evaluated_get(bpy.context.evaluated_depsgraph_get());me=eo.to_mesh();me.calc_loop_triangles()
  maxstretch=max((me.vertices[e.vertices[0]].co-me.vertices[e.vertices[1]].co).length/ln for e,ln in zip(obj.data.edges,lengths) if ln>1e-7)
  ratios=[(me.vertices[e.vertices[0]].co-me.vertices[e.vertices[1]].co).length/ln for e,ln in zip(obj.data.edges,lengths) if ln>.0005]
  if kind=='Grip':
   top=sorted(((me.vertices[e.vertices[0]].co-me.vertices[e.vertices[1]].co).length/ln,ln,(me.vertices[e.vertices[0]].co-me.vertices[e.vertices[1]].co).length,list(obj.data.vertices[e.vertices[0]].co)) for e,ln in zip(obj.data.edges,lengths) if ln>.0005)[-3:];print('STRETCH_DIAGNOSTIC',rig.name,top)
  finite=all(math.isfinite(c) for v in me.vertices for c in v.co)
  assert finite
  checks[kind]={'finite':finite,'max_edge_stretch':maxstretch,'edge_stretch_95_percentile':float(np.quantile(ratios,.95)),'max_edge_stretch_above_half_mm':max(ratios),'minimum_face_area':min(p.area for p in me.polygons)};eo.to_mesh_clear()
 assert len(obj.data.uv_layers)==1
 results[rig.name]={'uv_channels':[uv.name for uv in obj.data.uv_layers],'weighted_finger_vertices':bone_weights,'poses':checks}
with open(os.path.join(out,'finger_validation.json'),'w') as f:json.dump(results,f,indent=2)
print('FINGER_VALIDATION',json.dumps(results))
