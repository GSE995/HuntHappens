using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace HuntHappens.Editor
{
    public static class OuterLandscapeSetup
    {
        const string Folder = "Assets/Environment/OuterLandscape";
        const string RootName = "Outer Landscape";

        [MenuItem("Tools/Hunt Happens/Rebuild Outer Landscape")]
        public static void Build()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/ForestPrototype.unity")
                throw new InvalidOperationException("Open ForestPrototype in Edit mode first.");
            var template = GameObject.Find("Farm Map").transform.Find("Forest").GetChild(0);
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var old = GameObject.Find(RootName);
            if (old != null) Undo.DestroyObjectImmediate(old);
            var root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build outer landscape");
            var meadow = Material("Meadow", new Color(.33f, .46f, .22f));
            var hills = Material("Hills", new Color(.30f, .43f, .29f));
            var ridge = Material("Distant ridge", new Color(.39f, .49f, .51f));
            var peak = Material("Summit stone", new Color(.57f, .62f, .61f));
            var vertices = new List<Vector3>();
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            const int sides = 128, rings = 50;
            for (int r = 0; r <= rings; r++)
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides;
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                p *= (50 + r * 8) / Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y));
                vertices.Add(new Vector3(p.x, Height(p.x, p.y), p.y));
            }
            for (int r = 0; r < rings; r++)
            for (int i = 0; i < sides; i++)
            {
                int a = r * sides + i, b = r * sides + (i + 1) % sides;
                int c = a + sides, d = b + sides;
                var center = (vertices[a] + vertices[b] + vertices[c] + vertices[d]) * .25f;
                int material = center.y > 73 ? 3 : r > 18 ? 2 : r > 7 ? 1 : 0;
                triangles[material].AddRange(new[] { a, b, c, b, d, c });
            }
            var ground = new Mesh { name = "Surrounding hills and mountain ridge" };
            ground.SetVertices(vertices); ground.subMeshCount = 4;
            for (int i = 0; i < 4; i++) ground.SetTriangles(triangles[i], i);
            ground.RecalculateNormals(); ground.RecalculateBounds();
            RenderMesh("Hills and distant mountain", ground, new[] { meadow, hills, ridge, peak }, root.transform);

            // Reuse the map's tree silhouette; combine by sector/material to avoid hundreds of renderers.
            var bark = Material("Bark", new Color(.25f, .22f, .17f));
            var leaves = new[] {
                Material("Forest olive", new Color(.24f,.36f,.16f)),
                Material("Forest green", new Color(.18f,.31f,.19f)),
                Material("Forest distant", new Color(.28f,.40f,.30f)) };
            var batches = new Dictionary<int, List<CombineInstance>>();
            var crownMesh = TreeMesh(false);
            var trunkMesh = TreeMesh(true);
            var rng = new System.Random(87231);
            int count = 0;
            for (int z = -194; z <= 194; z += 9)
            for (int x = -194; x <= 194; x += 9)
            {
                float px = x + (float)rng.NextDouble() * 6, pz = z + (float)rng.NextDouble() * 6;
                float edge = Mathf.Max(Mathf.Abs(px), Mathf.Abs(pz));
                if (edge < 58 || edge > 192 || rng.NextDouble() < .25) continue;
                float size = .9f + (float)rng.NextDouble() * .9f;
                var matrix = Matrix4x4.TRS(new Vector3(px, Height(px,pz) - .15f, pz),
                    Quaternion.Euler(0, (float)rng.NextDouble()*360, 0), Vector3.one * size);
                int sector = (px > 0 ? 1 : 0) + (pz > 0 ? 2 : 0);
                int shade = edge > 130 ? 2 : rng.Next(2);
                foreach (var filter in template.GetComponentsInChildren<MeshFilter>())
                {
                    int mat = filter.name == "Trunk" ? 0 : shade + 1;
                    int key = sector * 4 + mat;
                    if (!batches.ContainsKey(key)) batches[key] = new List<CombineInstance>();
                    batches[key].Add(new CombineInstance { mesh = mat == 0 ? trunkMesh : crownMesh,
                        transform = matrix * template.worldToLocalMatrix * filter.transform.localToWorldMatrix });
                }
                count++;
            }
            foreach (var batch in batches)
            {
                var mesh = new Mesh { name = "Forest sector " + batch.Key, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(batch.Value.ToArray(), true, true);
                int mat = batch.Key % 4;
                RenderMesh(mesh.name, mesh, new[] { mat == 0 ? bark : leaves[mat - 1] }, root.transform);
            }
            Object.DestroyImmediate(crownMesh);
            Object.DestroyImmediate(trunkMesh);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Scene save failed");
            AssetDatabase.SaveAssets();
            Debug.Log("Outer landscape saved: " + count + " decorative trees, 400 m of surrounding terrain, no colliders.");
        }

        static Mesh TreeMesh(bool trunk)
        {
            const int segments = 10;
            int rings = trunk ? 1 : 6;
            var vertices = new List<Vector3>();
            var indices = new List<int>();
            for (int r = 0; r <= rings; r++)
            {
                float t = (float)r / rings;
                float y = trunk ? Mathf.Lerp(-1,1,t) : -Mathf.Cos(t*Mathf.PI)*.5f;
                float radius = trunk ? .5f : Mathf.Sin(t*Mathf.PI)*.5f;
                for (int i=0; i<segments; i++)
                {
                    float a=i*Mathf.PI*2/segments;
                    vertices.Add(new Vector3(Mathf.Cos(a)*radius,y,Mathf.Sin(a)*radius));
                    if(r==rings) continue;
                    int k=r*segments+i, n=r*segments+(i+1)%segments;
                    indices.AddRange(new[]{k,k+segments,n,n,k+segments,n+segments});
                }
            }
            var mesh=new Mesh(); mesh.SetVertices(vertices); mesh.SetTriangles(indices,0);
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh;
        }

        static float Hill(float x, float z, float cx, float cz, float width, float depth, float height)
        {
            float dx = (x-cx)/width, dz = (z-cz)/depth;
            return height * Mathf.Exp(-(dx*dx + dz*dz)*2);
        }

        static float Height(float x, float z)
        {
            float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z));
            float fade = Mathf.SmoothStep(0, 1, (edge-50)/45);
            float h = 2 + Mathf.PerlinNoise(x*.014f+31,z*.014f+17)*12;
            h += Hill(x,z,-100,105,65,80,22) + Hill(x,z,120,70,70,60,26);
            h += Hill(x,z,-70,-135,90,65,25) + Hill(x,z,-145,-35,70,90,23);
            h += Hill(x,z,85,285,80,85,106) + Hill(x,z,-20,270,80,70,67);
            h += Hill(x,z,170,255,65,90,58) + Hill(x,z,-230,170,95,100,47);
            h += (Mathf.PerlinNoise(x*.045f+9,z*.045f+5)-.5f)*10*Mathf.Clamp01((edge-150)/90);
            return -.12f + h*fade;
        }

        static Material Material(string name, Color color)
        {
            string path = Folder + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
            mat.color = color; mat.SetFloat("_Smoothness",0); EditorUtility.SetDirty(mat);
            return mat;
        }

        static void RenderMesh(string name, Mesh mesh, Material[] materials, Transform parent)
        {
            string path = Folder + "/" + name + ".asset";
            var saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null) { AssetDatabase.CreateAsset(mesh,path); saved = mesh; }
            else
            {
                saved.Clear(); saved.indexFormat = mesh.indexFormat;
                saved.vertices = mesh.vertices; saved.normals = mesh.normals;
                saved.subMeshCount = mesh.subMeshCount;
                for (int i=0; i<mesh.subMeshCount; i++) saved.SetTriangles(mesh.GetTriangles(i),i);
                saved.RecalculateBounds();
                Object.DestroyImmediate(mesh); EditorUtility.SetDirty(saved);
            }
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent,false);
            go.GetComponent<MeshFilter>().sharedMesh = saved;
            var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            // No NavigationStatic flag or colliders: this is scenery beyond the existing play boundary.
        }
    }
}
