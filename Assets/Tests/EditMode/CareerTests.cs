using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class CareerTests
    {
        static IslandCareer NewCareer(int seed = 777)
        {
            var s = NPCGenerationSettings.CreateDefault();
            s.seed = seed;
            return new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(s)));
        }

        static void BefriendFully(IslandCareer c, NPCData npc)
        {
            c.Population.Knowledge.Reveal(npc.id, NPCInfoFlags.All);
            c.Population.Knowledge.GetOrCreate(npc.id).affinity = 100;
        }

        static NPCData OrdinaryAdult(IslandCareer c) =>
            c.Population.Database.npcs.First(n => n.age >= 20 && n.age <= 30 && !n.isUnderground && !n.isIncarcerated &&
                                                  n.soccerPersonality == SoccerPersonality.Fighter && n.marketValue > 100000);

        // ---------------- スカウト ----------------

        [Test]
        public void ScoutingCostsMarketValueAndMovesNPCToTeam()
        {
            var c = NewCareer();
            var npc = OrdinaryAdult(c);
            BefriendFully(c, npc);
            c.Player.cash = 10000000000;
            long price = c.Scouting.Price(npc);
            Assert.AreEqual(npc.marketValue, price, "通常の性格は市場価値そのまま");

            long before = c.Player.cash;
            Assert.AreEqual(ScoutResult.Success, c.Scouting.Scout(npc.id));
            Assert.AreEqual(before - price, c.Player.cash);
            Assert.AreEqual(c.Player.teamClubId, npc.clubId);
            Assert.IsTrue(c.Player.FindContract(npc.id) != null);
            Assert.IsTrue(c.Population.Database.GetClubMembers(c.Player.teamClubId).Contains(npc));
            Assert.AreEqual(ScoutResult.AlreadyInTeam, c.Scouting.Scout(npc.id));
        }

        [Test]
        public void ScoutingRequirements()
        {
            var c = NewCareer();
            var db = c.Population.Database;
            var npc = OrdinaryAdult(c);
            Assert.AreEqual(ScoutResult.NotAcquainted, c.Scouting.Check(npc));

            c.Population.Knowledge.Reveal(npc.id, NPCInfoFlags.Basic);
            Assert.AreEqual(ScoutResult.LowAffinity, c.Scouting.Check(npc));

            c.Population.Knowledge.GetOrCreate(npc.id).affinity = 100;
            c.Player.cash = npc.marketValue - 1;
            Assert.AreEqual(ScoutResult.NotEnoughMoney, c.Scouting.Check(npc));

            var prisoner = db.npcs.First(n => n.isIncarcerated);
            BefriendFully(c, prisoner);
            Assert.AreEqual(ScoutResult.Incarcerated, c.Scouting.Check(prisoner));

            var mafia = db.npcs.First(n => n.isUnderground && !n.isIncarcerated && n.age >= 18);
            c.Population.Knowledge.Reveal(mafia.id, NPCInfoFlags.Basic);
            c.Population.Knowledge.GetOrCreate(mafia.id).affinity = 100;
            Assert.AreEqual(ScoutResult.NeedsBlackMarket, c.Scouting.Check(mafia), "裏の顔を知らないと裏の人間は獲得できない");
        }

        [Test]
        public void UnpaidSalaryTwiceMakesPlayerLeave()
        {
            var c = NewCareer();
            var npc = OrdinaryAdult(c);
            BefriendFully(c, npc);
            c.Player.cash = c.Scouting.Price(npc);
            int oldClub = npc.clubId;
            Assert.AreEqual(ScoutResult.Success, c.Scouting.Scout(npc.id));
            Assert.AreEqual(0L, c.Player.cash);

            c.Scouting.Payroll();
            Assert.IsTrue(c.Player.FindContract(npc.id) != null, "1回目はまだ残る");
            c.Scouting.Payroll();
            Assert.IsTrue(c.Player.FindContract(npc.id) == null, "2回目で退団");
            Assert.AreEqual(oldClub, npc.clubId);
        }

        // ---------------- 銀行・闇金 ----------------

        [Test]
        public void BankLoanHasLimitAndAccruesInterest()
        {
            var c = NewCareer();
            long limit = c.Bank.BankLimit();
            Assert.Greater(limit, 0L);
            Assert.IsTrue(c.Bank.Borrow(Lender.Bank, limit + 1, 365) == null, "枠を超えて借りられない");

            double annual = c.Bank.BankAnnualRate;
            long cashBefore = c.Player.cash;
            var loan = c.Bank.Borrow(Lender.Bank, 1000000, 400);
            Assert.IsTrue(loan != null);
            Assert.AreEqual(cashBefore + 1000000, c.Player.cash);

            for (int i = 0; i < 365; i++) c.Bank.TickDay();
            double expected = 1000000 * Math.Pow(1 + annual / 365, 365);
            Assert.Less(Math.Abs(loan.balance - expected), 10.0);

            c.Player.cash = 100000000;
            c.Bank.Repay(loan.id, loan.Balance);
            Assert.IsTrue(c.Economy.GetLoan(loan.id) == null, "完済したら消える");
        }

        [Test]
        public void LoanSharkNeedsBlackMarketAndEscalates()
        {
            var c = NewCareer();
            c.Player.blackMarketAccess = false;
            Assert.IsTrue(c.Bank.Borrow(Lender.LoanShark, 1000000, 10) == null);

            c.Player.blackMarketAccess = true;
            var loan = c.Bank.Borrow(Lender.LoanShark, 1000000, 10);
            Assert.IsTrue(loan != null);
            c.Player.cash = 0;

            var seen = new HashSet<EconomyEventType>();
            for (int d = 0; d < 10; d++) foreach (var e in c.AdvanceDay().economyEvents) seen.Add(e.type);
            Assert.Less(Math.Abs(loan.balance - 1100000), 1000.0, "トイチ：10日で1割");

            for (int d = 0; d < 20; d++) foreach (var e in c.AdvanceDay().economyEvents) seen.Add(e.type);
            Assert.IsTrue(seen.Contains(EconomyEventType.LoanSharkHarassment));
            Assert.IsTrue(seen.Contains(EconomyEventType.LoanSharkDemandsFix), "八百長の要求が来る");
            Assert.IsTrue(seen.Contains(EconomyEventType.LoanSharkSeizure));
        }

        // ---------------- 株 ----------------

        [Test]
        public void StocksTradeAndMove()
        {
            var c = NewCareer();
            Assert.IsTrue(c.Economy.stocks.Any(s => s.kind == StockKind.Club));
            Assert.IsTrue(c.Economy.stocks.Any(s => s.kind == StockKind.Shop));
            Assert.IsTrue(!c.Economy.stocks.Any(s => s.name == "裏市場"), "裏市場は上場しない");

            var stock = c.Economy.stocks[0];
            c.Player.cash = 100000000;
            long cost = c.Stocks.Cost(stock, 100);
            Assert.IsTrue(c.Stocks.Buy(stock.id, 100));
            Assert.AreEqual(100000000 - cost, c.Player.cash);

            var start = c.Economy.stocks.Select(s => s.price).ToList();
            for (int d = 0; d < 14; d++) c.AdvanceDay();
            Assert.IsTrue(c.Economy.stocks.Select(s => s.price).Where((p, i) => p != start[i]).Any(), "株価が動いていない");

            Assert.IsTrue(c.Stocks.Sell(stock.id, 100));
            Assert.IsTrue(c.Economy.GetHolding(stock.id) == null);
            Assert.IsTrue(!c.Stocks.Sell(stock.id, 1), "持っていない株は売れない");
        }

        // ---------------- toto・宝くじ ----------------

        [Test]
        public void TotoPaysOnlyTheCorrectPick()
        {
            var c = NewCareer();
            c.Player.cash = 10000000;
            var match = c.World.State.matches.Where(m => c.Betting.IsBettable(m) && !m.IsUnderground).OrderBy(m => m.day).First();
            var bets = new[] { TotoPick.Home, TotoPick.Draw, TotoPick.Away }.Select(p => c.Betting.PlaceBet(match.id, p, 10000)).ToList();
            Assert.IsTrue(bets.All(b => b != null));

            while (!match.played) c.AdvanceDay();
            Assert.AreEqual(1, bets.Count(b => b.won));
            var win = bets.First(b => b.won);
            Assert.AreEqual((long)Math.Floor(10000 * win.odds), win.payout);
            Assert.IsTrue(bets.All(b => b.settled));
        }

        [Test]
        public void UndergroundTotoNeedsBlackMarketAccess()
        {
            var c = NewCareer();
            var m = c.World.State.matches.First(x => x.IsUnderground && !x.played);
            c.Player.blackMarketAccess = false;
            Assert.IsTrue(!c.Betting.IsBettable(m));
            c.Player.blackMarketAccess = true;
            Assert.IsTrue(c.Betting.IsBettable(m));
            Assert.Greater(c.Betting.Odds(m, TotoPick.Home) * c.Betting.Probabilities(m)[0], 0.9f, "裏totoは還元率が高い");
        }

        [Test]
        public void LotteryPrizeTableAndDraw()
        {
            Assert.AreEqual(100000000L, BettingSystem.Prize(123456, 123456));
            Assert.AreEqual(1000000L, BettingSystem.Prize(923456, 123456));
            Assert.AreEqual(50000L, BettingSystem.Prize(993456, 123456));
            Assert.AreEqual(3000L, BettingSystem.Prize(999456, 123456));
            Assert.AreEqual(300L, BettingSystem.Prize(999996, 123456));
            Assert.AreEqual(0L, BettingSystem.Prize(999990, 123456));

            var c = NewCareer();
            var tickets = c.Betting.BuyLottery(50);
            Assert.AreEqual(50, tickets.Count);
            for (int d = 0; d < 8; d++) c.AdvanceDay();
            Assert.IsTrue(tickets.All(t => t.checkedResult));
            Assert.IsTrue(c.Economy.lastWinningNumber >= 0);
        }

        // ---------------- 知名度・ファン ----------------

        [Test]
        public void FanRequestsGrowWithFame()
        {
            var c = NewCareer();
            var npcs = c.Population.Database.npcs.Where(n => !n.isIncarcerated && n.fame < 20).Take(300).ToList();
            c.Player.fame = 0;
            Assert.AreEqual(0, npcs.Count(n => c.Fans.Roll(n) != null), "無名なら誰も寄ってこない");

            c.Player.fame = 85;
            for (int i = 0; i < 2; i++) c.AdvanceDay(); // 同じ日に同じ人へは1回だけ
            int requests = npcs.Count(n => c.Fans.Roll(n) != null);
            Assert.Greater(requests, 60);
        }

        [Test]
        public void AutographNeedsASignature()
        {
            var c = NewCareer();
            var req = new FanRequest { npcId = 0, type = FanRequestType.Autograph };
            Assert.AreEqual(FanResponseResult.NoSignature, c.Fans.Respond(req, true));

            var stroke = new SignatureStroke();
            for (int i = 0; i <= 20; i++) stroke.Add(i / 20f, 0.5f + 0.3f * (float)Math.Sin(i));
            c.Player.signature.strokes.Add(SignatureDesign.Simplify(stroke));
            Assert.IsTrue(c.Player.signature.IsValid, c.Player.signature.Validate());
            int before = c.Population.Knowledge.GetOrCreate(0).affinity;
            Assert.AreEqual(FanResponseResult.Accepted, c.Fans.Respond(req, true));
            Assert.Greater(c.Population.Knowledge.Get(0).affinity, before);
        }

        [Test]
        public void SignatureAndPerformanceValidation()
        {
            var sig = new SignatureDesign();
            Assert.IsTrue(!sig.IsValid);
            var bad = new SignatureStroke();
            bad.Add(0.1f, 0.1f); bad.Add(1.5f, 0.2f);
            sig.strokes.Add(bad);
            Assert.IsTrue(!sig.IsValid, "枠外の点");

            var perf = new GoalPerformanceDesign { name = "流星バク宙" };
            Assert.IsTrue(!perf.IsValid);
            perf.moves.AddRange(new[] { PerformanceMove.KneeSlide, PerformanceMove.Backflip, PerformanceMove.PointToSky });
            Assert.IsTrue(perf.IsValid);
            float fresh = perf.Score();
            perf.timesUsed = 10;
            Assert.Less(perf.Score(), fresh, "使い回すと飽きられる");
            perf.moves.AddRange(new[] { PerformanceMove.Dance, PerformanceMove.Dance, PerformanceMove.Dance, PerformanceMove.Dance });
            Assert.IsTrue(!perf.IsValid, "動きは6個まで");
        }

        [Test]
        public void GreatMatchRaisesFameAndMarketValue()
        {
            var c = NewCareer();
            long before = c.Player.marketValue;
            float gain = c.Fame.RecordMatch(MatchCategory.RegionalDerby, 9.5f, 3, 1, 4);
            Assert.Greater(gain, 5f);
            Assert.Greater(c.Player.marketValue, before);
            var perf = new GoalPerformanceDesign { name = "しーっ", moves = { PerformanceMove.ShushCrowd } };
            c.Fame.RecordGoalCelebration(perf);

            var issue = c.AdvanceDay().newspaper;
            Assert.IsTrue(issue.articles.Any(a => a.aboutPlayer && a.category == ArticleCategory.Player), "活躍が新聞に載る");
            Assert.IsTrue(issue.articles.Any(a => a.aboutPlayer && a.category == ArticleCategory.Gossip), "挑発パフォーマンスはゴシップに");
        }

        // ---------------- 新聞・人気投票 ----------------

        [Test]
        public void NewspaperCoversMatchesRankingGossipAndVote()
        {
            var c = NewCareer();
            var categories = new HashSet<ArticleCategory>();
            for (int d = 0; d < 31; d++)
            {
                var issue = c.AdvanceDay().newspaper;
                Assert.AreEqual(c.Population.Database.currentDay, issue.day);
                foreach (var a in issue.articles) categories.Add(a.category);
                for (int i = 1; i < issue.articles.Count; i++)
                    Assert.GreaterOrEqual(issue.articles[i - 1].priority, issue.articles[i].priority);
                Assert.IsTrue(!issue.articles.Any(a => a.title.Contains("裏フットサル") || a.title.Contains("刑務所リーグ")), "裏の試合は表の新聞に載らない");
            }
            foreach (var cat in new[] { ArticleCategory.Match, ArticleCategory.Ranking, ArticleCategory.Gossip, ArticleCategory.Vote, ArticleCategory.Lottery })
                Assert.IsTrue(categories.Contains(cat), cat + " の記事がない");

            Assert.AreEqual(10, c.Media.lastVote.Count);
            for (int i = 1; i < c.Media.lastVote.Count; i++) Assert.GreaterOrEqual(c.Media.lastVote[i - 1].votes, c.Media.lastVote[i].votes);
            Assert.IsTrue(c.Media.lastVote.All(v => v.npcId < 0 || !c.Population.Database.Get(v.npcId).isUnderground));
        }

        [Test]
        public void ScoutingMakesTheNews()
        {
            var c = NewCareer();
            var npc = OrdinaryAdult(c);
            BefriendFully(c, npc);
            c.Player.cash = 10000000000;
            c.Scouting.Scout(npc.id);
            var issue = c.AdvanceDay().newspaper;
            Assert.IsTrue(issue.articles.Any(a => a.category == ArticleCategory.Transfer && a.npcIds.Contains(npc.id)));
        }
    }
}
