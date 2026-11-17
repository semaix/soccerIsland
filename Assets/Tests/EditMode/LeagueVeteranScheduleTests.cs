using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class LeagueVeteranScheduleTests
    {
        // このファイルは各システムの動きを見るので、場所の制限（同じ場所にいる人としか話せない等）は外す
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault()))) { requirePresence = false };

        // ---------------- 協会リーグ ----------------

        [Test]
        public void LeagueTableAndScorersFollowTheMatches()
        {
            var c = NewCareer();
            for (int d = 0; d < 40; d++) c.AdvanceDay();
            var table = c.World.League.Table();
            Assert.Greater(table.Count, 5);
            for (int i = 1; i < table.Count; i++) Assert.GreaterOrEqual(table[i - 1].Points, table[i].Points);
            Assert.AreEqual(table.Sum(t => t.goalsFor), table.Sum(t => t.goalsAgainst), "得点と失点の合計は一致する");
            Assert.AreEqual(table.Sum(t => t.goalsFor), c.World.League.League.scorers.Sum(s => s.goals), "全ゴールに得点者がいる");
            var top = c.World.League.TopScorers(1)[0];
            Assert.Greater(top.goals, 0);
            Assert.IsTrue(!c.World.League.IsLeagueMatch(new SoccerIsland.World.Match { category = MatchCategory.UndergroundFutsal }), "裏の試合は公式戦ではない");
        }

        [Test]
        public void SeasonEndsWithAwardsInTheNews()
        {
            var c = NewCareer();
            CareerDayReport last = null;
            for (int d = 0; d < 365; d++) last = c.AdvanceDay();
            var aw = c.World.League.League.LastAwards;
            Assert.IsTrue(aw != null, "1年で表彰される");
            Assert.IsTrue(aw.championClubId >= 0 && aw.topScorerGoals > 0);
            Assert.IsTrue(last.newspaper.articles.Any(a => a.title.Contains("年間表彰")));
            Assert.AreEqual(0, c.World.League.League.table.Count, "次のシーズンが始まる");
            if (aw.rookieId >= 0) Assert.LessOrEqual(c.Population.Database.Get(aw.rookieId).age, 22);
        }

        [Test]
        public void ProtagonistGoalsCountInTheLeague()
        {
            var c = NewCareer();
            for (int i = 0; i < NPCAbilities.StatCount; i++) c.Player.abilities[i] = 95;
            var m = c.World.State.matches.First(x => !x.played && c.World.League.IsLeagueMatch(x));
            while (c.Population.Database.currentDay < m.day) c.AdvanceDay();
            var setup = MatchFactory.FromWorldMatch(c, m, Side.Home, 4);
            var session = new MatchSession(c, setup);
            var r = session.AutoPlay();
            Assert.AreEqual(r.homeScore, m.homeScorers.Count);
            int mine = c.World.League.League.scorers.Where(s => s.npcId == -1).Sum(s => s.goals);
            Assert.AreEqual(r.protagonist.goals, mine);
        }

        // ---------------- ベテラン・若手・師弟 ----------------

        static MatchTeam TeamOfAges(int seed, params int[] ages)
        {
            var t = MatchFactory.Generic("チーム", 60, 11, new SeededRandom(seed));
            for (int i = 0; i < t.players.Count; i++) t.players[i].age = ages[i % ages.Length];
            return t;
        }

        [Test]
        public void VeteransSteadyYoungstersInFrontOfBigCrowds()
        {
            var youngOnly = TeamOfAges(1, 18, 19, 20, 24);
            var withVet = TeamOfAges(1, 18, 19, 20, 35);
            float before = youngOnly.players.Where(p => p.IsYoung).Average(p => p.tension);
            new MatchEngine(new MatchSetup { home = youngOnly, away = TeamOfAges(2, 25), seed = 1, crowd = 5000, autoSelectCpuTactics = false });
            new MatchEngine(new MatchSetup { home = withVet, away = TeamOfAges(2, 25), seed = 1, crowd = 5000, autoSelectCpuTactics = false });
            Assert.Less(youngOnly.players.Where(p => p.IsYoung).Average(p => p.tension), before, "大観衆にのまれる");
            Assert.Greater(withVet.players.Where(p => p.IsYoung).Average(p => p.tension), before, "ベテランが落ち着かせる");
        }

        [Test]
        public void MentorAndPupilFireEachOtherUp()
        {
            var t = TeamOfAges(3, 25);
            t.players[5].npcId = 500;
            t.players[6].mentorIds.Add(500);
            float before = t.players[6].tension;
            var e = new MatchEngine(new MatchSetup { home = t, away = TeamOfAges(4, 25), seed = 2, autoSelectCpuTactics = false });
            Assert.Greater(t.players[6].tension, before);
            Assert.IsTrue(e.Log.Any(x => x.text.Contains("師匠")));
        }

        [Test]
        public void IslanderMentorsAreLinkedFromRelations()
        {
            var db = NewCareer().Population.Database;
            var pupil = db.npcs.First(n => n.relations.Any(r => r.type == RelationType.Mentor));
            var mp = MatchFactory.FromNPC(pupil, false, new SeededRandom(1));
            Assert.IsTrue(mp.mentorIds.Count > 0);
            Assert.AreEqual(pupil.age, mp.age);
        }

        // ---------------- 島民の1日 ----------------

        [Test]
        public void IslandersFollowADailyRoutine()
        {
            var c = NewCareer();
            var db = c.Population.Database;
            int day = db.currentDay;
            var worker = db.npcs.First(n => n.occupation == Occupation.Doctor && !n.IsInjured(day) && c.World.State.NextMatchOf(n.id, day, 0) == null);
            Assert.AreEqual(IslandPlace.Workplace, c.Schedule.WhereIs(worker, day, TimeOfDay.Morning));
            Assert.AreEqual("勇心病院", DailySchedule.PlaceName(worker, IslandPlace.Workplace));
            Assert.AreEqual(c.Schedule.WhereIs(worker, day, TimeOfDay.Evening), c.Schedule.WhereIs(worker, day, TimeOfDay.Evening), "同じ日・同じ時間なら同じ場所");

            var prisoner = db.npcs.First(n => n.isIncarcerated);
            foreach (TimeOfDay t in System.Enum.GetValues(typeof(TimeOfDay)))
                Assert.AreEqual(IslandPlace.Prison, c.Schedule.WhereIs(prisoner, day, t));

            var phantom = db.npcs.First(n => n.soccerPersonality == SoccerPersonality.Phantom);
            int nightsAtGround = Enumerable.Range(0, 30).Count(d => c.Schedule.WhereIs(phantom, d, TimeOfDay.Night) == IslandPlace.ClubGround);
            Assert.Greater(nightsAtGround, 10, "謎のプレーヤーは夜のグラウンドにいる");

            var bar = c.Schedule.PeopleAt(IslandPlace.Bar, day, TimeOfDay.Evening);
            Assert.Greater(bar.Count, 10, "夕方の飲み屋街はにぎわう");
            Assert.IsTrue(bar.All(n => n.age >= 20 || n.occupation == Occupation.Bartender));
        }

        [Test]
        public void TimePassesAndNightTurnsIntoTheNextDay()
        {
            var c = NewCareer();
            int day = c.Population.Database.currentDay;
            Assert.AreEqual(TimeOfDay.Morning, c.Now);
            Assert.IsTrue(c.AdvanceTime() == null && c.AdvanceTime() == null && c.AdvanceTime() == null);
            Assert.AreEqual(TimeOfDay.Night, c.Now);
            Assert.IsTrue(c.AdvanceTime() != null, "深夜の次は日付が変わる");
            Assert.AreEqual(day + 1, c.Population.Database.currentDay);
            Assert.AreEqual(TimeOfDay.Morning, c.Now);

            c.BlackMarket.nightOnly = true;
            c.Player.blackMarketAccess = true;
            c.BlackMarket.LearnLocation();
            Assert.IsTrue(!c.BlackMarket.CanEnter, "裏市場は深夜だけ");
            c.Player.timeOfDay = (int)TimeOfDay.Night;
            Assert.IsTrue(c.BlackMarket.CanEnter);
        }
    }
}
