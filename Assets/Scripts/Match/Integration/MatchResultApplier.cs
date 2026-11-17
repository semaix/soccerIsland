using SoccerIsland.Career;
using SoccerIsland.World;

namespace SoccerIsland.Match
{
    /// <summary>
    /// 試合結果を島に反映する：試合予定の結果、主人公の知名度・市場価値、島民の話題性、八百長の後始末。
    /// </summary>
    public static class MatchResultApplier
    {
        public static void Apply(IslandCareer career, MatchSetup setup, MatchResult result)
        {
            var db = career.Population.Database;

            if (setup.worldMatchId >= 0)
            {
                var m = career.World.State.GetMatch(setup.worldMatchId);
                if (m != null)
                {
                    m.homeScore = result.homeScore;
                    m.awayScore = result.awayScore;
                    m.played = true; // 当日の自動計算はスキップされる
                }
            }

            // 主人公
            var pro = result.protagonist;
            if (pro != null)
                career.Fame.RecordMatch(setup.category, pro.rating, pro.goals, pro.assists, setup.underground ? 0 : pro.beautifulSkills);

            // 島民：いい試合をすれば話題に、ひどければ評価が下がる
            if (!setup.underground)
                foreach (var team in new[] { setup.home, setup.away })
                    foreach (var p in team.players)
                    {
                        if (p.isProtagonist || p.npcId < 0) continue;
                        var n = db.Get(p.npcId);
                        if (n == null) continue;
                        float hype = p.rating >= 8f ? 0.05f : p.rating >= 7f ? 0.02f : p.rating <= 4.5f ? -0.02f : 0f;
                        hype += p.goals * 0.02f;
                        if (hype != 0f) career.Population.Market.AddHype(n, hype, "match_" + setup.worldMatchId);
                    }

            // 八百長
            foreach (var mission in setup.missions)
            {
                if (!(mission is ThrowMatchMission fix)) continue;
                if (result.fixingExposed) career.Fame.ReportMatchFixingExposed(setup.title);
                else if (result.ProtagonistLost && fix.loanId >= 0) career.Bank.ForgiveHalfForFix(fix.loanId);
            }
        }
    }
}
