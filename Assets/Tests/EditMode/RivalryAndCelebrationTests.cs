using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class RivalryAndCelebrationTests
    {
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));

        [Test]
        public void TownRelations()
        {
            Assert.IsTrue(TownRivalry.AreRivals(Town.Furugane, Town.Kirahama), "ヤンキーの街とチャラい街は犬猿の仲");
            Assert.AreEqual(TownRivalry.Between(Town.Kirahama, Town.Furugane), TownRivalry.Between(Town.Furugane, Town.Kirahama));
            Assert.IsTrue(TownRivalry.AreRivals(Town.Tenpou, Town.Yushin), "天峰台はプライドで嫌われている");
            Assert.Greater(TownRivalry.Between(Town.Yushin, Town.Furugane), 0, "人情でつながっている");
            Assert.Greater(TownRivalry.DerbyTension(Town.Furugane, Town.Kirahama), TownRivalry.DerbyTension(Town.Furugane, Town.Yushin));
        }

        static int GainAfterTalks(IslandCareer c, NPCData n, int talks)
        {
            for (int i = 0; i < talks; i++) { c.Population.Talk(n.id); c.AdvanceDay(); }
            return c.Population.Knowledge.Get(n.id).affinity;
        }

        [Test]
        public void RivalTownsAreSlowerToWarmUp()
        {
            var c1 = NewCareer(); // 主人公は古鐘町
            var friendly = c1.Population.Database.npcs.First(n => n.hometown == Town.Yushin && n.personality == Personality.Extrovert && !n.isIncarcerated);
            var rival = c1.Population.Database.npcs.First(n => n.hometown == Town.Kirahama && n.personality == Personality.Extrovert && !n.isIncarcerated);
            int f = GainAfterTalks(c1, friendly, 5);
            var c2 = NewCareer();
            int r = GainAfterTalks(c2, c2.Population.Database.Get(rival.id), 5);
            Assert.Greater(f, r, "ライバルの街の人は打ち解けにくい");
        }

        [Test]
        public void TenpouOnlyRespectsTheFamous()
        {
            var c = NewCareer();
            var proud = c.Population.Database.npcs.First(n => n.hometown == Town.Tenpou && n.personality == Personality.Extrovert);
            int unknown = c.Towns.ModifyAffinityGain(proud, 10);
            c.Player.fame = 60;
            int famous = c.Towns.ModifyAffinityGain(proud, 10);
            Assert.Greater(famous, unknown);
        }

        [Test]
        public void DerbiesHeatUpAndMoveReputation()
        {
            var c = NewCareer();
            var rng = new SeededRandom(1);
            var home = MatchFactory.Generic("古鐘町選抜", 60, 10, rng); home.town = (int)Town.Furugane;
            MatchFactory.InsertProtagonist(home, c.Player, 11);
            var away = MatchFactory.Generic("煌浜選抜", 60, 11, rng); away.town = (int)Town.Kirahama;
            float before = home.players.Average(p => p.tension);
            var setup = new MatchSetup { home = home, away = away, protagonistSide = Side.Home, seed = 5, category = MatchCategory.RegionalDerby };
            var e = new MatchEngine(setup);
            Assert.Greater(home.players.Average(p => p.tension), before + 5f, "因縁の相手には燃える");
            Assert.IsTrue(e.Log.Any(x => x.text.Contains("因縁の対決")));

            int mine = c.Towns.Of(Town.Furugane), theirs = c.Towns.Of(Town.Kirahama);
            var win = new MatchResult { homeScore = 2, awayScore = 0, protagonistSide = Side.Home, protagonist = home.Protagonist };
            MatchResultApplier.Apply(c, setup, win);
            Assert.Greater(c.Towns.Of(Town.Furugane), mine);
            Assert.Less(c.Towns.Of(Town.Kirahama), theirs);
        }

        [Test]
        public void ReputationAffectsFans()
        {
            var c = NewCareer();
            c.Player.fame = 60;
            var fan = c.Population.Database.npcs.First(n => n.hometown == Town.Kirahama && n.fame < 20 && n.personality == Personality.Introvert && !n.isIncarcerated);
            float plain = c.Fans.RequestChance(fan);
            c.Towns.Change(Town.Kirahama, -50);
            Assert.Less(c.Fans.RequestChance(fan), plain, "評判の悪い街では声をかけられない");
        }

        // ---------------- ゴールパフォーマンス ----------------

        static MatchSetup StrongProtagonistSetup(GoalPerformanceDesign perf, int seed)
        {
            var ps = new PlayerState();
            for (int i = 0; i < NPCAbilities.StatCount; i++) ps.abilities[i] = 95;
            var rng = new SeededRandom(seed);
            var home = MatchFactory.Generic("主人公FC", 70, 10, rng);
            MatchFactory.InsertProtagonist(home, ps, 11);
            var away = MatchFactory.Generic("弱小", 30, 11, rng);
            return new MatchSetup { home = home, away = away, protagonistSide = Side.Home, seed = seed, celebration = perf, crowd = 500, autoSelectCpuTactics = false };
        }

        [Test]
        public void GoalPerformanceHappensWhenTheProtagonistScores()
        {
            var perf = new GoalPerformanceDesign { name = "流星バク宙", moves = { PerformanceMove.KneeSlide, PerformanceMove.Backflip } };
            int celebrations = 0, protagonistGoals = 0;
            for (int i = 0; i < 6; i++)
            {
                var e = new MatchEngine(StrongProtagonistSetup(perf, i + 1)) { Controller = new AutoPilot(i) };
                var r = e.RunToEnd();
                celebrations += r.celebrations;
                protagonistGoals += r.protagonist.goals;
                Assert.AreEqual(r.celebrations, e.Log.Count(x => x.cutIn && x.moveName == "流星バク宙"));
            }
            Assert.Greater(protagonistGoals, 0);
            Assert.AreEqual(protagonistGoals, celebrations, "主人公のゴールのたびに披露する");
        }

        [Test]
        public void TakingYourShirtOffIsAYellowCard()
        {
            var perf = new GoalPerformanceDesign { name = "脱ぐ", moves = { PerformanceMove.ShirtOverHead } };
            for (int i = 0; i < 6; i++)
            {
                var e = new MatchEngine(StrongProtagonistSetup(perf, i + 1)) { Controller = new AutoPilot(i) };
                var r = e.RunToEnd();
                if (r.celebrations == 0) continue;
                Assert.IsTrue(e.Log.Any(x => x.type == MatchEventType.YellowCard && x.actor.isProtagonist && x.text.Contains("脱いで")));
                if (r.celebrations >= 2) Assert.IsTrue(r.protagonist.sentOff, "2回脱げば退場");
                return;
            }
            Assert.Fail("ゴールが入らなかった");
        }

        [Test]
        public void CelebrationsBuildFameAndGetStale()
        {
            var c = NewCareer();
            var perf = new GoalPerformanceDesign { name = "ダンス", moves = { PerformanceMove.Dance, PerformanceMove.Robot } };
            c.Player.goalPerformances.Add(perf);
            c.Player.selectedPerformance = 0;
            var setup = StrongProtagonistSetup(perf, 3);
            var result = new MatchResult { homeScore = 3, awayScore = 0, protagonistSide = Side.Home, protagonist = setup.home.Protagonist, celebrations = 3 };
            float fame = c.Player.fame;
            MatchResultApplier.Apply(c, setup, result);
            Assert.Greater(c.Player.fame, fame);
            Assert.AreEqual(3, perf.timesUsed, "使った回数が増えて、だんだん飽きられる");
            Assert.AreSame(perf, c.Player.SelectedPerformance);
        }
    }
}
