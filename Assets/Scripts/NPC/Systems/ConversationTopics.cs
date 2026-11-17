using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    public enum TopicKind
    {
        Match,     // 試合の話 → 実際にその試合に出場する
        Purchase,  // 買い物の話 → その商品は完売・商談中になっている
        Other,
    }

    /// <summary>会話中に出た話題。refId は試合ID・商品IDなど、話題の実体を指す。</summary>
    public struct TopicMention
    {
        public TopicKind kind;
        public int refId;
        public string line;
    }

    public struct TopicContext
    {
        public NPCDatabase db;
        public int day;
        /// <summary>プレイヤーへの好感度。裏の話は高くないとしない。</summary>
        public int affinity;
        public SeededRandom rng;
    }

    /// <summary>
    /// 会話の話題を提供する。話題は必ずワールドの実データ（試合予定・購入履歴）から作るので、
    /// NPCが話したことは実際に起きている。
    /// </summary>
    public interface IConversationTopicProvider
    {
        bool TryGetTopic(NPCData npc, TopicContext ctx, out TopicMention mention);
    }
}
