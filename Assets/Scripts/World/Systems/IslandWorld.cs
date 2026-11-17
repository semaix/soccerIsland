using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.World
{
    public class WorldDayReport
    {
        public List<Match> playedMatches = new List<Match>();
        public List<PurchaseRecord> purchases = new List<PurchaseRecord>();
        /// <summary>嵐で延期になった試合。</summary>
        public List<Match> postponed = new List<Match>();
        public DayReport population;
    }

    /// <summary>
    /// 島のワールド（試合予定・お店）をまとめる窓口（Unity非依存）。
    /// 生成時に会話システムへ話題プロバイダを登録するので、NPCは実際の予定・購入の話をするようになる。
    /// </summary>
    public class IslandWorld
    {
        public int scheduleAheadDays = 14;
        public int keepPlayedMatchDays = 30;
        /// <summary>新規ゲーム開始時、島民が開始前の1週間に買い物していたことにする日数。</summary>
        public int preSimulatedShoppingDays = 7;

        public IslandPopulation Population { get; }
        public WorldState State { get; }
        public MatchScheduler Scheduler { get; }
        public MatchSimulator Matches { get; }
        public ShopSimulator Shops { get; }
        public MatchTopicProvider MatchTopics { get; }
        public PurchaseTopicProvider PurchaseTopics { get; }
        public LeagueSystem League { get; }

        public IslandWorld(IslandPopulation population, WorldState state = null)
        {
            Population = population;
            bool isNew = state == null;
            State = state ?? new WorldState { items = ShopCatalog.CreateDefault() };

            var db = population.Database;
            var rng = population.Random;
            Scheduler = new MatchScheduler(db, State, rng);
            Matches = new MatchSimulator(db, population.Market, rng);
            Shops = new ShopSimulator(db, State, rng);
            MatchTopics = new MatchTopicProvider(State);
            PurchaseTopics = new PurchaseTopicProvider(State);
            League = new LeagueSystem(db, State);

            population.Conversation.AddTopicProvider(MatchTopics);
            population.Conversation.AddTopicProvider(PurchaseTopics);

            if (isNew)
                for (int d = db.currentDay - preSimulatedShoppingDays; d < db.currentDay; d++) Shops.TickDay(d);
            Scheduler.EnsureScheduledThrough(db.currentDay + scheduleAheadDays);
        }

        public Weather TodaysWeather => WeatherSystem.WeatherOf(Population.Database.seed, Population.Database.currentDay);
        public Weather WeatherOn(int day) => WeatherSystem.WeatherOf(Population.Database.seed, day);
        public Season CurrentSeason => WeatherSystem.SeasonOf(Population.Database.currentDay);

        /// <summary>今日の試合を消化し、買い物を進める。日付が進む直前に呼ぶ。嵐の日の試合は1週間延期。</summary>
        public WorldDayReport BeforeDayAdvance()
        {
            var report = new WorldDayReport();
            int today = Population.Database.currentDay;
            bool storm = TodaysWeather == Weather.Storm;
            foreach (var m in State.MatchesOn(today))
            {
                if (m.played) continue;
                if (storm) { if (Postpone(m)) report.postponed.Add(m); continue; }
                Matches.Play(m);
                League.Record(m);
                report.playedMatches.Add(m);
            }
            report.purchases = Shops.TickDay(today);
            return report;
        }

        /// <summary>主人公の年齢（新人王の判定に使う）。Career 側が差し込む。</summary>
        public Func<int> protagonistAge = () => 99;

        /// <summary>試合を延期する。出場メンバーがほかの試合と重ならない日まで、1週間ずつずらす。見つからなければ中止。</summary>
        bool Postpone(Match m)
        {
            for (int weeks = 1; weeks <= 4; weeks++)
            {
                int day = m.day + 7 * weeks;
                bool clash = false;
                foreach (var other in State.MatchesOn(day))
                {
                    foreach (var id in m.homeLineup) if (other.Involves(id)) { clash = true; break; }
                    if (!clash) foreach (var id in m.awayLineup) if (other.Involves(id)) { clash = true; break; }
                    if (clash) break;
                }
                if (clash) continue;
                m.day = day;
                return true;
            }
            State.matches.Remove(m); // 中止
            return false;
        }

        /// <summary>日付が進んだ後に呼ぶ。先の試合予定を作り、古い試合を消す。年が変わればリーグの表彰。</summary>
        public void AfterDayAdvance()
        {
            int today = Population.Database.currentDay;
            if (today > 0 && today % 365 == 0) League.CloseSeason(protagonistAge());
            Scheduler.EnsureScheduledThrough(today + scheduleAheadDays);
            State.matches.RemoveAll(m => m.played && m.day < today - keepPlayedMatchDays);
        }

        /// <summary>テスト・ツール用：ワールドと島民をまとめて1日進める。</summary>
        public WorldDayReport AdvanceDay()
        {
            var report = BeforeDayAdvance();
            report.population = Population.AdvanceDay();
            AfterDayAdvance();
            return report;
        }

        /// <summary>プレイヤーが試合を観に行く・参加する用：今日の試合一覧。</summary>
        public List<Match> TodaysMatches() => State.MatchesOn(Population.Database.currentDay);
    }
}
