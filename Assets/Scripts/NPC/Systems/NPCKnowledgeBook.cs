using System;
using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    /// <summary>プレイヤーがあるNPCについて知っていること。</summary>
    [Serializable]
    public class NPCKnowledgeEntry
    {
        public int npcId;
        public NPCInfoFlags known;
        /// <summary>プレイヤーへの好感度 0-100。上がると深い情報を話してくれる。</summary>
        public int affinity;
        public int talkCount;
        public int lastTalkDay = -1;
        public int talksToday;

        public bool Knows(NPCInfoFlags flag) => (known & flag) == flag;
    }

    /// <summary>プレイヤーが知っている「誰と誰がどういう関係か」。相関図の線。</summary>
    [Serializable]
    public struct KnownRelation
    {
        public int fromId;
        public int toId;
        public RelationType type;
        /// <summary>この情報を教えてくれたNPC（自分で見つけた場合は -1）。</summary>
        public int sourceId;
        public int discoveredDay;
    }

    /// <summary>
    /// プレイヤーの手帳。会話で得た情報・電話帳・相関図を保持する。
    /// NPCData とは別に持つので、2周目で「プレイヤーの知識」だけを引き継ぐこともできる。
    /// </summary>
    [Serializable]
    public class NPCKnowledgeBook
    {
        public List<NPCKnowledgeEntry> entries = new List<NPCKnowledgeEntry>();
        public List<KnownRelation> knownRelations = new List<KnownRelation>();

        [NonSerialized] Dictionary<int, NPCKnowledgeEntry> _index;
        [NonSerialized] HashSet<long> _relationKeys;

        void EnsureIndex()
        {
            if (_index != null) return;
            _index = new Dictionary<int, NPCKnowledgeEntry>();
            foreach (var e in entries) _index[e.npcId] = e;
            _relationKeys = new HashSet<long>();
            foreach (var r in knownRelations) _relationKeys.Add(PairKey(r.fromId, r.toId));
        }

        static long PairKey(int a, int b) => a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;

        public NPCKnowledgeEntry Get(int npcId)
        {
            EnsureIndex();
            return _index.TryGetValue(npcId, out var e) ? e : null;
        }

        public NPCKnowledgeEntry GetOrCreate(int npcId)
        {
            var e = Get(npcId);
            if (e != null) return e;
            e = new NPCKnowledgeEntry { npcId = npcId };
            entries.Add(e);
            _index[npcId] = e;
            return e;
        }

        public bool Knows(int npcId, NPCInfoFlags flag)
        {
            var e = Get(npcId);
            return e != null && e.Knows(flag);
        }

        /// <summary>新しく判明したフラグだけを返す。</summary>
        public NPCInfoFlags Reveal(int npcId, NPCInfoFlags flags)
        {
            var e = GetOrCreate(npcId);
            var newly = flags & ~e.known;
            e.known |= flags;
            return newly;
        }

        public bool KnowsRelation(int a, int b)
        {
            EnsureIndex();
            return _relationKeys.Contains(PairKey(a, b));
        }

        /// <summary>相関図に線を追加。既知なら false。</summary>
        public bool AddRelation(int fromId, int toId, RelationType type, int sourceId, int day)
        {
            EnsureIndex();
            if (!_relationKeys.Add(PairKey(fromId, toId))) return false;
            knownRelations.Add(new KnownRelation { fromId = fromId, toId = toId, type = type, sourceId = sourceId, discoveredDay = day });
            return true;
        }

        /// <summary>相関図描画用：あるNPCにつながる既知の線。</summary>
        public List<KnownRelation> RelationsOf(int npcId)
        {
            var list = new List<KnownRelation>();
            foreach (var r in knownRelations)
                if (r.fromId == npcId || r.toId == npcId) list.Add(r);
            return list;
        }

        /// <summary>電話帳：番号を知っているNPC。</summary>
        public List<int> PhoneBook()
        {
            var list = new List<int>();
            foreach (var e in entries) if (e.Knows(NPCInfoFlags.PhoneNumber)) list.Add(e.npcId);
            return list;
        }
    }
}
