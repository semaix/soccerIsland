using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class IslandEventTests
    {
        // このファイルは各システムの動きを見るので、場所の制限（同じ場所にいる人としか話せない等）は外す
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault()))) { requirePresence = false };

        static void Befriend(IslandCareer c, NPCData n, int affinity)
        {
            c.Population.Knowledge.Reveal(n.id, NPCInfoFlags.Basic);
            c.Population.Knowledge.GetOrCreate(n.id).affinity = affinity;
        }

        /// <summary>毎日話しかけて、目的の話題が出るまで待つ。</summary>
        static bool TalkUntil(IslandCareer c, NPCData n, System.Func<bool> done, int days = 40)
        {
            for (int d = 0; d < days && !done(); d++)
            {
                c.Interact(n.id);
                c.AdvanceDay();
            }
            return done();
        }

        // ---------------- 裏市場 ----------------

        [Test]
        public void BlackMarketMovesEveryFewDays()
        {
            var c = NewCareer();
            var seen = new System.Collections.Generic.HashSet<string>();
            string prev = c.BlackMarket.CurrentLocation;
            int changes = 0;
            for (int d = 0; d < 30; d++)
            {
                seen.Add(c.BlackMarket.CurrentLocation);
                c.AdvanceDay();
                if (c.BlackMarket.CurrentLocation != prev) { changes++; prev = c.BlackMarket.CurrentLocation; }
            }
            Assert.GreaterOrEqual(changes, 9, "3日ごとに場所が変わる");
            Assert.Greater(seen.Count, 3);
        }

        [Test]
        public void UndergroundShopkeeperTellsYouWhereItIs()
        {
            var c = NewCareer();
            var keeper = c.Population.Database.npcs.FirstOrDefault(n => n.isUnderground && !n.isIncarcerated &&
                (n.occupation == Occupation.Bartender || n.occupation == Occupation.Mafia));
            Assume.That(keeper == null ? 0 : 1, Is.GreaterThan(0));
            c.Player.blackMarketAccess = true;
            Assert.IsTrue(!c.BlackMarket.CanEnter, "場所を知らないと入れない");
            Befriend(c, keeper, 90);
            bool learned = false;
            for (int i = 0; i < 40 && !learned; i++)
            {
                var r = c.Interact(keeper.id);
                learned = c.BlackMarket.CanEnter;
                if (!learned) c.AdvanceDay();
            }
            Assert.IsTrue(learned, "今夜の場所を教えてもらえる");
            Assert.Greater(c.BlackMarket.Items().Count, 0);
            Assert.Greater(c.BlackMarket.UndergroundRanking(5).Count, 0);

            // 場所が変わるとまた聞き直し
            for (int d = 0; d < BlackMarket.PeriodDays; d++) c.AdvanceDay();
            Assert.IsTrue(!c.BlackMarket.CanEnter);
        }

        [Test]
        public void BlackMarketSellsInfoOnlyAboutUndergroundPeople()
        {
            var c = NewCareer();
            c.Player.blackMarketAccess = true;
            c.BlackMarket.LearnLocation();
            c.Player.cash = 10000000;
            var mafia = c.Population.Database.npcs.First(n => n.isUnderground);
            var normal = c.Population.Database.npcs.First(n => !n.isUnderground && n.registeredWithAssociation);
            Assert.IsTrue(!c.BlackMarket.BuyInfo(normal.id), "表の人の情報は売っていない");
            Assert.IsTrue(c.BlackMarket.BuyInfo(mafia.id));
            Assert.IsTrue(c.Population.Knowledge.Knows(mafia.id, NPCInfoFlags.MarketValue | NPCInfoFlags.UndergroundSecret));

            var item = c.World.State.items.First(i => i.isUnderground && i.status == ItemStatus.OnSale && i.stock > 0);
            int stock = item.stock;
            Assert.IsTrue(c.BlackMarket.Buy(item.id));
            Assert.AreEqual(stock - 1, item.stock);
        }

        // ---------------- 謎のプレーヤー ----------------

        [Test]
        public void HuntingDownTheMysteryPlayer()
        {
            var c = NewCareer();
            var phantom = c.Mystery.Phantom;
            Assert.IsTrue(phantom != null);
            Assert.IsTrue(!BossTeams.KnowsUndergroundKingsWeakness(c));

            Befriend(c, phantom, 50);
            for (int i = 0; i < 10; i++) c.Interact(phantom.id);
            Assert.IsTrue(!c.Mystery.Found, "手がかりなしでは正体を明かさない");

            // 島民の噂を集める
            foreach (var n in c.Population.Database.npcs.Where(x => x.id != phantom.id && x.personality == Personality.Extrovert).Take(200))
            {
                if (c.Mystery.Clues >= MysteryPlayerQuest.CluesNeeded) break;
                Befriend(c, n, 30);
                c.Interact(n.id);
            }
            Assert.AreEqual(MysteryPlayerQuest.CluesNeeded, c.Mystery.Clues);
            Assert.IsTrue(c.Population.Knowledge.Knows(phantom.id, NPCInfoFlags.Occupation), "正体の目星がつく");

            Assert.IsTrue(TalkUntil(c, phantom, () => c.Mystery.Found));
            Assert.IsTrue(BossTeams.KnowsUndergroundKingsWeakness(c), "裏の王者の弱点になる");
        }

        // ---------------- 漂流者 ----------------

        [Test]
        public void HelpingACastawayEarnsGratitude()
        {
            var c = NewCareer();
            var cast = c.Population.Database.npcs.First(n => n.isStranded);
            Assert.IsTrue(!cast.registeredWithAssociation);
            Assert.IsTrue(!c.Castaways.Register(cast.id), "仲良くないと頼まれない");
            Befriend(c, cast, 60);
            long full = c.Scouting.Price(cast);
            Assert.IsTrue(c.Castaways.Register(cast.id));
            Assert.IsTrue(cast.registeredWithAssociation);
            Assert.IsTrue(cast.AppearsInOfficialRanking || cast.isUnderground, "協会のランキングに載るようになる");
            Assert.Less(c.Scouting.Price(cast), full, "恩を感じて安く来てくれる");
            Assert.IsTrue(cast.isStranded, "島から出られないのは変わらない");
        }
    }
}
