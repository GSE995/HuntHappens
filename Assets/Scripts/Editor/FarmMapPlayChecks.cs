using System;
using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.Shop;
using HuntHappens.SmallGame;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class FarmMapPlayChecks
    {
        const string Key="FarmMapChecks";
        static int stage, balance, frames, notifications;
        static float since;
        static double deadline;
        static DeerSedation deer, second;
        static DeliveryManager wallet;
        static DeliveryZone zone;
        static ItemHolder player;
        static ShopManager shop;
        static Vector3[] route;
        static int corner;
        static Vector3[] initialRats;
        static RatAI[] rats;
        static DeerSedation[] wild;
        static Vector3[] initialDeer;
        static string report;
        static FarmMapPlayChecks()
        {
            if(SessionState.GetBool(Key,false)) ShopProgress.InventoryChecksRunning=true;
            EditorApplication.playModeStateChanged+=OnPlay;
        }
        public static void Begin()
        {
            if(EditorApplication.isPlaying)throw new Exception("Start in edit mode");
            SessionState.SetString(Key+"save",PlayerPrefs.GetString(ShopProgress.PlayerKey,""));
            SessionState.SetString(Key+"result","RUNNING");SessionState.SetBool(Key,true);
            ShopProgress.InventoryChecksRunning=true;ShopProgress.Reset();
            EditorApplication.EnterPlaymode();
        }
        public static string Result()=>SessionState.GetString(Key+"result","not run");
        public static void BeginRestore()
        {
            if(EditorApplication.isPlaying)throw new Exception("Start in edit mode");
            SessionState.SetBool(Key+"restore",true);SessionState.SetBool(Key,true);
            ShopProgress.InventoryChecksRunning=true;
            SessionState.SetInt(Key+"expectedBalance",ShopProgress.Load().balance);
            EditorApplication.EnterPlaymode();
        }
        static void OnPlay(PlayModeStateChange change)
        {
            if(change==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool(Key,false))
            {stage=SessionState.GetBool(Key+"restore",false)?7:0;since=Time.time;deadline=EditorApplication.timeSinceStartup+150;report=stage==7?SessionState.GetString(Key+"result","")+"\nRESTORE\n":"";EditorApplication.update+=Tick;}
            if(change==PlayModeStateChange.ExitingPlayMode)EditorApplication.update-=Tick;
            if(change==PlayModeStateChange.EnteredEditMode){ShopProgress.InventoryChecksRunning=false;}
        }
        static void Check(bool value,string message){if(!value)throw new Exception(message);report+="PASS "+message+"\n";}
        static void MovePlayer(Vector3 p)
        {var cc=player.GetComponent<CharacterController>();cc.enabled=false;player.transform.position=p;cc.enabled=true;Physics.SyncTransforms();}
        static void Tick()
        {
            if(!EditorApplication.isPlaying)return;
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Stage timeout "+stage);
                if(Time.time-since<.3f)return;
                switch(stage)
                {
                    case 0:
                        wallet=Object.FindAnyObjectByType<DeliveryManager>();zone=Object.FindObjectsByType<DeliveryZone>().First(z=>z.AutomaticReception);player=Object.FindAnyObjectByType<ItemHolder>();shop=Object.FindAnyObjectByType<ShopManager>();
                        Check(Vector3.Distance(player.transform.position,new Vector3(-10,0,-30))<1,"Player spawns in courtyard");
                        Check(FarmMapSetup.ValidateRoutes().Contains("Five"),"Five routes complete on baked NavMesh");
                        frames=Time.frameCount;rats=Object.FindObjectsByType<RatAI>();initialRats=rats.Select(r=>r.transform.position).ToArray();wild=Object.FindObjectsByType<DeerSedation>();initialDeer=wild.Select(d=>d.transform.position).ToArray();
                        var path=new NavMeshPath();
                        NavMesh.SamplePosition(player.transform.position,out var routeStart,1,NavMesh.AllAreas);
                        NavMesh.SamplePosition(new Vector3(4,0,-29),out var routeEnd,2,NavMesh.AllAreas);
                        Check(NavMesh.CalculatePath(routeStart.position,routeEnd.position,NavMesh.AllAreas,path)&&path.status==NavMeshPathStatus.PathComplete,"Shop walking path available");route=path.corners;corner=0;
                        player.GetComponent<HuntHappens.Player.FirstPersonMotor>().enabled=false;
                        stage++;since=Time.time;break;
                    case 1:
                        if(corner<route.Length)
                        {
                            var delta=route[corner]-player.transform.position;delta.y=0;
                            if(delta.magnitude<.25f)corner++;
                            else player.GetComponent<CharacterController>().Move(Vector3.ClampMagnitude(delta,.12f)+Vector3.down*.05f);
                            break;
                        }
                        if(Time.time-since<8)break;
                        Check(Time.frameCount>frames+10,"Play Mode advances frames");
                        Check(Vector3.Distance(player.transform.position,route[route.Length-1])<1,"CharacterController walks from start to shop: "+player.transform.position);
                        MovePlayer(new Vector3(5.3f,0,-29));
                        Check(shop.TryOpen(),"Existing shop opens at new counter");
                        wallet.RestoreBalance(1000);
                        Check(shop.TryBuy(UpgradeKind.BetterContracts),"Existing contract upgrade can be purchased");shop.Close();
                        Check(rats.Where((r,i)=>Vector3.Distance(r.transform.position,initialRats[i])>.2f).Any(),"Rats move around litter");
                        Check(wild.Where((d,i)=>Vector3.Distance(d.transform.position,initialDeer[i])>.2f).Any(),"Deer move in forest");
                        Check(rats.All(r=>r.transform.position.z>-2),"No rats in farm courtyard");
                        deer=wild.First(d=>d.gameObject.name!="Boss Deer");
                        MovePlayer(deer.transform.position+Vector3.back*2);
                        deer.SetDurations(.5f,120,1);Check(deer.ApplyDose(10),"Existing sedation accepts dart dose");
                        stage++;since=Time.time;break;
                    case 2:
                        if(deer.State!=DeerSedation.SedationState.Unconscious)break;
                        Check(player.TryPickUp(deer.GetComponent<PickupItem>()),"Player picks up sleeping deer through ItemHolder");
                        balance=wallet.Balance;wallet.BalanceChanged+=OnBalance;notifications=0;
                        MovePlayer(new Vector3(-12,.05f,-3));
                        deer.GetComponent<Rigidbody>().position=player.transform.position+Vector3.forward;
                        Physics.SyncTransforms();stage++;since=Time.time;break;
                    case 3:
                        Check(deer.IsDelivered,"Reception automatically accepts carried deer");
                        Check(wallet.Balance==balance+wallet.RewardPerDeer&&notifications==1,"One immediate payment at upgraded contract price");
                        Check(!player.IsHolding&&player.GetComponent<PlayerInventory>().Hand==null,"Carry and inventory state released");
                        Check(!deer.GetComponent<PickupItem>().CanPickUp(1000)&&!deer.ApplyDose(10),"Accepted deer cannot be caught again");
                        Check(deer.transform.IsChildOf(zone.Enclosure.transform),"Delivered deer is visible inside enclosure");
                        Check(!wallet.TryDeliver(deer,zone),"Repeat delivery cannot pay twice");
                        Check(ShopProgress.Load().balance==wallet.Balance&&ShopProgress.Load().acceptedDeer==1,"Wallet and accepted count saved in existing snapshot");
                        Check(Object.FindObjectsByType<TMPro.TextMeshProUGUI>().Any(t=>t.name=="Delivery Notification"&&t.text==wallet.Notification),"Delivery notice visible in HUD");
                        Check(Object.FindObjectsByType<TMPro.TextMeshProUGUI>().Any(t=>t.name=="Balance"&&t.text.Contains(wallet.Balance.ToString())),"HUD balance updated");
                        second=wild.First(d=>d!=deer&&d.name!="Boss Deer");
                        var a=second.GetComponent<NavMeshAgent>();a.Warp(new Vector3(-12,0,-3));
                        Check(!zone.IsReady(second)&&!wallet.TryDeliver(second,zone),"Wild deer inside reception rejected");
                        second.SetDurations(.5f,120,1);second.ApplyDose(10);
                        stage++;since=Time.time;break;
                    case 4:
                        if(second.State!=DeerSedation.SedationState.Unconscious)break;
                        Check(!zone.IsReady(second),"Unclaimed sleeping deer rejected");
                        Check(player.TryPickUp(second.GetComponent<PickupItem>()),"Player can pick up next deer");
                        player.Drop();second.GetComponent<Rigidbody>().position=new Vector3(-12,.2f,-3);
                        second.gameObject.AddComponent<SphereCollider>().radius=.2f;
                        balance=wallet.Balance;notifications=0;Physics.SyncTransforms();stage++;since=Time.time;break;
                    case 5:
                        Check(second.IsDelivered&&wallet.Balance==balance+wallet.RewardPerDeer&&notifications==1,"Released owned deer with multiple colliders pays exactly once");
                        Check(!zone.IsReady(null)&&!wallet.TryDeliver(null,zone),"Non-deer candidates rejected");
                        MovePlayer(new Vector3(-12,0,3));MovePlayer(new Vector3(-12,0,-3));
                        balance=wallet.Balance;stage++;since=Time.time;break;
                    case 6:
                        if(Time.time-since<8)break;
                        Check(wallet.Balance==balance,"Re-entering reception gives no duplicate reward");
                        Check(Object.FindAnyObjectByType<DeerSpawner>().PendingReplacements==0,"Wild replacements spawned successfully");
                        Check(Object.FindObjectsByType<DeerSedation>().Where(d=>d.name=="Deer Replacement").All(d=>d.transform.position.z>15),"Replacement deer spawn on forest clearings");
                        Check(PlayerPrefs.GetString(ShopProgress.PlayerKey,"")==SessionState.GetString(Key+"save",""),"Real player save unchanged by checks");
                        Finish("PASSED\n"+report);break;
                    case 7:
                        if(Time.time-since<3)break;
                        wallet=Object.FindAnyObjectByType<DeliveryManager>();
                        var enclosure=Object.FindAnyObjectByType<DeerEnclosure>();
                        Check(wallet.Balance==SessionState.GetInt(Key+"expectedBalance",-1),"Wallet restored after new Play session");
                        Check(enclosure.AcceptedCount==2&&enclosure.GetComponentsInChildren<DeerSedation>().Count(d=>d.IsDelivered)==2,"Two accepted deer restored inside pen");
                        Check(enclosure.GetComponentsInChildren<DeerSedation>().All(d=>!d.GetComponent<PickupItem>().CanPickUp(1000)&&!d.ApplyDose(10)),"Restored deer remain uncatchable");
                        Check(PlayerPrefs.GetString(ShopProgress.PlayerKey,"")==SessionState.GetString(Key+"save",""),"Real save unchanged after restore test");
                        SessionState.SetBool(Key+"restore",false);Finish(report);break;
                }
            }
            catch(Exception e){Finish("FAILED stage "+stage+": "+e.Message+"\n"+report);}
        }
        static void OnBalance()=>notifications++;
        static void Finish(string result)
        {
            if(wallet!=null)wallet.BalanceChanged-=OnBalance;
            SessionState.SetString(Key+"result",result);SessionState.SetBool(Key,false);EditorApplication.update-=Tick;
            System.IO.Directory.CreateDirectory("Logs");System.IO.File.WriteAllText("Logs/FarmMapPlayChecks.txt",result);
            Debug.Log(result);
            EditorApplication.ExitPlaymode();
        }
    }
}
