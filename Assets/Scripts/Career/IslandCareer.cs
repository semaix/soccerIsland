using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    [Serializable]
    public class CareerSaveData
    {
        public PlayerState player;
        public EconomyState economy;
        public MediaState media;
        public PhoneState phone;
        /// <summary>前世の記録（古い順）。</summary>
        public List<LegacyRecord> legacies = new List<LegacyRecord>();
        public RomanceState romance;
        public IslandEventsState events;
    }

    public class CareerDayReport
    {
        public WorldDayReport world;
        public DayReport population;
        public NewspaperIssue newspaper;
        public List<EconomyEvent> economyEvents = new List<EconomyEvent>();
        public List<FameEvent> fameEvents = new List<FameEvent>();
        /// <summary>この日の朝にかかってきた電話。</summary>
        public List<IncomingCall> incomingCalls = new List<IncomingCall>();
    }

    /// <summary>NPCに話しかけたときの結果（会話＋ファンからのお願い）。</summary>
    public class Interaction
    {
        public ConversationResult conversation;
        /// <summary>サイン・写真・握手のお願い（なければ null）。</summary>
        public FanRequest fanRequest;
        /// <summary>その人はここにいなかった（会話していない）。</summary>
        public bool notHere;
    }

    /// <summary>
    /// 主人公まわり（お金・スカウト・銀行・株・toto・宝くじ・知名度・新聞・人気投票）の窓口（Unity非依存）。
    /// </summary>
    public class IslandCareer
    {
        public IslandWorld World { get; }
        public IslandPopulation Population => World.Population;
        public PlayerState Player { get; }
        public EconomyState Economy { get; }
        public MediaState Media { get; }
        /// <summary>前世の記録（古い順）。1周目は空。</summary>
        public List<LegacyRecord> PastLives { get; }
        public LegacyRecord PreviousLife => PastLives.Count > 0 ? PastLives[PastLives.Count - 1] : null;

        public ScoutingSystem Scouting { get; }
        public BankSystem Bank { get; }
        public StockMarket Stocks { get; }
        public BettingSystem Betting { get; }
        public FameSystem Fame { get; }
        public FanInteractionSystem Fans { get; }
        public PopularityVote Vote { get; }
        public SoccerNewspaper Newspaper { get; }
        public TrainingSystem Training { get; }
        public AbilityExam Exam { get; }
        public PhoneSystem Phone { get; }
        public RomanceSystem Romance { get; }
        public InventorySystem Inventory { get; }
        public IslandEventsState Events { get; }
        public BlackMarket BlackMarket { get; }
        public MysteryPlayerQuest Mystery { get; }
        public CastawayQuest Castaways { get; }
        public TownReputation Towns { get; }
        public Hospital Hospital { get; }
        public PoliceBox Police { get; }
        public DailySchedule Schedule { get; }
        public TravelEvents Travel { get; private set; }
        /// <summary>直前の移動で起きた出来事。</summary>
        public List<TravelEvent> LastTravelEvents { get; } = new List<TravelEvent>();
        public TimeOfDay Now => (TimeOfDay)Player.timeOfDay;
        /// <summary>
        /// true（既定）：同じ街の同じ場所にいる島民にしか話しかけられない。お店はその街の街なか、
        /// 練習はグラウンド、病院は勇心病院、裏市場は開催場所に行く必要がある。電話はどこからでもかけられる。
        /// </summary>
        public bool requirePresence = true;
        public IslandLocation Here => new IslandLocation(Player.currentTown, (IslandPlace)Player.currentPlace);

        readonly List<EconomyEvent> _economyEvents = new List<EconomyEvent>();
        readonly List<FameEvent> _fameEvents = new List<FameEvent>();
        WorldDayReport _pendingWorld;

        public IslandCareer(IslandWorld world, CareerSaveData save = null)
        {
            World = world;
            Player = save?.player ?? new PlayerState();
            Economy = save?.economy ?? new EconomyState();
            Media = save?.media ?? new MediaState();
            PastLives = save?.legacies ?? new List<LegacyRecord>();
            bool isNew = save == null || save.economy == null;

            var db = world.Population.Database;
            var rng = world.Population.Random;
            Func<int> today = () => db.currentDay;

            Scouting = new ScoutingSystem(db, world.Population.Knowledge, world.Population.Market, Player, _economyEvents);
            Bank = new BankSystem(Player, Economy, _economyEvents, today);
            Stocks = new StockMarket(db, world.State, Economy, Player, rng, _economyEvents);
            Betting = new BettingSystem(db, world.State, Economy, Player, rng, _economyEvents);
            Fame = new FameSystem(Player, _fameEvents, today);
            Fans = new FanInteractionSystem(Player, world.Population.Knowledge, Fame, rng, _fameEvents, today);
            Vote = new PopularityVote(db, world.State, Player, rng);
            Newspaper = new SoccerNewspaper(db, world.State, Player, Economy, world.Population.Market, Vote, rng);
            Training = new TrainingSystem(Player, db, world.Population.Knowledge, rng);
            Exam = new AbilityExam(Player, db, Fame, rng);
            Phone = new PhoneSystem(Player, world.Population, world.State, Economy, save?.phone ?? new PhoneState(), rng);
            Events = save?.events ?? new IslandEventsState();
            BlackMarket = new BlackMarket(Player, world.Population, world.State, Events);
            Mystery = new MysteryPlayerQuest(world.Population, Events);
            Castaways = new CastawayQuest(Player, world.Population, Events);
            world.Population.Conversation.AddTopicProvider(new BlackMarketHintProvider(BlackMarket));
            world.Population.Conversation.AddTopicProvider(Mystery);
            world.Population.Conversation.AddTopicProvider(Castaways);
            Scouting.IsGrateful = Castaways.IsGrateful;
            Towns = new TownReputation(Player, Events);
            world.Population.Conversation.affinityModifier = Towns.ModifyAffinityGain;
            Fans.townMultiplier = Towns.FanMultiplier;
            world.protagonistAge = () => Player.age;
            Hospital = new Hospital(Player, world.Population);
            Police = new PoliceBox(Player, world.Population, Economy, _fameEvents, rng);
            Schedule = new DailySchedule(db, world.State);
            Inventory = new InventorySystem(Player, world.State, Fame, today);
            BlackMarket.inventory = Inventory;
            Romance = new RomanceSystem(Player, world.Population, world.State, save?.romance ?? new RomanceState(), _fameEvents, rng);

            if (isNew)
            {
                Stocks.ListInitialStocks();
                Player.marketValue = FameSystem.CalculateMarketValue(Player);
            }
            // 場所の制限（requirePresence）
            if (isNew) { Player.currentTown = Player.homeTown; Player.currentPlace = (int)IslandPlace.Home; }
            BlackMarket.playerIsThere = () => !requirePresence || (Here.place == IslandPlace.BlackMarket && Here.town == BlackMarket.CurrentTown);
            Inventory.playerInTown = t => !requirePresence || (Here.town == t && Here.place == IslandPlace.Workplace);
            Training.atGround = () => !requirePresence || Here.place == IslandPlace.ClubGround;
            Training.isHere = id => !requirePresence || IsHere(id);
            Hospital.atHospital = () => !requirePresence || Here.place == IslandPlace.Hospital;
            Training.weather = () => World.TodaysWeather;
            Travel = new TravelEvents(Player, world.Population, Towns, rng);
            RefreshBlackMarketAccess();
        }

        /// <summary>話しかける。会話に加えて、有名ならファンのお願いが来ることがある。その場にいない人には話しかけられない。</summary>
        public Interaction Interact(int npcId)
        {
            var npc = Population.Database.Get(npcId);
            if (npc == null) return new Interaction { notHere = true };
            if (requirePresence && !IsHere(npcId)) return new Interaction { notHere = true };
            var result = new Interaction { conversation = Population.Talk(npcId) };
            if (npc != null) result.fanRequest = Fans.Roll(npc);
            RefreshBlackMarketAccess();
            return result;
        }

        /// <summary>裏の顔を教えてもらった人が1人でもいれば裏市場（闇金・裏toto）が使える。</summary>
        public bool RefreshBlackMarketAccess()
        {
            if (Player.blackMarketAccess) return true;
            foreach (var e in Population.Knowledge.entries)
            {
                if (!e.Knows(NPCInfoFlags.UndergroundSecret)) continue;
                var n = Population.Database.Get(e.npcId);
                if (n != null && n.isUnderground) return Player.blackMarketAccess = true;
            }
            return false;
        }

        /// <summary>その日の試合が終わった直後（日付が進む前）に呼ぶ。</summary>
        public void BeforeDayAdvance(WorldDayReport worldReport)
        {
            _pendingWorld = worldReport;
            RefreshBlackMarketAccess();
            Betting.SettleBets();
            Stocks.TickDay(worldReport);
            Betting.DrawIfDue();
            Bank.TickDay();
            if (Population.Database.currentDay % 30 == 29) Scouting.Payroll();
        }

        /// <summary>日付が進んだ後に呼ぶ。朝刊を発行する。</summary>
        public CareerDayReport AfterDayAdvance(DayReport populationReport)
        {
            Fame.TickDay();
            Training.TickDay();
            if (Population.Database.currentDay % 365 == 0) Player.age++;
            RewardSeasonAwards();
            var report = new CareerDayReport
            {
                world = _pendingWorld,
                population = populationReport,
                economyEvents = new List<EconomyEvent>(_economyEvents),
                fameEvents = new List<FameEvent>(_fameEvents),
            };
            report.newspaper = Newspaper.Publish(Media, _pendingWorld, populationReport, _economyEvents, _fameEvents);
            report.incomingCalls = Phone.IncomingCalls();
            _economyEvents.Clear();
            _fameEvents.Clear();
            _pendingWorld = null;
            return report;
        }

        /// <summary>年間表彰を受けた人は話題になる。主人公なら知名度が大きく上がる。</summary>
        void RewardSeasonAwards()
        {
            var aw = World.League.League.LastAwards;
            if (aw == null || aw.announcedDay != Population.Database.currentDay) return;
            foreach (var id in new[] { aw.topScorerId, aw.mvpId, aw.rookieId })
            {
                if (id == -1) Fame.AddFame(8f);
                else if (id >= 0) { var n = Population.Database.Get(id); if (n != null) Population.Market.AddHype(n, 0.25f, "season_award"); }
            }
        }

        /// <summary>
        /// 時間を進める（朝→昼→夕方→深夜→次の日の朝）。深夜の次で日付が変わる。
        /// 日付が変わったときだけ CareerDayReport を返す。
        /// </summary>
        public CareerDayReport AdvanceTime()
        {
            if (Player.timeOfDay < (int)TimeOfDay.Night) { Player.timeOfDay++; return null; }
            Player.timeOfDay = (int)TimeOfDay.Morning;
            var report = AdvanceDay();
            // 朝は家で目を覚ます
            Player.currentTown = Player.homeTown;
            Player.currentPlace = (int)IslandPlace.Home;
            return report;
        }

        /// <summary>その島民が今どこにいるか。</summary>
        public IslandPlace WhereIs(int npcId) => Locate(npcId).place;

        public IslandLocation Locate(int npcId)
        {
            var n = Population.Database.Get(npcId);
            if (n == null) return new IslandLocation(Player.homeTown, IslandPlace.Home);
            return Schedule.Locate(n, Population.Database.currentDay, Now, (int)BlackMarket.CurrentTown);
        }

        public bool IsHere(int npcId) => Locate(npcId).Same(Here);

        /// <summary>今いる場所にいる島民。</summary>
        public List<NPCData> PeopleHere()
        {
            var list = new List<NPCData>();
            int day = Population.Database.currentDay;
            var here = Here;
            foreach (var n in Population.Database.npcs)
                if (Schedule.Locate(n, day, Now, (int)BlackMarket.CurrentTown).Same(here)) list.Add(n);
            return list;
        }

        /// <summary>
        /// 移動する。街の中は時間がかからない。隣の街は1時間帯、反対側は2時間帯かかる（深夜を越えると日付が変わる）。
        /// 日付が変わったときの報告を返す。行けない場所なら null（移動しない）。
        /// </summary>
        public List<CareerDayReport> MoveTo(Town town, IslandPlace place)
        {
            if (!IslandMap.Exists(town, place, Player.homeTown)) return null;
            if (place == IslandPlace.BlackMarket && (!Player.blackMarketAccess || !BlackMarket.KnowsLocation || BlackMarket.CurrentTown != town)) return null;
            var reports = new List<CareerDayReport>();
            LastTravelEvents.Clear();
            int cost = IslandMap.Distance(Player.currentTown, town);
            for (int i = 0; i < cost; i++)
            {
                // 移動の途中で何か起きることがある（通り道の街：最後の1区間は行き先の街）
                var ev = Travel.Roll(i == cost - 1 ? town : Player.currentTown, Now, World.TodaysWeather);
                if (ev != null) LastTravelEvents.Add(ev);
                var r = AdvanceTime();
                if (r != null) reports.Add(r);
            }
            Player.currentTown = town;
            Player.currentPlace = (int)place;
            return reports;
        }

        /// <summary>テスト・ツール用：ワールド・島民・主人公をまとめて1日進める。</summary>
        public CareerDayReport AdvanceDay()
        {
            var w = World.BeforeDayAdvance();
            BeforeDayAdvance(w);
            var p = Population.AdvanceDay();
            World.AfterDayAdvance();
            return AfterDayAdvance(p);
        }

        public CareerSaveData CreateSave() => new CareerSaveData { player = Player, economy = Economy, media = Media, phone = Phone.State, legacies = PastLives, romance = Romance.State, events = Events };
    }
}
