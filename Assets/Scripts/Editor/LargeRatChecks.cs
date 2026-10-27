using System;
using System.Collections;
using System.Linq;
using HuntHappens.SmallGame;
using HuntHappens.Player;
using HuntHappens.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class LargeRatChecks
    {
        static LargeRatChecks() => EditorApplication.playModeStateChanged += state => {
            if(state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("LargeRatChecks",false))
            { SessionState.SetBool("LargeRatChecks",false);new GameObject("Large Rat Checks").AddComponent<LargeRatCheckRunner>(); }
        };
        public static void RunBatch()
        {
            if(!Application.isBatchMode) throw new Exception("Use batch mode to isolate saved progress");
            ShopProgress.Reset();EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");SessionState.SetBool("LargeRatChecks",true);EditorApplication.EnterPlaymode();
        }
    }
    public sealed class LargeRatCheckRunner : MonoBehaviour
    {
        void Start() => StartCoroutine(SafeRun());
        IEnumerator SafeRun()
        {
            var run=Run();while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);yield break;}yield return next;}
            Debug.Log("Large rat checks PASSED: population, immediate bite/cooldown, wall blocking, leash, capture, no captive damage, $25 sale/save, respawn and player recovery.");EditorApplication.Exit(0);
        }
        void Check(bool ok,string label){if(!ok)throw new Exception("Large rat check: "+label);}
        IEnumerator Run()
        {
            yield return new WaitForSeconds(1);
            var rats=FindObjectsByType<RatAI>(FindObjectsSortMode.None);
            var spots=FindObjectsByType<RatGatheringSpot>(FindObjectsSortMode.None);
            Check(rats.Length==8+spots.Length && rats.Count(r=>r.isAggressive)==spots.Length,"one large rat per litter spot");
            foreach(var spot in spots)
                Check(rats.Count(r=>r.isAggressive && spots.OrderBy(s=>(s.transform.position-r.Home).sqrMagnitude).First()==spot)==1,"each litter spot has its own guard");
            Check(rats.Count(r=>r.isGrey)==2 && rats.Count(r=>!r.isGrey&&!r.isAggressive)==6,"original population intact");
            var rat=rats.First(r=>r.isAggressive);var agent=rat.GetComponent<NavMeshAgent>();
            var hunt=FindAnyObjectByType<RatHunting>();var health=hunt.GetComponent<PlayerHealth>();var input=hunt.GetComponent<PrototypeInput>();
            hunt.GetComponent<FirstPersonMotor>().enabled=false;hunt.GetComponent<CharacterController>().enabled=false;
            input.SetGameplayBlocked(false);
            foreach(var other in rats.Where(r=>r!=rat)){other.enabled=false;other.GetComponent<NavMeshAgent>().enabled=false;}
            var home=rat.transform.position;var playerSpawn=hunt.transform.position;
            agent.ResetPath();hunt.transform.position=home+Vector3.right*0.8f;Physics.SyncTransforms();
            yield return null;yield return null;Check(health.Current==80,"first foot contact immediately deals 20");
            yield return new WaitForSeconds(0.5f);Check(health.Current==80,"bite cooldown");
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=home+new Vector3(0.4f,0.35f,0);wall.transform.localScale=new Vector3(0.1f,0.7f,1);Physics.SyncTransforms();
            yield return new WaitForSeconds(2.5f);Check(health.Current==80,"no biting through walls");Destroy(wall);yield return null;
            hunt.transform.position=home+Vector3.right*24;
            yield return new WaitForSeconds(3);Check(Vector3.Distance(rat.transform.position,home)<3,"territory leash");
            var inventory = hunt.GetComponent<HuntHappens.Interaction.PlayerInventory>();
            Check(inventory.Receive(new HuntHappens.Interaction.InventoryEntry { kind = HuntHappens.Interaction.InventoryKind.Latte }), "latte received");
            var cage=Instantiate(hunt.cagePrefab,home,Quaternion.identity);cage.Owner=hunt;Check(cage.Interact(hunt),"bait accepted");
            float until=Time.time+20;while(!rat.Caught&&Time.time<until)yield return null;
            Check(rat.Caught && cage.Captive==rat && cage.Prompt.Contains("$25"),"natural bait capture");
            hunt.transform.position=cage.transform.position+Vector3.right*0.6f;int hp=health.Current;
            yield return new WaitForSeconds(2);Check(health.Current==hp,"captured rat harmless");
            var camera=hunt.GetComponentInChildren<Camera>();camera.transform.position=home+new Vector3(1.3f,0.9f,1.4f);camera.transform.LookAt(home+Vector3.up*0.2f);
            ShopVisualChecks.Capture(camera,null,"Logs/Large-rat-caught.png");
            Check(cage.Interact(hunt),"collect");hunt.Reload();Check(hunt.Rats==1&&hunt.LargeRats==1&&hunt.SaleTotal==25,"large species saved");
            var reception=FindAnyObjectByType<HuntHappens.Characters.SemenychDialogue>();
            camera.transform.localPosition=new Vector3(0,2.04f,0.45f);
            hunt.transform.position=reception.transform.position-reception.transform.forward*1.9f-Vector3.up*0.45f;
            camera.transform.LookAt(reception.transform.position);Physics.SyncTransforms();
            int money=hunt.shop.Wallet.Balance;Check(SemenychChecks.SellAtNpc(hunt),"sell through Semenych");Check(hunt.shop.Wallet.Balance==money+25,"paid exactly $25");
            hunt.Reload();Check(hunt.Rats==0&&hunt.LargeRats==0,"sale cleared saved inventory");
            yield return new WaitForSeconds(0.5f);Check(!SemenychChecks.SellAtNpc(hunt),"no duplicate sale");
            until=Time.time+22;while(rat.Caught&&Time.time<until)yield return null;
            Check(!rat.Caught&&rat.visual.gameObject.activeSelf&&rat.SalePrice==25,"large rat respawn");
            Check(health.TakeDamage(1000)&&health.Current==100&&Vector3.Distance(hunt.transform.position,playerSpawn)<0.1f,"player recovery at start");
            Check(!health.TakeDamage(5),"respawn protection");
            var legacy=JsonUtility.FromJson<ShopProgress.Data>("{\"version\":1,\"rats\":2,\"greyRats\":1,\"levels\":[0,0,0,0]}");Check(legacy.largeRats==0,"old save compatibility");
        }
    }
}
