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
                go.GetComponentInChildren<Animator>().speed = 0;
                npc = go.AddComponent<SemenychDialogue>();
            }
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
