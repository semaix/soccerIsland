using System;
using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    /// <summary>1回の会話の結果。UIはこれを見てセリフ表示・プロフィール更新・相関図更新を行う。</summary>
    public class ConversationResult
    {
        public int npcId;
        public List<string> lines = new List<string>();
        /// <summary>この会話で新しく判明した、話し相手本人の情報。</summary>
        public NPCInfoFlags revealed;
        /// <summary>噂話で新しく判明した、第三者の情報。</summary>
        public List<KeyValuePair<int, NPCInfoFlags>> revealedAboutOthers = new List<KeyValuePair<int, NPCInfoFlags>>();
        /// <summary>相関図に新しく追加された線。</summary>
        public List<KnownRelation> newRelations = new List<KnownRelation>();
        /// <summary>この会話によって起きた市場価値の変動。</summary>
        public List<MarketValueChange> marketChanges = new List<MarketValueChange>();
        /// <summary>この会話で出た話題（試合・買い物など）。</summary>
        public List<TopicMention> topics = new List<TopicMention>();
        public int affinityBefore;
        public int affinityAfter;
    }

    /// <summary>
    /// NPCとの会話。話しかけるほど情報が増え、噂が広まって市場価値が動く。
    ///
    /// ・性格で口の軽さが違う（外交的=よく喋る、内向的=少し、根暗=気分次第）
    /// ・好感度が上がるほど深い情報（才能度・電話番号・裏の顔）を教えてくれる
    /// ・噂話で第三者の情報・関係がわかる（同級生は「昔からうまかった」話をしてくれる）
    /// ・褒められた人は話題性↑、悪口を言われた人は話題性↓ → 市場価値が変動
    /// </summary>
    public class NPCConversationSystem
    {
        readonly NPCDatabase _db;
        readonly NPCKnowledgeBook _book;
        readonly MarketValueSystem _market;
        readonly SeededRandom _rng;

        public float gossipChance = 0.6f;
        public float topicChance = 0.5f;
        /// <summary>好感度の上がり方の補正（街のライバル意識・評判など）。Career 側が差し込む。</summary>
        public Func<NPCData, int, int> affinityModifier;

        readonly List<IConversationTopicProvider> _topicProviders = new List<IConversationTopicProvider>();

        /// <summary>ワールド側（試合・お店など）が話題を提供するために登録する。</summary>
        public void AddTopicProvider(IConversationTopicProvider provider)
        {
            if (provider != null && !_topicProviders.Contains(provider)) _topicProviders.Add(provider);
        }

        public void RemoveTopicProvider(IConversationTopicProvider provider) => _topicProviders.Remove(provider);

        public NPCConversationSystem(NPCDatabase db, NPCKnowledgeBook book, MarketValueSystem market, SeededRandom rng)
        {
            _db = db;
            _book = book;
            _market = market;
            _rng = rng;
        }

        struct RevealStep
        {
            public NPCInfoFlags flag;
            public int requiredAffinity;
            public RevealStep(NPCInfoFlags f, int a) { flag = f; requiredAffinity = a; }
        }

        /// <summary>情報が出てくる順番と、必要な好感度。</summary>
        static readonly RevealStep[] RevealOrder =
        {
            new RevealStep(NPCInfoFlags.Club, 0),
            new RevealStep(NPCInfoFlags.Position, 0),
            new RevealStep(NPCInfoFlags.SoccerPersonality, 10),
            new RevealStep(NPCInfoFlags.Personality, 15),
            new RevealStep(NPCInfoFlags.Salary, 25),
            new RevealStep(NPCInfoFlags.MarketValue, 30),
            new RevealStep(NPCInfoFlags.Effort, 40),
            new RevealStep(NPCInfoFlags.Talent, 50),
            new RevealStep(NPCInfoFlags.PhoneNumber, 50),
            new RevealStep(NPCInfoFlags.GrowthHint, 65),
            new RevealStep(NPCInfoFlags.UndergroundSecret, 80),
        };

        public ConversationResult Talk(int npcId) => Talk(_db.Get(npcId));

        public ConversationResult Talk(NPCData npc)
        {
            if (npc == null) throw new ArgumentNullException(nameof(npc));
            int day = _db.currentDay;
            var entry = _book.GetOrCreate(npc.id);
            var result = new ConversationResult { npcId = npc.id, affinityBefore = entry.affinity };

            if (entry.lastTalkDay != day) { entry.lastTalkDay = day; entry.talksToday = 0; }
            entry.talksToday++;
            entry.talkCount++;

            // ---- 挨拶・自己紹介 ----
            bool first = !entry.Knows(NPCInfoFlags.Basic);
            if (first)
            {
                result.revealed |= _book.Reveal(npc.id, NPCInfoFlags.Basic);
                result.lines.Add(NPCDialogue.Introduction(npc, _rng));
            }
            else if (entry.talksToday > 3)
            {
                // 1日に何度も話しかけるとうっとうしがられる
                entry.affinity = Math.Max(0, entry.affinity - 2);
                result.lines.Add(NPCDialogue.Annoyed(npc, _rng));
                result.affinityAfter = entry.affinity;
                return result;
            }
            else
            {
                result.lines.Add(NPCDialogue.Greeting(npc, entry.affinity, _rng));
            }

            // ---- 好感度 ----
            int gain = AffinityGain(npc, entry.talksToday);
            if (affinityModifier != null) gain = affinityModifier(npc, gain);
            entry.affinity = Math.Max(0, Math.Min(100, entry.affinity + gain));

            // ---- 本人の情報 ----
            int count = RevealCount(npc);
            for (int i = 0; i < RevealOrder.Length && count > 0; i++)
            {
                var step = RevealOrder[i];
                if (entry.Knows(step.flag) || entry.affinity < step.requiredAffinity) continue;
                if (step.flag == NPCInfoFlags.UndergroundSecret && !npc.isUnderground) { _book.Reveal(npc.id, step.flag); continue; }
                if (step.flag == NPCInfoFlags.PhoneNumber && string.IsNullOrEmpty(npc.phoneNumber)) { _book.Reveal(npc.id, step.flag); continue; }
                if (step.flag == NPCInfoFlags.Club && npc.clubId < 0) { _book.Reveal(npc.id, step.flag); continue; }

                result.revealed |= _book.Reveal(npc.id, step.flag);
                result.lines.Add(NPCDialogue.Reveal(npc, step.flag, _db, _rng));
                count--;
            }

            // ---- 近況（試合・買い物など、実際に起きていること） ----
            if (_topicProviders.Count > 0 && _rng.Chance(topicChance))
                TellTopic(npc, entry, result, day);

            // ---- 噂話 ----
            if (npc.relations.Count > 0 && _rng.Chance(GossipChance(npc)))
                Gossip(npc, result, day);

            // ---- スカウトに話しかけられた本人は少し話題になる ----
            float selfHype = 0.005f + (100 - npc.fame) * 0.0002f;
            if (entry.talkCount == 1) selfHype *= 2f;
            result.marketChanges.Add(_market.AddHype(npc, selfHype, "talked_by_scout"));

            result.affinityAfter = entry.affinity;
            return result;
        }

        void TellTopic(NPCData npc, NPCKnowledgeEntry entry, ConversationResult result, int day)
        {
            var ctx = new TopicContext { db = _db, day = day, affinity = entry.affinity, rng = _rng };
            int start = _rng.Range(0, _topicProviders.Count);
            for (int i = 0; i < _topicProviders.Count; i++)
            {
                var provider = _topicProviders[(start + i) % _topicProviders.Count];
                if (!provider.TryGetTopic(npc, ctx, out var mention)) continue;
                result.topics.Add(mention);
                if (!string.IsNullOrEmpty(mention.line)) result.lines.Add(mention.line);
                return;
            }
        }

        int AffinityGain(NPCData npc, int talksToday)
        {
            int gain;
            switch (npc.personality)
            {
                case Personality.Extrovert: gain = 6; break;
                case Personality.Introvert: gain = 3; break;
                default: gain = _rng.Chance(0.5f) ? 4 : 1; break;
            }
            if (npc.soccerPersonality == SoccerPersonality.MoodMaker) gain += 2;
            if (npc.soccerPersonality == SoccerPersonality.Phantom) gain = 1;
            if (talksToday > 1) gain = Math.Max(1, gain / 2);
            return gain;
        }

        int RevealCount(NPCData npc)
        {
            int c;
            switch (npc.personality)
            {
                case Personality.Extrovert: c = 2; break;
                case Personality.Introvert: c = 1; break;
                default: c = _rng.Chance(0.5f) ? 1 : 0; break;
            }
            if (npc.soccerPersonality == SoccerPersonality.MoodMaker || npc.soccerPersonality == SoccerPersonality.Egoist) c++;
            if (npc.soccerPersonality == SoccerPersonality.Phantom) c = _rng.Chance(0.3f) ? 1 : 0;
            return c;
        }

        float GossipChance(NPCData npc)
        {
            float c = gossipChance;
            if (npc.personality == Personality.Extrovert) c += 0.2f;
            if (npc.personality == Personality.Gloomy) c -= 0.2f;
            if (npc.occupation == Occupation.Reporter || npc.occupation == Occupation.Hairdresser || npc.occupation == Occupation.Bartender) c += 0.2f;
            return c;
        }

        void Gossip(NPCData speaker, ConversationResult result, int day)
        {
            // 未知の関係を優先して話す
            var unknown = new List<NPCRelation>();
            foreach (var r in speaker.relations)
                if (!_book.KnowsRelation(speaker.id, r.targetId)) unknown.Add(r);
            var rel = unknown.Count > 0 ? _rng.Pick(unknown) : _rng.Pick(speaker.relations);
            var target = _db.Get(rel.targetId);
            if (target == null) return;

            // 裏の関係は好感度が高くないと話さない
            if ((rel.type == RelationType.Boss || rel.type == RelationType.Subordinate) &&
                _book.Get(speaker.id).affinity < 60) return;

            if (_book.AddRelation(speaker.id, target.id, rel.type, speaker.id, day))
                result.newRelations.Add(new KnownRelation { fromId = speaker.id, toId = target.id, type = rel.type, sourceId = speaker.id, discoveredDay = day });

            // 第三者の名前は必ずわかる。関係に応じて追加情報
            var flags = NPCInfoFlags.Name | GossipFlagFor(rel.type);
            var newly = _book.Reveal(target.id, flags);
            if (newly != NPCInfoFlags.None)
                result.revealedAboutOthers.Add(new KeyValuePair<int, NPCInfoFlags>(target.id, newly));

            int tone = rel.affinity >= 30 ? 1 : (rel.affinity <= -20 ? -1 : 0);
            result.lines.Add(NPCDialogue.Gossip(speaker, target, rel, tone, _rng));

            // 噂が広まり市場価値が動く
            if (tone != 0)
            {
                float mag = 0.01f + speaker.fame * 0.0004f; // 有名人の発言ほど影響が大きい
                float amount = tone > 0 ? mag * _rng.Range(1f, 2.5f) : -mag * _rng.Range(1f, 2f);
                result.marketChanges.Add(_market.AddHype(target, amount, tone > 0 ? "praised_by_" + speaker.id : "badmouthed_by_" + speaker.id));
            }
        }

        static NPCInfoFlags GossipFlagFor(RelationType t)
        {
            switch (t)
            {
                case RelationType.Classmate: return NPCInfoFlags.Age | NPCInfoFlags.Talent;           // 小さい頃からうまかった
                case RelationType.Family: return NPCInfoFlags.Age | NPCInfoFlags.Occupation | NPCInfoFlags.Personality;
                case RelationType.Coworker: return NPCInfoFlags.Occupation | NPCInfoFlags.Effort;
                case RelationType.Rival: return NPCInfoFlags.SoccerPersonality | NPCInfoFlags.Position;
                case RelationType.Friend: return NPCInfoFlags.Personality | NPCInfoFlags.Club;
                case RelationType.Mentor:
                case RelationType.Pupil: return NPCInfoFlags.Talent | NPCInfoFlags.GrowthHint;
                case RelationType.Boss:
                case RelationType.Subordinate: return NPCInfoFlags.UndergroundSecret | NPCInfoFlags.Occupation;
                default: return NPCInfoFlags.None;
            }
        }
    }
}
