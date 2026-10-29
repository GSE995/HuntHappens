using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class DenseForestSetup
    {
        const string Art = "Assets/Art/QuaterniusNature/";
        [MenuItem("Tools/Hunt Happens/Densify Conifer Forest")]
        public static void Run()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlayingOrWillChangePlaymode || scene.path != "Assets/Scenes/ForestPrototype.unity")
                throw new InvalidOperationException("Open ForestPrototype in Edit Mode");
            var trees = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Forest tree ")).OrderBy(t => t.name).ToList();
            if (trees.Count == 0) throw new InvalidOperationException("No trees");
            var random = new System.Random(291026);
            var template = trees[0];
            for (int attempt = 0; attempt < 20000 && trees.Count < 230; attempt++)
            {
                var p = new Vector3(-46 + (float)random.NextDouble()*92, 0, -46 + (float)random.NextDouble()*92);
                if (FarmMapSetup.Clear(p) || trees.Any(t => (t.position-p).sqrMagnitude < 6.25f)) continue;
                // Keep extra clearance around props, animals, branches and interactable objects.
                if (Physics.OverlapSphere(p+Vector3.up, 1.2f).Any(c => c.name != "Ground" && !c.isTrigger)) continue;
                var tree = Object.Instantiate(template.gameObject, template.parent).transform;
                tree.name = "Forest tree dense " + trees.Count;
                tree.position = p;
                tree.rotation = Quaternion.Euler(0, (float)random.NextDouble()*360, 0);
                tree.localScale = Vector3.one * (.85f+(float)random.NextDouble()*.5f);
                trees.Add(tree);
            }
            for (int i=0; i<trees.Count; i++)
            {
                var tree = trees[i];
                // Hidden original crown retains the original height reference.
                var crown = tree.Find("Crown").GetComponent<Renderer>();
                var trunk = tree.Find("Trunk").GetComponent<Renderer>();
                float height = crown.bounds.max.y-trunk.bounds.min.y;
                float ground = trunk.bounds.min.y;
                foreach (Transform child in tree.Cast<Transform>().ToArray())
                    if (child.name != "Crown" && child.name != "Trunk") Object.DestroyImmediate(child.gameObject);
                crown.enabled = false; trunk.enabled = false;
                bool conifer = i % 7 != 0 || new[]{"71","123","193","200","256","267","376","473"}.Any(n=>tree.name=="Forest tree "+n);
                string model = (conifer ? "Pine_" : "CommonTree_") + (i%5+1);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Art+"FBX/"+model+".fbx");
                var visual = (GameObject)PrefabUtility.InstantiatePrefab(asset, tree);
                visual.name = "Quaternius " + model;
                var renderers = visual.GetComponentsInChildren<Renderer>();
                foreach(var r in renderers)
                    r.sharedMaterials = r.sharedMaterials.Select(m => AssetDatabase.LoadAssetAtPath<Material>(Art+"Materials/"+m.name+".mat") ?? throw new Exception("Missing material: "+m.name)).ToArray();
                var bounds = renderers[0].bounds;
                foreach(var r in renderers) bounds.Encapsulate(r.bounds);
                visual.transform.localScale *= height/bounds.size.y;
                bounds = renderers[0].bounds;
                foreach(var r in renderers) bounds.Encapsulate(r.bounds);
                visual.transform.position += Vector3.up*(ground-bounds.min.y);
                foreach(var component in visual.GetComponentsInChildren<Component>())
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            }
            if (GameObject.Find("Crows") != null) CrowSetup.ArrangeAtTrash();
            Physics.SyncTransforms();
            FarmMapSetup.Bake();
            string routes = FarmMapSetup.ValidateRoutes();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new Exception("Save failed");
            AssetDatabase.SaveAssets();
            Debug.Log("Dense forest: "+trees.Count+" trees. "+routes);
        }
    }
}
