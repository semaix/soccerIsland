using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.World;

namespace SoccerIsland.NPC.Tests
{
    public class WeatherAndTravelTests
    {
        static IslandCareer NewCareer() =>
            new IslandCareer(new IslandWorld(IslandPopulation.CreateNew(NPCGenerationSettings.CreateDefault())));

        [Test]
        public void SeasonsAndWeather()
        {
            Assert.AreEqual(Season.Spring, WeatherSystem.SeasonOf(0));
            Assert.AreEqual(Season.Summer, WeatherSystem.SeasonOf(120));
            Assert.AreEqual(Season.Winter, WeatherSystem.SeasonOf(300));
            Assert.AreEqual(Season.Spring, WeatherSystem.SeasonOf(365), "1年で一周");
            Assert.AreEqual(WeatherSystem.WeatherOf(1, 50), WeatherSystem.WeatherOf(1, 50), "同じ日は同じ天気");

            var year = Enumerable.Range(0, 365).Select(d => WeatherSystem.WeatherOf(7, d)).ToList();
            Assert.IsTrue(year.Contains(Weather.Rain) && year.Contains(Weather.Sunny));
            Assert.IsTrue(Enumerable.Range(0, 365).Where(d => WeatherSystem.WeatherOf(7, d) == Weather.Snow).All(d => WeatherSystem.SeasonOf(d) == Season.Winter), "雪は冬だけ");
            Assert.IsTrue(Enumerable.Range(0, 365).Where(d => WeatherSystem.WeatherOf(7, d) == Weather.Storm).All(d => WeatherSystem.SeasonOf(d) == Season.Summer || WeatherSystem.SeasonOf(d) == Season.Autumn), "台風は夏と秋");
        }

        [Test]
        public void StormsPostponeMatchesWithoutDoubleBooking()
        {
            var c = NewCareer();
            int seed = c.Population.Database.seed;
            // 試合が組まれている、最初の嵐の日まで進める
            SoccerIsland.World.Match target = null;
            int stormDay = -1;
            for (int d = 0; d < 365 && target == null; d++)
            {
                if (WeatherSystem.WeatherOf(seed, c.Population.Database.currentDay) == Weather.Storm)
                {
                    target = c.World.TodaysMatches().FirstOrDefault(m => !m.played);
                    if (target != null) { stormDay = c.Population.Database.currentDay; break; }
                }
                c.AdvanceDay();
            }
            Assume.That(target == null ? 0 : 1, Is.GreaterThan(0));

            var r = c.AdvanceDay();
            Assert.IsTrue(!target.played, "嵐の日には試合をしない");
            Assert.IsTrue(r.world.postponed.Contains(target) || !c.World.State.matches.Contains(target), "延期か中止");
            if (r.world.postponed.Contains(target)) Assert.Greater(target.day, stormDay);
            for (int d = 0; d < 35; d++)
            {
                var today = c.World.TodaysMatches();
                var ids = today.SelectMany(m => m.homeLineup.Concat(m.awayLineup)).ToList();
                Assert.AreEqual(ids.Count, ids.Distinct().Count(), "延期で同じ選手が2試合に入らない");
                c.AdvanceDay();
            }
        }

        static int StolenBalls(Weather w)
        {
            int n = 0;
            for (int i = 0; i < 12; i++)
            {
                var rng = new SeededRandom(i + 1);
                var home = MatchFactory.Generic("A", 60, 11, rng);
                var away = MatchFactory.Generic("B", 60, 11, rng);
                var e = new MatchEngine(new MatchSetup { home = home, away = away, seed = i + 1, weather = w, autoSelectCpuTactics = false });
                var r = e.RunToEnd();
                n += r.log.Count(x => x.type == MatchEventType.PassIntercepted || x.type == MatchEventType.DribbleBeaten);
            }
            return n;
        }

        [Test]
        public void RainMakesTheBallSlippery()
        {
            Assert.Greater(StolenBalls(Weather.Rain), StolenBalls(Weather.Sunny), "雨の日はボールを失いやすい");
        }

        [Test]
        public void BadWeatherChangesHowIslandersSpendTheDay()
        {
            var c = NewCareer();
            int seed = c.Population.Database.seed;
            int sunny = Enumerable.Range(0, 100).First(d => WeatherSystem.WeatherOf(seed, d) == Weather.Sunny);
            int rainy = Enumerable.Range(0, 100).First(d => WeatherSystem.WeatherOf(seed, d) == Weather.Rain);
            int groundSunny = c.Schedule.PeopleAt(IslandPlace.ClubGround, sunny, TimeOfDay.Evening).Count;
            int groundRainy = c.Schedule.PeopleAt(IslandPlace.ClubGround, rainy, TimeOfDay.Evening).Count;
            Assert.Less(groundRainy, groundSunny, "雨の日の夕方はグラウンドが空く");
            Assert.AreEqual(0, c.Schedule.PeopleAt(IslandPlace.Beach, rainy, TimeOfDay.Evening).Count, "雨の浜辺には誰もいない");
        }

        [Test]
        public void StormsKeepYouIndoorsForTraining()
        {
            var c = NewCareer();
            c.requirePresence = false;
            c.Training.weather = () => Weather.Storm;
            Assert.IsTrue(!c.Training.Train(TrainingMenu.Running).done);
            Assert.IsTrue(c.Training.Train(TrainingMenu.Weights).done, "筋トレならできる");
        }

        [Test]
        public void SummerBeachFestival()
        {
            var c = NewCareer();
            Assert.IsTrue(TournamentSystem.CreateBeachFestival(c) == null, "春には開かれない");
            while (c.World.CurrentSeason != Season.Summer) c.AdvanceDay();
            var t = TournamentSystem.CreateBeachFestival(c);
            Assert.AreEqual(8, t.State.entries.Count);
            var s = t.PlayerMatchSetup();
            Assert.AreEqual(MatchCategory.Beach, s.category);
            Assert.AreEqual(5, s.home.players.Count, "5人制");
            Assert.IsTrue(s.home.Protagonist != null || s.away.Protagonist != null);
            int rounds = 0;
            while (!t.State.finished && rounds++ < 5) t.AutoPlayRound();
            Assert.IsTrue(t.State.finished);
        }

        [Test]
        public void TheNewspaperCarriesTheForecast()
        {
            var c = NewCareer();
            var issue = c.AdvanceDay().newspaper;
            Assert.IsTrue(issue.articles.Any(a => a.title.StartsWith("今日の天気")));
        }

        // ---------------- 移動中の出来事 ----------------

        static List<TravelEvent> Travel(IslandCareer c, int trips)
        {
            var all = new List<TravelEvent>();
            for (int i = 0; i < trips; i++)
            {
                var to = c.Here.town == Town.Furugane ? Town.Kirahama : Town.Furugane;
                c.MoveTo(to, IslandPlace.Workplace);
                all.AddRange(c.LastTravelEvents);
            }
            return all;
        }

        [Test]
        public void ThingsHappenOnTheWay()
        {
            var c = NewCareer();
            var events = Travel(c, 60);
            Assert.Greater(events.Count, 10);
            var kinds = new HashSet<TravelEventKind>(events.Select(e => e.kind));
            Assert.IsTrue(kinds.Contains(TravelEventKind.FoundMoney) && kinds.Contains(TravelEventKind.YankeeTrouble));
            Assert.IsTrue(!kinds.Contains(TravelEventKind.FanEncounter), "無名のうちはファンに声をかけられない");
            Assert.IsTrue(events.All(e => !string.IsNullOrEmpty(e.text)));
        }

        [Test]
        public void FamousPeopleGetStoppedByFansAndYankeesBecomeFans()
        {
            var c = NewCareer();
            c.Player.fame = 80;
            var events = Travel(c, 60);
            var fans = events.Where(e => e.kind == TravelEventKind.FanEncounter).ToList();
            Assert.Greater(fans.Count, 0);
            Assert.IsTrue(fans.All(f => f.fanRequest != null));
            long cash = c.Player.cash;
            var yankees = events.Where(e => e.kind == TravelEventKind.YankeeTrouble).ToList();
            Assert.IsTrue(yankees.All(y => y.money == 0), "有名ならカツアゲされない");
        }

        [Test]
        public void WeakNobodiesGetShakenDown()
        {
            var c = NewCareer();
            c.Player.abilities.physical = 40;
            var yankees = Travel(c, 80).Where(e => e.kind == TravelEventKind.YankeeTrouble).ToList();
            Assume.That(yankees.Count, Is.GreaterThan(0));
            Assert.IsTrue(yankees.Any(y => y.money < 0 || y.text.Contains("へとへと")));
        }
    }
}
