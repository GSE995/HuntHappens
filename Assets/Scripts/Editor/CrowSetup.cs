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
            // The final placement is projected onto visible tree geometry below.
            for (int i = 0; i < 8; i++)
            {
                var tree = trees[i * trees.Length / 8];
                var perch = new GameObject("Perch " + (i + 1)).transform; perch.SetParent(root.transform);
                perch.position = tree.position;
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
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/ForestPrototype.unity") throw new System.InvalidOperationException("Open ForestPrototype in Edit Mode");
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
                Vector3 direction=spot.transform.position-tree.position; direction.y=0; direction.Normalize();
                var perch=bird.perch;
                Undo.RecordObjects(new Object[]{bird,bird.transform,perch},"Seat crow on tree canopy");
                perch.position=FindCanopySeat(tree,direction);
                perch.rotation=Quaternion.LookRotation(direction);
                bird.transform.SetPositionAndRotation(perch.position,perch.rotation); bird.feedingSpot=spot;
                PrefabUtility.RecordPrefabInstancePropertyModifications(bird);
                PrefabUtility.RecordPrefabInstancePropertyModifications(bird.transform);
                EditorUtility.SetDirty(bird);
            }
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                if (child.name.StartsWith("Crow Branch ")) Undo.DestroyObjectImmediate(child.gameObject);
            var scene=root.scene; EditorSceneManager.MarkSceneDirty(scene);
            if(!EditorSceneManager.SaveScene(scene)) throw new System.Exception("Scene save failed");
            AssetDatabase.SaveAssets();
            return "4 crows per occupied trash spot; 8m bait radius; safe trash at "+safe.transform.position;
        }
        // Cast against temporary copies of the actual render meshes, never the hidden proxy crown.
        // No foliage colliders remain in the scene: sleeping birds can still fall to the ground.
        internal static Vector3 FindCanopySeat(Transform tree, Vector3 direction)
        {
            var meshes = tree.GetComponentsInChildren<MeshFilter>()
                .Where(m => m.sharedMesh != null && m.GetComponent<Renderer>() != null && m.GetComponent<Renderer>().enabled).ToArray();
            if (meshes.Length == 0) throw new System.InvalidOperationException("No visible tree mesh: " + tree.name);
            var bounds = meshes[0].GetComponent<Renderer>().bounds;
            foreach (var mesh in meshes) bounds.Encapsulate(mesh.GetComponent<Renderer>().bounds);
            var temporary = new System.Collections.Generic.List<GameObject>();
            try
            {
                foreach (var mesh in meshes)
                {
                    var copy = new GameObject("Temporary canopy probe");
                    temporary.Add(copy);
                    copy.transform.SetParent(mesh.transform, false);
                    copy.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
                }
                Physics.SyncTransforms();
                // Approach the outer, trash-facing side from above, away from the trunk and apex.
                for (float fraction=.72f; fraction>=.35f; fraction-=.06f)
                {
                    var point=tree.position+direction*(Mathf.Min(bounds.extents.x,bounds.extents.z)*fraction);
                    var ray=new Ray(new Vector3(point.x,bounds.max.y+1,point.z),Vector3.down);
                    RaycastHit best=default; bool found=false;
                    foreach(var copy in temporary)
                        if(copy.GetComponent<MeshCollider>().Raycast(ray,out var hit,bounds.size.y+2) && hit.normal.y>.15f && (!found || hit.point.y>best.point.y))
                        { best=hit; found=true; }
                    if(found) return best.point+Vector3.up*.005f;
                }
                throw new System.InvalidOperationException("No canopy seat: "+tree.name);
            }
            finally { foreach(var copy in temporary) Object.DestroyImmediate(copy); }
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
