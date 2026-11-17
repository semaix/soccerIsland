using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace SoccerIsland.NPC.Tests
{
    public class NPCSystemTests
    {
        static IslandPopulation NewIsland(int seed = 12345)
        {
            var s = NPCGenerationSettings.CreateDefault();
            s.seed = seed;
            return IslandPopulation.CreateNew(s);
        }

        [Test]
        public void Generates1000NPCs()
        {
            var db = NewIsland().Database;
            Assert.AreEqual(1000, db.Count);
            Assert.IsTrue(db.npcs.Select(n => n.id).Distinct().Count() == 1000);
        }

        [Test]
        public void SameSeedProducesSameIsland()
        {
            var a = NewIsland(77).Database;
            var b = NewIsland(77).Database;
            for (int i = 0; i < a.Count; i++)
            {
                Assert.AreEqual(a.npcs[i].FullName, b.npcs[i].FullName);
                Assert.AreEqual(a.npcs[i].marketValue, b.npcs[i].marketValue);
                Assert.AreEqual(a.npcs[i].Overall, b.npcs[i].Overall);
            }
        }

        [Test]
        public void PhoneNumbersAreUnique()
        {
            var phones = NewIsland().Database.npcs.Where(n => n.phoneNumber != "").Select(n => n.phoneNumber).ToList();
            Assert.AreEqual(phones.Count, phones.Distinct().Count());
        }

        [Test]
        public void EveryTownAndSpecialPersonalityExists()
        {
            var db = NewIsland().Database;
            foreach (Town t in System.Enum.GetValues(typeof(Town)))
                Assert.Greater(db.GetByTown(t).Count, 100, t.ToString());
            Assert.GreaterOrEqual(db.npcs.Count(n => n.soccerPersonality == SoccerPersonality.God), 1);
            Assert.GreaterOrEqual(db.npcs.Count(n => n.soccerPersonality == SoccerPersonality.Gifted), 3);
            Assert.Greater(db.npcs.Count(n => n.gender == Gender.Female), 250);
            Assert.AreEqual(24, db.npcs.Count(n => n.isIncarcerated));
        }

        [Test]
        public void OfficialRankingExcludesUndergroundAndIsSorted()
        {
            var ranking = NewIsland().Database.GetOfficialMarketValueRanking(50);
            Assert.AreEqual(50, ranking.Count);
            Assert.IsTrue(ranking.All(n => n.AppearsInOfficialRanking));
            for (int i = 1; i < ranking.Count; i++)
                Assert.GreaterOrEqual(ranking[i - 1].marketValue, ranking[i].marketValue);
        }

        [Test]
        public void RelationsAreMutual()
        {
            var db = NewIsland().Database;
            foreach (var n in db.npcs)
                foreach (var r in n.relations)
                    Assert.IsTrue(db.Get(r.targetId).HasRelation(n.id), $"{n.id}->{r.targetId}");
        }

        [Test]
        public void ClassmatesShareTownAndBirthYear()
        {
            var db = NewIsland().Database;
            foreach (var n in db.npcs)
                foreach (var c in db.GetClassmates(n))
                {
                    Assert.AreEqual(n.hometown, c.hometown);
                    Assert.AreEqual(n.birthYear, c.birthYear);
                }
        }

        [Test]
        public void TalkingRevealsInfoProgressively()
        {
            var pop = NewIsland();
            var npc = pop.Database.npcs.First(n => n.personality == Personality.Extrovert && n.phoneNumber != "");
            Assert.AreEqual(NPCProfileView.Unknown, pop.GetProfile(npc.id).name);

            pop.Talk(npc.id);
            var p1 = pop.GetProfile(npc.id);
            Assert.AreEqual(npc.FullName, p1.name);
            Assert.AreEqual(NPCProfileView.Unknown, p1.talentRank, "才能度は初対面ではわからない");

            for (int day = 0; day < 30; day++)
            {
                pop.Talk(npc.id);
                pop.AdvanceDay();
            }
            var p2 = pop.GetProfile(npc.id);
            Assert.AreNotEqual(NPCProfileView.Unknown, p2.talentRank);
            Assert.AreEqual(npc.phoneNumber, p2.phoneNumber);
        }

        [Test]
        public void TalkingChangesMarketValue()
        {
            var pop = NewIsland();
            var npc = pop.Database.npcs.OrderByDescending(n => n.marketValue).First(n => n.fame < 80);
            long before = npc.marketValue;
            var result = pop.Talk(npc.id);
            Assert.IsTrue(result.marketChanges.Any(c => c.npcId == npc.id));
            Assert.Greater(npc.marketValue, before);
        }

        [Test]
        public void HypeDecaysOverTime()
        {
            var pop = NewIsland();
            var npc = pop.Database.npcs[0];
            pop.Market.AddHype(npc, 0.5f, "test");
            for (int i = 0; i < 120; i++) pop.AdvanceDay();
            Assert.Less(npc.hype, 0.01f);
        }

        [Test]
        public void AwakeningTypeEventuallyAwakens()
        {
            var pop = NewIsland();
            var sleepers = pop.Database.npcs.Where(n => n.growthType == GrowthType.Awakening && !n.hasAwakened && n.awakeningAge - n.age <= 3).ToList();
            Assume.That(sleepers.Count, Is.GreaterThan(0));
            var before = sleepers.ToDictionary(n => n.id, n => n.Overall);
            var events = new List<LifeEvent>();
            for (int y = 0; y < 4; y++) events.AddRange(pop.Lifecycle.AdvanceYear());
            foreach (var n in sleepers)
            {
                Assert.IsTrue(n.hasAwakened);
                Assert.Greater(n.Overall, before[n.id]);
                Assert.IsTrue(events.Any(e => e.type == LifeEventType.Awakened && e.npcId == n.id));
            }
        }
    }
}
