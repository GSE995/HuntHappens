using System;
using System.Collections.Generic;
using HuntHappens.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace HuntHappens.Editor
{
    public static class DaySkySetup
    {
        private const string Folder = "Assets/Environment/DaySky";

        [MenuItem("Hunt Happens/Environment/Set Up Day Sky")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before applying the sky.");
            if (SceneManager.GetActiveScene().path != "Assets/Scenes/ForestPrototype.unity")
                throw new InvalidOperationException("Open ForestPrototype before applying the sky.");
            System.IO.Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            Material sky = Material("Day Sky", "HuntHappens/Day Sky");
            Material cloudMaterial = Material("Soft Cloud", "HuntHappens/Soft Cloud");
            var existing = GameObject.Find("Day Sky");
            if (existing != null) Undo.DestroyObjectImmediate(existing);
            var root = new GameObject("Day Sky");
            Undo.RegisterCreatedObjectUndo(root, "Add day sky");
            var bank = new GameObject("Clouds").transform;
            bank.SetParent(root.transform, false);
            var controller = root.AddComponent<DaySky>();
            controller.sun = RenderSettings.sun;
            if (controller.sun == null) throw new InvalidOperationException("Scene needs a directional sun.");
            controller.skyMaterial = sky;
            controller.clouds = bank;
            controller.wrapWidth = 900;
            sky.SetVector("_SunDirection", -controller.sun.transform.forward);
            RenderSettings.skybox = sky;
            var camera = Camera.main;
            Undo.RecordObject(camera, "Enable sky background");
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = Mathf.Max(camera.farClipPlane, 650);
            PrefabUtility.RecordPrefabInstancePropertyModifications(camera);

            var meshes = new Mesh[5];
            for (int i = 0; i < meshes.Length; i++)
            {
                string path = $"{Folder}/Cloud {i + 1}.asset";
                meshes[i] = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (meshes[i] == null)
                {
                    meshes[i] = BuildCloud(i + 53);
                    AssetDatabase.CreateAsset(meshes[i], path);
                }
            }
            var random = new System.Random(7301);
            int index = 0;
            for (int x = -3; x <= 3; x++)
            for (int z = -3; z <= 3; z++)
            {
                if (random.NextDouble() < 0.26) continue;
                var cloud = new GameObject($"Cloud {++index:00}");
                cloud.transform.SetParent(bank, false);
                cloud.transform.localPosition = new Vector3(x * 125 + Range(random,-40,40), Range(random,85,115), z * 125 + Range(random,-40,40));
                cloud.transform.localRotation = Quaternion.Euler(0, Range(random,0,360), 0);
                cloud.transform.localScale = Vector3.one * Range(random,14,24);
                cloud.AddComponent<MeshFilter>().sharedMesh = meshes[index % meshes.Length];
                var renderer = cloud.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = cloudMaterial;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            if (!EditorSceneManager.SaveScene(SceneManager.GetActiveScene())) throw new InvalidOperationException("Scene save failed.");
            AssetDatabase.SaveAssets();
            Debug.Log($"Day sky saved: {index} clouds, 5 shared meshes, sun linked to {controller.sun.name}.");
        }

        private static Material Material(string name, string shaderName)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>($"{Folder}/{name}.mat");
            if (material != null) return material;
            Shader shader = Shader.Find(shaderName);
            if (shader == null) throw new InvalidOperationException($"Missing shader {shaderName}");
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, $"{Folder}/{name}.mat");
            return material;
        }

        private static float Range(System.Random r, float min, float max) => Mathf.Lerp(min,max,(float)r.NextDouble());

        // Sample a smooth density field: overlapping puffs become one continuous surface.
        private static Mesh BuildCloud(int seed)
        {
            var random = new System.Random(seed);
            var centers = new Vector3[7];
            var radii = new Vector3[7];
            for (int i = 0; i < 7; i++)
            {
                centers[i] = new Vector3((i % 4 - 1.5f) * .65f, Range(random,.05f,.5f), (i / 4 - .5f) * .65f);
                radii[i] = new Vector3(Range(random,.65f,.9f), Range(random,.6f,1.05f), Range(random,.55f,.85f));
            }
            float Density(Vector3 p)
            {
                float sum = 0;
                for (int i = 0; i < centers.Length; i++)
                {
                    Vector3 d = p - centers[i];
                    d = new Vector3(d.x/radii[i].x,d.y/radii[i].y,d.z/radii[i].z);
                    sum += Mathf.Exp(-d.sqrMagnitude * 2);
                }
                return sum - .6f;
            }
            Vector3 Normal(Vector3 p)
            {
                const float e = .008f;
                return new Vector3(Density(p-Vector3.right*e)-Density(p+Vector3.right*e),
                    Density(p-Vector3.up*e)-Density(p+Vector3.up*e),
                    Density(p-Vector3.forward*e)-Density(p+Vector3.forward*e)).normalized;
            }
            const int nx=40, ny=24, nz=28;
            var points=new Vector3[nx+1,ny+1,nz+1];
            var values=new float[nx+1,ny+1,nz+1];
            for(int x=0;x<=nx;x++) for(int y=0;y<=ny;y++) for(int z=0;z<=nz;z++)
            {
                Vector3 p=new Vector3(Mathf.Lerp(-2.4f,2.4f,x/(float)nx),Mathf.Lerp(-1.6f,2f,y/(float)ny),Mathf.Lerp(-1.7f,1.7f,z/(float)nz));
                points[x,y,z]=p; values[x,y,z]=Density(p);
            }
            int[,] offsets={{0,0,0},{1,0,0},{1,1,0},{0,1,0},{0,0,1},{1,0,1},{1,1,1},{0,1,1}};
            int[,] tetra={{0,5,1,6},{0,1,2,6},{0,2,3,6},{0,3,7,6},{0,7,4,6},{0,4,5,6}};
            int[,] edges={{0,1},{0,2},{0,3},{1,2},{1,3},{2,3}};
            var vertices=new List<Vector3>(); var normals=new List<Vector3>(); var triangles=new List<int>();
            var cp=new Vector3[8]; var cv=new float[8]; var polygon=new List<Vector3>(4);
            for(int x=0;x<nx;x++) for(int y=0;y<ny;y++) for(int z=0;z<nz;z++)
            {
                for(int c=0;c<8;c++) { int a=x+offsets[c,0],b=y+offsets[c,1],d=z+offsets[c,2];cp[c]=points[a,b,d];cv[c]=values[a,b,d]; }
                for(int t=0;t<6;t++)
                {
                    polygon.Clear();
                    for(int e=0;e<6;e++)
                    {
                        int a=tetra[t,edges[e,0]], b=tetra[t,edges[e,1]];
                        if((cv[a]>0)==(cv[b]>0)) continue;
                        polygon.Add(Vector3.Lerp(cp[a],cp[b],cv[a]/(cv[a]-cv[b])));
                    }
                    if(polygon.Count<3) continue;
                    Vector3 center=Vector3.zero; foreach(var p in polygon) center+=p; center/=polygon.Count;
                    Vector3 normal=Normal(center), axis=(polygon[0]-center).normalized, tangent=Vector3.Cross(normal,axis);
                    polygon.Sort((a,b)=>Mathf.Atan2(Vector3.Dot(a-center,tangent),Vector3.Dot(a-center,axis)).CompareTo(Mathf.Atan2(Vector3.Dot(b-center,tangent),Vector3.Dot(b-center,axis))));
                    int first=vertices.Count;
                    foreach(var p in polygon) { vertices.Add(p); normals.Add(Normal(p)); }
                    for(int n=1;n<polygon.Count-1;n++) { triangles.Add(first); triangles.Add(first+n); triangles.Add(first+n+1); }
                }
            }
            var mesh=new Mesh {name=$"Soft cloud {seed}",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles,0); mesh.RecalculateBounds();
            return mesh;
        }
    }
}
