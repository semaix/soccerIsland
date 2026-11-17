using System;
using SoccerIsland.Career;
using SoccerIsland.NPC;
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

            // 主人公：知名度と経験値。アイテムの効果は試合が終わると切れる
            var pro = result.protagonist;
            if (pro != null)
            {
                career.Player.matchBuffs.Clear();
                career.Fame.RecordMatch(setup.category, pro.rating, pro.goals, pro.assists, setup.underground ? 0 : pro.beautifulSkills);
                // ゴールパフォーマンスの評判（裏の試合は誰も見ていない）
                if (setup.celebration != null && !setup.underground)
                    for (int i = 0; i < result.celebrations; i++) career.Fame.RecordGoalCelebration(setup.celebration);
                result.levelUps = PlayerGrowth.AddExperience(career.Player, result.protagonistExp);
                foreach (var lv in result.levelUps)
                {
                    var m = SpecialMoveLibrary.ForProtagonistLevel(lv);
                    if (m != null) result.learnedSpecials.Add(m);
                }
            }

            // けが：島民も主人公も、治るまで試合に出られない
            int today = db.currentDay;
            foreach (var team in new[] { setup.home, setup.away })
                foreach (var p in team.players)
                {
                    if (!p.injured || p.injuryDays <= 0) continue;
                    if (p.isProtagonist) career.Player.injuredUntilDay = Math.Max(career.Player.injuredUntilDay, today + p.injuryDays);
                    else { var n = db.Get(p.npcId); if (n != null) n.injuredUntilDay = Math.Max(n.injuredUntilDay, today + p.injuryDays); }
                }

            // 地域対抗：街の評判
            if (result.protagonistSide.HasValue && setup.home.town >= 0 && setup.away.town >= 0)
            {
                var mine = result.protagonistSide == Side.Home ? setup.home : setup.away;
                var theirs = result.protagonistSide == Side.Home ? setup.away : setup.home;
                career.Towns.RecordDerby((Town)mine.town, (Town)theirs.town, result.ProtagonistWon, result.ProtagonistLost);
            }

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
