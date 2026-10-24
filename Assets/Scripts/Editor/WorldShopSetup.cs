using HuntHappens.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
namespace HuntHappens.Editor
{
    public static class WorldShopSetup
    {
        public static void RunBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            var shop = Object.FindAnyObjectByType<ShopManager>();
            var ground = GameObject.Find("Environment/Ground").GetComponent<Collider>();
            var old = GameObject.Find("Shop Goods"); if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject("Shop Goods");
            float[] columns = { -2.4f, -1.2f, 1.2f, 2.4f };
            string[] labels = { "КЛЕТКИ", "ХЛЕБ", "ДРОТИКИ", "ПРИЁМ КРЫС" };
            string[] materials = { "Cage Steel", "Bread", "Rat Pink", "Rat Fur" };
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Materials/Tranquilizer/HUD.asset");
            for (int i = 0; i < 4; i++)
            {
                Vector3 position = default; bool found = false;
                for (float depth = 2.9f; depth < 8f; depth += 0.7f)
                {
                    var desired = shop.Kiosk.transform.TransformPoint(new Vector3(columns[i], 0, -depth));
                    if (!ground.Raycast(new Ray(desired + Vector3.up * 5, Vector3.down), out var floor, 10)) continue;
                    if (!NavMesh.SamplePosition(floor.point - shop.Kiosk.transform.forward, out _, 0.5f, NavMesh.AllAreas)) continue;
                    bool blocked = false;
                    foreach (var collider in Physics.OverlapBox(floor.point + Vector3.up * 0.6f, new Vector3(0.5f, 0.55f, 0.5f), shop.Kiosk.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                        if (collider != ground) blocked = true;
                    if (!blocked) { position = floor.point; found = true; break; }
                }
                if (!found) throw new System.Exception("No clear place for shop product " + i);
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube); box.name = labels[i]; box.transform.SetParent(root.transform);
                box.transform.SetPositionAndRotation(position + Vector3.up * 0.45f, shop.Kiosk.transform.rotation); box.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
                box.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/SmallGame/" + materials[i] + ".mat");
                box.AddComponent<WorldShopItem>().Configure(shop, i);
                var obstacle = box.AddComponent<NavMeshObstacle>(); obstacle.shape = NavMeshObstacleShape.Box; obstacle.size = Vector3.one; obstacle.carving = true;
                var text = new GameObject("Product Label").AddComponent<TextMeshPro>();text.transform.SetParent(box.transform,false);
                text.transform.localPosition = new Vector3(0,0,-0.51f); text.font = font; text.fontSize = 1.15f;
                text.text = labels[i] + "\n$" + box.GetComponent<WorldShopItem>().Price; text.alignment = TextAlignmentOptions.Center;
                text.color = Color.white; text.rectTransform.sizeDelta = new Vector2(0.95f,0.7f);
                Physics.SyncTransforms();
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("World shop installed: three products and rat reception; upgrade UI preserved.");
        }
    }
}
