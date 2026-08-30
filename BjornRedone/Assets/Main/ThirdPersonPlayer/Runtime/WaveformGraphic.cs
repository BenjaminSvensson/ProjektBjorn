using System;
using UnityEngine;
using UnityEngine.UI;

namespace Bjorn.ThirdPerson
{
    [DisallowMultipleComponent]
    public sealed class WaveformGraphic : MaskableGraphic
    {
        [SerializeField, Range(1f, 6f)] private float minimumBarWidth = 1.5f;
        private float[] samples = Array.Empty<float>();

        public void SetSamples(float[] newSamples)
        {
            samples = newSamples ?? Array.Empty<float>();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect bounds = rectTransform.rect;
            if (samples.Length == 0 || bounds.width <= 0f || bounds.height <= 0f)
            {
                AddQuad(vertexHelper, new Rect(bounds.xMin, -0.75f, bounds.width, 1.5f), color * 0.35f);
                return;
            }

            float step = bounds.width / samples.Length;
            float barWidth = Mathf.Max(minimumBarWidth, step * 0.72f);
            float halfHeight = bounds.height * 0.46f;
            for (int i = 0; i < samples.Length; i++)
            {
                float amplitude = Mathf.Max(0.012f, Mathf.Abs(samples[i])) * halfHeight;
                float x = bounds.xMin + (i + 0.5f) * step;
                AddQuad(vertexHelper, new Rect(x - barWidth * 0.5f, -amplitude, barWidth, amplitude * 2f), color);
            }
        }

        private static void AddQuad(VertexHelper helper, Rect rect, Color tint)
        {
            int start = helper.currentVertCount;
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = tint;
            vertex.position = new Vector3(rect.xMin, rect.yMin);
            helper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMin, rect.yMax);
            helper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMax);
            helper.AddVert(vertex);
            vertex.position = new Vector3(rect.xMax, rect.yMin);
            helper.AddVert(vertex);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start + 2, start + 3, start);
        }
    }
}
