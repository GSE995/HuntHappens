using UnityEngine;
using UnityEngine.UI;
namespace HuntHappens.UI
{
    // A light, irregular ink outline rather than perfectly straight UI rectangles.
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class SketchPanel : MaskableGraphic
    {
        public Color ink = new Color(0.9f, 0.86f, 0.69f, 0.65f);
        public float thickness = 1.5f;
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear(); var r = rectTransform.rect;
            const int count = 40;
            var points = new Vector2[count];
            for (int i = 0; i < count; i++)
            {
                float t = (i % 10) / 10f; int edge = i / 10;
                Vector2 p = edge == 0 ? new Vector2(Mathf.Lerp(r.xMin, r.xMax, t), r.yMin) :
                    edge == 1 ? new Vector2(r.xMax, Mathf.Lerp(r.yMin, r.yMax, t)) :
                    edge == 2 ? new Vector2(Mathf.Lerp(r.xMax, r.xMin, t), r.yMax) :
                    new Vector2(r.xMin, Mathf.Lerp(r.yMax, r.yMin, t));
                p += new Vector2(Mathf.Sin(i * 7.3f), Mathf.Cos(i * 5.7f)) * 1.4f;
                points[i] = p;
            }
            vh.AddVert(r.center, color, Vector2.zero);
            for (int i = 0; i < count; i++) vh.AddVert(points[i], color, Vector2.zero);
            for (int i = 0; i < count; i++) vh.AddTriangle(0, i + 1, (i + 1) % count + 1);
            for (int i = 0; i < count; i++)
            {
                Vector2 a = points[i], b = points[(i + 1) % count], d = (b - a).normalized;
                Vector2 n = new Vector2(-d.y, d.x) * thickness;
                int start = vh.currentVertCount;
                vh.AddVert(a, ink, Vector2.zero); vh.AddVert(b, ink, Vector2.zero);
                vh.AddVert(b+n, ink, Vector2.zero); vh.AddVert(a+n, ink, Vector2.zero);
                vh.AddTriangle(start,start+1,start+2); vh.AddTriangle(start,start+2,start+3);
            }
        }
    }
}
