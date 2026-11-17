using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class FestivalAndRelationshipTests
    {
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));

        // ---------------- 年中行事 ----------------

        [Test]
        public void TheCalendarHasFestivals()
        {
            var cal = new IslandCalendar(1);
            Assert.IsTrue(cal.On(0).Any(f => f.kind == FestivalKind.NewYearKickoff), "元日は初蹴り");
            Assert.IsTrue(cal.On(365).Any(f => f.kind == FestivalKind.NewYearKickoff), "毎年ある");
            Assert.IsTrue(cal.On(150).Any(f => f.kind == FestivalKind.SummerFestival && f.place == IslandPlace.Beach && f.time == TimeOfDay.Night));
            Assert.IsTrue(cal.On(364).Any(f => f.kind == FestivalKind.YearEndGala));
            Assert.AreEqual(0, cal.On(100).Count(f => f.kind != FestivalKind.CleanupVolunteer));

            int seed = 5;
            int stormDay = Enumerable.Range(1, 300).First(d => WeatherSystem.WeatherOf(seed, d) == Weather.Storm && WeatherSystem.WeatherOf(seed, d + 1) != Weather.Storm);
            Assert.IsTrue(new IslandCalendar(seed).On(stormDay + 1).Any(f => f.kind == FestivalKind.CleanupVolunteer), "台風一過は清掃ボランティア");
        }

        [Test]
        public void IslandersGatherAtFestivals()
        {
            var c = NewCareer();
            var db = c.Population.Database;
            // 夏祭りの夜、煌浜の浜辺
            var fest = c.Schedule.calendar.On(150).First(f => f.kind == FestivalKind.SummerFestival);
            int crowd = db.npcs.Count(n => c.Schedule.Locate(n, 150, TimeOfDay.Night).Same(new IslandLocation(Town.Kirahama, IslandPlace.Beach)));
            int usual = db.npcs.Count(n => c.Schedule.Locate(n, 151, TimeOfDay.Night).Same(new IslandLocation(Town.Kirahama, IslandPlace.Beach)));
            Assert.Greater(crowd, 200, "島中から人が集まる");
            Assert.Less(usual, 20);
            Assert.IsTrue(db.npcs.Where(n => n.isIncarcerated).All(n => c.Schedule.Locate(n, 150, TimeOfDay.Night).place == IslandPlace.Prison), "受刑者は来られない");
        }

        [Test]
        public void JoiningAFestival()
        {
            var c = NewCareer();
            while (c.Population.Database.currentDay < 150) c.AdvanceDay();
            Assert.AreEqual("ここでは何もやっていない", c.JoinFestival().text);
            c.Player.timeOfDay = (int)TimeOfDay.Evening; // 古鐘町から煌浜までは1時間帯 → 着くと深夜
            c.MoveTo(Town.Kirahama, IslandPlace.Beach);
            Assert.AreEqual(TimeOfDay.Night, c.Now);
            Assert.IsTrue(c.CurrentFestival != null && c.CurrentFestival.kind == FestivalKind.SummerFestival);
            int rep = c.Towns.Of(Town.Kirahama);
            var r = c.JoinFestival();
            Assert.IsTrue(r.ok);
            Assert.Greater(r.peopleMet, 50);
            Assert.Greater(c.Towns.Of(Town.Kirahama), rep);
            Assert.IsTrue(!c.JoinFestival().ok, "1回だけ");
        }

        [Test]
        public void FestivalsMakePeopleFriendlier()
        {
            var c = NewCareer();
            var n = c.Population.Database.npcs.First(x => x.hometown == Town.Furugane && x.personality == Personality.Extrovert);
            int normal = c.Population.Conversation.affinityModifier(n, 10);
            c.Player.timeOfDay = (int)TimeOfDay.Morning;
            c.Player.currentTown = Town.Furugane;
            c.Player.currentPlace = (int)IslandPlace.ClubGround; // 元日の朝の初蹴り会場
            Assert.IsTrue(c.CurrentFestival != null);
            Assert.Greater(c.Population.Conversation.affinityModifier(n, 10), normal);
        }

        [Test]
        public void FestivalNewsAndSaveSurvival()
        {
            var c = NewCareer();
            while (c.Population.Database.currentDay < 29) c.AdvanceDay();
            Assert.IsTrue(c.AdvanceDay().newspaper.articles.Any(a => a.title.Contains("花見サッカー")));
            Assert.IsTrue(c.CreateSave().events.festivalsJoined != null);
        }

        // ---------------- 人間関係の変化 ----------------

        [Test]
        public void RelationshipsChangeOverTime()
        {
            var c = NewCareer();
            var dyn = c.Relationships;
            dyn.reconcileChance = 1f; dyn.marriageChance = 1f; dyn.splitChance = 1f;
            var changes = dyn.RunMonth();
            var kinds = new HashSet<RelationChangeKind>(changes.Select(x => x.kind));
            Assert.IsTrue(kinds.Contains(RelationChangeKind.Reconciled));
            Assert.IsTrue(kinds.Contains(RelationChangeKind.Married));
            Assert.IsTrue(kinds.Contains(RelationChangeKind.BecameFriends));
            var db = c.Population.Database;
            foreach (var ch in changes)
            {
                var a = db.Get(ch.a); var b = db.Get(ch.b);
                var ra = a.FindRelation(b.id); var rb = b.FindRelation(a.id);
                Assert.IsTrue(ra != null && rb != null, "関係は両方向");
                if (ch.kind == RelationChangeKind.Reconciled) Assert.AreEqual(RelationType.Friend, ra.type);
                if (ch.kind == RelationChangeKind.Married) Assert.AreEqual(RelationType.Spouse, rb.type);
                if (ch.kind == RelationChangeKind.MentorSplit) Assert.AreEqual(RelationType.Rival, ra.type);
            }
            // 結婚は1人1回
            var spouses = db.npcs.Where(n => n.relations.Count(r => r.type == RelationType.Spouse) > 1).ToList();
            Assert.AreEqual(0, spouses.Count);
        }

        [Test]
        public void ThePupilWhoSurpassesTheMasterSplits()
        {
            var c = NewCareer();
            var db = c.Population.Database;
            var pupil = db.npcs.First(n => n.relations.Any(r => r.type == RelationType.Mentor));
            var mentor = db.Get(pupil.relations.First(r => r.type == RelationType.Mentor).targetId);
            for (int i = 0; i < NPCAbilities.StatCount; i++) pupil.abilities[i] = 99;
            c.Population.Knowledge.AddRelation(pupil.id, mentor.id, RelationType.Mentor, -1, 0);
            c.Relationships.splitChance = 1f;
            var ch = c.Relationships.RunMonth().First(x => x.kind == RelationChangeKind.MentorSplit && x.a == pupil.id);
            Assert.IsTrue(ch.wasKnown);
            Assert.AreEqual(RelationType.Rival, c.Population.Knowledge.knownRelations.First(r => r.fromId == pupil.id && r.toId == mentor.id || r.fromId == mentor.id && r.toId == pupil.id).type, "相関図も描き換わる");
        }

        [Test]
        public void YourLoveInterestDoesNotMarrySomeoneElse()
        {
            var c = NewCareer();
            var db = c.Population.Database;
            var crushPair = db.npcs.SelectMany(a => a.relations.Where(r => r.type == RelationType.Friend && r.affinity >= 70 && a.age >= 24 && a.age <= 50).Select(r => a)).First(a => db.Get(a.relations.First(r => r.type == RelationType.Friend && r.affinity >= 70).targetId).age >= 24);
            c.Romance.State.entries.Add(new RomanceEntry { npcId = crushPair.id, love = 80, status = RomanceStatus.Dating });
            c.Relationships.marriageChance = 1f;
            var changes = c.Relationships.RunMonth();
            Assert.IsTrue(!changes.Any(x => x.kind == RelationChangeKind.Married && (x.a == crushPair.id || x.b == crushPair.id)));
        }

        [Test]
        public void BigRelationshipNewsMakesThePaper()
        {
            var c = NewCareer();
            c.Relationships.marriageChance = 1f;
            while (c.Population.Database.currentDay % 30 != 10) c.AdvanceDay();
            var r = c.AdvanceDay();
            Assert.IsTrue(r.relationChanges != null && r.relationChanges.Count > 0);
            Assert.IsTrue(r.newspaper.articles.Any(a => a.title.Contains("結婚")));
        }
    }
}
