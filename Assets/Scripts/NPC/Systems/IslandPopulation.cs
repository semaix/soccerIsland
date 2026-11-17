using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    /// <summary>
    /// NPC関連システムをまとめた窓口（Unity非依存）。
    /// NPCManager(MonoBehaviour) はこれを持つだけにして、ロジックはテスト可能なここに置く。
    /// </summary>
    public class IslandPopulation
    {
        public NPCDatabase Database { get; private set; }
        public NPCKnowledgeBook Knowledge { get; private set; }
        public MarketValueSystem Market { get; private set; }
        public NPCConversationSystem Conversation { get; private set; }
        public NPCLifecycleSystem Lifecycle { get; private set; }

        /// <summary>ゲーム中のランダムイベント用（生成用とは別系統）。</summary>
        public SeededRandom Random { get; private set; }

        public static IslandPopulation CreateNew(NPCGenerationSettings settings)
        {
            var db = NPCGenerator.Generate(settings);
            return new IslandPopulation(db, new NPCKnowledgeBook(), new SeededRandom(db.seed ^ 0x5EED));
        }

        public static IslandPopulation FromSave(IslandSaveData save)
        {
            save.database.RebuildIndex();
            return new IslandPopulation(save.database, save.knowledge ?? new NPCKnowledgeBook(), save.random ?? new SeededRandom(save.database.seed ^ 0x5EED));
        }

        IslandPopulation(NPCDatabase db, NPCKnowledgeBook book, SeededRandom rng)
        {
            Database = db;
            Knowledge = book;
            Random = rng;
            Market = new MarketValueSystem(db, rng);
            Conversation = new NPCConversationSystem(db, book, Market, rng);
            Lifecycle = new NPCLifecycleSystem(db, Market, rng);
        }

        public ConversationResult Talk(int npcId) => Conversation.Talk(npcId);

        public NPCProfileView GetProfile(int npcId) => NPCProfileView.Build(Database.Get(npcId), Knowledge, Database);

        /// <summary>1日進める。365日ごとに年が変わる。</summary>
        public DayReport AdvanceDay()
        {
            var report = new DayReport();
            Database.currentDay++;
            report.headlines = Market.TickDay(Database.currentDay);
            if (Database.currentDay % 365 == 0) report.lifeEvents = Lifecycle.AdvanceYear();
            return report;
        }

        public IslandSaveData CreateSave() => new IslandSaveData { database = Database, knowledge = Knowledge, random = Random };
    }

    public class DayReport
    {
        public List<MarketValueChange> headlines = new List<MarketValueChange>();
        public List<LifeEvent> lifeEvents = new List<LifeEvent>();
    }

    [System.Serializable]
    public class IslandSaveData
    {
        public NPCDatabase database;
        public NPCKnowledgeBook knowledge;
        public SeededRandom random;
    }
}
