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
            // 雨の日は荒れて番狂わせが起きやすく、雪の日は点が入りにくい
            var w = WeatherSystem.WeatherOf(_db.seed, m.day);
            float diff = (home - away) / (w == Weather.Rain ? 22f : 15f);
            float goals = baseGoals * (w == Weather.Snow ? 0.75f : 1f);
            m.homeScore = Poisson(goals * (float)Math.Exp(diff));
            m.awayScore = Poisson(goals * (float)Math.Exp(-diff));
            m.homeScorers = PickScorers(m.homeLineup, m.homeScore, m.IsUnderground);
            m.awayScorers = PickScorers(m.awayLineup, m.awayScore, m.IsUnderground);
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

        /// <summary>得点者：シュートがうまいFWほど点を取る。</summary>
        List<int> PickScorers(List<int> lineup, int goals, bool underground)
        {
            var result = new List<int>();
            if (lineup.Count == 0 || goals <= 0) return result;
            var w = new float[lineup.Count];
            for (int i = 0; i < lineup.Count; i++)
            {
                var n = _db.Get(lineup[i]);
                if (n == null) continue;
                float shoot = underground ? n.abilities.shooting : n.abilities.shooting * n.publicPerformanceRate;
                float pos = n.position == Position.FW ? 3f : n.position == Position.MF ? 1.5f : n.position == Position.DF ? 0.5f : 0.02f;
                w[i] = Math.Max(0.01f, (float)Math.Pow(shoot / 50f, 3) * pos);
            }
            for (int g = 0; g < goals; g++) result.Add(lineup[_rng.WeightedIndex(w)]);
            return result;
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
