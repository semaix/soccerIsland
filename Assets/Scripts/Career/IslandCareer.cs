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
            Hospital = new Hospital(Player, world.Population);
            Police = new PoliceBox(Player, world.Population, Economy, _fameEvents, rng);
            Inventory = new InventorySystem(Player, world.State, Fame, today);
            BlackMarket.inventory = Inventory;
            Romance = new RomanceSystem(Player, world.Population, world.State, save?.romance ?? new RomanceState(), _fameEvents, rng);

            if (isNew)
            {
                Stocks.ListInitialStocks();
                Player.marketValue = FameSystem.CalculateMarketValue(Player);
            }
            RefreshBlackMarketAccess();
        }

        /// <summary>話しかける。会話に加えて、有名ならファンのお願いが来ることがある。</summary>
        public Interaction Interact(int npcId)
        {
            var npc = Population.Database.Get(npcId);
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
