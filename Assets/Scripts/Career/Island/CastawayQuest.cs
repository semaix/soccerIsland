using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    /// <summary>
    /// 島に漂流して出られない人たち。協会に登録してあげると試合に出られるようになり、
    /// 恩を感じて、スカウトするときは市場価値の半額で来てくれる。
    /// </summary>
    public class CastawayQuest : IConversationTopicProvider
    {
        public long registrationFee = 1000000;
        public int requiredAffinity = 40;

        readonly PlayerState _player;
        readonly IslandPopulation _pop;
        readonly IslandEventsState _state;

        public CastawayQuest(PlayerState player, IslandPopulation pop, IslandEventsState state)
        {
            _player = player;
            _pop = pop;
            _state = state;
        }

        public bool IsGrateful(int npcId) => _state.registeredCastaways.Contains(npcId);

        public bool TryGetTopic(NPCData npc, TopicContext ctx, out TopicMention mention)
        {
            mention = default;
            if (!npc.isStranded || ctx.affinity < requiredAffinity || IsGrateful(npc.id)) return false;
            mention = new TopicMention { kind = TopicKind.Other, refId = npc.id,
                line = "国に帰りたいけど、この島からは出られないんだ……。せめて協会に登録できれば、ここで試合に出られるのに。" };
            return true;
        }

        /// <summary>漂流者を協会に登録してあげる。</summary>
        public bool Register(int npcId)
        {
            var n = _pop.Database.Get(npcId);
            if (n == null || !n.isStranded || IsGrateful(npcId)) return false;
            var e = _pop.Knowledge.Get(npcId);
            if (e == null || e.affinity < requiredAffinity) return false;
            if (!_player.TrySpend(registrationFee, "漂流者の協会登録", _pop.Database.currentDay)) return false;
            n.registeredWithAssociation = true;
            _state.registeredCastaways.Add(npcId);
            e.affinity = System.Math.Min(100, e.affinity + 30);
            _pop.Market.Refresh(n, "registered");
            return true;
        }
    }
}
