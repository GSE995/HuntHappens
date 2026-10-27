using System;
using System.Collections.Generic;
using System.Linq;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.SmallGame;
using UnityEngine;
using Object=UnityEngine.Object;
namespace HuntHappens.Editor
{
    public static class CageBaitVisualChecks
    {
        static void Check(bool ok,string label){if(!ok)throw new Exception("Cage bait visual: "+label);}
        static void Model(CageBaitVisual visual,int triangles)
        {
            Check(visual && visual.Model && visual.Model.activeSelf,"model exists");
            Check(visual.Model.GetComponentsInChildren<MeshFilter>(true).Sum(f=>f.sharedMesh.triangles.Length/3)==triangles,"correct Meshy geometry");
            Check(visual.Model.GetComponentsInChildren<Collider>(true).Length==0 && visual.Model.GetComponentsInChildren<Rigidbody>(true).Length==0,"render only");
            var b=WorldScaleSetup.Measure(visual.Model.transform,visual.Model.transform.parent);
            Check(b.size.x<=.321f && b.size.y<=.241f && b.size.z<=.381f && Mathf.Abs(b.min.y)<.002f,"fits and rests on cage floor");
        }
        public static string Run()
        {
            if(!Application.isPlaying)throw new Exception("Play Mode required");
            var inventory=Object.FindAnyObjectByType<PlayerInventory>();var hunt=inventory.GetComponent<RatHunting>();
            var arms=inventory.GetComponentInChildren<FirstPersonArms>();
            var held=arms.MiniatureCage.GetComponent<CageCargoVisual>();
            var objects=new List<GameObject>();
            try
            {
                var kinds=new[]{InventoryKind.Bread,InventoryKind.CheeseSandwich,InventoryKind.Latte};var counts=new[]{1541,1054,1044};
                for(int i=0;i<3;i++)
                {
                    var cage=Object.Instantiate(hunt.cagePrefab,new Vector3(11+i*1.3f,0,-38.5f),Quaternion.identity);objects.Add(cage.gameObject);
                    cage.RestoreCargo(0,hunt.cagePrefab,true,kinds[i]);Model(cage.GetComponent<CageBaitVisual>(),counts[i]);
                    Check(cage.bait.GetComponent<MeshRenderer>()==null,"placeholder removed");
                    var entry=new InventoryEntry{kind=InventoryKind.Cage,cageBaited=true,cageBait=kinds[i]};
                    held.Bind(entry);Model(arms.MiniatureCage.GetComponent<CageBaitVisual>(),counts[i]);
                    var drop=WorldInventoryItem.Spawn(inventory,entry,new Vector3(30,1,30),Quaternion.identity);objects.Add(drop.gameObject);
                    Model(drop.GetComponent<CageBaitVisual>(),counts[i]);
                    if(i<2){Check(cage.TryStealBait(),"theft allowed");Check(!cage.bait.activeSelf,"theft hides bait");cage.RestoreCargo(0,hunt.cagePrefab,true,kinds[i]);}
                    else Check(!cage.TryStealBait(),"latte cannot be stolen");
                    // Switch the existing visual back and forth, rather than testing only a fresh instance.
                    var next=kinds[(i+1)%3];cage.RestoreCargo(0,hunt.cagePrefab,true,next);Model(cage.GetComponent<CageBaitVisual>(),counts[(i+1)%3]);
                    cage.RestoreCargo(0,hunt.cagePrefab,false);Check(!cage.bait.activeSelf,"empty cage hides bait");
                    cage.RestoreCargo(0,hunt.cagePrefab,true,kinds[i]);
                }
                held.Bind(new InventoryEntry{kind=InventoryKind.Cage});Check(!arms.MiniatureCage.GetComponent<CageBaitVisual>().Model.transform.parent.gameObject.activeSelf,"empty carried cage hides bait");
                Capture();
                return "PASSED: all 3 Meshy baits in placed, held and dropped cages; bounds, floor, switches, empty state, theft; no bait physics";
            }
            finally{held.Bind(inventory.Hand);foreach(var go in objects)if(go){go.SetActive(false);Object.Destroy(go);}}
        }
        static void Capture()
        {
            var probe=new GameObject("Cage bait preview");var camera=probe.AddComponent<Camera>();camera.enabled=false;camera.fieldOfView=58;
            camera.transform.position=new Vector3(12.3f,1.6f,-41.2f);camera.transform.LookAt(new Vector3(12.3f,.2f,-38.5f));
            var previous=RenderTexture.active;var target=new RenderTexture(1280,850,24);var png=new Texture2D(1280,850,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;png.ReadPixels(new Rect(0,0,1280,850),0,0);png.Apply();System.IO.File.WriteAllBytes("Logs/Cage-bait-models.png",ImageConversion.EncodeToPNG(png));}
            finally{RenderTexture.active=previous;camera.targetTexture=null;Object.Destroy(target);Object.Destroy(png);Object.Destroy(probe);}
        }
    }
}
