import bpy, math, os
from mathutils import Vector
OUT=os.environ.get('HANDS_OUTPUT',os.path.dirname(os.path.abspath(__file__)))
os.makedirs(os.path.join(OUT,'renders'),exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
m=bpy.data.materials.new('Peach clay'); m.diffuse_color=(.82,.48,.30,1); m.use_nodes=True
p=m.node_tree.nodes.get('Principled BSDF'); p.inputs['Base Color'].default_value=m.diffuse_color; p.inputs['Roughness'].default_value=.5; p.inputs['Subsurface Weight'].default_value=.06
def ell(loc,scale):
 bpy.ops.mesh.primitive_uv_sphere_add(segments=40,ring_count=28,location=loc)
 o=bpy.context.object; o.scale=scale; bpy.ops.object.transform_apply(location=False,rotation=False,scale=True); return o
def digit(x,z,length,r,lean,curl):
 vs=[]; fs=[]; n=28; k=28
 for j in range(n+1):
  t=j/n; rad=r*(1-.16*t+.03*math.sin(t*math.pi*3))
  if t>.75: rad*=math.sqrt(max(.00001,1-((t-.75)/.25)**2))
  for i in range(k):
   a=i*2*math.pi/k; vs.append((x+lean*t+rad*math.cos(a),-curl*t*t+rad*.86*math.sin(a),z+length*t))
 for j in range(n):
  for i in range(k):
   a=j*k+i; b=j*k+(i+1)%k; fs.append((a,b,b+k,a+k))
 fs.extend([tuple(range(k-1,-1,-1)),tuple(n*k+i for i in range(k))])
 me=bpy.data.meshes.new('Tapered curved finger'); me.from_pydata(vs,[],fs); me.update(); o=bpy.data.objects.new('Finger',me); bpy.context.collection.objects.link(o); return o
for s in [-1,1]:
 parts=[ell((0,0,-.10),(.94,.34,.88)),ell((0,0,.22),(.91,.33,.48)),ell((0,.02,-.75),(.57,.30,.63)),ell((0,.05,-1.4),(.48,.29,.95))]
 for a in [(-.72,.32,1.22,.21,-.12,.18),(-.25,.40,1.55,.22,-.035,.23),(.25,.42,1.65,.225,.035,.26),(.74,.33,1.42,.215,.12,.22)]: parts.append(digit(*a))
 # Lower the thumb root and open its angle so the thumb-index web stays low.
 parts.append(ell((.69,-.07,-.36),(.40,.32,.43)))
 th=digit(0,0,1.05,.28,.02,.18); th.rotation_euler[1]=math.radians(62); th.location=(.73,-.07,-.30); parts.append(th)
 bpy.ops.object.select_all(action='DESELECT')
 for o in parts: o.select_set(True)
 bpy.context.view_layer.objects.active=parts[0]; bpy.ops.object.join(); o=bpy.context.object; o.name='Right Hand' if s==1 else 'Left Hand'
 md=o.modifiers.new('Unified sculpt','REMESH'); md.mode='VOXEL'; md.voxel_size=.032; bpy.ops.object.modifier_apply(modifier=md.name)
 md=o.modifiers.new('Soft transitions','SMOOTH'); md.factor=.72; md.iterations=10; bpy.ops.object.modifier_apply(modifier=md.name)
 md=o.modifiers.new('Surface finish','SUBSURF'); md.levels=1
 for v in o.data.vertices: v.co.x*=s
 for f in o.data.polygons: f.use_smooth=True
 o.data.materials.append(m); o.location=(s*2,0,0); o.rotation_euler[1]=s*math.radians(-8); o.rotation_euler[2]=s*math.radians(-6)
def aim(o,target): o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for name,loc,power,size,col in [('Key',(-3,-4,7),1000,5,(1,.86,.73)),('Fill',(4,-4,2),650,5,(.78,.87,1)),('Rim',(1,3,5),1100,4,(1,.8,.65))]:
 bpy.ops.object.light_add(type='AREA',location=loc); o=bpy.context.object; o.name=name; o.data.energy=power; o.data.shape='DISK'; o.data.size=size; o.data.color=col; aim(o,(0,0,0))
sc=bpy.context.scene; sc.world.color=(.12,.15,.19); sc.render.engine='CYCLES'; sc.cycles.samples=32; sc.cycles.use_denoising=True; sc.render.image_settings.file_format='PNG'; sc.render.resolution_percentage=100
bpy.ops.object.camera_add(); cam=bpy.context.object; sc.camera=cam
views=[('fps_view',(0,-10,4.3),(0,0,.8),1600,900,8.7),('front_view',(0,-12,.3),(0,0,0),1200,900,8.5),('back_view',(0,12,1),(0,0,0),1200,900,8.5),('perspective_view',(7,-11,5),(0,0,0),1200,900,8.8)]
def view(v):
 name,loc,target,w,h,size=v; cam.location=loc; aim(cam,target); cam.data.type='ORTHO'; cam.data.ortho_scale=size
 if name=='fps_view': cam.data.type='PERSP'; cam.data.lens=43
 sc.render.resolution_x=w; sc.render.resolution_y=h; sc.render.filepath=os.path.join(OUT,'renders',name+'.png')
for v in views:
 view(v); bpy.ops.render.render(write_still=True)
view(views[0]); bpy.context.preferences.filepaths.save_version=0
for a in bpy.context.screen.areas:
 if a.type=='VIEW_3D': a.spaces.active.region_3d.view_perspective='CAMERA'
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(OUT,'CartoonHands_v01.blend'))
