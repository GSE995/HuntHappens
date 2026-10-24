using System;
using System.Linq;
using HuntHappens.SmallGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
namespace HuntHappens.Editor
{
    public static class LitterPileSetup
    {
        static readonly string[] Names = {"OpenBag", "Bottle", "FoodBox", "PaperCup", "Wrapper", "Bread", "Can"};
        static readonly float[] Sizes = {0.65f, 0.30f, 0.42f, 0.19f, 0.24f, 0.18f, 0.16f};
        static Bounds BoundsOf(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception("Missing mesh: " + go.name);
            var bounds = renderers[0].bounds;
            foreach (var r in renderers) bounds.Encapsulate(r.bounds);
            return bounds;
        }
        public static void RunBatch()
        {
            AssetDatabase.Refresh();
            var prefabs = new GameObject[Names.Length];
            for (int i = 0; i < Names.Length; i++)
            {
                string folder = "Assets/Art/Props/Litter/" + Names[i] + "/";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(folder + Names[i] + ".mat");
                if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, folder + Names[i] + ".mat"); }
                mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "texture_0_base_color.png"));
                mat.SetFloat("_Metallic", i == 6 ? 0.35f : 0f);
                mat.SetFloat("_Smoothness", i == 1 || i == 6 ? 0.25f : 0.1f);
                // Thin, crumpled packaging needs both sides visible from the player's camera.
                mat.SetFloat("_Cull", 0);
                var normal = (TextureImporter)AssetImporter.GetAtPath(folder + "texture_0_normal.png");
                normal.textureType = TextureImporterType.NormalMap; normal.SaveAndReimport();
                mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + "texture_0_normal.png")); mat.EnableKeyword("_NORMALMAP");
                var root = new GameObject(Names[i]);
                var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(folder + Names[i] + ".fbx"));
                model.transform.SetParent(root.transform, false);
                foreach (var r in model.GetComponentsInChildren<Renderer>()) r.sharedMaterials = r.sharedMaterials.Select(_ => mat).ToArray();
                var b = BoundsOf(root); float scale = Sizes[i] / Mathf.Max(b.size.x, b.size.y, b.size.z);
                model.transform.localScale *= scale;
                b = BoundsOf(root); model.transform.position -= new Vector3(b.center.x, b.min.y, b.center.z);
                // Small decorative scraps do not block rats or placement rays with invisible boxes.
                prefabs[i] = PrefabUtility.SaveAsPrefabAsset(root, folder + Names[i] + ".prefab"); Object.DestroyImmediate(root);
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            var litter = GameObject.Find("Forest Litter");
            var ground = GameObject.Find("Environment/Ground").GetComponent<Collider>();
            var old = litter.transform.Find("Scattered Litter"); if (old) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject("Scattered Litter"); group.transform.SetParent(litter.transform, false);
            var bags = litter.GetComponentsInChildren<RatGatheringSpot>().OrderBy(x => x.transform.position.x).ToArray();
            if (bags.Length != 3) throw new Exception("Expected three gathering spots");
            int count = 0;
            for (int pile = 0; pile < bags.Length; pile++)
            {
                var pileRoot = new GameObject("Litter Pile " + (pile + 1)); pileRoot.transform.SetParent(group.transform, false);
                for (int i = 0; i < prefabs.Length; i++)
                {
                    Vector2[] offsets = {new Vector2(0.65f, 0.2f), new Vector2(0.5f, -0.45f), new Vector2(-0.62f, 0.25f), new Vector2(-0.25f, -0.58f), new Vector2(0.35f, 0.65f), new Vector2(-0.48f, 0.65f), new Vector2(0.05f, -0.78f)};
                    Vector2 offset = offsets[i];
                    Vector3 pos = bags[pile].transform.position + Quaternion.Euler(0, pile * 113, 0) * new Vector3(offset.x, 0, offset.y);
                    if (!ground.Raycast(new Ray(pos + Vector3.up * 4, Vector3.down), out var floor, 8)) throw new Exception("No ground under litter");
                    var item = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[i]); item.transform.SetParent(pileRoot.transform, false);
                    item.transform.SetPositionAndRotation(floor.point, Quaternion.Euler(0, i * 73 + pile * 137, 0));
                    var b = BoundsOf(item); item.transform.position += Vector3.up * (floor.point.y - b.min.y);
                    if (Mathf.Abs(BoundsOf(item).min.y - floor.point.y) > 0.005f) throw new Exception("Floating litter");
                    count++;
                }
            }
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Debug.Log("Litter piles verified: " + count + " props, 7 types, 3 piles; ground contact checked.");
        }
        public static void PreviewBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            var bags = GameObject.Find("Forest Litter").GetComponentsInChildren<RatGatheringSpot>().OrderBy(x => x.transform.position.x).ToArray();
            var camera = new GameObject("Litter Preview").AddComponent<Camera>(); camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            camera.fieldOfView = 48;
            var target = new RenderTexture(1200, 900, 24); target.Create(); var image = new Texture2D(1200, 900, TextureFormat.RGB24, false);
            int frame = 0, index = 0;
            EditorApplication.update += Render;
            void Render()
            {
                camera.transform.position = bags[index].transform.position + (index == 1 ? new Vector3(-2.3f, 3.2f, -2.3f) : new Vector3(2.3f, 2.5f, 2.3f));
                camera.transform.LookAt(bags[index].transform.position + Vector3.up * 0.1f);
                RenderPipeline.SubmitRenderRequest(camera, new UniversalRenderPipeline.SingleCameraRequest {destination = target});
                if (++frame < 25) return;
                var previous = RenderTexture.active; RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 1200, 900), 0, 0); image.Apply();
                System.IO.File.WriteAllBytes("Logs/Litter-pile-" + (index + 1) + ".png", image.EncodeToPNG()); RenderTexture.active = previous;
                frame = 0; if (++index < bags.Length) return;
                EditorApplication.update -= Render; target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(image); EditorApplication.Exit(0);
            }
        }
    }
}
