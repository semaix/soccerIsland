using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class HospitalAndPoliceTests
    {
        // このファイルは各システムの動きを見るので、場所の制限（同じ場所にいる人としか話せない等）は外す
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault()))) { requirePresence = false };

        [Test]
        public void RoughFoulsCauseInjuriesThatLast()
        {
            int injuries = 0;
            var c = NewCareer();
            for (int i = 0; i < 25 && injuries == 0; i++)
            {
                var rng = new SeededRandom(i + 1);
                var home = MatchFactory.Generic("主人公FC", 60, 10, rng);
                MatchFactory.InsertProtagonist(home, c.Player, 11);
                var away = MatchFactory.Generic("荒くれ", 60, 11, rng);
                foreach (var p in away.players) p.soccerPersonality = SoccerPersonality.Fighter;
                var setup = new MatchSetup { home = home, away = away, protagonistSide = Side.Home, seed = i + 1, autoSelectCpuTactics = false };
                var e = new MatchEngine(setup) { Controller = new AutoPilot(i) };
                var r = e.RunToEnd();
                var hurt = r.log.Where(x => x.type == MatchEventType.Injury).ToList();
                injuries += hurt.Count;
                foreach (var ev in hurt) Assert.IsTrue(!ev.actor.CanTouchBall, "けがをしたらボールに関われない");
                if (hurt.Any(x => x.actor.isProtagonist))
                {
                    MatchResultApplier.Apply(c, setup, r);
                    Assert.IsTrue(c.Player.IsInjured(c.Population.Database.currentDay + 1), "けがは試合後も続く");
                }
            }
            Assert.Greater(injuries, 0);
        }

        [Test]
        public void InjuredIslandersMissMatchesAndTheProtagonistCoachesFromTheBench()
        {
            var c = NewCareer();
            var db = c.Population.Database;
            var m = c.World.State.matches.First(x => !x.played && x.homeLineup.Count >= 5 && !x.IsUnderground && x.category != MatchCategory.PrisonLeague);
            var hurt = db.Get(m.homeLineup[0]);
            hurt.injuredUntilDay = db.currentDay + 10;
            var setup = MatchFactory.FromWorldMatch(c, m, Side.Home, 1);
            Assert.IsTrue(!setup.home.players.Any(p => p.npcId == hurt.id), "けが人は出られない");

            c.Player.injuredUntilDay = db.currentDay + 5;
            var s2 = MatchFactory.FromWorldMatch(c, m, Side.Home, 2);
            var pro = s2.home.Protagonist;
            Assert.IsTrue(pro != null && !pro.CanTouchBall, "けがをした主人公はベンチ");
            var e = new MatchEngine(s2);
            Assert.IsTrue(e.CanCoach, "ベンチから指示は出せる");
        }

        [Test]
        public void HospitalHalvesRecoveryAndDoctorsHelpFriends()
        {
            var c = NewCareer();
            int day = c.Population.Database.currentDay;
            Assert.AreEqual("どこも悪くない", c.Hospital.TreatProtagonist());
            c.Player.injuredUntilDay = day + 10;
            long cash = c.Player.cash;
            c.Hospital.TreatProtagonist();
            Assert.AreEqual(day + 5, c.Player.injuredUntilDay);
            Assert.AreEqual(cash - c.Hospital.fee, c.Player.cash);

            var doc = c.Population.Database.npcs.First(n => n.occupation == Occupation.Doctor);
            c.Population.Knowledge.Reveal(doc.id, NPCInfoFlags.Basic);
            c.Population.Knowledge.GetOrCreate(doc.id).affinity = 60;
            cash = c.Player.cash;
            c.Hospital.TreatProtagonist();
            Assert.AreEqual(cash, c.Player.cash, "知り合いの医者なら無料");
        }

        [Test]
        public void ReportingTheLoanSharkToThePolice()
        {
            var c = NewCareer();
            c.Player.blackMarketAccess = true;
            Assert.AreEqual("通報することがない", c.Police.ReportLoanShark());
            var cop = c.Population.Database.npcs.First(n => n.occupation == Occupation.Police && !n.isUnderground);
            c.Population.Knowledge.Reveal(cop.id, NPCInfoFlags.Basic);
            c.Population.Knowledge.GetOrCreate(cop.id).affinity = 80;
            var mafia = c.Population.Database.npcs.First(n => n.occupation == Occupation.Mafia);
            c.Population.Knowledge.Reveal(mafia.id, NPCInfoFlags.Basic);
            c.Population.Knowledge.GetOrCreate(mafia.id).affinity = 50;
            Assert.Greater(c.Police.ReportChance(), 0.8f, "仲の良い警官がいると動いてくれる");

            bool busted = false;
            for (int i = 0; i < 6 && !busted; i++)
            {
                c.Bank.Borrow(Lender.LoanShark, 1000000, 10);
                c.Police.ReportLoanShark();
                busted = !c.Economy.loans.Any(l => l.lender == Lender.LoanShark);
                for (int d = 0; d < 7; d++) c.AdvanceDay();
            }
            Assert.IsTrue(busted, "闇金が摘発される");
            Assert.Less(c.Population.Knowledge.Get(mafia.id).affinity, 50, "マフィアに恨まれる");
        }
    }
}
