using UnityEngine;
using UnityEngine.UI;

namespace HuntHappens.UI
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RatRewardGraphic : MaskableGraphic
    {
        public float Intensity = 1;
        public float Wobble;
        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            for (int side = -1; side <= 1; side += 2)
            {
                // Each ear pivots at its lower edge; the two sides sway in opposite directions.
                Vector2 pivot = new Vector2(side * 173, 44);
                float angle = Wobble * side * Mathf.Deg2Rad;
                Vector2 center = pivot + Rotate(new Vector2(0, 48), angle);
                Disc(mesh, center, 56, 62, angle, new Color(1,0.65f,0.12f,0.10f*Intensity));
                Disc(mesh, center, 51, 57, angle, new Color(1,0.79f,0.28f,Intensity));
                Disc(mesh, center, 43, 49, angle, new Color(0.48f,0.27f,0.16f,Intensity));
                Disc(mesh, center + new Vector2(0,1), 38, 44, angle, new Color(0.83f,0.48f,0.43f,Intensity));
                for (int i=0;i<3;i++)
                {
                    Vector2 a = new Vector2(side*245, 6-i*20);
                    Vector2 b = new Vector2(side*(296+(i==1?9:0)), 25-i*39);
                    Stroke(mesh,a,b,5,new Color(1,0.64f,0.1f,0.12f*Intensity));
                    Stroke(mesh,a,b,2,new Color(1,0.83f,0.39f,Intensity));
                }
            }
        }
        static Vector2 Rotate(Vector2 p,float angle) => new Vector2(p.x*Mathf.Cos(angle)-p.y*Mathf.Sin(angle),p.x*Mathf.Sin(angle)+p.y*Mathf.Cos(angle));
        static void Disc(VertexHelper mesh, Vector2 center,float rx,float ry,float angle,Color color)
        {
            const int steps=32;
            int first=mesh.currentVertCount;
            mesh.AddVert(center,color,Vector2.zero);
            for(int i=0;i<steps;i++)
            {
                float a=i*Mathf.PI*2/steps;
                mesh.AddVert(center+Rotate(new Vector2(Mathf.Cos(a)*rx,Mathf.Sin(a)*ry),angle),color,Vector2.zero);
            }
            for(int i=0;i<steps;i++)mesh.AddTriangle(first,first+1+i,first+1+(i+1)%steps);
        }
        static void Stroke(VertexHelper mesh,Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x)*width;
            int first=mesh.currentVertCount;
            mesh.AddVert(a+n,color,Vector2.zero);mesh.AddVert(b+n*0.55f,color,Vector2.zero);
            mesh.AddVert(b-n*0.55f,color,Vector2.zero);mesh.AddVert(a-n,color,Vector2.zero);
            mesh.AddTriangle(first,first+1,first+2);mesh.AddTriangle(first,first+2,first+3);
        }
    }
}
