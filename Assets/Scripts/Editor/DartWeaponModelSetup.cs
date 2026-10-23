using System.Linq;
using HuntHappens.Weapons;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace HuntHappens.Editor
{
    public static class DartWeaponModelSetup
    {
        private const string Folder = "Assets/Art/Weapons/";
        private const string Name = "Meshy_AI_lowpoly_cartoon_spear_1023210152_texture";
        public static void IntegrateBatch()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/ForestPrototype.unity");
            var gun = Object.FindAnyObjectByType<TranquilizerGun>();
            Transform root = gun.WeaponVisual;
            root.gameObject.SetActive(true);
            var existing = root.Find("Imported Dart Weapon");
            if (existing != null) Object.DestroyImmediate(existing.gameObject);
            foreach (Transform child in root.Cast<Transform>().ToArray())
                if (child.name != "Muzzle" && child.name != "Muzzle Flash") Object.DestroyImmediate(child.gameObject);
            var material = AssetDatabase.LoadAssetAtPath<Material>(Folder + "DartWeapon.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + Name + ".png"));
                material.SetFloat("_Smoothness", 0.2f);
                string normalPath = Folder + Name + "_normal.png";
                var normalImporter = (TextureImporter)AssetImporter.GetAtPath(normalPath);
                normalImporter.textureType = TextureImporterType.NormalMap;
                normalImporter.SaveAndReimport();
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                material.EnableKeyword("_NORMALMAP");
                AssetDatabase.CreateAsset(material, Folder + "DartWeapon.mat");
            }
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + Name + ".fbx"), root);
            model.name = "Imported Dart Weapon";
            var renderers = model.GetComponentsInChildren<Renderer>();
            Bounds LocalBounds()
            {
                bool first = true; Bounds bounds = default;
                foreach (var renderer in renderers)
                {
                    var filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null) continue;
                    foreach (var v in filter.sharedMesh.vertices)
                    {
                        var point = root.InverseTransformPoint(filter.transform.TransformPoint(v));
                        if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                        else bounds.Encapsulate(point);
                    }
                }
                return bounds;
            }
            var bounds = LocalBounds();
            Debug.Log("Imported weapon native bounds: " + bounds);
            Vector3 axis = bounds.size.x > bounds.size.y && bounds.size.x > bounds.size.z ? Vector3.right :
                bounds.size.y > bounds.size.z ? Vector3.up : Vector3.forward;
            model.transform.localRotation = Quaternion.FromToRotation(axis, Vector3.forward) * model.transform.localRotation;
            bounds = LocalBounds();
            model.transform.localScale *= 0.9f / bounds.size.z;
            bounds = LocalBounds();
            model.transform.localPosition += new Vector3(-bounds.center.x, -bounds.center.y, 0.18f - bounds.center.z);
            foreach (var renderer in renderers)
            {
                renderer.sharedMaterials = renderer.sharedMaterials.Select(_ => material).ToArray();
                renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
            foreach (var child in model.GetComponentsInChildren<Transform>()) child.gameObject.layer = 2;
            foreach (var collider in model.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
            root.Find("Muzzle").localPosition = new Vector3(0, 0.02f, 0.66f);
            root.Find("Muzzle Flash").localPosition = root.Find("Muzzle").localPosition;
            PrefabUtility.SaveAsPrefabAsset(root.gameObject, Folder + "DartWeapon.prefab");
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene);
            Debug.Log("Imported dart weapon installed; firing controller and projectile retained.");
        }
    }
}
