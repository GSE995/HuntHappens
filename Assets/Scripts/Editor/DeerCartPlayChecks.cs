using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HuntHappens.Animals;
using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Shop;
using HuntHappens.Transport;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class DeerCartPlayChecks
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static void Set(object target,string field,object value) => target.GetType().GetField(field,Private).SetValue(target,value);
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode.");
            bool isolated = ShopProgress.InventoryChecksRunning;
            string playerSave = PlayerPrefs.GetString(ShopProgress.PlayerKey, "");
            bool testExists = PlayerPrefs.HasKey(ShopProgress.TestKey);
            string testSave = PlayerPrefs.GetString(ShopProgress.TestKey, "");
            var cursor = Cursor.lockState; bool cursorVisible = Cursor.visible;
            var created = new List<GameObject>();
            string report = "";
            void Check(bool value,string name) { if (!value) throw new Exception("Cart check failed: "+name); report+="PASS "+name+"\n"; }
            GameObject Create(string name) { var go=new GameObject(name); created.Add(go);return go; }
            Vector3 origin = new Vector3(0,0,200);
            DeerSedation Animal(string name,Vector3 position)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Animals/"+name+".prefab");
                Vector3 spawn = Object.FindObjectsByType<DeerSedation>(FindObjectsSortMode.None).First(d=>d.GetComponent<NavMeshAgent>().enabled && d.GetComponent<NavMeshAgent>().isOnNavMesh).transform.position;
                var go=Object.Instantiate(prefab,spawn,Quaternion.identity); created.Add(go);
                go.GetComponent<DeerAI>().enabled=false;go.GetComponent<NavMeshAgent>().enabled=false;
                if(go.GetComponent<DeerLeader>()!=null)go.GetComponent<DeerLeader>().enabled=false;
                go.transform.position=position;
                return go.GetComponent<DeerSedation>();
            }
            void Sleep(DeerSedation deer)
            { deer.GetComponent<DeerPhysics>().FallAsleep(); Set(deer,"currentState",DeerSedation.SedationState.Unconscious);deer.GetComponent<DeerAI>().SetSedationState(DeerAI.DeerState.Unconscious);Physics.SyncTransforms(); }
            try
            {
                ShopProgress.InventoryChecksRunning=true;ShopProgress.Reset();
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);created.Add(floor);
                floor.name="Cart check ground";floor.transform.position=origin+Vector3.down*.5f;floor.transform.localScale=new Vector3(60,1,60);
                var player=Create("Cart check player");player.transform.position=origin+new Vector3(0,.03f,-2.8f);
                var cc=player.AddComponent<CharacterController>();cc.height=2;cc.center=Vector3.up;cc.radius=.3f;
                var input=player.AddComponent<PrototypeInput>();input.enabled=false;
                var holder=player.AddComponent<ItemHolder>();
                var view=Create("Cart check camera").transform;view.SetParent(player.transform,false);view.localPosition=Vector3.up*1.8f;holder.SetView(view);
                var inventory=player.AddComponent<PlayerInventory>();inventory.Reload();
                var driver=player.AddComponent<CartDriver>();
                var motor=player.AddComponent<FirstPersonMotor>();motor.enabled=false;
                var cart=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(DeerCartSetup.PrefabPath),origin+Vector3.up*.03f,Quaternion.identity).GetComponent<DeerCart>();created.Add(cart.gameObject);
                var deer=Animal("Deer",origin+new Vector3(3,.05f,0));
                Physics.SyncTransforms(); Cursor.lockState=CursorLockMode.Locked;
                Check(!cart.TryLoad(deer,holder),"awake animal cannot be loaded");
                for(int i=0;i<PlayerInventory.Capacity;i++) { inventory.Receive(new InventoryEntry{kind=InventoryKind.Trap});inventory.SelectSlot(i); }
                inventory.Receive(new InventoryEntry{kind=InventoryKind.Bread});
                Check(!driver.TryTake(cart) && inventory.IsEquipped(InventoryKind.Bread),"full hands reject handles without losing inventory");
                ShopProgress.Reset();inventory.Reload();
                Sleep(deer);
                holder.SetCarryUpgrade(2);
                Check(holder.MaximumMass==40 && deer.GetComponent<Rigidbody>().mass==120 && !holder.TryPickUp(deer.GetComponent<PickupItem>()),"120 kg deer exceeds maximum upgraded hand capacity");
                player.transform.position=origin+new Vector3(2,.05f,-2);Physics.SyncTransforms();
                var loadingWall=GameObject.CreatePrimitive(PrimitiveType.Cube);created.Add(loadingWall);
                loadingWall.transform.position=origin+new Vector3(1.8f,1,0);loadingWall.transform.localScale=new Vector3(.2f,2,5);Physics.SyncTransforms();
                Check(!cart.TryLoad(deer,holder),"cannot load through a solid wall");
                Object.DestroyImmediate(loadingWall);
                Check(cart.TryLoad(deer,holder),"sleeping deer loads from beside cart");
                Check(deer.Cart==cart && deer.DeliveryReserved && deer.GetComponent<Rigidbody>().isKinematic,"cargo secured and recovery reserved");
                Check(!deer.GetComponent<DeerPhysics>().TryBeginRecovery(),"secured cargo cannot wake in cart");
                Set(deer,"elapsed",1000f);typeof(DeerSedation).GetMethod("Update",Private).Invoke(deer,null);
                Check(deer.State==DeerSedation.SedationState.Unconscious,"sleep timer cannot wake secured animal on a long trip");
                var second=Animal("BossDeer",origin+new Vector3(3,.05f,0));Sleep(second);
                Check(!holder.TryPickUp(second.GetComponent<PickupItem>()) && second.GetComponent<Rigidbody>().mass==240,"leader also too heavy");
                Check(!cart.TryLoad(second,holder),"one-animal capacity prevents second load");
                second.transform.position=origin+Vector3.right*12;Physics.SyncTransforms();
                player.transform.position=cart.HandlePosition;Physics.SyncTransforms();
                Check(driver.TryTake(cart),"take handles with empty hands");
                Check(!inventory.Receive(new InventoryEntry{kind=InventoryKind.Gun}) && !inventory.SelectSlot(0),"hands occupied while pushing");
                Set(input,"<SprintHeld>k__BackingField",true);
                Check(Mathf.Approximately(motor.CurrentMoveSpeed,2.5f),"loaded speed ignores sprint");
                Vector3 start=cart.transform.position;Vector3 local=deer.transform.localPosition;
                for(int i=0;i<12;i++)driver.Move(new Vector3(0,-.01f,.1f));
                Check(cart.transform.position.z-start.z>.9f,"cart and player move together");
                Check((deer.transform.localPosition-local).magnitude<.001f,"cargo remains secured during travel");
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);created.Add(wall);
                wall.name="Cart check wall";wall.transform.position=cart.transform.position+new Vector3(0,1,2.2f);wall.transform.localScale=new Vector3(8,2,.4f);Physics.SyncTransforms();
                start=cart.transform.position;
                for(int i=0;i<30;i++)driver.Move(new Vector3(0,-.01f,.1f));
                Check(cart.transform.position.z-start.z<.35f,"solid obstacle stops cart and cargo");
                Check(Vector3.ProjectOnPlane(player.transform.position-cart.HandlePosition,Vector3.up).magnitude<.2f,"player cannot walk through stopped cart");
                Object.DestroyImmediate(wall);
                player.transform.rotation=Quaternion.Euler(0,45,0);
                for(int i=0;i<20;i++)driver.Move(new Vector3(0,-.01f,0));
                Check(Quaternion.Angle(Quaternion.identity,cart.transform.rotation)>1,"cart steers with player heading");
                motor.ApplyImpulse(Vector3.right*3);
                Check(!driver.IsDriving && cart.Cargo==deer,"knockback releases handles without dropping secured cargo");
                Check(cart.Driver==null && !driver.IsDriving,"release leaves parked cart");
                var blockedUnload=GameObject.CreatePrimitive(PrimitiveType.Cube);created.Add(blockedUnload);
                blockedUnload.transform.position=cart.transform.position+Vector3.up;blockedUnload.transform.localScale=new Vector3(15,2,15);Physics.SyncTransforms();
                Check(!cart.TryUnload(holder) && cart.Cargo==deer,"blocked unloading preserves cargo");
                Object.DestroyImmediate(blockedUnload);
                Check(cart.TryUnload(holder),"unload onto clear supported ground");
                Check(deer.Cart==null && !deer.DeliveryReserved && !deer.GetComponent<Rigidbody>().isKinematic && deer.GetComponent<BoxCollider>().enabled,"unload restores physics and recovery");
                deer.transform.position=origin+Vector3.left*12;
                second.transform.position=cart.transform.position+Vector3.right*3;player.transform.position=cart.transform.position+new Vector3(2,0,-2);Physics.SyncTransforms();
                Check(cart.TryLoad(second,holder),"240 kg leader fits cart capacity");
                var wallet=Create("Cart check wallet").AddComponent<DeliveryManager>();wallet.RestoreBalance(0);
                var reception=Create("Cart check reception");reception.transform.position=cart.transform.position;
                var volume=reception.AddComponent<BoxCollider>();volume.center=Vector3.up*2;volume.size=new Vector3(8,5,6);
                var zone=reception.AddComponent<DeliveryZone>();zone.ConfigureReception(holder,wallet,null);
                Check(!wallet.TryDeliver(second,zone),"reception waits until cart has stopped");
                Set(cart,"<LastMovedAt>k__BackingField",Time.time-3);
                Check(wallet.TryDeliver(second,zone) && wallet.Balance==wallet.RewardPerDeer && wallet.DeliveredCount==1,"reception accepts loaded cart and pays once");
                Check(cart.Cargo==null && !wallet.TryDeliver(second,zone) && wallet.DeliveredCount==1,"delivery clears cart and duplicate reward is blocked");
                // Trap captures use the same loading action and leave a reusable trap.
                deer.transform.position=cart.transform.position+Vector3.right*3;
                Set(deer,"currentState",DeerSedation.SedationState.Awake);
                var trap=Create("Cart check trap").AddComponent<HuntHappens.Traps.AnimalTrap>();
                Check(deer.TryTrap(trap),"trap capture available");Physics.SyncTransforms();
                Check(cart.TryLoad(deer,holder) && deer.Trap==null && deer.State==DeerSedation.SedationState.Unconscious,"load releases trapped deer without hand pickup");
                cart.enabled=false;
                Check(deer.Cart==null && !deer.DeliveryReserved && !deer.GetComponent<Rigidbody>().isKinematic,"disabling cart releases cargo safely");
                Check(PlayerPrefs.GetString(ShopProgress.PlayerKey,"")==playerSave,"player save untouched");
                report += "PASS all cart checks";
                System.IO.Directory.CreateDirectory("Logs");System.IO.File.WriteAllText("Logs/DeerCartPlayChecks.txt",report);
                return report;
            }
            finally
            {
                for(int i=created.Count-1;i>=0;i--)if(created[i]!=null)Object.DestroyImmediate(created[i]);
                if(testExists)PlayerPrefs.SetString(ShopProgress.TestKey,testSave);else PlayerPrefs.DeleteKey(ShopProgress.TestKey);
                ShopProgress.InventoryChecksRunning=isolated;Cursor.lockState=cursor;Cursor.visible=cursorVisible;
            }
        }
    }
}
