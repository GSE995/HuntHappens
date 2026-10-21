using System;
using System.IO;
using HuntHappens.Player;
using HuntHappens.Shop;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.EventSystems;

namespace HuntHappens.Editor
{
    [InitializeOnLoad]
    public static class ShopVisualChecks
    {
        private const string Pending = "HuntHappens.ShopVisualChecks";
        private static double deadline;
        static ShopVisualChecks() => EditorApplication.playModeStateChanged += OnPlay;
        public static void RunBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
            SessionState.SetBool(Pending, true);
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            EditorApplication.EnterPlaymode();
        }
        private static void OnPlay(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.SetBool(Pending, false);
            deadline = EditorApplication.timeSinceStartup + 90;
            EditorApplication.update += Tick;
        }
        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Shop visual capture timed out");
                if (Time.timeSinceLevelLoad < 1) return;
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) throw new InvalidOperationException("Run without -nographics");
                ShopManager shop = UnityEngine.Object.FindAnyObjectByType<ShopManager>();
                var motor = UnityEngine.Object.FindAnyObjectByType<FirstPersonMotor>();
                motor.enabled = false;
                var controller = motor.GetComponent<CharacterController>();
                controller.enabled = false;
                Camera camera = motor.GetComponentInChildren<Camera>();
                motor.transform.position = shop.Kiosk.transform.position - shop.Kiosk.transform.forward * 8;
                camera.transform.position = motor.transform.position + Vector3.up * 2.3f;
                camera.transform.rotation = Quaternion.LookRotation(shop.Kiosk.transform.position + Vector3.up * 1.6f - camera.transform.position);
                Canvas hud = UnityEngine.Object.FindAnyObjectByType<HuntHappens.Interaction.InteractionHUD>().GetComponentInChildren<Canvas>();
                hud.enabled = false;
                Capture(camera, null, "Logs/Shop-Kiosk.png");
                motor.transform.position = shop.Kiosk.InteractionPoint.position - shop.Kiosk.transform.forward * 1.6f - Vector3.up * 1.2f;
                camera.transform.position = motor.transform.position + Vector3.up * 1.6f;
                Physics.SyncTransforms();
                if (!shop.TryOpen()) throw new InvalidOperationException("Cannot open shop for capture");
                shop.GetComponent<ShopUI>().SendMessage("LateUpdate");
                Canvas canvas = shop.GetComponentInChildren<Canvas>();
                Capture(camera, canvas, "Logs/Shop-UI.png");
                Debug.Log("Shop visual capture PASSED: Logs/Shop-Kiosk.png and Logs/Shop-UI.png rendered through URP. Inspect images for layout.");
                EditorApplication.update -= Tick;
                EditorApplication.Exit(0);
            }
            catch (Exception exception) { Debug.LogException(exception); EditorApplication.update -= Tick; EditorApplication.Exit(1); }
        }
        private static void Capture(Camera camera, Canvas canvas, string path)
        {
            var target = new RenderTexture(1920, 1080, 24);
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            RenderTexture oldTarget = camera.targetTexture;
            try
            {
                camera.targetTexture = target;
                if (canvas != null)
                {
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = camera;
                    canvas.planeDistance = 1;
                    canvas.GetComponent<UnityEngine.UI.CanvasScaler>().SendMessage("Update");
                    Canvas.ForceUpdateCanvases();
                }
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest { destination = target });
                if (canvas != null)
                {
                    var ui = canvas.GetComponentInParent<ShopUI>();
                    var button = ui.BuyButton(UpgradeKind.MoreTraps);
                    var pointer = new PointerEventData(EventSystem.current)
                    {
                        position = RectTransformUtility.WorldToScreenPoint(camera, button.transform.position),
                        button = PointerEventData.InputButton.Left
                    };
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointer, hits);
                    if (hits.Count == 0 || hits[0].gameObject != button.gameObject)
                        throw new InvalidOperationException("Rendered SHOP buy button blocked: depth=" + button.GetComponent<CanvasRenderer>().absoluteDepth +
                            ", screen=" + pointer.position + ", first=" + (hits.Count > 0 ? hits[0].gameObject.name : "none"));
                    ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
                    if (ui.GetComponent<ShopManager>().Message != "Недостаточно денег") throw new InvalidOperationException("UI click did not reach shop purchase handler");
                    Debug.Log("Rendered Shop UI raycast/click PASSED: visible buy button receives pointer click and reports insufficient funds.");
                }
                RenderTexture.active = target;
                texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = oldTarget;
                RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
