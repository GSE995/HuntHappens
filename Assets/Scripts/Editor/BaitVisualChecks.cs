using System;
using System.Linq;
using System.Reflection;
using HuntHappens.Interaction;
using HuntHappens.Player;
using HuntHappens.Shop;
using HuntHappens.SmallGame;
using UnityEngine;
using Object=UnityEngine.Object;
namespace HuntHappens.Editor
{
    public static class BaitVisualChecks
    {
        static readonly MethodInfo Draw=typeof(PlayerInventory).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic);
        static readonly FieldInfo Held=typeof(PlayerInventory).GetField("handVisual",BindingFlags.Instance|BindingFlags.NonPublic);
        static void Check(bool ok,string message){if(!ok)throw new Exception("Bait visual: "+message);}
        static int Triangles(GameObject go)=>go.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
        public static string Run()
        {
            if(!Application.isPlaying)throw new InvalidOperationException("Play Mode required");
            var inventory=Object.FindAnyObjectByType<PlayerInventory>();var hunt=inventory.GetComponent<RatHunting>();
            var input=inventory.GetComponent<PrototypeInput>();var camera=inventory.GetComponentInChildren<Camera>();
            bool flag=ShopProgress.InventoryChecksRunning,blocked=input.GameplayBlocked;
            var existing=Object.FindObjectsByType<WorldInventoryItem>().ToHashSet();
            try
            {
                ShopProgress.InventoryChecksRunning=true;input.SetGameplayBlocked(false);
                var kinds=new[]{InventoryKind.Bread,InventoryKind.CheeseSandwich,InventoryKind.Latte};var counts=new[]{1541,1054,1044};
                for(int i=0;i<3;i++)
                {
                    ShopProgress.Reset();inventory.Reload();hunt.Reload();hunt.shop.ReloadProgress();
                    Check(inventory.Receive(new InventoryEntry{kind=kinds[i]}),"receive "+kinds[i]);Draw.Invoke(inventory,null);
                    var held=(GameObject)Held.GetValue(inventory);
                    Check(held && held.activeSelf && Triangles(held)==counts[i],"held Meshy geometry "+kinds[i]);
                    Check(!held.GetComponent<Collider>().enabled && held.GetComponent<Rigidbody>().isKinematic,"held physics disabled");
                    Check(held.transform.localScale==Vector3.one,"natural held scale");
                    Check(held.GetComponentsInChildren<MeshRenderer>().All(r=>r.sharedMaterials.All(m=>m && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap"))),"textures assigned");
                    var arms=inventory.GetComponentInChildren<FirstPersonArms>();
                    var hero=arms.GetComponentInChildren<HeroFirstPersonArms>();
                    Check(arms.Mode==FirstPersonArms.VisualMode.Bait,"bait pose selected");
                    arms.Draw(new Vector3(-.28f,-.45f,.57f),Quaternion.identity,new Vector3(.34f,-.28f,.43f),Quaternion.identity,true,0);
                    typeof(HeroFirstPersonArms).GetMethod("LateUpdate",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(hero,null);
                    var grip=held.GetComponent<BaitHandAttachment>();Check(grip!=null,"palm attachment exists");grip.UpdatePose();
                    Check(Vector3.Distance(held.transform.TransformPoint(grip.ItemContact),grip.PalmPosition)<.001f,"palm contact within 1mm");
                    var wrist=hero.RightWrist;var oldPosition=wrist.position;var oldRotation=wrist.rotation;
                    var oldItem=held.transform.position;var step=new Vector3(.03f,.02f,-.015f);
                    wrist.position+=step;grip.UpdatePose();Check(Vector3.Distance(held.transform.position,oldItem+step)<.001f,"follows wrist motion");
                    wrist.rotation=Quaternion.AngleAxis(15,camera.transform.forward)*wrist.rotation;grip.UpdatePose();
                    Check(Vector3.Distance(held.transform.TransformPoint(grip.ItemContact),grip.PalmPosition)<.001f,"contact maintained during rotation");
                    wrist.SetPositionAndRotation(oldPosition,oldRotation);grip.UpdatePose();
                    Capture(camera,"Logs/Held-"+kinds[i]+".png");
                    Check(inventory.SelectSlot(0),"stow");Check(inventory.SelectSlot(0),"equip");Draw.Invoke(inventory,null);
                    Check(Triangles((GameObject)Held.GetValue(inventory))==counts[i],"slot model");
                    Check(inventory.DropHand(),"drop");
                    var dropped=Object.FindObjectsByType<WorldInventoryItem>().FirstOrDefault(x=>!existing.Contains(x)&&x.enabled&&x.Entry?.kind==kinds[i]);
                    Check(dropped && Triangles(dropped.gameObject)==counts[i],"dropped model");
                    Check(dropped.GetComponent<Collider>().enabled && !dropped.GetComponent<Rigidbody>().isKinematic,"drop physics");
                    Check(dropped.Take(inventory),"pickup");Draw.Invoke(inventory,null);
                    Check(Triangles((GameObject)Held.GetValue(inventory))==counts[i],"picked model");
                    inventory.Reload();hunt.Reload();Check(inventory.Count(kinds[i])==1,"saved quantity");
                    Check(inventory.SelectSlot(0),"stow after reload");inventory.Reload();Check(inventory.SelectSlot(0),"equip after reload");Draw.Invoke(inventory,null);
                    Check(Triangles((GameObject)Held.GetValue(inventory))==counts[i],"reloaded model");
                }
                return "PASSED: bread 1541, cheese 1054, latte 1044; held meshes/textures/scale, palm contact and wrist motion, slots, Q/E, physics, saved reload";
            }
            finally
            {
                foreach(var drop in Object.FindObjectsByType<WorldInventoryItem>())if(!existing.Contains(drop))Object.Destroy(drop.gameObject);
                ShopProgress.Reset();ShopProgress.InventoryChecksRunning=flag;inventory.Reload();hunt.Reload();hunt.shop.ReloadProgress();input.SetGameplayBlocked(blocked);
            }
        }
        static void Capture(Camera camera,string path)
        {
            var previousTarget=camera.targetTexture;var previousActive=RenderTexture.active;
            var target=new RenderTexture(1200,850,24);var png=new Texture2D(1200,850,TextureFormat.RGB24,false);
            try{camera.targetTexture=target;camera.Render();RenderTexture.active=target;png.ReadPixels(new Rect(0,0,1200,850),0,0);png.Apply();System.IO.File.WriteAllBytes(path,ImageConversion.EncodeToPNG(png));}
            finally{camera.targetTexture=previousTarget;RenderTexture.active=previousActive;Object.Destroy(target);Object.Destroy(png);}
        }
    }
}
