using System;
using System.Collections;
using System.Linq;
using HuntHappens.Player;
using HuntHappens.Shop;
using HuntHappens.SmallGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class WorldShopChecks
    {
        static WorldShopChecks() => EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("WorldShopCheck",false))
            { SessionState.SetBool("WorldShopCheck",false);new GameObject("World Shop Checks").AddComponent<WorldShopCheckRunner>(); }
        };
        public static void RunBatch() { EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");SessionState.SetBool("WorldShopCheck",true);EditorApplication.EnterPlaymode(); }
    }
    public sealed class WorldShopCheckRunner : MonoBehaviour
    {
        private void Start() => StartCoroutine(SafeRun());
        private IEnumerator SafeRun()
        {
            var routine=Run();
            while(true)
            {
                object next;
                try { if(!routine.MoveNext())break;next=routine.Current; }
                catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);yield break;}
                yield return next;
            }
            Debug.Log("World shop checks PASSED: hover/name/price, purchase all goods, funds, cooldown, wall/distance blocking, sale once, persistence and upgrade UI.");EditorApplication.Exit(0);
        }
        private static void Check(bool condition,string message) { if(!condition)throw new Exception("World shop check: "+message); }
        private IEnumerator Run()
        {
            yield return new WaitForSeconds(1);
            var hunt=FindAnyObjectByType<RatHunting>();var input=hunt.GetComponent<PrototypeInput>();var camera=hunt.GetComponentInChildren<Camera>();var shop=hunt.shop;
            hunt.GetComponent<FirstPersonMotor>().enabled=false;hunt.GetComponent<CharacterController>().enabled=false;
            var rats=FindObjectsByType<RatAI>(FindObjectsSortMode.None);
            Check(rats.Count(r=>r.isGrey)==12 && rats.Count(r=>!r.isGrey&&!r.isAggressive)==15,"rare grey population");
            foreach(var rat in new[]{rats.First(r=>!r.isGrey&&!r.isAggressive),rats.First(r=>r.isGrey)})
            {
                var cage=Instantiate(hunt.cagePrefab,rat.transform.position,Quaternion.identity);cage.Owner=hunt;
                Check(cage.Interact(hunt)&&RatFearChecks.WhenCalm(cage,()=>cage.TryCapture(rat))&&cage.Interact(hunt),"capture and collect species");
            }
            hunt.Reload();Check(hunt.Rats==2&&hunt.GreyRats==1&&hunt.SaleTotal==25,"species survive capture and save");
            var products=FindObjectsByType<WorldShopItem>(FindObjectsSortMode.None);Array.Sort(products,(a,b)=>a.Kind.CompareTo(b.Kind));Check(products.Length==4,"four world displays");
            int cages=hunt.Cages,bread=hunt.Bread,darts=hunt.Darts;
            Aim(products[0]);Check(!products[0].TryUse(input)&&shop.Wallet.Balance==0,"insufficient funds");
            shop.Wallet.RestoreBalance(100);
            for(int i=0;i<3;i++)
            {
                Aim(products[i]);Check(WorldShopItem.FindTarget(camera,hunt.transform)==products[i],"hover selects nearest product");
                Check(products[i].Prompt.Contains(products[i].Title)&&products[i].Prompt.Contains("$"+products[i].Price),"name and price prompt");
                Check(products[i].TryUse(input),"purchase product "+i);Check(!products[i].TryUse(input),"purchase cooldown");yield return new WaitForSeconds(0.5f);
            }
            Check(hunt.Cages==cages+1&&hunt.Bread==bread+1&&hunt.Darts==darts+1&&shop.Wallet.Balance==71,"inventory and prices");
            Aim(products[1]);
            var blocker=GameObject.CreatePrimitive(PrimitiveType.Cube);blocker.transform.position=Vector3.Lerp(camera.transform.position,products[1].transform.position,0.5f);blocker.transform.localScale=Vector3.one*0.5f;Physics.SyncTransforms();
            Check(WorldShopItem.FindTarget(camera,hunt.transform)==null&&!products[1].TryUse(input),"cannot buy through wall");Destroy(blocker);yield return null;
            hunt.transform.position-=products[1].transform.forward*6;Physics.SyncTransforms();Check(!products[1].TryUse(input),"cannot buy from afar");
            Aim(products[1]);input.SetGameplayBlocked(true);Check(!products[1].TryUse(input),"blocked input cannot trade");input.SetGameplayBlocked(false);yield return new WaitForSeconds(0.1f);
            ShopProgress.SaveSupplies(hunt.Cages,hunt.Bread,2,hunt.Darts,1);hunt.Reload();Aim(products[3]);Check(products[3].TryUse(input)&&hunt.Rats==0&&shop.Wallet.Balance==96,"rat reception");yield return new WaitForSeconds(0.5f);Check(!products[3].TryUse(input),"no duplicate sale");
            shop.SaveProgress();hunt.Reload();Check(hunt.Cages==cages+1&&hunt.Bread==bread+1&&hunt.Darts==darts+1,"save survives trade");
            camera.transform.position=shop.Kiosk.transform.TransformPoint(new Vector3(0,4,-10));camera.transform.LookAt(shop.Kiosk.transform.position+Vector3.up);
            ShopVisualChecks.Capture(camera,null,"Logs/World-shop.png");
            camera.transform.localPosition=new Vector3(0,2.04f,0.45f);
            hunt.transform.position=shop.Kiosk.InteractionPoint.position-shop.Kiosk.transform.forward*1.6f-Vector3.up*1.7f;Physics.SyncTransforms();
            Check(shop.TryOpen(),"upgrade interface opens");
            var ui=shop.GetComponent<ShopUI>();Check(ui.BuyButton(UpgradeKind.MoreTraps)!=null,"upgrade button preserved");
            foreach(var button in ui.GetComponentsInChildren<UnityEngine.UI.Button>()) Check(!button.name.StartsWith("Supply"),"no supplies in interface");
            shop.Close();
            var black=rats.First(r=>!r.isGrey&&!r.isAggressive);var grey=rats.First(r=>r.isGrey);
            black.transform.position=new Vector3(-0.35f,0,0);grey.transform.position=new Vector3(0.35f,0,0);
            black.visual.gameObject.SetActive(true);grey.visual.gameObject.SetActive(true);
            camera.transform.position=new Vector3(1.1f,0.9f,1.5f);camera.transform.LookAt(new Vector3(0,0.12f,0));
            yield return new WaitForSeconds(0.3f);
            ShopVisualChecks.Capture(camera,null,"Logs/Grey-rat-comparison.png");
            void Aim(WorldShopItem item)
            {
                camera.transform.localPosition=new Vector3(0,2.04f,0.45f);
                hunt.transform.position=item.transform.position-item.transform.forward*1.9f-Vector3.up*0.45f;
                camera.transform.LookAt(item.transform.position);Physics.SyncTransforms();
            }
        }
    }
}
