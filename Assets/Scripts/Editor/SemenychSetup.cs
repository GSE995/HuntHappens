using HuntHappens.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class SemenychSetup
    {
        public static string Run()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Use Edit Mode");
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != "Assets/Scenes/ForestPrototype.unity")
                throw new System.InvalidOperationException("Open ForestPrototype first");
            var npc = Object.FindAnyObjectByType<SemenychDialogue>();
            if (npc == null)
            {
                var farmer = Object.FindAnyObjectByType<FarmerDialogue>();
                var go = Object.Instantiate(farmer.gameObject);
                go.name = "Семёныч — приём крыс";
                Object.DestroyImmediate(go.GetComponent<FarmerDialogue>());
                Object.DestroyImmediate(go.GetComponent<HeroWander>());
                Object.DestroyImmediate(go.GetComponent<UnityEngine.AI.NavMeshAgent>());
                go.transform.SetParent(null);
                go.transform.position = new Vector3(11, .05f, -34);
                go.transform.rotation = Quaternion.Euler(0, 180, 0);
                npc = go.AddComponent<SemenychDialogue>();
            }
            ApplyAppearance(npc);
            var intro = Dialogue("SemenychIntroduction", new[] {
                "Тихо ты, клетки не тряси. Я Семёныч. Принимаю крыс, плачу деньгами. Зачем они мне? Вот за вопросы я не плачу",
                "Решишь подзаработать — ставь крысоловку и клади приманку. Только у каждой крысы свои запросы",
                "Обычная на простой хлеб идёт. Серая поразборчивее — ей бутерброд с сыром подавай",
                "А большая — городская, на людей насмотрелась, теперь требования завышенные. Только на кофе латте и ловится",
                "На хлеб даже не смотрит, будто ипотеку уже выплатила",
                "Поймаешь — приноси. Разберёмся по цене" });
            var hints = Dialogue("SemenychHints", new[] {
                "Обычной крысе хлеба достаточно. Не трать на неё деликатесы",
                "Серая хлеб понюхала и ушла? Сыр забыл. Бутерброд ей нужен",
                "Большую видел? На латте её лови. Сам не выпей по дороге",
                "Приманку подбирай под крысу. Они тут привередливее покупателей на рынке",
                "Крысоловка пустая? А приманку ты положил или надеялся на силу убеждения?" });
            var so = new SerializedObject(npc);
            so.FindProperty("introduction").objectReferenceValue = intro;
            so.FindProperty("hints").objectReferenceValue = hints;
            so.FindProperty("dialogueFont").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Art/UI/FieldHUD/Dialogue Sans SDF.asset");
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(npc.gameObject.scene);
            AssetDatabase.SaveAssets();
            if (!EditorSceneManager.SaveOpenScenes()) throw new System.Exception("Scene save failed");
            return npc.name + " at " + npc.transform.position;
        }
        public static void ApplyAppearance(SemenychDialogue npc)
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Use Edit Mode");
            const string folder = "Assets/Art/MouseCatcher/";
            const string stem = "Meshy_AI_skinny_camper_character_20261022_image-to-3d-texture";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "SemenychRig.fbx");
            if (model == null) throw new System.InvalidOperationException("MouseCatcher model is missing");
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + "MouseCatcher.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(folder + stem + ".png"));
                material.SetFloat("_Smoothness", 0.15f);
                AssetDatabase.CreateAsset(material, folder + "MouseCatcher.mat");
            }
            // Feet remain fixed in the idle clip; the farmer's per-vertex grounding is unnecessary.
            var grounding = npc.GetComponent<HeroGrounding>();
            if (grounding != null) Object.DestroyImmediate(grounding);
            for (int i = npc.transform.childCount - 1; i >= 0; i--)
            {
                var child = npc.transform.GetChild(i);
                if (child.GetComponentInChildren<Renderer>() != null) Object.DestroyImmediate(child.gameObject);
            }
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, npc.transform);
            visual.name = "MouseCatcher";
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = model.transform.localRotation;
            visual.transform.localScale = model.transform.localScale;
            var renderers = visual.GetComponentsInChildren<Renderer>();
            // Imported skinned-renderer bounds include skeleton padding. Measure actual geometry.
            var bounds = new Bounds();
            bool hasBounds = false;
            foreach (var renderer in renderers)
            {
                var skin = renderer as SkinnedMeshRenderer;
                if (skin != null)
                {
                    var baked = new Mesh();
                    skin.BakeMesh(baked, true);
                    foreach (var vertex in baked.vertices)
                    {
                        var point = skin.transform.TransformPoint(vertex);
                        if (!hasBounds) { bounds = new Bounds(point, Vector3.zero); hasBounds = true; }
                        else bounds.Encapsulate(point);
                    }
                    Object.DestroyImmediate(baked);
                }
                else
                {
                    if (!hasBounds) { bounds = renderer.bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderer.bounds);
                }
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            float height = npc.GetComponent<CapsuleCollider>().height;
            float scale = height / bounds.size.y;
            visual.transform.localScale *= scale;
            float floor = npc.transform.position.y;
            foreach (var hit in Physics.RaycastAll(npc.transform.position + Vector3.up * 0.5f, Vector3.down, 2f, ~0, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(npc.transform)) floor = Mathf.Max(floor, hit.point.y);
            visual.transform.position += new Vector3(
                (npc.transform.position.x - bounds.center.x) * scale,
                floor - (npc.transform.position.y + (bounds.min.y - npc.transform.position.y) * scale),
                (npc.transform.position.z - bounds.center.z) * scale);
            SemenychIdleSetup.Configure(visual);
            WorldScaleSetup.ConfigureSemenych(npc);
            EditorSceneManager.MarkSceneDirty(npc.gameObject.scene);
        }

        private static DialogueData Dialogue(string name, string[] lines)
        {
            string path = "Assets/Dialogues/" + name + ".asset";
            var data = AssetDatabase.LoadAssetAtPath<DialogueData>(path);
            if (data == null) { data = ScriptableObject.CreateInstance<DialogueData>(); AssetDatabase.CreateAsset(data, path); }
            var so = new SerializedObject(data);
            so.FindProperty("speakerName").stringValue = "Семёныч";
            var array = so.FindProperty("lines"); array.arraySize = lines.Length;
            for (int i = 0; i < lines.Length; i++) array.GetArrayElementAtIndex(i).stringValue = lines[i];
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }
    }
}
