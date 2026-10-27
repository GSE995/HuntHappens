using System;
using System.IO;
using System.Reflection;
using HuntHappens.Delivery;
using HuntHappens.Interaction;
using HuntHappens.UI;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class DeerRewardEffectChecks
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        public static string Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run in Play Mode");
            var hud = UnityEngine.Object.FindAnyObjectByType<InteractionHUD>();
            var canvas = hud.GetComponentInChildren<Canvas>();
            var camera = hud.GetComponentInChildren<Camera>();
            if (camera == null) camera = Camera.main;
            var root = new GameObject("Reward checks", typeof(RectTransform));
            var rect = root.GetComponent<RectTransform>(); rect.SetParent(canvas.transform, false);
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            var walletObject = new GameObject("Test Wallet", typeof(RectTransform));
            var target = walletObject.GetComponent<RectTransform>(); target.SetParent(rect, false);
            target.anchorMin = target.anchorMax = Vector2.one; target.anchoredPosition = new Vector2(-115,-65);
            var source = root.AddComponent<DeliveryManager>();
            var effect = root.AddComponent<DeerRewardEffect>();
            float oldScale = Time.timeScale;
            bool oldEnabled = canvas.enabled;
            var oldMode = canvas.renderMode; var oldCamera = canvas.worldCamera; float oldDistance = canvas.planeDistance;
            try
            {
                Time.timeScale = 0; canvas.enabled = true;
                effect.Build(rect, target, hud.ThemeFont, source);
                var reward = rect.Find("Deer Reward").GetComponent<RectTransform>();
                source.RestoreBalance(1250);
                Check((int)Get(effect,"pending") == 0, "balance restore must not celebrate");
                var publish = (Action<int>)typeof(DeliveryManager).GetField("DeerRewarded",Private).GetValue(source);
                CheckDelivery(source, root.transform); publish(125);
                Invoke(effect,"LateUpdate");
                Check(reward.gameObject.activeSelf && reward.Find("Reward Amount").GetComponent<TMP_Text>().text == "+$225", "merge same-frame rewards");
                Check(reward.Find("Reward Caption").GetComponent<TMP_Text>().text.Contains("×2"), "merged count");
                float before = (float)Get(effect,"elapsed"); Invoke(effect,"LateUpdate");
                Check((float)Get(effect,"elapsed") == before, "pause freezes reward timer");
                publish(75); Check((int)Get(effect,"pending") == 75, "delivery during flight queued");
                canvas.enabled = true;
                Check(reward.GetComponentInChildren<AntlerRewardGraphic>().GetComponent<CanvasRenderer>() != null, "antler renderer present");
                Invoke(effect,"Render",0.22f);
                Directory.CreateDirectory("Logs");
                Capture(camera,canvas,"Logs/DeerReward-hold.png",1920,1080);
                Invoke(effect,"Render",0.76f);
                Check(reward.anchoredPosition.x > 0 && reward.localScale.x < 1, "flight moves and shrinks toward wallet");
                Capture(camera,canvas,"Logs/DeerReward-flight.png",1280,720);
                Invoke(effect,"Render",0.95f);
                Check(Vector2.Distance(reward.anchoredPosition,rect.InverseTransformPoint(target.position)) < 0.1f, "arrival uses actual wallet coordinates");
                typeof(DeerRewardEffect).GetField("elapsed",Private).SetValue(effect,0.95f);
                Invoke(effect,"LateUpdate"); Invoke(effect,"LateUpdate");
                Check(reward.Find("Reward Amount").GetComponent<TMP_Text>().text == "+$75", "queued reward starts after prior reward");
                effect.enabled = false; publish(100);
                Check(!reward.gameObject.activeSelf && target.localScale == Vector3.one && (int)Get(effect,"pending") == 0, "disable clears reward and pulse");
                const string result = "PASSED: real delivery event and duplicate protection; restore ignored; same-frame sum/count; pause; queued delivery; curved flight/shrink; actual wallet destination; next reward; disable cleanup; renders at 1080p and 720p. No player balance or saved progress changed.";
                File.WriteAllText("Logs/DeerRewardChecks.txt", result); return result;
            }
            finally
            {
                Time.timeScale = oldScale; canvas.enabled = oldEnabled;
                canvas.renderMode = oldMode; canvas.worldCamera = oldCamera; canvas.planeDistance = oldDistance;
                UnityEngine.Object.DestroyImmediate(root);
            }
        }
        static void CheckDelivery(DeliveryManager source, Transform cleanup)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Animals/Deer.prefab");
            var original = UnityEngine.Object.FindAnyObjectByType<HuntHappens.Animals.DeerSedation>();
            var animal = UnityEngine.Object.Instantiate(prefab, original.transform.position, Quaternion.identity, cleanup);
            var deer = animal.GetComponent<HuntHappens.Animals.DeerSedation>();
            typeof(HuntHappens.Animals.DeerSedation).GetField("currentState",Private).SetValue(deer,HuntHappens.Animals.DeerSedation.SedationState.Unconscious);
            var player = UnityEngine.Object.FindAnyObjectByType<ItemHolder>();
            typeof(PickupItem).GetField("<LastHolder>k__BackingField",Private).SetValue(animal.GetComponent<PickupItem>(),player);
            var zoneObject = new GameObject("Test reception"); zoneObject.transform.SetParent(cleanup,false);
            zoneObject.transform.position = deer.transform.position;
            var volume = zoneObject.AddComponent<BoxCollider>(); volume.size = Vector3.one * 20;
            var zone = zoneObject.AddComponent<DeliveryZone>(); zone.ConfigureReception(player,source,null);
            source.SetReward(100);
            Check(source.TryDeliver(deer,zone) && source.Balance == 1350, "real delivery publishes exact payment");
            Check(!source.TryDeliver(deer,zone) && source.Balance == 1350, "duplicate delivery rejected");
            Check((int)Get(cleanup.GetComponent<DeerRewardEffect>(),"pending") == 100, "real delivery queues reward only once");
        }
        static object Get(object instance,string field) => instance.GetType().GetField(field,Private).GetValue(instance);
        static void Invoke(object instance,string method,params object[] args) => instance.GetType().GetMethod(method,Private).Invoke(instance,args);
        static void Check(bool value,string message) { if (!value) throw new Exception("Deer reward: " + message); }
        static void Capture(Camera camera,Canvas canvas,string path,int width,int height)
        {
            var method = typeof(FieldHUDCheckRunner).GetMethod("Capture",BindingFlags.Static|BindingFlags.NonPublic);
            method.Invoke(null,new object[]{camera,canvas,path,width,height});
        }
    }
}
