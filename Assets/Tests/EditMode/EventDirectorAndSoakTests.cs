using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class EventDirectorAndSoakTests
    {
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));

        static MatchSetup AnySetup(IslandCareer c, int seed, int crowd = 100, MatchCategory cat = MatchCategory.Kusa)
        {
            var rng = new SeededRandom(seed);
            var home = MatchFactory.Generic("主人公FC", 60, 10, rng);
            MatchFactory.InsertProtagonist(home, c.Player, 11);
            return new MatchSetup { home = home, away = MatchFactory.Generic("相手", 55, 11, rng), protagonistSide = Side.Home, seed = seed, crowd = crowd, category = cat };
        }

        [Test]
        public void LoanSharkOfferLeadsToAThrownMatch()
        {
            var c = NewCareer();
            c.requirePresence = false;
            c.Player.blackMarketAccess = true;
            var loan = c.Bank.Borrow(Lender.LoanShark, 1000000, 10);
            c.Player.cash = 0;
            for (int d = 0; d < 25 && c.Player.fixOfferLoanId < 0; d++) c.AdvanceDay();
            Assert.AreEqual(loan.id, c.Player.fixOfferLoanId, "闇金が八百長を持ちかけてくる");
            Assert.IsTrue(c.AnswerFixOffer(true));

            var setup = AnySetup(c, 1);
            var session = new MatchSession(c, setup);
            Assert.IsTrue(session.Event is ThrowMatchMission, "次の試合は「わざと負けろ」");
            Assert.AreEqual(-1, c.Player.acceptedFixLoanId, "1試合だけ");
            Assert.IsTrue(!(new MatchSession(c, AnySetup(c, 2)).Event is ThrowMatchMission));
        }

        [Test]
        public void DecliningTheOfferMeansNoFix()
        {
            var c = NewCareer();
            c.Player.fixOfferLoanId = 3;
            c.AnswerFixOffer(false);
            Assert.AreEqual(-1, c.Player.fixOfferLoanId);
            Assert.AreEqual(-1, c.Player.acceptedFixLoanId);
        }

    static int Count<T>(Func<IslandCareer, MatchSetup> make)
        {
            int n = 0;
            for (int i = 0; i < 40; i++)
            {
                var c = NewCareerCached();
                var s = make(c);
                s.seed = i + 1;
                if (EventDirector.Prepare(c, s, new SeededRandom(i + 1)) is T) n++;
            }
            return n;
        }

        [Test]
        public void TheDirectorPicksEventsFromTheSituation()
        {
            Assert.Greater(Count<CrampMission>(c => { c.Player.fatigue = 80; var s = AnySetup(c, 1); c.Player.fatigue = 80; return s; }), 5, "疲れていると足がつる");
            Assert.Greater(Count<FanShowcaseMission>(c => { c.Player.fatigue = 0; c.Player.fame = 50; return AnySetup(c, 1, 3000); }), 5, "大観衆の前では足技を見せる");
            Assert.Greater(Count<OffsideTrapMission>(c => { c.Player.fatigue = 0; c.Player.fame = 0; c.Player.age = 35; c.Player.position = Position.DF; return AnySetup(c, 1); }), 5, "ベテランDFはオフサイドトラップ");
            Assert.AreEqual(40, Count<WinMission>(c => { c.Player.fatigue = 0; c.Player.fame = 0; c.Player.age = 26; c.Player.position = Position.MF; return AnySetup(c, 1, 2000, MatchCategory.RegionalDerby); }), "大一番は勝利");
        }

        static IslandCareer _cached;
        static IslandCareer NewCareerCached() => _cached ?? (_cached = NewCareer());

        [Test]
        public void MissionsPayOut()
        {
            var c = NewCareer();
            var setup = AnySetup(c, 3, 3000, MatchCategory.RegionalDerby);
            setup.missions.Add(new WinMission());
            var result = new MatchResult { homeScore = 2, awayScore = 0, protagonistSide = Side.Home, protagonist = setup.home.Protagonist };
            result.missions.Add(new MissionResult { title = "勝利せよ", status = MissionStatus.Success, detail = "2-0" });
            long cash = c.Player.cash;
            float fame = c.Player.fame;
            MatchResultApplier.Apply(c, setup, result);
            Assert.Greater(c.Player.cash, cash, "勝利給");
            Assert.Greater(c.Player.fame, fame);
            Assert.IsTrue(result.missions[0].detail.Contains("勝利給"));
        }

        // ---------------- 長期の通しテスト ----------------

        [Test]
        public void ThreeYearsOnTheIslandWithoutBreaking()
        {
            var c = NewCareer();
            var rng = new SeededRandom(2026);
            var sw = Stopwatch.StartNew();
            c.Player.cash = 50000000;
            var towns = (Town[])Enum.GetValues(typeof(Town));
            int talks = 0, matches = 0, trainings = 0;
            for (int day = 0; day < 365 * 3; day++)
            {
                // 朝：どこかの街へ
                var t = towns[rng.Range(0, towns.Length)];
                var places = IslandMap.PlacesIn(t, c.Player.homeTown);
                c.MoveTo(t, places[rng.Range(0, places.Count)]);
                foreach (var n in c.PeopleHere().Take(3)) { c.Interact(n.id); talks++; }
                if (c.Here.place == IslandPlace.ClubGround && c.Training.Train((TrainingMenu)rng.Range(0, 7)).done) trainings++;

                // 自分のチームの試合があれば出る（週に1回くらい）
                if (day % 7 == 6)
                {
                    var m = c.World.TodaysMatches().FirstOrDefault(x => !x.played && x.awayLineup.Count > 0 && !x.IsUnderground && x.category != MatchCategory.PrisonLeague);
                    if (m != null)
                    {
                        var s = new MatchSession(c, MatchFactory.FromWorldMatch(c, m, Side.Home));
                        s.AutoPlay();
                        matches++;
                    }
                }
                if (day % 30 == 0) { c.Betting.BuyLottery(1); c.Stocks.Buy(c.Economy.stocks[rng.Range(0, c.Economy.stocks.Count)].id, 10); }
                c.AdvanceDay();
            }
            var db = c.Population.Database;
            Assert.AreEqual(3, db.currentYear - 2026);
            Assert.GreaterOrEqual(c.World.League.League.history.Count, 3, "毎年表彰がある");
            Assert.Greater(talks, 500);
            Assert.Greater(matches, 30);
            Assert.Greater(trainings, 20);
            Assert.Less(c.World.State.matches.Count, 400, "終わった試合は掃除されている");
            Assert.Less(c.Media.issues.Count, 61, "新聞は60号まで");
            Assert.IsTrue(db.npcs.All(n => n.marketValue > 0), "市場価値が壊れていない");
            Assert.Less(sw.Elapsed.TotalSeconds, 300, "3年分が現実的な時間で回る");
        }
    }
}
