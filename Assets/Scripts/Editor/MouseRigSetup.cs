using System.Linq;
using HuntHappens.SmallGame;
using UnityEditor;
using UnityEngine;
namespace HuntHappens.Editor
{
    public static class MouseRigSetup
    {
        public static void RunBatch()
        {
            const string path="Assets/Art/SmallGame/Rat.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var visual=root.GetComponent<RatAI>().visual;
                if(visual.Find("Mouse Skeleton")!=null)throw new System.Exception("Mouse already rigged");
                var filters=visual.GetComponentsInChildren<MeshFilter>();
                var points=filters.SelectMany(f=>f.sharedMesh.vertices.Select(v=>visual.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
                var skeleton=Bone("Mouse Skeleton",visual,visual,Vector3.zero);
                var body=Bone("Body",skeleton,visual,new Vector3(0,0.12f,0.08f));
                var legs=new Transform[4];var centers=new Vector3[4];
                for(int n=0;n<4;n++)
                {
                    bool left=n%2==0,front=n<2;
                    var foot=points.Where(p=>p.y<0.04f && (p.x<0)==left && (p.z>0.12f)==front && p.z> -0.12f).ToArray();
                    if(foot.Length==0)throw new System.Exception("Cannot locate mouse foot "+n);
                    centers[n]=new Vector3(foot.Average(p=>p.x),0.065f,foot.Average(p=>p.z));
                    legs[n]=Bone("Leg "+n,body,visual,centers[n]);
                }
                var tail=new Transform[3];
                tail[0]=Bone("Tail Base",body,visual,new Vector3(0,0.10f,-0.17f));
                tail[1]=Bone("Tail Middle",tail[0],visual,new Vector3(0.02f,0.10f,-0.25f));
                tail[2]=Bone("Tail Tip",tail[1],visual,new Vector3(0.07f,0.14f,-0.33f));
                var bones=new[]{body}.Concat(legs).Concat(tail).ToArray();
                for(int part=0;part<filters.Length;part++)
                {
                    var filter=filters[part];var mesh=Object.Instantiate(filter.sharedMesh);mesh.name="Mouse Rigged "+part;
                    Matrix4x4 matrix=visual.worldToLocalMatrix*filter.transform.localToWorldMatrix;
                    var vertices=mesh.vertices;var normals=mesh.normals;var tangents=mesh.tangents;var weights=new BoneWeight[vertices.Length];
                    for(int v=0;v<vertices.Length;v++)
                    {
                        Vector3 p=vertices[v]=matrix.MultiplyPoint3x4(vertices[v]);
                        if(normals.Length==vertices.Length)normals[v]=matrix.inverse.transpose.MultiplyVector(normals[v]).normalized;
                        if(tangents.Length==vertices.Length){Vector3 t=matrix.MultiplyVector(new Vector3(tangents[v].x,tangents[v].y,tangents[v].z)).normalized;tangents[v]=new Vector4(t.x,t.y,t.z,tangents[v].w);}
                        int leg=(p.z>0.12f?0:2)+(p.x<0?0:1);
                        float weight=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.025f,0.09f,p.y)))*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.04f,0.095f,Vector2.Distance(new Vector2(p.x,p.z),new Vector2(centers[leg].x,centers[leg].z)))));
                        float tailWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(-0.15f,-0.23f,p.z));
                        if(p.z> -0.24f)tailWeight*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(0.13f,0.18f,p.y));
                        if(tailWeight>0)
                        {
                            float segment=Mathf.Clamp((-p.z-0.21f)/0.08f,0,2);int a=Mathf.Min(1,Mathf.FloorToInt(segment));float blend=segment-a;
                            weights[v]=new BoneWeight{boneIndex0=5+a,weight0=tailWeight*(1-blend),boneIndex1=6+a,weight1=tailWeight*blend,boneIndex2=0,weight2=1-tailWeight};
                        }
                        else weights[v]=new BoneWeight{boneIndex0=1+leg,weight0=weight,boneIndex1=0,weight1=1-weight};
                    }
                    mesh.vertices=vertices;mesh.normals=normals;mesh.tangents=tangents;mesh.boneWeights=weights;mesh.bindposes=bones.Select(b=>b.worldToLocalMatrix*visual.localToWorldMatrix).ToArray();mesh.RecalculateBounds();
                    AssetDatabase.CreateAsset(mesh,"Assets/Art/Animals/Mouse/MouseRigged-"+part+".asset");
                    var skin=new GameObject("Rigged Mouse "+part).AddComponent<SkinnedMeshRenderer>();skin.transform.SetParent(visual,false);skin.sharedMesh=mesh;skin.sharedMaterials=filter.GetComponent<Renderer>().sharedMaterials;skin.bones=bones;skin.rootBone=body;skin.updateWhenOffscreen=true;
                    var bounds=mesh.bounds;bounds.Expand(0.12f);skin.localBounds=bounds;
                    filter.gameObject.SetActive(false);
                }
                var animation=root.AddComponent<RatAnimation>();animation.legs=legs;animation.tail=tail;
                PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
                Debug.Log("Mouse rig installed: four weighted legs and three tail bones.");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        private static Transform Bone(string name,Transform parent,Transform visual,Vector3 position)
        {var bone=new GameObject(name).transform;bone.SetParent(parent,false);bone.position=visual.TransformPoint(position);return bone;}
        public static void InspectBatch()
        {
            var root=PrefabUtility.LoadPrefabContents("Assets/Art/SmallGame/Rat.prefab");
            var visual=root.GetComponent<RatAI>().visual;
            var points=visual.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>visual.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
            System.IO.File.WriteAllLines("Logs/Mouse-points.csv",points.Where((p,i)=>i%5==0).Select(p=>$"{p.x},{p.y},{p.z}"));
            for(float z=-0.4f;z<0.4f;z+=0.05f){var slice=points.Where(p=>p.z>=z&&p.z<z+0.05f).ToArray();if(slice.Length>0)Debug.Log($"SLICE z={z:F2} n={slice.Length} x={slice.Min(p=>p.x):F3}..{slice.Max(p=>p.x):F3} y={slice.Min(p=>p.y):F3}..{slice.Max(p=>p.y):F3}");}
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
