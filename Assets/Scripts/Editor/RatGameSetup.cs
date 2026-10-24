using HuntHappens.SmallGame;
using HuntHappens.Player;
using HuntHappens.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
namespace HuntHappens.Editor
{
    public static class RatGameSetup
    {
        private static Material fur, pink, metal, bread;
        public static void RunBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            System.IO.Directory.CreateDirectory("Assets/Art/SmallGame"); AssetDatabase.Refresh();
            fur = Mat("Rat Fur", new Color(0.28f,0.24f,0.21f)); pink = Mat("Rat Pink",new Color(0.72f,0.43f,0.4f));
            metal = Mat("Cage Steel",new Color(0.32f,0.4f,0.44f)); bread = Mat("Bread",new Color(0.88f,0.59f,0.26f));
            var cage = new GameObject("Rat Cage");
            var component = cage.AddComponent<RatCage>();
            var box = cage.AddComponent<BoxCollider>(); box.center = new Vector3(0,0.25f,0); box.size = new Vector3(0.64f,0.5f,0.9f);
            Part(cage.transform,"Floor",PrimitiveType.Cube,new Vector3(0,0.025f,0),new Vector3(0.64f,0.05f,0.9f),metal);
            for (int i=0;i<6;i++) for(int side=-1;side<=1;side+=2)
                Part(cage.transform,"Bars",PrimitiveType.Cube,new Vector3(side*0.3f,0.26f,-0.4f+i*0.16f),new Vector3(0.025f,0.5f,0.025f),metal);
            for(int i=0;i<5;i++)
            {
                Part(cage.transform,"Roof",PrimitiveType.Cube,new Vector3(-0.3f+i*0.15f,0.5f,0),new Vector3(0.025f,0.025f,0.88f),metal);
                Part(cage.transform,"Back",PrimitiveType.Cube,new Vector3(-0.3f+i*0.15f,0.25f,-0.44f),new Vector3(0.025f,0.5f,0.025f),metal);
            }
            component.door = new GameObject("Sliding Door").transform; component.door.SetParent(cage.transform,false); component.door.localPosition=new Vector3(0,0.65f,0.44f);
            for(int i=0;i<5;i++) Part(component.door,"Door Bar",PrimitiveType.Cube,new Vector3(-0.3f+i*0.15f,0,0),new Vector3(0.025f,0.48f,0.025f),metal);
            component.bait = Part(cage.transform,"Bread Bait",PrimitiveType.Sphere,new Vector3(0,0.1f,-0.2f),new Vector3(0.15f,0.1f,0.12f),bread); component.bait.SetActive(false);
            var cagePrefab = PrefabUtility.SaveAsPrefabAsset(cage,"Assets/Art/SmallGame/RatCage.prefab").GetComponent<RatCage>(); Object.DestroyImmediate(cage);
            var rat = new GameObject("Rat"); var ai=rat.AddComponent<RatAI>(); var agent=rat.GetComponent<NavMeshAgent>(); agent.radius=0.15f;agent.height=0.35f;agent.speed=0.8f;agent.angularSpeed=480;agent.acceleration=8;agent.stoppingDistance=0.08f;
            ai.visual=new GameObject("Rat Visual").transform; ai.visual.SetParent(rat.transform,false);
            Part(ai.visual,"Body",PrimitiveType.Sphere,new Vector3(0,0.17f,0),new Vector3(0.3f,0.27f,0.48f),fur);
            Part(ai.visual,"Head",PrimitiveType.Sphere,new Vector3(0,0.18f,0.25f),new Vector3(0.22f,0.2f,0.25f),fur);
            Part(ai.visual,"Nose",PrimitiveType.Sphere,new Vector3(0,0.15f,0.38f),Vector3.one*0.07f,pink);
            for(int side=-1;side<=1;side+=2)
            {
                Part(ai.visual,"Ear",PrimitiveType.Sphere,new Vector3(side*0.1f,0.3f,0.2f),new Vector3(0.12f,0.14f,0.05f),pink);
                Part(ai.visual,"Eye",PrimitiveType.Sphere,new Vector3(side*0.1f,0.22f,0.29f),Vector3.one*0.035f,metal);
                for(int foot=-1;foot<=1;foot+=2) Part(ai.visual,"Foot",PrimitiveType.Sphere,new Vector3(side*0.1f,0.035f,foot*0.15f),new Vector3(0.09f,0.06f,0.13f),pink);
            }
            for(int i=0;i<8;i++) Part(ai.visual,"Tail",PrimitiveType.Sphere,new Vector3(Mathf.Sin(i*0.4f)*0.1f,0.04f,-0.24f-i*0.055f),new Vector3(0.045f-i*0.003f,0.04f,0.08f),pink);
            var ratPrefab=PrefabUtility.SaveAsPrefabAsset(rat,"Assets/Art/SmallGame/Rat.prefab");Object.DestroyImmediate(rat);
            var player=Object.FindAnyObjectByType<PrototypeInput>();var shop=Object.FindAnyObjectByType<ShopManager>();
            var hunting=player.GetComponent<RatHunting>();if(hunting==null)hunting=player.gameObject.AddComponent<RatHunting>();
            hunting.cagePrefab=cagePrefab;hunting.ground=GameObject.Find("Environment/Ground").GetComponent<Collider>();hunting.shop=shop;
            var old=GameObject.Find("Small Game");if(old!=null)Object.DestroyImmediate(old);
            var root=new GameObject("Small Game");
            for(int i=0;i<8;i++)
            {
                Vector3 origin=i<4?player.transform.position:shop.Kiosk.transform.position;
                Vector3 desired=origin+Quaternion.Euler(0,i*70,0)*Vector3.forward*(6+i%3*2);
                if(!NavMesh.SamplePosition(desired,out var nav,5,NavMesh.AllAreas))throw new System.Exception("No rat spawn");
                var instance=(GameObject)PrefabUtility.InstantiatePrefab(ratPrefab);instance.transform.SetParent(root.transform);instance.transform.position=nav.position;
            }
            var basket=new GameObject("Free Bread Basket");basket.transform.SetParent(root.transform);
            Vector3 near=shop.Kiosk.InteractionPoint.position+shop.Kiosk.transform.right*3.5f;
            basket.transform.position=new Vector3(near.x,0,near.z);basket.AddComponent<BreadBasket>();var basketBox=basket.AddComponent<BoxCollider>();basketBox.center=new Vector3(0,0.55f,0);basketBox.size=new Vector3(0.8f,1.1f,0.7f);
            Part(basket.transform,"Crate",PrimitiveType.Cube,new Vector3(0,0.4f,0),new Vector3(0.8f,0.8f,0.7f),fur);
            for(int i=0;i<3;i++)Part(basket.transform,"Loaf",PrimitiveType.Capsule,new Vector3(-0.23f+i*0.23f,0.9f,0),new Vector3(0.2f,0.12f,0.4f),bread);
            var sign=new GameObject("Bread Sign").AddComponent<TMPro.TextMeshPro>();sign.transform.SetParent(basket.transform,false);sign.transform.localPosition=new Vector3(0,1.45f,0);sign.transform.rotation=shop.Kiosk.transform.rotation;sign.font=AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Materials/Tranquilizer/HUD.asset");sign.text="ХЛЕБ\nБЕСПЛАТНО";sign.fontSize=2;sign.alignment=TMPro.TextAlignmentOptions.Center;sign.rectTransform.sizeDelta=new Vector2(2,0.7f);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();Debug.Log("Rat game installed: eight rats, cage prefab, free bread and shop supplies.");
        }
        private static Material Mat(string name,Color color)
        { string path="Assets/Art/SmallGame/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.color=color;return m; }
        private static GameObject Part(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat)
        {var part=GameObject.CreatePrimitive(type);part.name=name;part.transform.SetParent(parent,false);part.transform.localPosition=pos;part.transform.localScale=scale;Object.DestroyImmediate(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=mat;return part;}
    }
}
