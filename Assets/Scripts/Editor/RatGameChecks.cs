using System;
using System.Collections;
using HuntHappens.SmallGame;
using HuntHappens.Player;
using HuntHappens.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class RatGameChecks
    {
        static RatGameChecks() { EditorApplication.playModeStateChanged += state => {
            if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool("RatCheck",false))
            { SessionState.SetBool("RatCheck",false); new GameObject("Rat Check Runner").AddComponent<RatCheckRunner>(); }
        }; }
        public static void RunBatch() { EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");SessionState.SetBool("RatCheck",true);EditorApplication.EnterPlaymode(); }
    }
    public sealed class RatCheckRunner : MonoBehaviour
    {
        private void Start() { StartCoroutine(SafeRun()); }
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
            Debug.Log("Rat game checks PASSED: placement, bait consumption, natural approach/capture, cage reuse, sale once, prices, ammunition and save reload.");EditorApplication.Exit(0);
        }
        private static void Check(bool condition,string message) { if(!condition)throw new Exception("Rat check: "+message); }
        private IEnumerator Run()
        {
            var legacy = JsonUtility.FromJson<ShopProgress.Data>("{\"version\":1,\"balance\":25,\"levels\":[0,0,0,0]}");
            Check(legacy.cages == 1 && legacy.bread == 5 && legacy.darts == 6, "old saves receive starter supplies");
            yield return new WaitForSeconds(1);
            var hunt=FindAnyObjectByType<RatHunting>();var input=hunt.GetComponent<PrototypeInput>();var camera=hunt.GetComponentInChildren<Camera>();
            hunt.GetComponent<FirstPersonMotor>().enabled=false;
            Check(hunt.Cages==1&&hunt.Bread==5&&hunt.Rats==0,"starter inventory");
            var rat=FindAnyObjectByType<RatAI>();
            Vector3 point=rat.transform.position; point.y=0;
            var controller=hunt.GetComponent<CharacterController>();controller.enabled=false;
            hunt.transform.position=point+new Vector3(0,0,-2);camera.transform.LookAt(point);
            Cursor.lockState=CursorLockMode.Locked;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Digit4));InputSystem.Update();input.ReadFrame();
            var action=(InputAction)typeof(PrototypeInput).GetField("selectCage",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(input);
            Check(action.WasPressedThisFrame(),"key 4 action fires");
            hunt.SelectCageTool();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();input.ReadFrame();
            Check(hunt.Selected,"key 4 selects cage");
            Physics.SyncTransforms();
            var cage=hunt.TryPlace();Check(cage!=null,"valid cage placement");Check(hunt.Available==0,"inventory deducted");Check(hunt.TryPlace()==null,"cannot duplicate cage");
            Check(cage.Interact(hunt)&&cage.Baited&&hunt.Bread==4,"bait consumes exactly one bread");
            hunt.transform.position=point+new Vector3(0,0,-10);
            var agent=rat.GetComponent<NavMeshAgent>();
            if(NavMesh.SamplePosition(point+Vector3.right*2,out var spawn,2,NavMesh.AllAreas))agent.Warp(spawn.position);
            float deadline=Time.time+25;
            bool legsMoved=false, tailMoved=false;
            while(cage.Captive==null&&Time.time<deadline)
            {
                foreach(var animation in FindObjectsByType<RatAnimation>(FindObjectsSortMode.None))
                {
                    foreach(var leg in animation.legs) legsMoved |= Quaternion.Angle(leg.localRotation,Quaternion.identity)>3;
                    foreach(var segment in animation.tail) tailMoved |= Quaternion.Angle(segment.localRotation,Quaternion.identity)>1;
                }
                yield return new WaitForSeconds(0.1f);
            }
            Check(legsMoved && tailMoved, "legs and tail animate during real navigation");
            Check(cage.Captive!=null,"rat follows bait naturally");
            yield return new WaitForSeconds(0.8f);
            var capturedAnimation=cage.Captive.GetComponent<RatAnimation>();
            Check(capturedAnimation!=null && capturedAnimation.legs.Length==4 && capturedAnimation.tail.Length==3,"mouse skeleton present");
            foreach(var leg in capturedAnimation.legs)Check(Quaternion.Angle(leg.localRotation,Quaternion.identity)<0.2f,"legs settle in cage");
            camera.transform.position=point+new Vector3(1.5f,1,1.5f);camera.transform.LookAt(point+Vector3.up*0.2f);
            ShopVisualChecks.Capture(camera, null, "Logs/Rat-caught.png");yield return new WaitForSeconds(0.5f);
            camera.transform.localPosition=new Vector3(0,2.04f,0.45f);
            Check(cage.Interact(hunt)&&hunt.Rats==1&&hunt.Available==1,"collect rat and reusable cage");Check(!cage.Interact(hunt),"cannot collect twice");
            var shop=hunt.shop;Vector3 kiosk=shop.Kiosk.InteractionPoint.position;
            hunt.transform.position=kiosk-shop.Kiosk.transform.forward*1.6f-Vector3.up*1.7f;camera.transform.LookAt(kiosk);Physics.SyncTransforms();
            Check(shop.TryOpen(),"open actual shop");
            shop.GetComponent<ShopUI>().SendMessage("LateUpdate");
            ShopVisualChecks.Capture(camera, shop.GetComponentInChildren<Canvas>(), "Logs/Rat-shop.png");yield return new WaitForSeconds(1);
            ShopVisualChecks.Capture(camera, shop.GetComponentInChildren<Canvas>(), "Logs/Rat-shop.png");
            int sale=hunt.SaleTotal;
            Check(hunt.Sell()&&shop.Wallet.Balance==sale,"sale reward");Check(!hunt.Sell()&&shop.Wallet.Balance==sale,"no repeat reward");
            shop.Wallet.RestoreBalance(10);
            Check(!hunt.Buy(2)&&shop.Wallet.Balance==10,"cannot buy dart without funds");
            Check(hunt.Buy(1)&&shop.Wallet.Balance==8&&hunt.Bread==5,"bread price");
            shop.Wallet.RestoreBalance(40);Check(hunt.Buy(0)&&shop.Wallet.Balance==25&&hunt.Cages==2,"cage price");
            int darts=hunt.Darts;Check(hunt.Buy(2)&&hunt.Darts==darts+1&&shop.Wallet.Balance==13,"dart price");
            Check(hunt.UseDart()&&hunt.Darts==darts,"dart consumption");
            shop.SaveProgress();hunt.Reload();Check(hunt.Cages==2&&hunt.Bread==5&&hunt.Darts==darts&&hunt.Rats==0,"supplies preserved by wallet save");
            var basket=FindAnyObjectByType<BreadBasket>();Check(basket.Take(hunt)&&!basket.Take(hunt),"free bread cooldown");
            shop.Close(); yield return new WaitForSeconds(0.1f);
            var gun = hunt.GetComponent<HuntHappens.Weapons.TranquilizerGun>();gun.SelectTool(true);
            int beforeShot = hunt.Darts;
            Check(gun.TryFire() && hunt.Darts == beforeShot - 1, "real shot consumes one dart");
            while (hunt.UseDart()) { }
            yield return new WaitForSeconds(0.8f);
            Check(!gun.TryFire(), "empty gun cannot fire");
            InputSystem.RemoveDevice(keyboard);
        }
    }
}
