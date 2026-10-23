import bpy,bmesh,os,math,json
import numpy as np
from mathutils import Vector,Matrix
REPO_SOURCE=os.path.abspath(os.path.join(os.path.dirname(__file__),'..','..','..','Assets','Characters','Camper','Models','Camper.fbx'))
SOURCE=os.environ.get('CAMPER_SOURCE',REPO_SOURCE if os.path.isfile(REPO_SOURCE) else r'E:\unity\Meshy_AI_cartoon_camper_rigged_biped\Meshy_AI_cartoon_camper_rigged_biped_Character_output.fbx')
OUT=os.environ.get('CAMPER_HANDS_OUTPUT',os.path.dirname(os.path.abspath(__file__)))
os.makedirs(os.path.join(OUT,'renders'),exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True);bpy.ops.import_scene.fbx(filepath=SOURCE)
original=next(o for o in bpy.context.scene.objects if o.type=='MESH');original_arm=next(o for o in bpy.context.scene.objects if o.type=='ARMATURE')
report={'source':SOURCE,'method':'extract original arms; preserve UV; simple subdivision; add finger chains and smooth skin weights','hands':{}}
hands=[]
def smooth(x):x=min(1,max(0,x));return x*x*(3-2*x)
def nearest(p,a,b):
 d=b-a;t=min(1,max(0,(p-a).dot(d)/d.length_squared));return (p-a-t*d).length,t
def orient_components(mesh):
 bm=bmesh.new();bm.from_mesh(mesh);bm.normal_update();seen=set()
 for seed in bm.faces:
  if seed in seen:continue
  stack=[seed];seen.add(seed);faces=[];verts=set()
  while stack:
   f=stack.pop();faces.append(f);verts.update(f.verts)
   for e in f.edges:
    for other in e.link_faces:
     if other not in seen:seen.add(other);stack.append(other)
  center=sum((v.co for v in verts),Vector())/len(verts)
  volume=sum((f.calc_center_median()-center).dot(f.normal)*f.calc_area() for f in faces)
  if volume< -1e-12:bmesh.ops.reverse_faces(bm,faces=faces)
 bm.normal_update();bm.to_mesh(mesh);bm.free()
definitions=[('Little',-.105,.195,.300),('Ring',-.055,.195,.335),('Middle',0,.195,.355),('Index',.057,.195,.330)]
for side,sign in [('Left',1),('Right',-1)]:
 b=original_arm.data.bones[side+'Hand'];w=original_arm.matrix_world@b.head_local;d=((original_arm.matrix_world@b.tail_local)-w).normalized()
 u=Vector((0,-1 if side=='Left' else 1,0));u=(u-d*u.dot(d)).normalized();n=d.cross(u).normalized();inv=Matrix((u,n,d))
 obj=original.copy();obj.data=original.data.copy();bpy.context.collection.objects.link(obj);world=original.matrix_world.copy();obj.parent=None;obj.matrix_world=Matrix.Identity(4)
 for m in list(obj.modifiers):obj.modifiers.remove(m)
 # Meshy supplies a spare UVMap before the actual render atlas "uv". Unity uses
 # UV0, so retain only the render atlas instead of exporting the spare channel.
 render_uv=next((uv.name for uv in obj.data.uv_layers if uv.active_render),obj.data.uv_layers.active.name)
 for uv in list(obj.data.uv_layers):
  if uv.name!=render_uv:obj.data.uv_layers.remove(uv)
 obj.data.uv_layers.active_index=0
 allowed={original.vertex_groups[name].index for name in [side+'Arm',side+'ForeArm',side+'Hand']}
 mask=[sum(g.weight for g in v.groups if g.group in allowed) for v in obj.data.vertices]
 bm=bmesh.new();bm.from_mesh(obj.data);bm.faces.ensure_lookup_table();bmesh.ops.delete(bm,geom=[f for f in bm.faces if min(mask[v.index] for v in f.verts)<.45],context='FACES');bm.to_mesh(obj.data);bm.free()
 for v in obj.data.vertices:v.co=inv@((world@v.co)-w)
 # FPS geometry ends just behind the wrist; keep the original full-body mesh intact.
 bm=bmesh.new();bm.from_mesh(obj.data)
 bmesh.ops.bisect_plane(bm,geom=list(bm.verts)+list(bm.edges)+list(bm.faces),dist=.000001,plane_co=(0,0,-.075),plane_no=(0,0,1),clear_inner=True,clear_outer=False)
 rim=[e for e in bm.edges if e.is_boundary and all(abs(v.co.z+.075)<.00001 for v in e.verts)]
 if rim:bmesh.ops.holes_fill(bm,edges=rim,sides=0)
 bm.to_mesh(obj.data);bm.free()
 obj.name='Camper_'+side+'_ArmMesh'
 # Preserve the source atlas while opening only tiny seams between fused fingertips.
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 for gap in [-.080,-.027,.030]:
  bpy.ops.mesh.primitive_uv_sphere_add(segments=24,ring_count=16,location=(gap*sign,.07,.365));cut=bpy.context.object;cut.scale=(.004,.35,.120)
  bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);bpy.context.view_layer.objects.active=obj
  mod=obj.modifiers.new('Narrow finger separation','BOOLEAN');mod.operation='DIFFERENCE';mod.solver='EXACT';mod.object=cut
  bpy.ops.object.modifier_apply(modifier=mod.name);bpy.data.objects.remove(cut,do_unlink=True)
 bpy.context.view_layer.objects.active=obj
 bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.remove_doubles(bm,verts=list(bm.verts),dist=.00015);bmesh.ops.dissolve_degenerate(bm,edges=list(bm.edges),dist=.00010);bm.to_mesh(obj.data);bm.free()
 sub=obj.modifiers.new('Joint deformation density','SUBSURF');sub.subdivision_type='SIMPLE';sub.levels=2;bpy.ops.object.modifier_apply(modifier=sub.name)
 bm=bmesh.new();bm.from_mesh(obj.data);bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(obj.data);bm.free()
 for f in obj.data.polygons:f.use_smooth=True
 obj.vertex_groups.clear()
 armdata=bpy.data.armatures.new('Camper_'+side+'_FingerSkeleton');rig=bpy.data.objects.new('Camper_'+side+'_FPS',armdata);bpy.context.collection.objects.link(rig)
 bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 def bone(name,h,t,parent=None,deform=True):
  b=armdata.edit_bones.new(name);b.head=h;b.tail=t;b.use_deform=deform
  if parent:b.parent=armdata.edit_bones[parent]
  b.align_roll(Vector((0,1,0)))
  return b
 elbow=inv@((original_arm.matrix_world@original_arm.data.bones[side+'ForeArm'].head_local)-w)
 bone('Forearm',elbow,(0,0,0));bone('Wrist',(0,0,0),(0,.035,.09),'Forearm');bone('Palm',(0,.035,.09),(0,.05,.19),'Wrist')
 bone('Guide_Forward',(0,0,.20),(0,0,.22),'Wrist',False);bone('Guide_Palm',(0,.15,0),(0,.17,0),'Wrist',False)
 paths={}
 for name,x,z0,z1 in definitions:
  pts=[Vector((sign*x,.045+(t*.035),z0+(z1-z0)*t)) for t in [0,.36,.72,1]];paths[name]=pts
 paths['Thumb']=[Vector((sign*x,y,z)) for x,y,z in [(.085,.065,.105),(.120,.090,.150),(.137,.112,.198),(.133,.12,.244)]]
 for name,pts in paths.items():
  parent='Palm'
  for j in range(3):bone(f'{name}_{j+1:02d}',pts[j],pts[j+1],parent);parent=f'{name}_{j+1:02d}'
  bone(name+'_tip',pts[-1],pts[-1]+Vector((0,0,.008)),parent,False)
 bpy.ops.object.mode_set(mode='OBJECT');rig.show_in_front=True;armdata.display_type='OCTAHEDRAL'
 groups={b.name:obj.vertex_groups.new(name=b.name) for b in armdata.bones if b.use_deform}
 for v in obj.data.vertices:
  p=v.co;finger_data=[]
  for name,pts in paths.items():
   dist,j,t=min((nearest(p,pts[j],pts[j+1])[0],j,nearest(p,pts[j],pts[j+1])[1]) for j in range(3))
   strength=smooth((p.z-pts[0].z-.005)/.055)
   if name=='Thumb':strength*=smooth((p.x*sign-.070)/.045)
   else:strength*=1-smooth((p.x*sign-.085)/.04)
   finger_data.append((dist,name,(j+t)/3,strength))
  weights={};scores=[math.exp(-dist*dist/.00035) for dist,_,_,_ in finger_data];total=sum(scores)+1e-30;assigned=0
  x=p.x*sign;owner='Thumb' if x>.105 and p.z<.26 else 'Index' if x>.030 else 'Middle' if x>-.027 else 'Ring' if x>-.080 else 'Little'
  separation=smooth((p.z-.275)/.040)
  for score,(dist,name,t,strength) in zip(scores,finger_data):
   ownership=(1-separation)*score/total+separation*(1 if name==owner else 0)
   weight=strength*ownership;assigned+=weight;u=max(0,t*3-.5);j=min(2,int(u));frac=min(1,u-j)
   freedom=1 if name=='Thumb' else smooth((p.z-.260)/.040)
   key=name+'_01';weights[key]=weights.get(key,0)+weight*(1-freedom)
   key=f'{name}_{j+1:02d}';weights[key]=weights.get(key,0)+weight*freedom*(1-frac);key=f'{name}_{min(2,j+1)+1:02d}';weights[key]=weights.get(key,0)+weight*freedom*frac
  rest=1-assigned;fore=1-smooth((p.z+.08)/.10);palm=smooth((p.z-.025)/.07)
  weights['Forearm']=rest*fore;weights['Wrist']=rest*(1-fore)*(1-palm);weights['Palm']=rest*(1-fore)*palm
  for name,value in weights.items():
   if value>1e-7:groups[name].add([v.index],value,'REPLACE')
 names=list(groups);idx={g.index:i for i,g in enumerate(groups.values())};W=np.zeros((len(obj.data.vertices),len(names)))
 for v in obj.data.vertices:
  for g in v.groups:W[v.index,idx[g.group]]=g.weight
 edges=np.array([tuple(e.vertices) for e in obj.data.edges]);aa=edges[:,0];bb=edges[:,1];degree=np.maximum(1,np.bincount(np.concatenate((aa,bb)),minlength=len(W)))[:,None]
 for iteration in range(8):
  accum=np.zeros_like(W);np.add.at(accum,aa,W[bb]);np.add.at(accum,bb,W[aa]);W=.45*W+.55*accum/degree
 for v in obj.data.vertices:
  row=W[v.index];ids=np.argsort(row)[-4:];total=row[ids].sum()
  for g in groups.values():g.remove([v.index])
  for i in ids:
   if row[i]>1e-7:groups[names[i]].add([v.index],float(row[i]/total),'REPLACE')
 mod=obj.modifiers.new('Finger and wrist skinning','ARMATURE');mod.object=rig;obj.parent=rig
 # Keep the original anatomical handedness; never reflect the mesh.
 orient_components(obj.data)
 obj.data.normals_split_custom_set([(0,0,0)]*len(obj.data.loops))
 # Boolean cutters may add their own spare UVMap again; strip it after edits.
 for uv in list(obj.data.uv_layers):
  if uv.name!=render_uv:obj.data.uv_layers.remove(uv)
 obj.data.uv_layers.active_index=0
 rig.animation_data_create();actions={}
 for kind,curl in [('Open',0),('Grip',.70),('Point',.58)]:
  act=bpy.data.actions.new('Camper_'+side+'_'+kind);act.use_fake_user=True;rig.animation_data.action=act
  for f,blend in [(1,0),(12,1),(24,0)]:
   for name in paths:
    for j in range(3):
     pb=rig.pose.bones[f'{name}_{j+1:02d}'];pb.rotation_mode='XYZ';factor=[.65,.85,.55][j]
     amount=curl*(.12 if kind=='Point' and name=='Index' else 1)*(.65 if name=='Thumb' else 1)
     pb.rotation_euler=(amount*factor*blend,0,0);pb.keyframe_insert(data_path='rotation_euler',frame=f,group=pb.name)
  actions[kind]=act
 rig.animation_data.action=actions['Open'];bpy.context.scene.frame_set(1)
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);rig.select_set(True);bpy.context.view_layer.objects.active=rig
 bpy.ops.export_scene.fbx(filepath=os.path.join(OUT,'CamperFPS_'+side+'.fbx'),use_selection=True,object_types={'MESH','ARMATURE'},axis_forward='-Z',axis_up='Y',add_leaf_bones=False,bake_anim=True,bake_anim_use_all_actions=False,bake_anim_use_nla_strips=False,bake_anim_simplify_factor=0,use_mesh_modifiers=True,mesh_smooth_type='FACE',path_mode='STRIP')
 rig.location=(.28 if side=='Right' else -.28,0,0)
 report['hands'][side]={'vertices':len(obj.data.vertices),'triangles':sum(len(p.vertices)-2 for p in obj.data.polygons),'finger_chains':5,'phalanges_per_finger':3,'bones':len(armdata.bones),'unweighted':sum(not v.groups for v in obj.data.vertices),'max_influences':max(len(v.groups) for v in obj.data.vertices),'source_uv_preserved':bool(obj.data.uv_layers)}
 hands.append((rig,obj,actions))
bpy.data.objects.remove(original,do_unlink=True);bpy.data.objects.remove(original_arm,do_unlink=True)
s=bpy.context.scene;s.render.engine='CYCLES';s.cycles.samples=24;s.cycles.use_denoising=True;s.render.resolution_x=1600;s.render.resolution_y=900;s.render.resolution_percentage=100;s.render.image_settings.file_format='PNG';s.render.fps=30
s.world=bpy.data.worlds.new('Hands studio');s.world.color=(.10,.12,.15)
bpy.ops.object.camera_add(location=(0,1.0,.65));cam=bpy.context.object;target=Vector((0,0,-.04));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.20;s.camera=cam
for pos,power,size in [((-1,1,1),130,1.5),((1,.3,.5),80,1.0)]:
 bpy.ops.object.light_add(type='AREA',location=pos);l=bpy.context.object;l.data.energy=power;l.data.size=size;l.rotation_euler=(target-l.location).to_track_quat('-Z','Y').to_euler()
for kind in ['Open','Grip','Point']:
 for rig,obj,actions in hands:rig.animation_data.action=actions[kind]
 s.frame_set(12);s.render.filepath=os.path.join(OUT,'renders',kind.lower()+'.png');bpy.ops.render.render(write_still=True)
for rig,obj,actions in hands:rig.animation_data.action=actions['Open']
s.frame_set(1);s.frame_start=1;s.frame_end=24;bpy.ops.file.pack_all();bpy.context.preferences.filepaths.save_version=0
bpy.ops.object.select_all(action='DESELECT')
for rig,obj,actions in hands:rig.select_set(True)
bpy.context.view_layer.objects.active=hands[0][0]
for area in bpy.context.screen.areas:
 if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.shading.type='MATERIAL'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'CamperFPSHands_Rigged.blend'))
with open(os.path.join(OUT,'rig_report.json'),'w') as f:json.dump(report,f,indent=2)
print('CAMPER_FPS_HANDS_COMPLETE',json.dumps(report))
