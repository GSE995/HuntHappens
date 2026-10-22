import bpy, bmesh, math, os, json
import numpy as np
from mathutils import Vector, Matrix
OUT=os.environ.get('HANDS_OUTPUT',os.path.dirname(os.path.abspath(__file__)))
SOURCE=os.environ.get('HANDS_SOURCE',os.path.join(os.path.dirname(os.path.dirname(os.path.abspath(__file__))),'Iteration01','CartoonHands_v01.blend'))
os.makedirs(os.path.join(OUT,'renders'),exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=SOURCE)
sc=bpy.context.scene; sc.render.fps=30; sc.unit_settings.system='METRIC'; sc.unit_settings.scale_length=1
S=.065
hands=[bpy.data.objects['Left Hand'],bpy.data.objects['Right Hand']]
report={'source':SOURCE,'meters_per_original_unit':S,'geometry':{},'actions':{},'deformation':{}}
original={o.name:o.matrix_world.copy() for o in hands}
for o in hands:
 bm=bmesh.new(); bm.from_mesh(o.data)
 before=bm.calc_volume(signed=True)
 bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces)); bm.to_mesh(o.data)
 report['geometry'][o.name]={'vertices':len(bm.verts),'faces':len(bm.faces),'nonmanifold_edges':sum(not e.is_manifold for e in bm.edges),'signed_volume_before':before,'signed_volume_after':bm.calc_volume(signed=True),'fingers':5}
 assert all(e.is_manifold for e in bm.edges),'Open mesh'
 bm.free()
 for v in o.data.vertices:v.co=(original[o.name]@v.co)*S
 o.matrix_world=Matrix.Identity(4)
for o in sc.objects:
 if o.type in {'CAMERA','LIGHT'}:
  o.location*=S
  if o.type=='CAMERA':o.data.clip_start=.005; o.data.clip_end=100; o.data.ortho_scale*=S
  else:o.data.energy*=S*S; o.data.size*=S
ad=bpy.data.armatures.new('Hands_Skeleton'); rig=bpy.data.objects.new('Hands_Rig',ad); sc.collection.objects.link(rig)
bpy.context.view_layer.objects.active=rig; rig.select_set(True); bpy.ops.object.mode_set(mode='EDIT')
def bone(name,head,tail,parent=None):
 b=ad.edit_bones.new(name); b.head=head; b.tail=tail
 if parent:b.parent=ad.edit_bones[parent]
 return b
bone('Root',(0,0,-.18),(0,0,-.14)); ad.edit_bones['Root'].use_deform=False
digits=[('Little',-.72,.32,1.22,-.12,.18),('Ring',-.25,.40,1.55,-.035,.23),('Middle',.25,.42,1.65,.035,.26),('Index',.74,.33,1.42,.12,.22)]
paths={}; localpaths={}
for o in hands:
 side='L' if o.name.startswith('Left') else 'R'; sign=-1 if side=='L' else 1
 def world(p):return (original[o.name]@Vector((p[0]*sign,p[1],p[2])))*S
 bone('Forearm.'+side,world((0,.04,-2.3)),world((0,.02,-.85)),'Root')
 bone('Wrist.'+side,world((0,.02,-.85)),world((0,0,-.35)),'Forearm.'+side)
 bone('Palm.'+side,world((0,0,-.35)),world((0,0,.45)),'Wrist.'+side)
 for name,x,z,l,lean,curl in digits+[('Thumb',0,0,1.05,.02,.18)]:
  pts=[]
  for t in [0,.35,.70,1.0]:
   p=Vector((x+lean*t,-curl*t*t,z+l*t))
   if name=='Thumb':p=Matrix.Rotation(math.radians(62),3,'Y')@p+Vector((.73,-.07,-.30))
   pts.append(p)
  paths[(side,name)]=[world(p) for p in pts]; localpaths[(side,name)]=pts
  parent='Palm.'+side
  for j in range(3):
   bn=f'{name}_{j+1:02d}.{side}'; bone(bn,world(pts[j]),world(pts[j+1]),parent); parent=bn
bpy.ops.object.mode_set(mode='OBJECT'); rig.show_in_front=True; ad.display_type='OCTAHEDRAL'
def clamp(x):return min(1,max(0,x))
def smooth(x):x=clamp(x);return x*x*(3-2*x)
def segment(p,a,b):
 d=b-a; t=clamp((p-a).dot(d)/d.length_squared); return (p-(a+t*d)).length,t
for o in hands:
 side='L' if o.name.startswith('Left') else 'R'; inv=original[o.name].inverted(); sign=-1 if side=='L' else 1
 groups={b.name:o.vertex_groups.new(name=b.name) for b in ad.bones if b.use_deform and b.name.endswith('.'+side)}
 for v in o.data.vertices:
  q=inv@(v.co/S); q.x*=sign
  # Finger ownership by distance to curved centerlines, blended into the palm only near roots.
  candidates=[]
  for (sd,name),pts in localpaths.items():
   if sd!=side:continue
   best=min((segment(q,pts[j],pts[j+1])[0],j,segment(q,pts[j],pts[j+1])[1]) for j in range(3))
   dist,j,t=best; along=(j+t)/3
   if name=='Thumb':
    axis=(pts[-1]-pts[0]).normalized(); advance=(q-pts[0]).dot(axis); strength=smooth((advance-.04)/.32)
   else:strength=smooth((q.z-pts[0].z+.04)/.37)
   candidates.append((dist,name,along,strength))
  dist,name,t,strength=min(candidates)
  w={}
  if strength>0:
   # Blend rotations across each joint with at most two adjacent phalanges.
   u=t*3-.5; j=max(0,min(2,int(math.floor(u)))); frac=clamp(u-j)
   if u<0:j=0;frac=0
   w[f'{name}_{j+1:02d}.{side}']=strength*(1-frac)
   nxt=min(2,j+1); key=f'{name}_{nxt+1:02d}.{side}';w[key]=w.get(key,0)+strength*frac
  palm=1-strength; wrist=smooth((-q.z-.35)/.55); fore=smooth((-q.z-.85)/.45)
  w['Palm.'+side]=palm*(1-wrist); w['Wrist.'+side]=palm*wrist*(1-fore); w['Forearm.'+side]=palm*wrist*fore
  total=sum(w.values())
  for key,val in w.items():
   if val>1e-7:groups[key].add([v.index],val/total,'REPLACE')
 # Diffuse weights over mesh adjacency to eliminate abrupt ownership boundaries
 # at the thumb web and finger bases, then retain four linear-skin influences.
 names=list(groups); indices={groups[n].index:i for i,n in enumerate(names)}
 weights=np.zeros((len(o.data.vertices),len(names)),dtype=np.float64)
 for v in o.data.vertices:
  for g in v.groups:weights[v.index,indices[g.group]]=g.weight
 edges=np.array([tuple(e.vertices) for e in o.data.edges]); aa=edges[:,0];bb=edges[:,1]
 degree=np.bincount(np.concatenate((aa,bb)),minlength=len(weights))[:,None]
 for iteration in range(18):
  accum=np.zeros_like(weights);np.add.at(accum,aa,weights[bb]);np.add.at(accum,bb,weights[aa]);weights=.45*weights+.55*accum/degree
 for v in o.data.vertices:
  row=weights[v.index];ids=np.argsort(row)[-4:];total=float(row[ids].sum())
  for group in groups.values():group.remove([v.index])
  for i in ids:
   if row[i]>1e-7:groups[names[i]].add([v.index],float(row[i]/total),'REPLACE')
 mod=o.modifiers.new('Hands skin deformation','ARMATURE'); mod.object=rig; mod.use_deform_preserve_volume=False
 bpy.context.view_layer.objects.active=o; bpy.ops.object.modifier_move_up(modifier=mod.name)
 o.parent=rig
 report['geometry'][o.name]['unweighted_vertices']=sum(len(v.groups)==0 for v in o.data.vertices)
 report['geometry'][o.name]['max_influences']=max(len(v.groups) for v in o.data.vertices)
def pose(curlL=0,curlR=0,reach=0,lift=0,weapon=False,recoil=0,spread=0):
 for pb in rig.pose.bones:pb.rotation_mode='XYZ';pb.rotation_euler=(0,0,0);pb.location=(0,0,0)
 for side,curl in [('L',curlL),('R',curlR)]:
  for name,_,_,_,_,_ in digits:
   factor={'Little':1.05,'Ring':1,'Middle':.95,'Index':.85}[name]
   if weapon and side=='R' and name=='Index':factor=.33
   for j in range(3):rig.pose.bones[f'{name}_{j+1:02d}.{side}'].rotation_euler.x=-curl*factor*[.65,.9,.6][j]
  for j in range(3):rig.pose.bones[f'Thumb_{j+1:02d}.{side}'].rotation_euler.x=-curl*[.32,.55,.45][j]
  fore=rig.pose.bones['Forearm.'+side]; fore.location=(0,lift*S,(-reach+recoil)*S)
  rig.pose.bones['Wrist.'+side].rotation_euler.x=-.10*curl
  if weapon:
   rig.pose.bones['Wrist.'+side].rotation_euler.y=math.radians(12 if side=='L' else -8)
   rig.pose.bones['Wrist.'+side].rotation_euler.z=math.radians(-38 if side=='L' else 12)
   rig.pose.bones['Wrist.'+side].rotation_euler.x=math.radians(-22 if side=='L' else -30)
   fore.location.x=(-.30 if side=='L' else .10)*S
  fore.rotation_euler.z=spread*(1 if side=='L' else -1)
def key(frame):
 for pb in rig.pose.bones:
  pb.keyframe_insert(data_path='rotation_euler',frame=frame,group=pb.name); pb.keyframe_insert(data_path='location',frame=frame,group=pb.name)
specs={
 'Hands_Idle':[(1,(.08,.08,0,0,False,0,0)),(16,(.11,.11,.02,.025,False,0,.012)),(31,(.08,.08,0,0,False,0,0))],
 'Hands_Grab':[(1,(.08,.08,0)),(10,(.16,.16,.35)),(22,(.85,.85,.45)),(30,(.8,.8,.4))],
 'Hands_Hold':[(1,(.72,.72,.25)),(16,(.75,.75,.27,.015)),(31,(.72,.72,.25))],
 'Hands_Throw':[(1,(.75,.75,.10)),(9,(.9,.9,-.15)),(15,(.08,.08,.65,.08)),(28,(.08,.08,0))],
 'Hands_Weapon':[(1,(.72,.9,.22,0,True)),(16,(.74,.92,.24,.015,True)),(31,(.72,.9,.22,0,True))],
 'Hands_Shoot':[(1,(.72,.9,.22,0,True)),(4,(.75,.94,.22,.03,True,.13)),(10,(.72,.9,.22,0,True)),(18,(.72,.9,.22,0,True))],
 'Hands_Trap':[(1,(.65,.65,.12)),(14,(.7,.7,.42,-.5)),(22,(.10,.10,.48,-.52)),(38,(.08,.08,0))]}
actions={}; rig.animation_data_create()
for name,keys in specs.items():
 act=bpy.data.actions.new(name); rig.animation_data.action=act; act.use_fake_user=True
 for f,args in keys:pose(*args); key(f)
 actions[name]=act;report['actions'][name]={'frames':[keys[0][0],keys[-1][0]],'fps':30}
rig.animation_data.action=actions['Hands_Idle']; sc.frame_start=1;sc.frame_end=38;sc.frame_set(1)
# Measure the skinned cage at every frame: all weights normalized, finite coordinates,
# no new zero-area faces, and bounded edge stretch. Topology is unchanged by the rig.
for o in hands:
 rest=[v.co.copy() for v in o.data.vertices]; edges=[tuple(e.vertices) for e in o.data.edges]
 lengths=[(rest[a]-rest[b]).length for a,b in edges]; sub=next(m for m in o.modifiers if m.type=='SUBSURF');sub.show_viewport=False
 worst=0; minarea=1; bad=0
 for name,act in actions.items():
  rig.animation_data.action=act
  for f in range(1,int(act.frame_range[1])+1):
   sc.frame_set(f); deps=bpy.context.evaluated_depsgraph_get(); eo=o.evaluated_get(deps); me=eo.to_mesh()
   for (a,b),ln in zip(edges,lengths):
    if ln>1e-7:worst=max(worst,(me.vertices[a].co-me.vertices[b].co).length/ln)
   minarea=min(minarea,min(p.area for p in me.polygons));bad+=sum(not math.isfinite(c) for v in me.vertices for c in v.co)
   eo.to_mesh_clear()
 sub.show_viewport=True
 report['deformation'][o.name]={'max_edge_stretch':worst,'min_face_area_m2':minarea,'nonfinite_coordinates':bad}
 assert bad==0 and minarea>1e-14
sc.cycles.samples=32
for filename,name,frame in [('idle','Hands_Idle',1),('grab','Hands_Grab',22),('weapon','Hands_Weapon',1),('hold','Hands_Hold',1)]:
 rig.animation_data.action=actions[name];sc.frame_set(frame);sc.render.filepath=os.path.join(OUT,'renders',filename+'.png');bpy.ops.render.render(write_still=True)
rig.animation_data.action=actions['Hands_Idle'];sc.frame_set(1)
bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in hands:o.select_set(True)
bpy.context.view_layer.objects.active=rig
# Each Action is a separate FBX take; no camera/light or presentation objects exported.
subdivisions=[m for o in hands for m in o.modifiers if m.type=='SUBSURF']
for m in subdivisions:m.show_viewport=False;m.show_render=False
bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'CartoonHands_Rigged.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},global_scale=1,apply_unit_scale=True,axis_forward='-Z',axis_up='Y',add_leaf_bones=False,use_armature_deform_only=False,bake_anim=True,bake_anim_use_all_actions=True,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,path_mode='AUTO',mesh_smooth_type='FACE',use_mesh_modifiers=True)
for m in subdivisions:m.show_viewport=True;m.show_render=True
rig.animation_data.action=actions['Hands_Idle'];sc.frame_set(1);sc.frame_end=31
sc.render.filepath='//renders/idle.png';bpy.context.preferences.filepaths.save_version=0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'CartoonHands_v02.blend'))
with open(os.path.join(OUT,'quality_report.json'),'w',encoding='utf-8') as f:json.dump(report,f,indent=2)
print('RIG_GENERATION_COMPLETE',json.dumps(report))
