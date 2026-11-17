using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class ReincarnationTests
    {
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));

        /// <summary>1周目：何人か獲得し、化ける選手と知り合ってから引退する。</summary>
        static LegacyRecord LiveFirstLife(out IslandCareer c)
        {
            c = NewCareer();
            var db = c.Population.Database;
            c.Player.cash = 100000000000;
            c.Player.fame = 100; // エゴイストにも断られないように
            foreach (var n in db.npcs.Where(x => x.age >= 20 && x.age <= 30 && !x.isUnderground && !x.isIncarcerated).OrderByDescending(x => x.Overall).Take(5))
            {
                c.Population.Knowledge.Reveal(n.id, NPCInfoFlags.All);
                c.Population.Knowledge.GetOrCreate(n.id).affinity = 100;
                Assert.AreEqual(ScoutResult.Success, c.Scouting.Scout(n.id));
            }
            foreach (var n in db.npcs.Where(x => x.growthType == GrowthType.Awakening && x.potential >= 70).Take(4))
                c.Population.Knowledge.Reveal(n.id, NPCInfoFlags.Basic);
            c.Player.abilities.shooting = 90;
            c.Player.level = 12;
            Assert.IsTrue(ReincarnationSystem.Retire(c) == null, "若すぎて引退できない");
            c.Player.age = 33;
            return ReincarnationSystem.Retire(c);
        }

        [Test]
        public void RetiringRecordsThePastLife()
        {
            var legacy = LiveFirstLife(out var c);
            Assert.AreEqual(1, legacy.cycle);
            Assert.AreEqual(5, legacy.teammates.Count);
            Assert.AreEqual(90, legacy.abilities.shooting);
            Assert.AreEqual(12, legacy.level);
            Assert.GreaterOrEqual(legacy.bloomers.Count, 1);
        }

        [Test]
        public void NextLifeIsTheSameIslandFromAnotherTownWithDejaVu()
        {
            var legacy = LiveFirstLife(out var first);
            var second = ReincarnationSystem.StartNextCycle(legacy, null);
            Assert.AreEqual(2, second.Player.cycle);
            Assert.AreNotEqual(legacy.homeTown, second.Player.homeTown, "違う街から始まる");
            Assert.AreEqual(1, second.Player.level, "能力は引き継がない");
            Assert.AreEqual(1, second.PastLives.Count);

            var a = first.Population.Database.npcs;
            var b = second.Population.Database.npcs;
            Assert.IsTrue(a.Take(50).Select(n => n.FullName).SequenceEqual(b.Take(50).Select(n => n.FullName)), "同じ島民がいる");

            Assert.IsTrue(second.Player.dejaVu.Count > 0);
            foreach (var id in second.Player.dejaVu)
                Assert.IsTrue(second.Population.Knowledge.Knows(id, NPCInfoFlags.GrowthHint), "どの選手が化けるか知っている");
            Assert.AreEqual(0, second.Player.contracts.Count);
        }

        [Test]
        public void PastSelfIsTheMaskedFinalBoss()
        {
            var legacy = LiveFirstLife(out _);
            var second = ReincarnationSystem.StartNextCycle(legacy, null);
            var boss = BossTeams.Build(BossKind.PastSelf, second, new SeededRandom(1));
            Assert.AreEqual(11, boss.players.Count);
            var leader = boss.players.First(p => p.name == "仮面の男");
            Assert.AreEqual(90, leader.stats.shooting, "前世の自分の能力");
            Assert.IsTrue(leader.specials.Any(m => m.name == "移住者シュート"), "同じ技を使ってくる");
            Assert.IsTrue(boss.traits.Contains(BossTrait.GeniusVision));
            var names = legacy.teammates.Select(t => t.name).ToList();
            Assert.IsTrue(!boss.players.Any(p => names.Contains(p.name) || p.name == legacy.playerName), "正体は明かさない");

            Assert.IsTrue(TournamentSystem.CreateSummitCup(NewCareer()) == null, "1周目には出ない");
            var cup = TournamentSystem.CreateSummitCup(second);
            Assert.AreEqual(BossKind.PastSelf, cup.State.entries[cup.State.alive.Last()].boss, "決勝で当たる");
        }

        [Test]
        public void ThirdLifeUnlocksTheIslandMaster()
        {
            var legacy = LiveFirstLife(out _);
            var second = ReincarnationSystem.StartNextCycle(legacy, null);
            Assert.IsTrue(TournamentSystem.CreateReincarnationFinal(second) == null);
            second.Player.age = 40;
            var legacy2 = ReincarnationSystem.Retire(second);
            var third = ReincarnationSystem.StartNextCycle(legacy2, second.PastLives);
            Assert.AreEqual(3, third.Player.cycle);
            Assert.AreEqual(2, third.PastLives.Count);
            var final = TournamentSystem.CreateReincarnationFinal(third);
            Assert.IsTrue(final != null);
            var boss = BossTeams.Build(BossKind.IslandMaster, third, new SeededRandom(2));
            Assert.IsTrue(boss.players.Any(p => p.name == "島主" && p.stats.shooting >= 95));
        }

        [Test]
        public void AgeGoesUpEachYear()
        {
            var c = NewCareer();
            int age = c.Player.age;
            for (int d = 0; d < 365; d++) c.AdvanceDay();
            Assert.AreEqual(age + 1, c.Player.age);
        }

        // ---------------- 恋愛 ----------------

        static NPCData Crush(IslandCareer c, Personality p)
        {
            var n = c.Population.Database.npcs.First(x => x.age >= 22 && x.age <= 35 && x.personality == p && !x.isIncarcerated);
            c.Population.Knowledge.Reveal(n.id, NPCInfoFlags.Basic);
            c.Population.Knowledge.GetOrCreate(n.id).affinity = 70;
            return n;
        }

        [Test]
        public void RomanceIsLockedInTheFirstLife()
        {
            var c = NewCareer();
            var n = Crush(c, Personality.Extrovert);
            Assert.IsTrue(!c.Romance.Date(n.id, DateSpot.Bar).ok);
            var minor = c.Population.Database.npcs.First(x => x.age < 20);
            c.Player.cycle = 2;
            c.Population.Knowledge.Reveal(minor.id, NPCInfoFlags.Basic);
            c.Population.Knowledge.GetOrCreate(minor.id).affinity = 90;
            Assert.IsTrue(!c.Romance.Date(minor.id, DateSpot.Cafe).ok, "20歳未満は対象外");
        }

        [Test]
        public void DatesGiftsAndConfession()
        {
            var c = NewCareer();
            c.Player.cycle = 2;
            c.Player.cash = 10000000;
            var n = Crush(c, Personality.Introvert);

            var liked = c.Romance.Date(n.id, DateSpot.Cafe);
            Assert.IsTrue(liked.ok);
            Assert.IsTrue(!c.Romance.Date(n.id, DateSpot.Cafe).ok, "1日1回");
            c.AdvanceDay();
            var meh = c.Romance.Date(n.id, DateSpot.Bar);
            Assert.Greater(liked.loveDelta, meh.loveDelta, "好みの場所だと喜ぶ");

            var item = c.World.State.items.First(i => !i.alwaysSoldOutOnSurface && !i.isUnderground && !i.requiresNegotiation);
            item.status = ItemStatus.OnSale;
            item.stock = 1; // 最後の1個
            Assert.IsTrue(c.Romance.Gift(n.id, item.id).ok);
            Assert.AreEqual(ItemStatus.SoldOut, item.status, "一点物は完売する");

            Assert.IsTrue(!c.Romance.Confess(n.id).ok, "まだ早い");
            c.Romance.State.Get(n.id).love = 100;
            c.Population.Knowledge.Get(n.id).affinity = 90;
            Assert.IsTrue(c.Romance.Confess(n.id).ok);
            Assert.AreEqual(n.id, c.Romance.State.PartnerId);
            var other = Crush(c, Personality.Extrovert);
            c.Romance.State.entries.Add(new RomanceEntry { npcId = other.id, love = 100 });
            Assert.IsTrue(!c.Romance.Confess(other.id).ok, "恋人は1人");
        }

        [Test]
        public void FamousRomanceMakesTheNewsAndPartnerFiresUp()
        {
            var c = NewCareer();
            c.Player.cycle = 2;
            c.Player.fame = 60;
            var m = c.World.State.matches.First(x => !x.played && !x.IsUnderground && x.category != MatchCategory.PrisonLeague && x.homeLineup.Any(id => c.Population.Database.Get(id).age >= 22));
            var n = c.Population.Database.Get(m.homeLineup.First(id => c.Population.Database.Get(id).age >= 22));
            c.Population.Knowledge.Reveal(n.id, NPCInfoFlags.Basic);
            c.Population.Knowledge.GetOrCreate(n.id).affinity = 70;
            c.Romance.State.entries.Add(new RomanceEntry { npcId = n.id, love = 100 });
            c.Population.Knowledge.Get(n.id).affinity = 95;
            Assert.IsTrue(c.Romance.Confess(n.id).ok);
            Assert.IsTrue(c.AdvanceDay().newspaper.articles.Any(a => a.title.Contains("熱愛発覚")));

            var withPartner = MatchFactory.FromWorldMatch(c, m, Side.Home, 1);
            float t = withPartner.home.players.First(p => p.npcId == n.id).tension;
            c.Romance.State.Get(n.id).status = RomanceStatus.Dating;
            var without = MatchFactory.FromWorldMatch(c, m, Side.Home, 1);
            Assert.Greater(t, without.home.players.First(p => p.npcId == n.id).tension);
        }
    }
}
