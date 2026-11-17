using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    public enum FameEventType
    {
        GreatMatch,        // 大活躍
        GoalCelebration,   // ゴールパフォーマンス
        ProvocativeCelebration, // 挑発パフォーマンス（ゴシップのネタ）
        FanServiceGood,    // 神対応
        FanServiceBad,     // 塩対応
        FameMilestone,     // 知名度の節目
        MatchFixingExposed, // 八百長がばれた
        TournamentWon,      // 大会優勝
        RomanceRumor,       // デート目撃
        RomanceRevealed,    // 熱愛発覚
        LoanSharkBusted,    // 闇金摘発
    }

    public struct FameEvent
    {
        public FameEventType type;
        public int day;
        public int npcId;
        public float fameDelta;
        public string text;
    }

    /// <summary>
    /// 主人公の知名度・市場価値。試合での活躍・ゴールパフォーマンス・ファン対応で変わる。
    /// 試合本編はまだないので、試合シーンから RecordMatch / RecordGoalCelebration を呼ぶ想定。
    /// </summary>
    public class FameSystem
    {
        public int inactivityGraceDays = 14;
        public float inactivityDecayPerDay = 0.05f;

        static readonly int[] Milestones = { 20, 40, 60, 80, 95 };

        readonly PlayerState _player;
        readonly List<FameEvent> _events;
        readonly Func<int> _today;

        public FameSystem(PlayerState player, List<FameEvent> events, Func<int> today)
        {
            _player = player;
            _events = events;
            _today = today;
        }

        /// <summary>知名度から主人公の市場価値を出す（知名度0で50万円、50で約1800万円、80で約1.5億円）。</summary>
        public static long CalculateMarketValue(PlayerState p)
        {
            double v = 500000 * Math.Exp(p.fame / 14.0) * (1 + p.form * 0.2);
            return MarketValueCalculator.RoundValue(v);
        }

        static float Importance(MatchCategory c)
        {
            switch (c)
            {
                case MatchCategory.IslandSelect: return 3f;
                case MatchCategory.RegionalDerby: return 2f;
                case MatchCategory.UndergroundFutsal: return 0.3f; // 裏の試合は表の評判にならない
                case MatchCategory.PrisonLeague: return 0.3f;
                default: return 1f;
            }
        }

        /// <summary>試合の結果を記録する。rating は 0〜10（6が普通）。beautifulSkills は綺麗な足技の回数。</summary>
        public float RecordMatch(MatchCategory category, float rating, int goals, int assists, int beautifulSkills)
        {
            _player.matchesPlayed++;
            _player.goals += goals;
            _player.assists += assists;
            _player.lastMatchDay = _today();
            _player.form = Clamp(_player.form * 0.7f + (rating - 6f) / 4f * 0.3f, -1f, 1f);

            float gain = ((rating - 6f) * 1.2f + goals * 1.5f + assists * 0.8f + beautifulSkills * 0.4f) * Importance(category);
            if (gain > 0) gain *= 1f - _player.fame / 120f; // 有名になるほど上がりにくい
            float delta = AddFame(gain);
            if (rating >= 8.5f)
                _events.Add(new FameEvent { type = FameEventType.GreatMatch, day = _today(), npcId = -1, fameDelta = delta,
                    text = $"{MatchLabels.Of(category)}で評価{rating:0.0}の大活躍" });
            return delta;
        }

        /// <summary>ゴールパフォーマンスをしたとき。派手で新鮮なほど知名度が上がる。</summary>
        public float RecordGoalCelebration(GoalPerformanceDesign design)
        {
            if (design == null || !design.IsValid) return 0f;
            float score = design.Score();
            design.timesUsed++;
            float delta = AddFame(score * 0.08f * (1f - _player.fame / 150f));
            _events.Add(new FameEvent
            {
                type = design.IsProvocative ? FameEventType.ProvocativeCelebration : FameEventType.GoalCelebration,
                day = _today(), npcId = -1, fameDelta = delta, text = design.name,
            });
            return delta;
        }

        public float AddFame(float amount)
        {
            float before = _player.fame;
            _player.fame = Clamp(_player.fame + amount, 0f, 100f);
            _player.marketValue = CalculateMarketValue(_player);
            foreach (var m in Milestones)
                if (before < m && _player.fame >= m)
                    _events.Add(new FameEvent { type = FameEventType.FameMilestone, day = _today(), npcId = -1, fameDelta = m });
            return _player.fame - before;
        }

        /// <summary>八百長がばれたとき。知名度が大きく下がり、新聞に載る。</summary>
        public void ReportMatchFixingExposed(string matchName)
        {
            float d = AddFame(-15f);
            _events.Add(new FameEvent { type = FameEventType.MatchFixingExposed, day = _today(), npcId = -1, fameDelta = d, text = matchName });
        }

        /// <summary>大会で優勝したとき。</summary>
        public void ReportTournamentWin(string tournamentName, float fame)
        {
            float d = AddFame(fame);
            _events.Add(new FameEvent { type = FameEventType.TournamentWon, day = _today(), npcId = -1, fameDelta = d, text = tournamentName });
        }

        /// <summary>しばらく試合に出ないと忘れられていく。</summary>
        public void TickDay()
        {
            if (_player.lastMatchDay >= 0 && _today() - _player.lastMatchDay > inactivityGraceDays)
                AddFame(-inactivityDecayPerDay);
            _player.form *= 0.99f;
            _player.marketValue = CalculateMarketValue(_player);
        }

        static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
