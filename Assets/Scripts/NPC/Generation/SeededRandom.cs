using System;
using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    /// <summary>
    /// プラットフォーム・.NETバージョンに依存しない決定的乱数（xorshift128+）。
    /// 同じシードなら必ず同じ島が生成される（周回プレイ・デバッグ用）。
    /// </summary>
    [Serializable]
    public class SeededRandom
    {
        public ulong s0;
        public ulong s1;

        public SeededRandom() : this(0) { }

        public SeededRandom(int seed)
        {
            ulong x = (ulong)(uint)seed + 0x9E3779B97F4A7C15UL;
            s0 = SplitMix(ref x);
            s1 = SplitMix(ref x);
            if (s0 == 0 && s1 == 0) s1 = 1;
        }

        static ulong SplitMix(ref ulong x)
        {
            x += 0x9E3779B97F4A7C15UL;
            ulong z = x;
            z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
            z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
            return z ^ (z >> 31);
        }

        public ulong NextULong()
        {
            ulong a = s0, b = s1;
            s0 = b;
            a ^= a << 23;
            s1 = a ^ b ^ (a >> 17) ^ (b >> 26);
            return s1 + b;
        }

        /// <summary>[0,1)</summary>
        public double NextDouble() => (NextULong() >> 11) * (1.0 / (1UL << 53));

        public float Value => (float)NextDouble();

        /// <summary>[min, maxExclusive)</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            return min + (int)(NextDouble() * (maxExclusive - min));
        }

        /// <summary>[min, max]</summary>
        public float Range(float min, float max) => min + (float)NextDouble() * (max - min);

        public bool Chance(float probability) => NextDouble() < probability;

        /// <summary>正規分布（Box-Muller）。</summary>
        public float Gaussian(float mean, float stdDev)
        {
            double u1 = 1.0 - NextDouble();
            double u2 = NextDouble();
            double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return (float)(mean + z * stdDev);
        }

        public int GaussianInt(float mean, float stdDev, int min, int max)
        {
            int v = (int)Math.Round(Gaussian(mean, stdDev));
            return v < min ? min : (v > max ? max : v);
        }

        public T Pick<T>(IReadOnlyList<T> list) => list[Range(0, list.Count)];

        /// <summary>重み付き抽選。weights の長さは候補数と同じ。</summary>
        public int WeightedIndex(IReadOnlyList<float> weights)
        {
            float total = 0f;
            for (int i = 0; i < weights.Count; i++) total += Math.Max(0f, weights[i]);
            if (total <= 0f) return Range(0, weights.Count);
            float r = Value * total;
            for (int i = 0; i < weights.Count; i++)
            {
                r -= Math.Max(0f, weights[i]);
                if (r < 0f) return i;
            }
            return weights.Count - 1;
        }

        public T WeightedPick<T>(IReadOnlyList<KeyValuePair<T, float>> table)
        {
            var w = new float[table.Count];
            for (int i = 0; i < table.Count; i++) w[i] = table[i].Value;
            return table[WeightedIndex(w)].Key;
        }

        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Range(0, i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }
}
