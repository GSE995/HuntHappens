using System;
using System.Collections;
using System.Linq;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.SmallGame;
using HuntHappens.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class FieldHUDChecks
    {
        static FieldHUDChecks()=>EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredPlayMode&&SessionState.GetBool("FieldHUDChecks",false)){SessionState.SetBool("FieldHUDChecks",false);new GameObject("HUD checks").AddComponent<FieldHUDCheckRunner>();}};
        public static void RunBatch(){if(!Application.isBatchMode)throw new Exception("Batch only");EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");SessionState.SetBool("FieldHUDChecks",true);EditorApplication.EnterPlaymode();}
    }
    public sealed class FieldHUDCheckRunner:MonoBehaviour
    {
        void Start()=>StartCoroutine(Safe());
        IEnumerator Safe(){var run=Run();while(true){object next;try{if(!run.MoveNext())break;next=run.Current;}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);yield break;}yield return next;}Debug.Log("Field HUD checks PASSED: Tab/Escape/button, input blocking, real inventory/catch value, health flash, 1080p/720p renders.");EditorApplication.Exit(0);}
        void Check(bool value,string why){if(!value)throw new Exception("Field HUD: "+why);}
        IEnumerator Run()
        {
            yield return new WaitForSeconds(1);
            var hunt=FindAnyObjectByType<RatHunting>();var input=hunt.GetComponent<PrototypeInput>();hunt.GetComponent<FirstPersonMotor>().enabled=false;
            var hud=FindAnyObjectByType<InteractionHUD>();var field=hud.Field;var canvas=hud.GetComponentInChildren<Canvas>();var camera=hunt.GetComponentInChildren<Camera>();
            Check(field!=null && !field.IsOpen,"inventory starts closed");
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            var keyboard=InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Tab));InputSystem.Update();input.ReadFrame();
            Check(field.IsOpen && input.GameplayBlocked,"Tab opens inventory and blocks game");
            Check(!input.ThrowPressed && !input.InteractPressed && input.Move==Vector2.zero,"no input leaks");
            ShopProgress.SaveSupplies(2,5,4,12,2,1);hunt.Reload();hunt.shop.Wallet.RestoreBalance(125);field.Refresh();
            Check(field.GetComponentsInChildren<TMP_Text>().Any(t=>t.name=="Total Value" && t.text.Contains("$65")),"real catch total");
            canvas.enabled=true;Capture(camera,canvas,"Logs/FieldHUD-inventory.png",1920,1080);
            yield return new WaitForSeconds(0.25f);canvas.enabled=true;Capture(camera,canvas,"Logs/FieldHUD-inventory.png",1920,1080);
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();input.ReadFrame();yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Escape));InputSystem.Update();input.ReadFrame();field.Refresh();Check(!field.IsOpen&&!input.GameplayBlocked,"Escape closes");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();input.ReadFrame();yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Tab));InputSystem.Update();input.ReadFrame();Check(field.IsOpen,"reopen");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());InputSystem.Update();input.ReadFrame();yield return null;yield return null;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.Tab));InputSystem.Update();input.ReadFrame();Check(!field.IsOpen&&!input.GameplayBlocked,"Tab closes");
            field.SetOpen(true);
            var button=field.GetComponentInChildren<UnityEngine.UI.Button>();
            canvas.enabled=true;Capture(camera,canvas,"Logs/FieldHUD-inventory-720.png",1280,720);
            var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(camera,button.transform.position)};
            var hits=new System.Collections.Generic.List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);
            Check(hits.Any(h=>h.gameObject==button.gameObject),"close button receives raycast");ExecuteEvents.Execute(button.gameObject,pointer,ExecuteEvents.pointerClickHandler);
            Check(!field.IsOpen&&!input.GameplayBlocked,"button closes");
            hunt.SelectCageTool();field.Refresh();canvas.enabled=true;
            Capture(camera,canvas,"Logs/FieldHUD-game.png",1920,1080);yield return new WaitForSeconds(0.25f);canvas.enabled=true;Capture(camera,canvas,"Logs/FieldHUD-game.png",1920,1080);
            var health=hunt.GetComponent<PlayerHealth>();Check(health.TakeDamage(5),"damage accepted");hud.SendMessage("LateUpdate");canvas.enabled=true;
            var flash=canvas.transform.Find("Damage Flash").GetComponent<UnityEngine.UI.Image>();Check(flash.color.a>0.2f,"damage flash");
            yield return new WaitForSeconds(0.75f);Check(flash.color.a==0,"flash fades");
            field.SetOpen(true);field.SetOpen(false);input.SetGameplayBlocked(true);field.SetOpen(true);Check(!field.IsOpen,"another menu owns blocking");input.SetGameplayBlocked(false);
        }
        static void Capture(Camera camera,Canvas canvas,string file,int width,int height)
        {
            var rt=new RenderTexture(width,height,24);rt.Create();var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;var old=camera.targetTexture;camera.targetTexture=rt;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=0.5f;
            canvas.GetComponent<UnityEngine.UI.CanvasScaler>().SendMessage("Update");Canvas.ForceUpdateCanvases();
            RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});
            RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();System.IO.File.WriteAllBytes(file,image.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=old;rt.Release();Destroy(rt);Destroy(image);
        }
    }
}
