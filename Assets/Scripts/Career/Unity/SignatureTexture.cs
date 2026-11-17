using UnityEngine;

namespace SoccerIsland.Career.UnityIntegration
{
    /// <summary>
    /// 自作サインを Texture2D に描く（色紙・ユニフォーム・ボールに貼る用）。
    /// 端末内で描画するだけで、画像を外部に送ったり保存したりはしない。
    /// </summary>
    public static class SignatureTexture
    {
        public static Texture2D Render(SignatureDesign design, int width, int height, Color ink, int thickness = 3)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            var clear = new Color32(0, 0, 0, 0);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = clear;

            Color32 c = ink;
            if (design != null)
            {
                foreach (var s in design.strokes)
                    for (int i = 1; i < s.Count; i++)
                        Line(pixels, width, height,
                             s.xs[i - 1] * (width - 1), s.ys[i - 1] * (height - 1),
                             s.xs[i] * (width - 1), s.ys[i] * (height - 1), thickness, c);
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        static void Line(Color32[] px, int w, int h, float x0, float y0, float x1, float y1, int thickness, Color32 c)
        {
            float dist = Mathf.Max(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0));
            int steps = Mathf.Max(1, Mathf.CeilToInt(dist));
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                Dot(px, w, h, Mathf.RoundToInt(Mathf.Lerp(x0, x1, t)), Mathf.RoundToInt(Mathf.Lerp(y0, y1, t)), thickness, c);
            }
        }

        static void Dot(Color32[] px, int w, int h, int cx, int cy, int r, Color32 c)
        {
            for (int y = -r; y <= r; y++)
                for (int x = -r; x <= r; x++)
                {
                    if (x * x + y * y > r * r) continue;
                    int px_ = cx + x, py = cy + y;
                    if (px_ < 0 || py < 0 || px_ >= w || py >= h) continue;
                    px[py * w + px_] = c;
                }
        }
    }
}
