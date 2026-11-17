using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class BossAndTournamentTests
    {
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));

        static MatchSetup Versus(MatchTeam home, MatchTeam away, int seed, bool broken = false) =>
            new MatchSetup { home = home, away = away, protagonistSide = Side.Home, seed = seed, unbeatableBroken = broken, autoSelectCpuTactics = false };

        static MatchTeam Mine(int seed, int strength = 60)
        {
            var t = MatchFactory.Generic("主人公FC", strength, 11, new SeededRandom(seed));
            t.players.RemoveAt(10);
            t.players.Add(MatchFactory.FromProtagonist(new PlayerState()));
            return t;
        }

        [Test]
        public void BossTeamsAreBuiltFromIslanders()
        {
            var c = NewCareer();
            var rng = new SeededRandom(1);
            foreach (BossKind k in Enum.GetValues(typeof(BossKind)))
            {
                if (k == BossKind.None) continue;
                var t = BossTeams.Build(k, c, rng);
                Assert.AreEqual(11, t.players.Count, k.ToString());
                Assert.AreEqual(1, t.players.Count(p => p.position == Position.GK), k + " のキーパー");
                Assert.Greater(t.traits.Count, 0);
            }
            var mafia = BossTeams.Build(BossKind.Mafia, c, rng);
            Assert.IsTrue(mafia.traits.Contains(BossTrait.Intimidation) && mafia.traits.Contains(BossTrait.BribedReferee));
            Assert.IsTrue(mafia.players.Where(p => p.npcId >= 0).All(p => c.Population.Database.Get(p.npcId).occupation == Occupation.Mafia));

            var god = BossTeams.Build(BossKind.GodTeam, c, rng);
            Assert.IsTrue(god.players.Any(p => p.soccerPersonality == SoccerPersonality.God), "神様のチームにはゴッドがいる");
        }

        [Test]
        public void IntimidationShakesTheOpponent()
        {
            var me = Mine(1);
            float before = me.players.Average(p => p.tension);
            var boss = MatchFactory.Generic("怖い人たち", 60, 11, new SeededRandom(2));
            boss.traits.Add(BossTrait.Intimidation);
            new MatchEngine(Versus(me, boss, 3));
            Assert.Less(me.players.Average(p => p.tension), before - 5f);
        }

        static int GoalsAgainst(Func<int, MatchTeam> boss, int matches)
        {
            int g = 0;
            for (int i = 0; i < matches; i++)
                g += new MatchEngine(Versus(Mine(i), boss(i), i + 1)) { Controller = new AutoPilot(i) }.RunToEnd().homeScore;
            return g;
        }

        [Test]
        public void IronWallKeeperConcedesLess()
        {
            int normal = GoalsAgainst(i => MatchFactory.Generic("普通", 60, 11, new SeededRandom(100 + i)), 15);
            int wall = GoalsAgainst(i => { var t = MatchFactory.Generic("鉄壁", 60, 11, new SeededRandom(100 + i)); t.traits.Add(BossTrait.IronWall); return t; }, 15);
            Assert.Less(wall, normal);
        }

    static int Stopped(bool genius)
        {
            int n = 0;
            for (int i = 0; i < 10; i++)
            {
                var b = MatchFactory.Generic("天才", 60, 11, new SeededRandom(200 + i));
                if (genius) b.traits.Add(BossTrait.GeniusVision);
                var e = new MatchEngine(Versus(Mine(i), b, i + 1)) { Controller = new AutoPilot(i) };
                e.RunToEnd();
                n += e.Log.Count(x => x.side == Side.Home && (x.type == MatchEventType.DribbleBeaten || x.type == MatchEventType.PassIntercepted));
            }
            return n;
        }

        [Test]
        public void GeniusVisionReadsYourCommands()
        {
            Assert.Greater(Stopped(true), Stopped(false), "読まれるとボールを奪われやすい");
        }

        [Test]
        public void UnbeatableTeamNeedsItsWeaknessFound()
        {
            int winsLocked = 0, winsBroken = 0;
            for (int i = 0; i < 6; i++)
            {
                var b1 = MatchFactory.Generic("裏の王者", 55, 11, new SeededRandom(300 + i)); b1.traits.Add(BossTrait.Unbeatable);
                var r1 = new MatchEngine(Versus(Mine(i, 60), b1, i + 1)) { Controller = new AutoPilot(i) }.RunToEnd();
                if (r1.ProtagonistWon) winsLocked++;
                var b2 = MatchFactory.Generic("裏の王者", 55, 11, new SeededRandom(300 + i)); b2.traits.Add(BossTrait.Unbeatable);
                var r2 = new MatchEngine(Versus(Mine(i, 60), b2, i + 1, broken: true)) { Controller = new AutoPilot(i) }.RunToEnd();
                if (r2.ProtagonistWon) winsBroken++;
            }
            Assert.AreEqual(0, winsLocked, "弱点を知らないと勝てない");
            Assert.Greater(winsBroken, winsLocked);
        }

        [Test]
        public void KnowingThePhantomBreaksTheUnbeatable()
        {
            var c = NewCareer();
            Assert.IsTrue(!BossTeams.KnowsUndergroundKingsWeakness(c));
            var phantom = c.Population.Database.npcs.First(n => n.soccerPersonality == SoccerPersonality.Phantom);
            c.Population.Knowledge.Reveal(phantom.id, NPCInfoFlags.Name | NPCInfoFlags.SoccerPersonality);
            Assert.IsTrue(BossTeams.KnowsUndergroundKingsWeakness(c));
        }

        // ---------------- トーナメント ----------------

        [Test]
        public void GodCupRunsToAChampion()
        {
            var c = NewCareer();
            var t = TournamentSystem.CreateGodCup(c);
            Assert.AreEqual(8, t.State.entries.Count);
            Assert.AreEqual(t.State.PlayerIndex, t.State.alive[0]);
            Assert.AreEqual(BossKind.GodTeam, t.State.entries[t.State.alive[7]].boss, "神様のチームは決勝まで当たらない山");

            int rounds = 0;
            while (!t.State.finished && rounds++ < 5) t.AutoPlayRound();
            Assert.IsTrue(t.State.finished);
            Assert.AreEqual(7, t.State.history.Count);
            Assert.IsTrue(t.State.champion >= 0);
            Assert.IsTrue(t.State.history.All(h => h.winner == h.a || h.winner == h.b));
            Assert.IsTrue(t.State.history.Where(h => !h.penalties).All(h => (h.winner == h.a) == (h.scoreA > h.scoreB)));
        }

        [Test]
        public void WinningPaysPrizeAndMakesNews()
        {
            var c = NewCareer();
            var t = TournamentSystem.CreateGodCup(c);
            long cash = c.Player.cash;
            while (!t.State.finished)
            {
                var setup = t.PlayerMatchSetup();
                MatchResult fake = null;
                if (setup != null)
                    fake = new MatchResult { protagonistSide = setup.protagonistSide, homeScore = setup.protagonistSide == Side.Home ? 3 : 0, awayScore = setup.protagonistSide == Side.Home ? 0 : 3 };
                t.CompleteRound(fake);
            }
            Assert.IsTrue(t.State.PlayerWon);
            Assert.AreEqual(cash + t.State.prize, c.Player.cash);
            Assert.IsTrue(c.AdvanceDay().newspaper.articles.Any(a => a.title.Contains("優勝")));
        }

        [Test]
        public void UndergroundTournamentNeedsAccessAndFee()
        {
            var c = NewCareer();
            c.Player.blackMarketAccess = false;
            Assert.IsTrue(TournamentSystem.CreateUndergroundTournament(c) == null);
            c.Player.blackMarketAccess = true;
            long cash = c.Player.cash;
            var t = TournamentSystem.CreateUndergroundTournament(c, 500000);
            Assert.IsTrue(t != null);
            Assert.AreEqual(cash - 500000, c.Player.cash);
            Assert.IsTrue(t.State.underground);
            Assert.AreEqual(BossKind.UndergroundKings, t.State.entries[t.State.alive.Last()].boss);
            var s = t.PlayerMatchSetup();
            Assert.IsTrue(s.underground);
        }

        [Test]
        public void RegionalCupPutsProtagonistInHomeTownSelect()
        {
            var c = NewCareer();
            var t = TournamentSystem.CreateRegionalCup(c);
            Assert.AreEqual(4, t.State.entries.Count);
            var s = t.PlayerMatchSetup();
            var mine = s.protagonistSide == Side.Home ? s.home : s.away;
            Assert.IsTrue(mine.Protagonist != null);
            Assert.AreEqual(11, mine.players.Count);
            Assert.AreEqual(NPCLabels.Of(c.Player.homeTown) + "選抜", mine.name);
        }
    }
}
