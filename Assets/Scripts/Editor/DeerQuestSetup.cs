using System.Linq;
using HuntHappens.Animals;
using HuntHappens.Delivery;
using HuntHappens.Shop;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HuntHappens.Editor
{
    public static class DeerQuestSetup
    {
        [MenuItem("Tools/Hunt Happens/Configure Deer Quest")]
        public static void Configure()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Exit Play Mode first");
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (scene.path != "Assets/Scenes/ForestPrototype.unity") throw new System.InvalidOperationException("Open ForestPrototype first");
            const string bossPath = "Assets/Prefabs/Animals/BossDeer.prefab";
            var boss = PrefabUtility.LoadPrefabContents(bossPath);
            try
            {
                if (boss.GetComponent<DeerLeader>() == null) boss.AddComponent<DeerLeader>();
                var settings = new SerializedObject(boss.GetComponent<DeerSedation>());
                settings.FindProperty("requiredDose").floatValue = 3;
                settings.FindProperty("sleepDuration").floatValue = 60;
                settings.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(boss, bossPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(boss); }
            if (Object.FindAnyObjectByType<DeerCaptureZone>() == null)
            {
                foreach (var deer in Object.FindObjectsByType<DeerSedation>()) Object.DestroyImmediate(deer.gameObject);
                var ordinary = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Animals/Deer.prefab").GetComponent<DeerSedation>();
                var leader = AssetDatabase.LoadAssetAtPath<GameObject>(bossPath).GetComponent<DeerSedation>();
                for (int i = 0; i < 2; i++)
                {
                    var zone = new GameObject(i == 0 ? "Deer Zone 1 - Training" : "Deer Zone 2 - Leader").AddComponent<DeerCaptureZone>();
                    zone.transform.position = i == 0 ? new Vector3(-28, .05f, 28) : new Vector3(34.27f, .05f, 36.17f);
                    zone.zoneId = i == 0 ? "training" : "guarded";
                    zone.ordinaryCount = 3; zone.hasLeader = i == 1;
                    zone.ordinaryPrefab = ordinary; zone.leaderPrefab = leader;
                }
            }
            foreach (var spawner in Object.FindObjectsByType<DeerSpawner>()) spawner.enabled = false;
            var shop = Object.FindAnyObjectByType<ShopManager>();
            if (!Object.FindObjectsByType<WorldShopItem>().Any(x => x.Kind == 6))
            {
                var bread = Object.FindObjectsByType<WorldShopItem>().First(x => x.Kind == 1);
                var chocolate = Object.Instantiate(bread, bread.transform.parent);
                chocolate.name = "Chocolate Stock";
                chocolate.transform.position += bread.transform.forward * -1.5f;
                chocolate.Configure(shop, 6);
                var material = new Material(bread.GetComponent<Renderer>().sharedMaterial);
                material.color = new Color(.23f, .09f, .035f);
                AssetDatabase.CreateAsset(material, "Assets/Materials/Shop/Chocolate.mat");
                chocolate.GetComponent<Renderer>().sharedMaterial = material;
                chocolate.GetComponentInChildren<TMP_Text>().text = "ШОКОЛАД\n$" + shop.ChocolatePrice;
            }
            var dialogue = AssetDatabase.LoadAssetAtPath<HuntHappens.Characters.DialogueData>("Assets/Dialogues/FarmerIntroduction.asset");
            var serialized = new SerializedObject(dialogue);
            serialized.FindProperty("lines").GetArrayElementAtIndex(7).stringValue = "Вожак обожает шоколад! Купи в лавке и брось в стороне от стада. Пока ест — забирай остальных. Но попадёшь в него дротиком — разозлится. Самому вожаку нужно несколько попаданий.";
            serialized.FindProperty("lines").GetArrayElementAtIndex(8).stringValue = "Поможешь вернуть всех домой? Обычных ищи на двух полянах, на дальней — ещё и вожак. У тебя есть $15. Начни с крыс, заработай на транквилизатор и дротики в лавке. Олень слишком тяжёлый для одного. Подкати тележку к спящему, погрузи его и довези к входу в загон.";
            serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(dialogue);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
    }
}
