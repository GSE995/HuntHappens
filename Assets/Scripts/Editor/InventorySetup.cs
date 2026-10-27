using HuntHappens.Interaction;
using HuntHappens.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Editor
{
    public static class InventorySetup
    {
        public static void Apply()
        {
            if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before setup.");
            var player = Object.FindAnyObjectByType<PlayerInteraction>();
            var shop = Object.FindAnyObjectByType<ShopManager>();
            if (player == null || shop == null) throw new System.InvalidOperationException("Open the gameplay scene.");
            if (player.GetComponent<PlayerInventory>() == null) Undo.AddComponent<PlayerInventory>(player.gameObject);
            BaitVisualSetup.Configure(player.GetComponent<PlayerInventory>());
            var products = Object.FindObjectsByType<WorldShopItem>(FindObjectsSortMode.None);
            var template = System.Array.Find(products, p => p.Kind == 0);
            if (template == null) throw new System.InvalidOperationException("Shop shelf not found.");
            for (int kind = 4; kind <= 5; kind++)
            {
                if (System.Array.Exists(products, p => p.Kind == kind)) continue;
                var ground = player.GetComponent<HuntHappens.SmallGame.RatHunting>().ground;
                Vector3 position = default; bool found = false;
                for (float depth = 4.3f; depth < 12 && !found; depth += 1.3f)
                {
                    var desired = shop.Kiosk.transform.TransformPoint(new Vector3(kind == 4 ? -2.4f : 2.4f, 0, -depth));
                    if (!ground.Raycast(new Ray(desired + Vector3.up * 6, Vector3.down), out var floor, 12)) continue;
                    if (!NavMesh.SamplePosition(floor.point - shop.Kiosk.transform.forward, out _, 0.8f, NavMesh.AllAreas)) continue;
                    bool blocked = false;
                    foreach (var collider in Physics.OverlapBox(floor.point + Vector3.up * 0.6f, new Vector3(0.55f,0.55f,0.55f), shop.Kiosk.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                        if (collider != ground) blocked = true;
                    if (!blocked) { position = floor.point + Vector3.up * 0.45f; found = true; }
                }
                if (!found) throw new System.InvalidOperationException("No clear ground for product " + kind);
                var item = Object.Instantiate(template, template.transform.parent);
                Undo.RegisterCreatedObjectUndo(item.gameObject, "Add inventory shop item");
                item.Configure(shop, kind); item.name = kind == 4 ? "ТРАНКВИЛИЗАТОР" : "ЛОВУШКИ";
                item.transform.SetPositionAndRotation(position, shop.Kiosk.transform.rotation);
                var label = item.GetComponentInChildren<TextMeshPro>();
                if (label != null) { label.text = item.name + "\n$" + item.Price; label.fontSize = kind == 4 ? 0.8f : 1.15f; }
                Physics.SyncTransforms();
            }
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene); AssetDatabase.SaveAssets();
        }
    }
}
