using UnityEngine;
using UnityEngine.UI;

namespace HuntHappens.UI
{
    // Tapered mirrored branches keep the silhouette crisp at any Canvas scale.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class AntlerRewardGraphic : MaskableGraphic
    {
        public float Intensity = 1;
        public float Burst = 1;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            for (int side = -1; side <= 1; side += 2)
            {
                Branch(mesh, side, new Vector2(105,-12), new Vector2(180,15), 15,12);
                Branch(mesh, side, new Vector2(180,15), new Vector2(221,62), 12,9);
                Branch(mesh, side, new Vector2(221,62), new Vector2(235,110), 9,5);
                Branch(mesh, side, new Vector2(235,110), new Vector2(228,145), 5,0);
                Branch(mesh, side, new Vector2(166,10), new Vector2(169,58), 10,4);
                Branch(mesh, side, new Vector2(169,58), new Vector2(150,91), 4,0);
                Branch(mesh, side, new Vector2(205,43), new Vector2(253,51), 9,4);
                Branch(mesh, side, new Vector2(253,51), new Vector2(279,79), 4,0);
                Branch(mesh, side, new Vector2(231,91), new Vector2(266,114), 6,0);
                // Only three short impact rays per side; no full-screen starburst.
                for (int i=0;i<3;i++)
                {
                    float angle = (-24 + i*42)*Mathf.Deg2Rad;
                    Vector2 direction = new Vector2(Mathf.Cos(angle)*side, Mathf.Sin(angle));
                    Segment(mesh, direction*285, direction*(295+Burst*24), 2.5f, 0, new Color(1,0.78f,0.25f,Intensity*Burst));
                }
            }
        }
        void Branch(VertexHelper mesh, int side, Vector2 a, Vector2 b, float widthA, float widthB)
        {
            a.x *= side; b.x *= side;
            Segment(mesh,a,b,widthA+9,widthB+6,new Color(1,0.57f,0.08f,0.10f*Intensity));
            Segment(mesh,a,b,widthA+4,widthB+2,new Color(1,0.68f,0.12f,0.22f*Intensity));
            Segment(mesh,a,b,widthA,widthB,new Color(1,0.79f,0.26f,Intensity));
            Segment(mesh,a,b,widthA*0.35f,widthB*0.35f,new Color(1,0.95f,0.65f,Intensity));
        }
        static void Segment(VertexHelper mesh, Vector2 a, Vector2 b, float wa, float wb, Color tint)
        {
            Vector2 d=(b-a).normalized, n=new Vector2(-d.y,d.x);
            int start=mesh.currentVertCount;
            mesh.AddVert(a+n*wa,tint,Vector2.zero); mesh.AddVert(b+n*wb,tint,Vector2.zero);
            mesh.AddVert(b-n*wb,tint,Vector2.zero); mesh.AddVert(a-n*wa,tint,Vector2.zero);
            mesh.AddTriangle(start,start+1,start+2); mesh.AddTriangle(start,start+2,start+3);
        }
    }
}
