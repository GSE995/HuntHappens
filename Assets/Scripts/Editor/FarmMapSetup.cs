using System.Collections.Generic;
using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.SmallGame;
using HuntHappens.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class FarmMapSetup
    {
        static Material wood, bark, grass, leaf, stone, dirt, roof, cream;
        static Transform environment;
        static readonly Vector3 Left = new Vector3(-27, 0, 27), Right = new Vector3(30, 0, 30);
        static readonly Vector3[] Road = Enumerable.Range(0, 41).Select(i => new Vector3(16 + 5 * Mathf.Sin((-50 + i * 2.5f) * .075f), .025f, -50 + i * 2.5f)).ToArray();
        static readonly Vector3[][] Trails = {
            new[]{new Vector3(-12,0,-6),new Vector3(-12,0,1),new Vector3(-5,0,7),new Vector3(16,0,6)},
            new[]{new Vector3(-12,0,1),new Vector3(-21,0,12),Left},
            new[]{new Vector3(19,0,8),new Vector3(28,0,12),Right},
            new[]{new Vector3(-27,0,27),new Vector3(-12,0,30),new Vector3(20,0,35)},
            new[]{new Vector3(20,0,35),new Vector3(30,0,30)},
            new[]{new Vector3(-12,0,-30),new Vector3(-12,0,-20),new Vector3(-12,0,-10)},
            new[]{new Vector3(-10,0,-30),new Vector3(16,0,-30)}
        };
        [MenuItem("Tools/Hunt Happens/Rebuild Farm Map")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Edit mode required");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/ForestPrototype.unity") throw new System.InvalidOperationException("Open ForestPrototype first");
            if (GameObject.Find("Farm Map") != null) throw new System.InvalidOperationException("Map already built; restore backup to regenerate");
            environment = GameObject.Find("Environment").transform;
            wood = Load("ForestPrototype/Crate"); bark = Load("ForestPrototype/Bark"); grass = Load("ForestPrototype/Grass");
            leaf = Load("ForestPrototype/Leaves"); stone = Load("ForestPrototype/Rock");
            System.IO.Directory.CreateDirectory("Assets/Materials/FarmMap");
            dirt = Mat("Ochre paths", new Color(.62f,.46f,.25f)); roof = Mat("Terracotta",new Color(.48f,.18f,.10f));
            cream = Mat("Warm plaster",new Color(.82f,.73f,.52f));
            var map = Group("Farm Map", environment);
            var farm = Group("Farm", map); var shopGroup = Group("Shop", map); var pen = Group("Enclosure",map);
            var roads = Group("Road and trails",map); var forest = Group("Forest",map); var zones = Group("Animal zones",map); var decor = Group("Decorations",map);
            // Keep ground and boundary colliders, replace only the old scattered layout.
            var treeTemplate = Object.Instantiate(environment.Find("Tree 00").gameObject); treeTemplate.name="Tree template";
            foreach(Transform t in environment.Cast<Transform>().ToArray())
                if(t.name.StartsWith("Tree ") || t.name.StartsWith("Rock ") || t.name.StartsWith("Crate ")) Object.DestroyImmediate(t.gameObject);
            Patch("Farm courtyard",new Vector3(-12,.012f,-31),new Vector3(48,.02f,24),dirt,farm);
            Patch("Pen pasture",new Vector3(-15,.015f,-13),new Vector3(32,.02f,15),grass,pen);
            Path("Main road",Road,5.5f,roads);
            for(int i=0;i<Trails.Length;i++) Path("Trail "+i,Trails[i],3.6f,roads);
            House(farm,new Vector3(-29,0,-32));
            // Relocate the complete existing shop and every product with the same rigid transform.
            var kiosk=Object.FindAnyObjectByType<ShopInteractable>().transform;
            Vector3 old=kiosk.position; Quaternion turn=Quaternion.Euler(0,90,0); Vector3 next=new Vector3(8,.05f,-29);
            var goods=GameObject.Find("Shop Goods").transform;
            var basket=Object.FindAnyObjectByType<BreadBasket>().transform;
            foreach(Transform t in goods) {t.position=next+turn*(t.position-old);t.rotation=turn*t.rotation;}
            basket.position=next+turn*(basket.position-old); basket.rotation=turn*basket.rotation;
            kiosk.SetPositionAndRotation(next,turn*kiosk.rotation); kiosk.SetParent(shopGroup,true); goods.SetParent(shopGroup,true);basket.SetParent(shopGroup,true);
            // Existing kiosk is the counter of a deeper building, facing west into the courtyard.
            Box("Shop back room",new Vector3(11,1.7f,-29),new Vector3(4,3.4f,7),wood,shopGroup);
            Box("Shop canopy",new Vector3(8,3.6f,-29),new Vector3(10,.3f,8),roof,shopGroup);
            Sign("МАГАЗИН",new Vector3(3.4f,3,-29),90,shopGroup);
            var truck=Object.FindAnyObjectByType<PickupTruckDelivery>().transform;
            truck.position=new Vector3(-27,.05f,-41);truck.rotation=Quaternion.Euler(0,90,0);truck.SetParent(farm,true);
            var pickups=GameObject.Find("Prototype Interaction Objects").transform;
            int n=0; foreach(Transform t in pickups) t.position=new Vector3(-35+(n++%3)*2,t.position.y,-22-(n/3)*2);
            pickups.SetParent(decor,true);
            GameObject.Find("Player").transform.SetPositionAndRotation(new Vector3(-10,.1f,-30),Quaternion.Euler(0,25,0));
            GameObject.Find("Hero").transform.position=new Vector3(-20,.05f,-26);
            Fence(new Vector3(-39,0,-44),new Vector3(1,0,-44),farm);
            Fence(new Vector3(-39,0,-44),new Vector3(-39,0,-20),farm);
            Fence(new Vector3(-31,0,-20),new Vector3(-16,0,-20),pen);
            Fence(new Vector3(-8,0,-20),new Vector3(1,0,-20),pen);
            Fence(new Vector3(-31,0,-20),new Vector3(-31,0,-6),pen);
            Fence(new Vector3(1,0,-20),new Vector3(1,0,-6),pen);
            Fence(new Vector3(-31,0,-6),new Vector3(-16,0,-6),pen);
            Fence(new Vector3(-8,0,-6),new Vector3(1,0,-6),pen);
            Box("Gate lintel",new Vector3(-12,3.8f,-6),new Vector3(8,.3f,.3f),bark,pen);
            Box("Gate post",new Vector3(-16,1.9f,-6),new Vector3(.4f,3.8f,.4f),bark,pen);
            Box("Gate post",new Vector3(-8,1.9f,-6),new Vector3(.4f,3.8f,.4f),bark,pen);
            Sign("ПРИЁМ ОЛЕНЕЙ",new Vector3(-12,3.1f,-5.7f),180,pen);
            Sign("ВОЛЬЕР",new Vector3(-23,2.1f,-20.2f),0,pen);
            Patch("Reception pad",new Vector3(-12,.045f,-3),new Vector3(8,.04f,6),cream,pen);
            Box("Shelter roof",new Vector3(-26,2.8f,-13),new Vector3(7,.3f,5),wood,pen);
            for(int i=0;i<4;i++) Box("Shelter post",new Vector3(-29+(i%2)*6,1.4f,-15+(i/2)*4),new Vector3(.25f,2.8f,.25f),bark,pen);
            Box("Feeding trough",new Vector3(-22,.4f,-17),new Vector3(3,.8f,1),wood,pen);
            var enclosure=pen.gameObject.AddComponent<DeerEnclosure>();
            var places=new List<Transform>();
            for(int i=0;i<10;i++) {var t=Group("Resting place "+i,pen);t.position=new Vector3(-25+(i%5)*5,0,-10-(i/5)*6);t.rotation=Quaternion.Euler(0,i*67,0);places.Add(t);}
            Set(enclosure,"displayPrefab",AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Animals/Deer.prefab").GetComponent<DeerSedation>());
            SetArray(enclosure,"restingPlaces",places.ToArray());
            var reception=Group("Automatic deer reception",pen); reception.position=new Vector3(-12,0,-3);
            var volume=reception.gameObject.AddComponent<BoxCollider>();volume.isTrigger=true;volume.center=new Vector3(0,2,0);volume.size=new Vector3(8,5,6);
            reception.gameObject.AddComponent<DeliveryZone>().ConfigureReception(Object.FindAnyObjectByType<ItemHolder>(),Object.FindAnyObjectByType<DeliveryManager>(),enclosure);
            // Two broad clearings and spawn anchors on opposite sides of the road.
            var anchors=new List<Transform>();
            foreach(var p in new[]{Left,Right}) {var t=Group("Deer clearing "+anchors.Count,zones);t.position=p;anchors.Add(t);Disc("Clearing grass",p+Vector3.up*.02f,12,grass,zones);}
            var spawner=Object.FindAnyObjectByType<DeerSpawner>();SetArray(spawner,"clearingCenters",anchors.ToArray());SetFloat(spawner,"spawnRadius",8);SetFloat(spawner,"minimumPlayerDistance",12);
            var deer=GameObject.Find("Prototype Deer").GetComponentsInChildren<DeerSedation>();
            for(int i=0;i<deer.Length;i++){deer[i].transform.position=(i%2==0?Left:Right)+new Vector3((i-1)*3,.05f,0);SetFloat(deer[i].GetComponent<DeerAI>(),"roamRadius",10);SetFloat(deer[i].GetComponent<DeerAI>(),"wanderRadius",6);}
            GameObject.Find("Boss Deer").transform.position=Right+new Vector3(4,.05f,4);
            var litter=GameObject.Find("Forest Litter").transform;
            Vector3[] patches={new Vector3(-7,0,5),new Vector3(28,0,9),new Vector3(-24,0,10)};
            var bags=litter.Cast<Transform>().Where(t=>t.name=="TrashBag").ToArray();
            Vector3[] origins=bags.Select(t=>t.position).ToArray();
            var scattered=litter.Find("Scattered Litter");
            if(scattered!=null) foreach(Transform t in scattered) {int j=Enumerable.Range(0,origins.Length).OrderBy(k=>(t.position-origins[k]).sqrMagnitude).First();t.position+=patches[j]-origins[j];}
            for(int i=0;i<bags.Length;i++) bags[i].position=patches[i];
            litter.SetParent(zones,true);
            var rats=Object.FindObjectsByType<RatAI>();
            for(int i=0;i<rats.Length;i++){int j=i%3;rats[i].transform.position=patches[j]+new Vector3((i/3-1)*1.8f,0,2);rats[i].territoryRadius=7;}
            for(int i=0;i<3;i++) {Disc("Litter patch",patches[i]+Vector3.up*.025f,3.5f,dirt,zones);Box("Discarded crate",patches[i]+new Vector3(1,.5f,-1),Vector3.one,wood,decor);}
            var rng=new System.Random(31415);
            for(int i=0;i<580;i++)
            {
                Vector3 p=new Vector3(-46+(float)rng.NextDouble()*92,0,-46+(float)rng.NextDouble()*92);
                if(Clear(p) || forest.Cast<Transform>().Any(t=>(t.position-p).sqrMagnitude<8)) continue;
                var t=Object.Instantiate(treeTemplate,forest).transform;t.name="Forest tree "+i;t.position=p;
                float s=.8f+(float)rng.NextDouble()*.65f;t.localScale=new Vector3(s,s,s);t.rotation=Quaternion.Euler(0,(float)rng.NextDouble()*360,0);
                foreach(var c in t.GetComponentsInChildren<Collider>()) if(c.name=="Crown") Object.DestroyImmediate(c);
            }
            Object.DestroyImmediate(treeTemplate);
            for(int i=0;i<24;i++)
            {
                Vector3 p=new Vector3(-44+(float)rng.NextDouble()*88,0,4+(float)rng.NextDouble()*40);
                if(Clear(p))continue;
                var rock=Primitive("Forest stone",PrimitiveType.Sphere,p+Vector3.up*.45f,new Vector3(1.6f,1,1.3f),stone,decor,true);rock.transform.rotation=Quaternion.Euler(0,i*41,15);
            }
            for(int row=0;row<3;row++) {Patch("Garden bed",new Vector3(-35,.06f,-29+row*3),new Vector3(3,.1f,2),bark,farm);for(int i=0;i<4;i++)Primitive("Garden plant",PrimitiveType.Sphere,new Vector3(-36+(i%2)*1.5f,.3f,-29.5f+row*3+(i/2)),Vector3.one*.55f,leaf,farm,false);}
            Physics.SyncTransforms(); Bake(); ValidateRoutes();
            foreach(var component in Object.FindObjectsByType<Component>()) if(PrefabUtility.IsPartOfPrefabInstance(component))PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log("Farm map built, routes validated, scene saved.");
        }
        static bool Clear(Vector3 p)
        {
            if(p.x>-42&&p.x<14&&p.z<-2)return true;
            if(Vector3.Distance(p,Left)<13||Vector3.Distance(p,Right)<13)return true;
            if(new[]{new Vector3(-7,0,5),new Vector3(28,0,9),new Vector3(-24,0,10)}.Any(t=>Vector3.Distance(p,t)<5))return true;
            if(NearPath(p,Road,5))return true;
            return Trails.Any(path=>NearPath(p,path,4));
        }
        public static void FinishLandscape()
        {
            if(EditorApplication.isPlaying)throw new System.Exception("Edit mode required");
            var map=GameObject.Find("Farm Map").transform;
            if(map.Find("Landscape details")!=null)return;
            var details=Group("Landscape details",map);
            var colors=new[]{new Color(.16f,.34f,.13f),new Color(.24f,.43f,.17f),new Color(.31f,.49f,.17f)};
            var foliage=colors.Select((c,i)=>Mat("Foliage "+i,c)).ToArray();
            var clearing=Mat("Clearing meadow",new Color(.48f,.62f,.27f));
            var rng=new System.Random(219);
            foreach(var r in map.Find("Forest").GetComponentsInChildren<Renderer>())if(r.name=="Crown")r.sharedMaterial=foliage[rng.Next(3)];
            foreach(var r in map.Find("Animal zones").GetComponentsInChildren<Renderer>())if(r.name=="Clearing grass")r.sharedMaterial=clearing;
            for(int i=0;i<180;i++)
            {
                Vector3 p=new Vector3(-45+(float)rng.NextDouble()*90,0,-45+(float)rng.NextDouble()*90);
                if(Clear(p))continue;
                float s=.5f+(float)rng.NextDouble()*.7f;
                Primitive("Undergrowth",PrimitiveType.Sphere,p+Vector3.up*s*.45f,new Vector3(s*1.7f,s,s*1.5f),foliage[rng.Next(3)],details,false);
            }
            // Narrower, rounded trail joins distinguish them from the main road.
            dirt=AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/FarmMap/Ochre paths.mat");
            var paths=map.Find("Road and trails");
            foreach(Transform t in paths.Cast<Transform>().Where(t=>t.name.StartsWith("Trail ")).ToArray())Object.DestroyImmediate(t.gameObject);
            for(int i=0;i<Trails.Length;i++)
            {
                var controls=Trails[i];var smooth=new List<Vector3>();
                for(int j=0;j<controls.Length-1;j++)for(int k=0;k<8;k++)
                {
                    float t=k/8f;var a=controls[Mathf.Max(0,j-1)];var b=controls[j];var c=controls[j+1];var d=controls[Mathf.Min(controls.Length-1,j+2)];
                    smooth.Add(.5f*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t));
                }
                smooth.Add(controls[controls.Length-1]);Path("Trail "+i,smooth.ToArray(),2.8f,paths);
            }
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        static bool NearPath(Vector3 p,Vector3[] path,float width)
        { for(int i=1;i<path.Length;i++){var a=path[i-1];var d=path[i]-a;float t=Mathf.Clamp01(Vector3.Dot(p-a,d)/d.sqrMagnitude);if(Vector3.Distance(p,a+d*t)<width)return true;}return false; }
        static void House(Transform parent,Vector3 p)
        {
            Box("Farmhouse walls",p+Vector3.up*2,new Vector3(10,4,8),cream,parent);
            for(int side=-1;side<=1;side+=2) {var panel=Box("Pitched tile roof",p+new Vector3(side*2.7f,4.9f,0),new Vector3(6.4f,.3f,10),roof,parent);panel.transform.rotation=Quaternion.Euler(0,0,-side*25);}
            Box("Chimney",p+new Vector3(-2,5.6f,1),new Vector3(1,3,1),stone,parent);
            Box("Door",p+new Vector3(5.03f,1.1f,0),new Vector3(.1f,2.2f,1.5f),bark,parent);
            for(int side=-1;side<=1;side+=2)Box("Window",p+new Vector3(5.06f,2.3f,side*2.6f),new Vector3(.1f,1.3f,1.4f),Load("Delivery/Windows"),parent);
            Sign("ФЕРМА",p+new Vector3(5.15f,3.3f,0),90,parent);
        }
        static void Fence(Vector3 a,Vector3 b,Transform parent)
        {
            int count=Mathf.CeilToInt(Vector3.Distance(a,b)/2.5f);
            for(int i=0;i<=count;i++)Box("Fence post",Vector3.Lerp(a,b,(float)i/count)+Vector3.up*.85f,new Vector3(.22f,1.7f,.22f),bark,parent);
            for(int i=0;i<2;i++){var beam=Box("Fence rail",(a+b)/2+Vector3.up*(.5f+i*.65f),new Vector3(.14f,.18f,Vector3.Distance(a,b)),wood,parent);beam.transform.rotation=Quaternion.LookRotation(b-a);}
        }
        static void Sign(string label,Vector3 p,float yaw,Transform parent)
        {
            var t=Group(label,parent);t.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));
            var board=Box("Signboard",p,new Vector3(5,.85f,.15f),cream,t);board.transform.rotation=t.rotation;
            var text=new GameObject("Label").AddComponent<TextMeshPro>();text.transform.SetParent(t,false);text.transform.localPosition=new Vector3(0,0,-.09f);text.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Materials/Tranquilizer/HUD.asset");text.text=label;text.fontSize=5;text.color=new Color(.18f,.12f,.06f);text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=new Vector2(4.8f,.8f);
        }
        static void Path(string name,Vector3[] points,float width,Transform parent)
        {
            var root=Group(name,parent);
            for(int i=1;i<points.Length;i++){Vector3 a=points[i-1],b=points[i];a.y=b.y=.035f;var segment=Box("Path",(a+b)/2,new Vector3(width,.025f,Vector3.Distance(a,b)+.2f),dirt,root,false);segment.transform.rotation=Quaternion.LookRotation(b-a);Disc("Curve",a,width*.5f,dirt,root);}
        }
        static void Disc(string name,Vector3 p,float radius,Material mat,Transform parent)=>Primitive(name,PrimitiveType.Cylinder,p,new Vector3(radius*2,.012f,radius*2),mat,parent,false);
        static void Patch(string name,Vector3 p,Vector3 size,Material mat,Transform parent)=>Box(name,p,size,mat,parent,false);
        static GameObject Box(string n,Vector3 p,Vector3 s,Material m,Transform parent,bool collider=true)=>Primitive(n,PrimitiveType.Cube,p,s,m,parent,collider);
        static GameObject Primitive(string n,PrimitiveType type,Vector3 p,Vector3 s,Material m,Transform parent,bool collider)
        {var g=GameObject.CreatePrimitive(type);g.name=n;g.transform.SetParent(parent);g.transform.position=p;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=m;if(!collider)Object.DestroyImmediate(g.GetComponent<Collider>());return g;}
        static Transform Group(string n,Transform p){var t=new GameObject(n).transform;t.SetParent(p,false);return t;}
        static Material Load(string n)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/"+n+".mat");
        static Material Mat(string n,Color c){var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.color=c;m.SetFloat("_Smoothness",0);AssetDatabase.CreateAsset(m,"Assets/Materials/FarmMap/"+n+".mat");return m;}
        static void Set(Object o,string key,Object value){var s=new SerializedObject(o);s.FindProperty(key).objectReferenceValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        static void SetFloat(Object o,string key,float value){var s=new SerializedObject(o);s.FindProperty(key).floatValue=value;s.ApplyModifiedPropertiesWithoutUndo();}
        static void SetArray(Object o,string key,Transform[] value){var s=new SerializedObject(o);var a=s.FindProperty(key);a.arraySize=value.Length;for(int i=0;i<value.Length;i++)a.GetArrayElementAtIndex(i).objectReferenceValue=value[i];s.ApplyModifiedPropertiesWithoutUndo();}
        public static void Bake()
        {
            var sources=new List<NavMeshBuildSource>();var markups=new List<NavMeshBuildMarkup>();
            foreach(var c in Object.FindObjectsByType<Collider>())
            {
                if(c.isTrigger || !c.enabled)continue;
                bool dynamic=c.GetComponentInParent<DeerAI>()!=null||c.GetComponentInParent<RatAI>()!=null||c.GetComponentInParent<HuntHappens.Characters.HeroWander>()!=null||c.GetComponentInParent<ItemHolder>()!=null||c.GetComponentInParent<PickupItem>()!=null;
                markups.Add(new NavMeshBuildMarkup{root=c.transform,ignoreFromBuild=dynamic,overrideArea=!dynamic&&c.name!="Ground",area=1});
            }
            UnityEngine.AI.NavMeshBuilder.CollectSources((Transform)null,~0,NavMeshCollectGeometry.PhysicsColliders,0,markups,sources);
            var settings=NavMesh.GetSettingsByIndex(0);settings.agentRadius=WorldScaleSetup.NavigationRadius;settings.agentHeight=WorldScaleSetup.NavigationHeight;settings.agentClimb=.25f;settings.overrideVoxelSize=true;settings.voxelSize=.15f;
            var data=UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings,sources,new Bounds(Vector3.zero,new Vector3(100,24,100)),Vector3.zero,Quaternion.identity);
            if(data==null)throw new System.Exception("Bake failed");
            // Detach registered tiles before replacing their serialized data.
            var navigation=Object.FindAnyObjectByType<ForestNavigation>();navigation.SetData(null);
            string path="Assets/Navigation/FarmMap.asset";var old=AssetDatabase.LoadAssetAtPath<NavMeshData>(path);
            if(old==null)AssetDatabase.CreateAsset(data,path);else{EditorUtility.CopySerialized(data,old);Object.DestroyImmediate(data);data=old;}
            navigation.SetData(data);AssetDatabase.SaveAssets();
        }
        public static string ValidateRoutes()
        {
            Vector3 start=new Vector3(-10,0,-30);var destinations=new[]{new Vector3(4,0,-29),new Vector3(-12,0,-3),Left,Right,new Vector3(16,0,42)};
            foreach(var target in destinations)
            {
                if(!NavMesh.SamplePosition(start,out var a,1,NavMesh.AllAreas)||!NavMesh.SamplePosition(target,out var b,2,NavMesh.AllAreas))throw new System.Exception("No NavMesh at "+target);
                var path=new NavMeshPath();if(!NavMesh.CalculatePath(a.position,b.position,NavMesh.AllAreas,path)||path.status!=NavMeshPathStatus.PathComplete)throw new System.Exception("Route blocked: "+target);
            }
            return "Five complete routes: shop, reception, both clearings, north road";
        }
        public static string Capture()
        {
            var go=new GameObject("Temporary map capture");var camera=go.AddComponent<Camera>();
            camera.transform.SetPositionAndRotation(new Vector3(0,35,0),Quaternion.Euler(90,0,0));camera.orthographic=true;camera.orthographicSize=52;camera.nearClipPlane=.1f;camera.farClipPlane=90;camera.backgroundColor=new Color(.1f,.16f,.1f);camera.clearFlags=CameraClearFlags.SolidColor;
            var rt=new RenderTexture(1600,1600,24);var previous=RenderTexture.active;
            try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;var texture=new Texture2D(1600,1600,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,1600),0,0);texture.Apply();System.IO.Directory.CreateDirectory("Screenshots");System.IO.File.WriteAllBytes("Screenshots/FarmMap-top.png",texture.EncodeToPNG());Object.DestroyImmediate(texture);}
            finally{RenderTexture.active=previous;camera.targetTexture=null;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(go);}
            return "Screenshots/FarmMap-top.png";
        }
    }
}
