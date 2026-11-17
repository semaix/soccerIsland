using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    /// <summary>NPCが話したこと（試合・買い物）が実際に起きているかの検査。</summary>
    public class WorldLinkTests
    {
        struct Said
        {
            public int npcId;
            public int day;
            public int affinity;
            public TopicMention topic;
        }

        static IslandWorld NewWorld(int seed = 4242)
        {
            var s = NPCGenerationSettings.CreateDefault();
            s.seed = seed;
            return new IslandWorld(IslandPopulation.CreateNew(s));
        }

        /// <summary>毎日ランダムな島民に話しかけ、話題に出たことをその場で検証しながら集める。</summary>
        static List<Said> TalkAround(IslandWorld world, int days, int talksPerDay, System.Action<Said, IslandWorld> checkNow)
        {
            var pop = world.Population;
            var rng = new SeededRandom(99);
            var said = new List<Said>();
            for (int d = 0; d < days; d++)
            {
                for (int t = 0; t < talksPerDay; t++)
                {
                    var npc = pop.Database.npcs[rng.Range(0, pop.Database.Count)];
                    var r = pop.Talk(npc.id);
                    foreach (var topic in r.topics)
                    {
                        var s = new Said { npcId = npc.id, day = pop.Database.currentDay, affinity = r.affinityAfter, topic = topic };
                        checkNow(s, world);
                        said.Add(s);
                    }
                }
                world.AdvanceDay();
            }
            return said;
        }

        [Test]
        public void MatchTalkMeansNPCActuallyPlays()
        {
            var world = NewWorld();
            var said = TalkAround(world, 40, 60, (s, w) =>
            {
                if (s.topic.kind != TopicKind.Match) return;
                var m = w.State.GetMatch(s.topic.refId);
                Assert.IsTrue(m != null, "試合が存在しない");
                Assert.IsTrue(m.Involves(s.npcId), "話した本人が出場メンバーにいない");
                Assert.IsTrue(!m.played && m.day >= s.day && m.day <= s.day + 7, "終わった試合・遠い試合の話をした");
            }).Where(s => s.topic.kind == TopicKind.Match).ToList();

            Assert.Greater(said.Count, 20, "試合の話がほとんど出ていない");
            foreach (var s in said)
            {
                var m = world.State.GetMatch(s.topic.refId);
                if (m == null) continue; // 古い試合は掃除される
                if (m.day < world.Population.Database.currentDay)
                {
                    Assert.IsTrue(m.played, "予定日を過ぎても試合が行われていない");
                    Assert.IsTrue(m.Involves(s.npcId), "当日のメンバーから外れている");
                }
            }
        }

        [Test]
        public void PurchaseTalkMeansItemIsSoldOutOrNegotiating()
        {
            var world = NewWorld();
            var said = TalkAround(world, 40, 60, (s, w) =>
            {
                if (s.topic.kind != TopicKind.Purchase) return;
                var item = w.State.GetItem(s.topic.refId);
                Assert.IsTrue(item.status == ItemStatus.SoldOut || item.status == ItemStatus.Negotiating,
                    $"{item.name} が {item.status} のまま");
                if (item.status == ItemStatus.Negotiating) Assert.AreEqual(s.npcId, item.buyerId);
                Assert.IsTrue(w.State.purchases.Any(p => p.npcId == s.npcId && p.itemId == item.id), "購入履歴がない");
            }).Where(s => s.topic.kind == TopicKind.Purchase).ToList();
            Assert.Greater(said.Count, 3, "買い物の話がほとんど出ていない");
        }

        [Test]
        public void UndergroundTopicsNeedTrust()
        {
            var world = NewWorld();
            TalkAround(world, 40, 60, (s, w) =>
            {
                bool underground =
                    s.topic.kind == TopicKind.Match && w.State.GetMatch(s.topic.refId).IsUnderground ||
                    s.topic.kind == TopicKind.Purchase && w.State.GetItem(s.topic.refId).isUnderground;
                if (underground) Assert.GreaterOrEqual(s.affinity, 60, "信頼されていないのに裏の話をした");
            });
        }

        [Test]
        public void ScheduleCoversAllCategoriesWithoutDoubleBooking()
        {
            var world = NewWorld();
            var seen = new HashSet<MatchCategory>();
            for (int d = 0; d < 62; d++)
            {
                var today = world.TodaysMatches();
                var ids = today.SelectMany(m => m.homeLineup.Concat(m.awayLineup)).ToList();
                Assert.AreEqual(ids.Count, ids.Distinct().Count(), $"{d}日目に同じ選手が2試合に出ている");
                foreach (var m in today) seen.Add(m.category);
                world.AdvanceDay();
            }
            foreach (MatchCategory c in System.Enum.GetValues(typeof(MatchCategory)))
                Assert.IsTrue(seen.Contains(c), c + " の試合がない");
        }

        [Test]
        public void AlwaysSoldOutItemsAreOnlyInTheBlackMarket()
        {
            var world = NewWorld();
            for (int d = 0; d < 30; d++) world.AdvanceDay();
            foreach (var item in world.State.items.Where(i => i.alwaysSoldOutOnSurface))
            {
                Assert.AreEqual(ItemStatus.SoldOut, item.SurfaceStatus);
                Assert.IsTrue(!world.State.purchases.Any(p => p.itemId == item.id), "表で常に完売の商品が売れた");
            }
            var black = world.State.items.Where(i => i.isUnderground).Select(i => i.id).ToList();
            foreach (var p in world.State.purchases.Where(p => black.Contains(p.itemId)))
                Assert.IsTrue(world.Population.Database.Get(p.npcId).isUnderground, "表の人間が裏市場で買い物した");
        }
    }
}
