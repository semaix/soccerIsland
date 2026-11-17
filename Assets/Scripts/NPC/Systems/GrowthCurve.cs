using System;

namespace SoccerIsland.NPC
{
    /// <summary>
    /// 成長タイプと年齢から「潜在能力のうち何割が発揮されているか」(0〜1) を返す。
    /// 下手な選手が将来めちゃくちゃ上手くなる（覚醒型・晩成）のはここで表現する。
    /// </summary>
    public static class GrowthCurve
    {
        const float ChildhoodStartAge = 6f;
        const float ChildhoodLevel = 0.08f;
        const float Floor = 0.15f;

        struct Shape
        {
            public float peakStart, peakEnd, declinePerYear, rampExponent;
            public Shape(float ps, float pe, float d, float exp) { peakStart = ps; peakEnd = pe; declinePerYear = d; rampExponent = exp; }
        }

        static Shape ShapeOf(GrowthType type)
        {
            switch (type)
            {
                case GrowthType.Early:     return new Shape(20f, 25f, 0.055f, 0.7f);
                case GrowthType.Late:      return new Shape(30f, 34f, 0.040f, 1.4f);
                case GrowthType.Longevity: return new Shape(26f, 35f, 0.025f, 1.0f);
                default:                   return new Shape(25f, 29f, 0.045f, 1.0f);
            }
        }

        public static float Evaluate(GrowthType type, int age, int awakeningAge = 0)
        {
            if (type == GrowthType.Awakening) return EvaluateAwakening(age, awakeningAge);
            return EvaluateShape(ShapeOf(type), age);
        }

        static float EvaluateShape(Shape s, float age)
        {
            if (age <= ChildhoodStartAge) return ChildhoodLevel;
            if (age < s.peakStart)
            {
                float t = (age - ChildhoodStartAge) / (s.peakStart - ChildhoodStartAge);
                return ChildhoodLevel + (1f - ChildhoodLevel) * (float)Math.Pow(t, s.rampExponent);
            }
            if (age <= s.peakEnd) return 1f;
            return Math.Max(Floor, 1f - (age - s.peakEnd) * s.declinePerYear);
        }

        /// <summary>覚醒前は普通型の4割程度で頭打ち。覚醒すると数年で一気にピークへ。</summary>
        static float EvaluateAwakening(int age, int awakeningAge)
        {
            if (awakeningAge <= 0) awakeningAge = 24;
            if (age < awakeningAge)
            {
                float normal = EvaluateShape(ShapeOf(GrowthType.Normal), age);
                return Math.Min(0.4f, normal * 0.45f);
            }
            float years = age - awakeningAge;
            float peakEnd = awakeningAge + 8f;
            if (age <= peakEnd) return Math.Min(1f, 0.7f + years * 0.15f);
            return Math.Max(Floor, 1f - (age - peakEnd) * 0.04f);
        }

        /// <summary>努力度による発揮率。努力0でも55%、努力100で100%。</summary>
        public static float EffortFactor(int effort) => 0.55f + 0.45f * Math.Max(0, Math.Min(100, effort)) / 100f;

        /// <summary>隠し能力の現在値を、到達上限・年齢・努力から再計算する。</summary>
        public static void RecalculateAbilities(NPCData npc)
        {
            float curve = Evaluate(npc.growthType, npc.age, npc.awakeningAge);
            float effort = EffortFactor(npc.effort);
            for (int i = 0; i < NPCAbilities.StatCount; i++)
            {
                int cap = npc.statPotential[i];
                // メンタルは年齢でほぼ衰えない
                float c = i == 8 ? Math.Max(curve, Math.Min(1f, npc.age / 30f)) : curve;
                // スピード・スタミナは衰えが早い
                if ((i == 6 || i == 7) && npc.age > 30) c *= 0.92f;
                npc.abilities[i] = (int)Math.Round(5 + (cap - 5) * c * effort);
            }
            if (npc.growthType == GrowthType.Awakening)
                npc.hasAwakened = npc.age >= npc.awakeningAge;
        }
    }
}
