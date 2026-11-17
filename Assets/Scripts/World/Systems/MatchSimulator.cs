using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.World
{
    /// <summary>
    /// プレイヤーが関わらない試合の結果を簡易計算する（プレイヤーが出る試合は試合シーン側で処理する）。
    /// 裏の試合では「裏で本気を出す」選手が本来の実力を出す。
    /// </summary>
    public class MatchSimulator
    {
        public float baseGoals = 1.4f;
        /// <summary>島代表の相手（本土選抜）の強さ。</summary>
        public float mainlandStrength = 65f;

        readonly NPCDatabase _db;
        readonly MarketValueSystem _market;
        readonly SeededRandom _rng;

        public MatchSimulator(NPCDatabase db, MarketValueSystem market, SeededRandom rng)
        {
            _db = db;
            _market = market;
            _rng = rng;
        }

        public void Play(Match m)
        {
            if (m.played) return;
            float home = Strength(m.homeLineup, m.IsUnderground, mainlandStrength);
            float away = Strength(m.awayLineup, m.IsUnderground, mainlandStrength);
            m.homeScore = Poisson(baseGoals * (float)Math.Exp((home - away) / 15f));
            m.awayScore = Poisson(baseGoals * (float)Math.Exp((away - home) / 15f));
            m.played = true;

            // 勝ったチームは少し話題になる。島代表・地域対抗は注目度が高い
            float hype = m.category == MatchCategory.IslandSelect ? 0.06f
                       : m.category == MatchCategory.RegionalDerby ? 0.03f
                       : m.IsUnderground || m.category == MatchCategory.PrisonLeague ? 0f : 0.01f;
            if (hype <= 0f || m.homeScore == m.awayScore) return;
            var winners = m.homeScore > m.awayScore ? m.homeLineup : m.awayLineup;
            foreach (var id in winners)
            {
                var n = _db.Get(id);
                if (n != null) _market.AddHype(n, hype, "won_match_" + m.id);
            }
        }

        float Strength(List<int> lineup, bool underground, float fallback)
        {
            if (lineup.Count == 0) return fallback;
            float sum = 0f;
            foreach (var id in lineup)
            {
                var n = _db.Get(id);
                if (n != null) sum += underground ? n.Overall : n.PublicOverall;
            }
            return sum / lineup.Count;
        }

        int Poisson(float lambda)
        {
            double l = Math.Exp(-Math.Min(lambda, 8f));
            int k = 0;
            double p = 1.0;
            do { k++; p *= _rng.NextDouble(); } while (p > l);
            return k - 1;
        }
    }
}
