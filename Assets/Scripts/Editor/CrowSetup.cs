using System.Linq;
using HuntHappens.SmallGame;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class CrowSetup
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/ForestPrototype.unity") throw new System.InvalidOperationException("Open ForestPrototype first");
            var trees = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(t => t.name.StartsWith("Forest tree ")).OrderBy(t => t.name).ToArray();
            if (trees.Length == 0) throw new System.InvalidOperationException("No forest trees");
            if (GameObject.Find("Crows") != null) return ArrangeAtTrash();
            const string folder = "Assets/Art/SmallGame";
            var black = Material(folder + "/Crow Feathers.mat", new Color(.035f, .045f, .065f));
            var beak = Material(folder + "/Crow Beak.mat", new Color(.12f, .13f, .15f));
            var bark = Material(folder + "/Crow Branch.mat", new Color(.24f, .15f, .09f));
            var bird = new GameObject("Crow");
            var ai = bird.AddComponent<CrowAI>();
            var body = bird.GetComponent<Rigidbody>(); body.mass = .5f; body.isKinematic = true; body.useGravity = false;
            var collider = bird.GetComponent<SphereCollider>(); collider.center = Vector3.up * .18f; collider.radius = .23f;
            Part(bird.transform, "Body", new Vector3(0,.19f,0), new Vector3(.28f,.32f,.44f), black);
            Part(bird.transform, "Head", new Vector3(0,.39f,.17f), Vector3.one * .23f, black);
            Part(bird.transform, "Beak", new Vector3(0,.37f,.33f), new Vector3(.09f,.08f,.22f), beak);
            Part(bird.transform, "Tail", new Vector3(0,.17f,-.29f), new Vector3(.2f,.07f,.3f), black);
            for (int side = -1; side <= 1; side += 2)
            {
                var wing = new GameObject(side < 0 ? "Left Wing" : "Right Wing").transform;
                wing.SetParent(bird.transform, false); wing.localPosition = new Vector3(side*.12f,.24f,0);
                Part(wing, "Feathers", new Vector3(side*.13f,0,-.06f), new Vector3(.32f,.065f,.36f), black);
                if (side < 0) ai.leftWing = wing; else ai.rightWing = wing;
                Part(bird.transform, "Foot", new Vector3(side*.07f,.025f,.04f), new Vector3(.04f,.05f,.18f), beak);
            }
            WorldScaleSetup.ConfigureCrow(bird);
            var prefab = PrefabUtility.SaveAsPrefabAsset(bird, folder + "/Crow.prefab");
            Object.DestroyImmediate(bird);
            var root = new GameObject("Crows"); Undo.RegisterCreatedObjectUndo(root, "Add crows");
            // Spread perches across the actual forest, outside each crown's silhouette.
            for (int i = 0; i < 8; i++)
            {
                var tree = trees[i * trees.Length / 8];
                var crown = tree.Find("Crown").GetComponent<Renderer>().bounds;
                var branch = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                branch.name = "Crow Branch " + (i + 1); branch.transform.SetParent(root.transform);
                branch.transform.position = new Vector3(tree.position.x + crown.extents.x*.6f, crown.center.y-.5f, tree.position.z);
                branch.transform.rotation = Quaternion.Euler(0,0,90);
                branch.transform.localScale = new Vector3(.13f,crown.extents.x*.7f,.13f);
                branch.GetComponent<Renderer>().sharedMaterial = bark;
                var perch = new GameObject("Perch " + (i + 1)).transform; perch.SetParent(root.transform);
                perch.position = new Vector3(tree.position.x + crown.extents.x*1.2f,crown.center.y-.4f,tree.position.z);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                instance.name = "Crow " + (i + 1); instance.transform.position = perch.position;
                instance.GetComponent<CrowAI>().perch = perch;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new System.Exception("Scene save failed");
            AssetDatabase.SaveAssets();
            return ArrangeAtTrash();
        }
        public static string ArrangeAtTrash()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first");
            var root = GameObject.Find("Crows");
            var enclosure = Object.FindAnyObjectByType<HuntHappens.Delivery.DeerEnclosure>();
            var pasture = enclosure != null ? enclosure.transform.Find("Pen pasture") : null;
            if (root == null || pasture == null) throw new System.InvalidOperationException("Crows and enclosure pasture required");
            var bounds = pasture.GetComponent<Renderer>().bounds;
            var spots = Object.FindObjectsByType<RatGatheringSpot>().OrderBy(s => s.transform.position.x).ToArray();
            if (spots.Length != 3) throw new System.InvalidOperationException("Expected three trash spots");
            var safe = spots.OrderBy(s => bounds.SqrDistance(new Vector3(s.transform.position.x,bounds.center.y,s.transform.position.z))).First();
            var occupied = spots.Where(s => s != safe).ToArray();
            var birds = root.GetComponentsInChildren<CrowAI>().OrderBy(c => c.name).ToArray();
            if (birds.Length != 8) throw new System.InvalidOperationException("Expected eight crows");
            var trees = Object.FindObjectsByType<Transform>().Where(t => t.name.StartsWith("Forest tree ") && t.Find("Crown") != null).ToList();
            var chosen = new Transform[8];
            for (int i=0; i<8; i++)
            {
                var spot = occupied[i/4];
                var tree = trees.OrderBy(t => Vector3.SqrMagnitude(t.position-spot.transform.position)).First();
                if (Vector3.Distance(tree.position,spot.transform.position)>12) throw new System.InvalidOperationException("No nearby tree within 12m");
                chosen[i]=tree; trees.Remove(tree);
            }
            foreach(var spot in spots)
            {
                Undo.RecordObject(spot,"Crow territory"); spot.crowsAllowed=spot!=safe; spot.crowBaitRadius=8;
                EditorUtility.SetDirty(spot);
            }
            for(int i=0;i<birds.Length;i++)
            {
                var bird=birds[i]; var spot=occupied[i/4]; var tree=chosen[i];
                var crown=tree.Find("Crown").GetComponent<Renderer>().bounds;
                Vector3 direction=spot.transform.position-tree.position; direction.y=0; direction.Normalize();
                float extent=Mathf.Max(crown.extents.x,crown.extents.z);
                var branch=root.transform.Find("Crow Branch "+(i+1)); var perch=bird.perch;
                Undo.RecordObjects(new Object[]{bird,bird.transform,branch,perch},"Move crow to trash tree");
                Vector3 center=new Vector3(tree.position.x,crown.center.y-.5f,tree.position.z);
                branch.position=center+direction*extent*.6f;
                branch.rotation=Quaternion.FromToRotation(Vector3.up,direction);
                branch.localScale=new Vector3(.13f,extent*.7f,.13f);
                perch.position=center+direction*extent*1.2f+Vector3.up*.1f;
                perch.rotation=Quaternion.LookRotation(direction);
                bird.transform.SetPositionAndRotation(perch.position,perch.rotation); bird.feedingSpot=spot;
                PrefabUtility.RecordPrefabInstancePropertyModifications(bird);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bird.transform);
                EditorUtility.SetDirty(bird);
            }
            var scene=root.scene; EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new System.Exception("Scene save failed");
            AssetDatabase.SaveAssets();
            return "4 crows per occupied trash spot; 8m bait radius; safe trash at "+safe.transform.position;
        }
        private static Material Material(string path, Color color)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.color = color; EditorUtility.SetDirty(mat); return mat;
        }
        private static void Part(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Sphere); part.name = name;
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = scale;
            Object.DestroyImmediate(part.GetComponent<Collider>()); part.GetComponent<Renderer>().sharedMaterial = material;
        }
    }
}
