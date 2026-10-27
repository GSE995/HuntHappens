using HuntHappens.Interaction;
using UnityEngine;
namespace HuntHappens.SmallGame
{
    // The same render-only bait display is used by placed, carried and dropped cages.
    public sealed class CageBaitVisual : MonoBehaviour
    {
        [SerializeField] private GameObject breadPrefab, cheesePrefab, lattePrefab;
        [SerializeField] private Transform anchor;
        private GameObject model;
        private InventoryKind shown;
        public GameObject Model => model;
        public void Configure(CageBaitVisual template, Transform target)
        {
            breadPrefab=template.breadPrefab;cheesePrefab=template.cheesePrefab;lattePrefab=template.lattePrefab;anchor=target;
        }
        public void Show(InventoryKind kind)
        {
            if(!anchor)return;
            bool visible=RatBait.IsBait(kind);anchor.gameObject.SetActive(visible);
            if(!visible || (shown==kind && model))return;
            shown=kind;
            if(model){model.SetActive(false);Destroy(model);}
            var source=kind==InventoryKind.Bread?breadPrefab:kind==InventoryKind.CheeseSandwich?cheesePrefab:lattePrefab;
            if(!source)return;
            model=new GameObject("Cage Bait "+kind);model.transform.SetParent(anchor,false);model.layer=anchor.gameObject.layer;
            bool initialized=false;Bounds bounds=default;
            foreach(var filter in source.GetComponentsInChildren<MeshFilter>())
            {
                var renderer=filter.GetComponent<MeshRenderer>();if(!renderer)continue;
                var part=new GameObject(filter.name,typeof(MeshFilter),typeof(MeshRenderer));part.layer=model.layer;part.transform.SetParent(model.transform,false);
                part.transform.localPosition=source.transform.InverseTransformPoint(filter.transform.position);
                part.transform.localRotation=Quaternion.Inverse(source.transform.rotation)*filter.transform.rotation;
                part.transform.localScale=filter.transform.lossyScale;
                part.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;part.GetComponent<MeshRenderer>().sharedMaterials=renderer.sharedMaterials;
                var b=filter.sharedMesh.bounds;
                for(int i=0;i<8;i++)
                {
                    var point=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    point=model.transform.InverseTransformPoint(part.transform.TransformPoint(point));
                    if(!initialized){bounds=new Bounds(point,Vector3.zero);initialized=true;}else bounds.Encapsulate(point);
                }
            }
            if(!initialized)return;
            float scale=Mathf.Min(1,Mathf.Min(.32f/Mathf.Max(.001f,bounds.size.x),Mathf.Min(.24f/Mathf.Max(.001f,bounds.size.y),.38f/Mathf.Max(.001f,bounds.size.z))));
            model.transform.localScale=Vector3.one*scale;
            model.transform.localPosition=-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z)*scale;
        }
    }
}
