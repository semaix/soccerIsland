using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Match
{
    /// <summary>性格バランスの診断結果。UIでは reasons をそのまま見せる。</summary>
    public class ChemistryReport
    {
        /// <summary>-100（最悪）〜 100（最高）。</summary>
        public float score;
        public List<string> reasons = new List<string>();
        /// <summary>カードの出やすさの上乗せ。</summary>
        public float cardRisk;

        /// <summary>連携（パス・ワンツー）への補正。</summary>
        public float Teamwork => score / 400f;
    }

    /// <summary>
    /// チームの性格バランスを診断する。バランスが取れているほど雰囲気が良い。
    ///
    /// ・内向的／外交的／根暗が偏らないほど良い。根暗が多いと暗く、外交的ばかりだと騒がしい
    /// ・エゴイストは1人ならエース、2人以上だと衝突
    /// ・献身的がエース（エゴイスト）を支えると噛み合う
    /// ・センス的は献身的がいないとまとまらない
    /// ・ヒステリー・ファイターが多いとカードが増える
    /// ・ムードメーカーは1〜2人が理想、多すぎるとうるさい
    /// </summary>
    public static class TeamChemistry
    {
        public static ChemistryReport Evaluate(IList<MatchPlayer> players)
        {
            var r = new ChemistryReport();
            int n = 0, intro = 0, extro = 0, gloomy = 0;
            int ego = 0, devoted = 0, sense = 0, hyst = 0, fighter = 0, mood = 0, god = 0;
            foreach (var p in players)
            {
                if (!p.CanPlay || p.isProtagonist) continue; // 主人公は診断の対象外
                n++;
                switch (p.personality)
                {
                    case Personality.Introvert: intro++; break;
                    case Personality.Extrovert: extro++; break;
                    default: gloomy++; break;
                }
                switch (p.soccerPersonality)
                {
                    case SoccerPersonality.Egoist: ego++; break;
                    case SoccerPersonality.Devoted: devoted++; break;
                    case SoccerPersonality.Sense: sense++; break;
                    case SoccerPersonality.Hysteric: hyst++; break;
                    case SoccerPersonality.Fighter: fighter++; break;
                    case SoccerPersonality.MoodMaker: mood++; break;
                    case SoccerPersonality.God: god++; break;
                    case SoccerPersonality.Gifted: sense++; break;
                }
            }
            if (n == 0) return r;

            // ---- 普段の性格のバランス ----
            int max = Math.Max(intro, Math.Max(extro, gloomy));
            int min = Math.Min(intro, Math.Min(extro, gloomy));
            float balance = 1f - (float)(max - min) / n;
            Add(r, (balance - 0.5f) * 40f, balance >= 0.7f ? "内向的・外交的・根暗のバランスが良い" : balance < 0.4f ? "性格が偏っている" : null);
            if (gloomy * 3 > n) Add(r, -15f, "根暗が多く、ベンチが暗い");
            if (extro == n && n >= 5) Add(r, -6f, "外交的ばかりで騒がしく、集中力がない");

            // ---- サッカーの性格 ----
            if (ego == 1) Add(r, 6f, "エースがいる");
            else if (ego >= 2) Add(r, -12f * (ego - 1), "エゴイスト同士が衝突している");
            if (ego >= 1 && devoted >= 2) Add(r, 8f, "献身的な選手がエースを支えている");
            Add(r, Math.Min(devoted, 4) * 3f, null);
            if (sense >= 1 && devoted >= 1) Add(r, 5f, "センスのある選手を周りが活かしている");
            if (sense >= 3 && devoted == 0) Add(r, -8f, "センス的な選手ばかりでまとまりがない");
            if (hyst >= 2) { Add(r, -10f, "ヒステリー同士がぶつかる"); }
            r.cardRisk += 0.03f * hyst;
            if (fighter >= 1 && fighter <= 2) Add(r, 4f, "体を張れる選手がいる");
            if (fighter >= 4) { Add(r, -10f, "ファイターが多く荒れやすい"); r.cardRisk += 0.05f; }
            if (mood >= 1 && mood <= 2) Add(r, 8f, "ムードメーカーが盛り上げている");
            if (mood >= 3) Add(r, -5f, "ムードメーカーが多すぎてうるさい");
            if (god >= 1) Add(r, 10f, "神がいる");

            r.score = Math.Max(-100f, Math.Min(100f, r.score));
            return r;
        }

        static void Add(ChemistryReport r, float v, string reason)
        {
            r.score += v;
            if (reason != null) r.reasons.Add((v >= 0 ? "＋ " : "－ ") + reason);
        }
    }
}
