using System;
using System.IO;
using System.Reflection;
using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.SmallGame;
using HuntHappens.UI;
using TMPro;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class RatRewardEffectChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode");
            var hud=UnityEngine.Object.FindAnyObjectByType<InteractionHUD>();
            var canvas=hud.GetComponentInChildren<Canvas>();
            var root=new GameObject("Rat reward checks",typeof(RectTransform));
            var rect=root.GetComponent<RectTransform>();rect.SetParent(canvas.transform,false);
            rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;
            var targetObject=new GameObject("Target",typeof(RectTransform));
            var target=targetObject.GetComponent<RectTransform>();target.SetParent(rect,false);
            target.anchorMin=target.anchorMax=Vector2.one;target.anchoredPosition=new Vector2(-115,-65);
            var source=root.AddComponent<DeliveryManager>();var hunting=root.AddComponent<RatHunting>();
            var effect=root.AddComponent<DeerRewardEffect>();
            bool enabled=canvas.enabled;float scale=Time.timeScale;
            var mode=canvas.renderMode;var camera=canvas.worldCamera;float distance=canvas.planeDistance;
            try
            {
                Time.timeScale=0;canvas.enabled=false;
                effect.Build(rect,target,hud.ThemeFont,source,hunting);
                var publish=(Action<int,int>)typeof(RatHunting).GetField("RatsSold",Private).GetValue(hunting);
                publish(50,3);publish(25,1);Invoke(effect,"LateUpdate");
                var reward=rect.Find("Deer Reward").GetComponent<RectTransform>();
                Check(!reward.gameObject.activeSelf,"hidden dialogue HUD defers reward");
                canvas.enabled=true;Invoke(effect,"LateUpdate");
                Check(reward.Find("Reward Amount").GetComponent<TMP_Text>().text=="+$75","sale sum");
                Check(reward.Find("Reward Caption").GetComponent<TMP_Text>().text=="УЛОВ ПРОДАН ×4","rat count rather than transaction count");
                var ears=reward.GetComponentInChildren<RatRewardGraphic>();
                Check(ears!=null&&!reward.Find("Antler Flash").gameObject.activeSelf,"ears replace antlers");
                Invoke(effect,"Render",0.08f);float wobble=ears.Wobble;
                Invoke(effect,"Render",0.22f);Check(Mathf.Abs(wobble-ears.Wobble)>0.1f,"ears sway");
                Directory.CreateDirectory("Logs");
                Capture(hud,canvas,"Logs/RatReward-hold.png",1920,1080);
                float elapsed=(float)Get(effect,"elapsed");Invoke(effect,"LateUpdate");Check((float)Get(effect,"elapsed")==elapsed,"pause freezes timer");
                Invoke(effect,"Render",0.76f);Check(reward.anchoredPosition.x>0&&ears.Intensity==0,"only sum flies to wallet");
                Capture(hud,canvas,"Logs/RatReward-flight.png",1280,720);
                publish(10,1);
                var deer=(Action<int>)typeof(DeliveryManager).GetField("DeerRewarded",Private).GetValue(source);deer(100);
                Set(effect,"elapsed",0.95f);Invoke(effect,"LateUpdate");Invoke(effect,"LateUpdate");
                Check(reward.Find("Antler Flash").gameObject.activeSelf&&!ears.gameObject.activeSelf,"queued deer uses antlers");
                Set(effect,"elapsed",0.95f);Invoke(effect,"LateUpdate");Invoke(effect,"LateUpdate");
                Check(ears.gameObject.activeSelf&&reward.Find("Reward Amount").GetComponent<TMP_Text>().text=="+$10","queued rat sale follows deer without overlap");
                effect.enabled=false;publish(10,1);Check((int)Get(effect,"pendingRats")==0&&!reward.gameObject.activeSelf,"disable clears and ignores rewards");
                const string result="PASSED: hidden HUD deferral, rat sale sum/count, ears without antlers, ear sway, pause, money-only flight, shared deer/rat queue, disable cleanup, 1080p/720p renders. No player progress changed.";
                File.WriteAllText("Logs/RatRewardChecks.txt",result);return result;
            }
            finally
            {
                Time.timeScale=scale;canvas.enabled=enabled;canvas.renderMode=mode;canvas.worldCamera=camera;canvas.planeDistance=distance;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        public static string Transactions()
        {
            var hunt=UnityEngine.Object.FindAnyObjectByType<RatHunting>();int events=0,total=0;bool valid=true;
            Action<int,int> callback=(sum,count)=>{events++;total+=count;valid &=sum>0&&count>0&&hunt.Rats==0&&HuntHappens.Shop.ShopProgress.Load().balance==hunt.shop.Wallet.Balance;};
            hunt.RatsSold+=callback;
            try
            {
                string result=SemenychChecks.Run();
                Check(events>0&&total>=events&&valid,"successful sales publish paid amount/count after save");
                string report=result+" Rat reward events: "+events+", rats: "+total+"; all emitted after persisted transactions.";
                File.WriteAllText("Logs/RatRewardTransactions.txt",report);return report;
            }
            finally {hunt.RatsSold-=callback;}
        }
        static object Get(object obj,string name)=>obj.GetType().GetField(name,Private).GetValue(obj);
        static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Private).SetValue(obj,value);
        static void Invoke(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Private).Invoke(obj,args);
        static void Check(bool value,string message){if(!value)throw new Exception("Rat reward: "+message);}
        static void Capture(InteractionHUD hud,Canvas canvas,string path,int width,int height)
        {
            var camera=hud.GetComponentInChildren<Camera>();if(camera==null)camera=Camera.main;
            typeof(FieldHUDCheckRunner).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{camera,canvas,path,width,height});
        }
    }
}
