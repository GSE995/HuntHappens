using System.Linq;
using HuntHappens.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace HuntHappens.Editor
{
    public static class RatBaitSetup
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first");
            var bread = Object.FindObjectsByType<WorldShopItem>(FindObjectsSortMode.None).First(x => x.Kind == 1);
            var ground = GameObject.Find("Environment/Ground").GetComponent<Collider>();
            for (int kind = 7; kind <= 8; kind++)
            {
                if (Object.FindObjectsByType<WorldShopItem>(FindObjectsSortMode.None).Any(x => x.Kind == kind)) continue;
                Vector3 position = default;
                bool found = false;
                for (float depth = 1.5f; depth <= 9 && !found; depth += 1.5f)
                    for (int side = -1; side <= 1 && !found; side += 2)
                    {
                        var desired = bread.transform.position + bread.transform.forward * -depth + bread.transform.right * side * 1.5f;
                        if (!ground.Raycast(new Ray(desired + Vector3.up * 5, Vector3.down), out var floor, 10)) continue;
                        if (!NavMesh.SamplePosition(floor.point - bread.transform.forward, out _, .5f, NavMesh.AllAreas)) continue;
                        if (Physics.OverlapBox(floor.point + Vector3.up * .5f, Vector3.one * .6f, bread.transform.rotation, ~0, QueryTriggerInteraction.Ignore).Any(c => c != ground)) continue;
                        position = floor.point + Vector3.up * .45f; found = true;
                    }
                if (!found) throw new System.Exception("No free shop position for bait " + kind);
                var stock = Object.Instantiate(bread, bread.transform.parent);
                stock.name = kind == 7 ? "Cheese Sandwich Stock" : "Latte Stock";
                stock.transform.position = position; stock.Configure(bread.Shop, kind);
                var label = stock.GetComponentInChildren<TMP_Text>();
                label.text = (kind == 7 ? "СЫР" : "ЛАТТЕ") + "\n$" + stock.Price;
                label.fontSize = .9f;
                Physics.SyncTransforms();
            }
            var scene = bread.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new System.Exception("Scene save failed");
            AssetDatabase.SaveAssets();
            return "Added shop stock: sandwich $3, latte $5";
        }
    }
}
