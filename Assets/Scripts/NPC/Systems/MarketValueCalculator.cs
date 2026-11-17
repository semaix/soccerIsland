using System;

namespace SoccerIsland.NPC
{
    /// <summary>
    /// 市場価値・年俸の計算式。
    /// 市場は「表で見えている実力」しか知らないので、覚醒前の覚醒型や裏で本気を出すタイプは安く評価される
    /// ＝ スカウトの掘り出し物になる。
    /// </summary>
    public static class MarketValueCalculator
    {
        public const long MinimumValue = 10000;   // 1万円
        public const long RoundUnit = 10000;      // 1万円単位

        /// <summary>市場が認識している実力（総合値相当）。</summary>
        public static float PerceivedOverall(NPCData npc)
        {
            float current = npc.PublicOverall;
            // 若いほど将来性が評価される。ただし市場が読めるのは才能の半分程度
            float youth = Clamp01((27f - npc.age) / 13f);
            float expectedPotential = npc.growthType == GrowthType.Awakening && !npc.hasAwakened
                ? current + 5f                    // 覚醒は誰にも予想できない
                : npc.potential * npc.publicPerformanceRate;
            float upside = Math.Max(0f, expectedPotential - current);
            return current + upside * youth * 0.5f;
        }

        /// <summary>話題性(hype)を含まない基本の市場価値。</summary>
        public static double BaseValue(NPCData npc)
        {
            float perceived = PerceivedOverall(npc);
            // 総合30で約10万円、50で約140万円、70で約2000万円、85で約1.5億円、95で約6億円
            double value = 100000 * Math.Exp((perceived - 30f) / 7.5f);

            // 年齢：30歳を過ぎると下がる。子供は契約できないので割安
            if (npc.age > 30) value *= Math.Max(0.15, 1.0 - (npc.age - 30) * 0.07);
            if (npc.age < 15) value *= 0.6;

            // 知名度
            value *= 0.8 + npc.fame / 100.0 * 0.6;

            value *= PersonalityFactor(npc.soccerPersonality);

            // 服役中は出所まで使えない
            if (npc.isIncarcerated) value *= 0.4;
            return value;
        }

        public static double PersonalityFactor(SoccerPersonality p)
        {
            switch (p)
            {
                case SoccerPersonality.Egoist: return 1.05;
                case SoccerPersonality.Sense: return 1.10;
                case SoccerPersonality.Hysteric: return 0.85;
                case SoccerPersonality.Fighter: return 0.95;
                case SoccerPersonality.Devoted: return 0.95;
                case SoccerPersonality.God: return 1.6;
                case SoccerPersonality.Gifted: return 1.3;
                case SoccerPersonality.Phantom: return 0.7; // 誰も実力を知らない
                default: return 1.0;
            }
        }

        /// <summary>話題性と日々の揺らぎ(noise)込みの市場価値。</summary>
        public static long Calculate(NPCData npc, float noise = 0f)
        {
            double v = BaseValue(npc) * (1.0 + npc.hype) * (1.0 + noise);
            return RoundValue(v);
        }

        public static long RoundValue(double v)
        {
            long r = (long)Math.Round(v / RoundUnit) * RoundUnit;
            return Math.Max(MinimumValue, r);
        }

        /// <summary>サッカーの年俸。協会登録者は市場価値の数%、裏の人間は現金払いで割高。</summary>
        public static long CalculateSalary(NPCData npc)
        {
            if (npc.clubId < 0 || npc.age < 16 || npc.isIncarcerated) return 0;
            double rate;
            switch (npc.soccerPersonality)
            {
                case SoccerPersonality.Egoist: rate = 0.12; break;
                case SoccerPersonality.Devoted: rate = 0.05; break;
                case SoccerPersonality.God: rate = 0.15; break;
                default: rate = 0.08; break;
            }
            if (!npc.registeredWithAssociation) rate *= 1.6;
            long s = (long)Math.Round(npc.marketValue * rate / RoundUnit) * RoundUnit;
            return Math.Max(0, s);
        }

        static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
