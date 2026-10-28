using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class NaturePreviewBuilder
{
    const string Root = "Assets/Art/QuaterniusNature";
    const string ScenePath = Root + "/NaturePreview.unity";
    static NaturePreviewBuilder()
    {
        if (!File.Exists(ScenePath)) EditorApplication.delayCall += BuildWhenReady;
    }
    static void BuildWhenReady()
    {
        if (EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode)
        { EditorApplication.delayCall += BuildWhenReady; return; }
        Build();
    }
    [MenuItem("Tools/Quaternius/Открыть каталог природы")]
    public static void Open()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        { Debug.LogWarning("Сначала выйдите из Play Mode, затем откройте каталог природы."); return; }
        for (int i=0;i<SceneManager.sceneCount;i++)
        {
            var current=SceneManager.GetSceneAt(i);
            if(current.isDirty && string.IsNullOrEmpty(current.path))
            { Debug.LogWarning("Сохраните новую сцену перед открытием каталога природы."); return; }
        }
        if (!EditorSceneManager.SaveOpenScenes()) return;
        AssetDatabase.SaveAssets();
        if (!File.Exists(ScenePath)) Build();
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        var view=EditorWindow.GetWindow<SceneView>();
        view.LookAt(new Vector3(56,0,45),Quaternion.Euler(55,0,0),95);
        view.Focus();
        Selection.activeObject=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
    }
    static void Build()
    {
        Directory.CreateDirectory(Root + "/Materials");
        var paths = AssetDatabase.FindAssets("t:Model", new[]{Root + "/FBX"})
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p=>p).ToArray();
        // The archive contains alternate FBX exports in subfolders: show each named model once.
        paths = paths.GroupBy(Path.GetFileNameWithoutExtension).Select(g=>g.OrderBy(p=>p.Length).First()).ToArray();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(scene);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new Exception("URP Lit shader missing");
        var cache = new System.Collections.Generic.Dictionary<string,Material>();
        int i=0;
        foreach (var path in paths)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);
            go.name=Path.GetFileNameWithoutExtension(path);
            foreach(var renderer in go.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterials=renderer.sharedMaterials.Select(old=>
                {
                    string name=old ? old.name : "Default";
                    if(cache.TryGetValue(name,out var found)) return found;
                    var mat=new Material(shader){name=name};
                    var tex=old ? old.mainTexture : null;
                    string texturePath=Root+"/Textures/"+name+".png";
                    if(name.StartsWith("Leaves_") && File.Exists(Root+"/Textures/"+name+"_C.png")) texturePath=Root+"/Textures/"+name+"_C.png";
                    if(!File.Exists(texturePath)) texturePath=Root+"/Textures/"+name+"_Diffuse.png";
                    if(!tex) tex=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                    mat.SetTexture("_BaseMap",tex);
                    mat.SetColor("_BaseColor",tex ? Color.white : (old && old.HasProperty("_Color") ? old.color : Color.white));
                    mat.SetFloat("_Smoothness",0.05f);
                    mat.SetFloat("_Cull",0);
                    if(tex && (name.Contains("Lea") || name.Contains("Grass") || name.Contains("Flower") || name.Contains("Fern")))
                    { mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",0.4f);mat.EnableKeyword("_ALPHATEST_ON"); }
                    string mp=Root+"/Materials/"+name.Replace(':','_')+".mat";
                    AssetDatabase.CreateAsset(mat,mp);cache[name]=mat;return mat;
                }).ToArray();
            }
            var renderers=go.GetComponentsInChildren<Renderer>();
            var bounds=new Bounds(go.transform.position,Vector3.zero);
            foreach(var r in renderers) bounds.Encapsulate(r.bounds);
            go.transform.position=new Vector3((i%9)*14-bounds.center.x,-bounds.min.y,(i/9)*14-bounds.center.z);
            var label=new GameObject(go.name+" Label");SceneManager.MoveGameObjectToScene(label,scene);
            label.transform.position=new Vector3((i%9)*14,0.08f,(i/9)*14-5);
            label.transform.rotation=Quaternion.Euler(90,0,0);
            var text=label.AddComponent<TextMesh>();text.text=go.name;text.characterSize=0.3f;text.fontSize=32;text.anchor=TextAnchor.MiddleCenter;text.color=Color.black;
            i++;
        }
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Preview Ground";
        floor.transform.position=new Vector3(56,-0.05f,49);floor.transform.localScale=new Vector3(15,1,14);
        var ground=new Material(shader);ground.SetColor("_BaseColor",new Color(0.58f,0.64f,0.48f));ground.SetFloat("_Smoothness",0);
        AssetDatabase.CreateAsset(ground,Root+"/Materials/PreviewGround.mat");floor.GetComponent<Renderer>().sharedMaterial=ground;
        var light=new GameObject("Preview Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(50,-30,0);
        var cam=new GameObject("Preview Camera").AddComponent<Camera>();cam.transform.position=new Vector3(56,95,-65);cam.transform.LookAt(new Vector3(56,0,45));cam.farClipPlane=500;cam.backgroundColor=new Color(0.55f,0.72f,0.85f);cam.clearFlags=CameraClearFlags.SolidColor;
        RenderSettings.ambientLight=new Color(0.65f,0.65f,0.65f);
        EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        if(SceneView.lastActiveSceneView) SceneView.lastActiveSceneView.LookAt(new Vector3(56,0,45),Quaternion.Euler(55,0,0),95);
        Debug.Log("Nature preview ready: "+i+" models, "+cache.Count+" materials. "+ScenePath);
    }
}
