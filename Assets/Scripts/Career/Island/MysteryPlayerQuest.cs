using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    /// <summary>
    /// 謎のプレーヤーを探す。島民の噂から手がかりを3つ集めると正体の目星がつき、
    /// 本人に話しかけると正体（幻影）がわかる。わかると「裏の王者」の弱点になる。
    /// </summary>
    public class MysteryPlayerQuest : IConversationTopicProvider
    {
        public const int CluesNeeded = 3;
        public float rumorChance = 0.25f;

        readonly IslandPopulation _pop;
        readonly IslandEventsState _state;

        public MysteryPlayerQuest(IslandPopulation pop, IslandEventsState state)
        {
            _pop = pop;
            _state = state;
        }

        public NPCData Phantom
        {
            get { foreach (var n in _pop.Database.npcs) if (n.soccerPersonality == SoccerPersonality.Phantom) return n; return null; }
        }

        public int Clues => _state.mysteryClues;
        public bool Found => _state.phantomFound;

        public bool TryGetTopic(NPCData npc, TopicContext ctx, out TopicMention mention)
        {
            mention = default;
            var phantom = Phantom;
            if (phantom == null || _state.phantomFound) return false;

            if (npc.id == phantom.id)
            {
                if (_state.mysteryClues < CluesNeeded) return false;
                _state.phantomFound = true;
                _pop.Knowledge.Reveal(phantom.id, NPCInfoFlags.Name | NPCInfoFlags.SoccerPersonality | NPCInfoFlags.UndergroundSecret);
                mention = new TopicMention { kind = TopicKind.Other, refId = phantom.id,
                    line = "……よく見つけたな。そうだ、夜のグラウンドにいるのは俺だ。誰にも言うなよ。" };
                return true;
            }

            if (_state.clueSources.Contains(npc.id) || !ctx.rng.Chance(rumorChance)) return false;
            _state.clueSources.Add(npc.id);
            _state.mysteryClues++;
            string line;
            switch (_state.mysteryClues)
            {
                case 1: line = "知ってる？ 夜中、誰もいないグラウンドで信じられないボールを蹴るやつがいるんだって。"; break;
                case 2: line = $"謎のプレーヤー？ 見たってやつは、みんな{NPCLabels.Of(phantom.hometown)}の方だって言ってた。"; break;
                default:
                    line = $"昼間は{NPCLabels.Of(phantom.occupation)}をしてるって噂だよ。……まさか、あの{phantom.familyName}さん？";
                    _pop.Knowledge.Reveal(phantom.id, NPCInfoFlags.Name | NPCInfoFlags.Occupation);
                    break;
            }
            mention = new TopicMention { kind = TopicKind.Other, refId = -1, line = line };
            return true;
        }
    }
}
