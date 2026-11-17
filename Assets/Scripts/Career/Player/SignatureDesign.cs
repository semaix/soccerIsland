using System;
using System.Collections.Generic;

namespace SoccerIsland.Career
{
    /// <summary>サインの1画。座標は 0〜1 に正規化（左下 0,0）。</summary>
    [Serializable]
    public class SignatureStroke
    {
        public List<float> xs = new List<float>();
        public List<float> ys = new List<float>();

        public int Count => Math.Min(xs.Count, ys.Count);

        public void Add(float x, float y)
        {
            xs.Add(x);
            ys.Add(y);
        }
    }

    /// <summary>
    /// 自作サイン。UIで指／マウスでなぞった線を保存する。
    /// 端末内のセーブにだけ保存し、外部には送らない。
    /// </summary>
    [Serializable]
    public class SignatureDesign
    {
        public const int MaxStrokes = 12;
        public const int MaxPointsPerStroke = 256;
        public const int MaxTotalPoints = 1024;

        public List<SignatureStroke> strokes = new List<SignatureStroke>();

        public bool IsEmpty => strokes.Count == 0;

        public string Validate()
        {
            if (strokes.Count == 0) return "サインが描かれていません";
            if (strokes.Count > MaxStrokes) return $"画数が多すぎます（{MaxStrokes}画まで）";
            int total = 0;
            foreach (var s in strokes)
            {
                if (s.Count < 2) return "短すぎる線があります";
                if (s.Count > MaxPointsPerStroke) return "長すぎる線があります";
                for (int i = 0; i < s.Count; i++)
                    if (s.xs[i] < 0f || s.xs[i] > 1f || s.ys[i] < 0f || s.ys[i] > 1f) return "枠からはみ出しています";
                total += s.Count;
            }
            return total > MaxTotalPoints ? "線が多すぎます" : null;
        }

        public bool IsValid => Validate() == null;

        /// <summary>
        /// 描画中の線を間引いて保存できる大きさにする（近すぎる点を捨てる）。
        /// </summary>
        public static SignatureStroke Simplify(SignatureStroke raw, float minDistance = 0.01f)
        {
            var result = new SignatureStroke();
            if (raw.Count == 0) return result;
            float lx = Clamp01(raw.xs[0]), ly = Clamp01(raw.ys[0]);
            result.Add(lx, ly);
            for (int i = 1; i < raw.Count; i++)
            {
                float x = Clamp01(raw.xs[i]), y = Clamp01(raw.ys[i]);
                float dx = x - lx, dy = y - ly;
                bool last = i == raw.Count - 1;
                if (dx * dx + dy * dy < minDistance * minDistance && !last) continue;
                if (result.Count >= MaxPointsPerStroke) break;
                result.Add(x, y);
                lx = x; ly = y;
            }
            return result;
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
