using System;
using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    /// <summary>
    /// 島の全NPCとクラブを保持するデータベース。
    /// シリアライズされるのは npcs / clubs / 日付などの生データのみで、検索用インデックスはロード後に再構築する。
    /// </summary>
    [Serializable]
    public class NPCDatabase
    {
        public int seed;
        public int currentYear;
        /// <summary>ゲーム開始からの通算日数。</summary>
        public int currentDay;
        public List<NPCData> npcs = new List<NPCData>();
        public List<SoccerClub> clubs = new List<SoccerClub>();

        [NonSerialized] Dictionary<int, NPCData> _byId;
        [NonSerialized] Dictionary<string, NPCData> _byPhone;
        [NonSerialized] Dictionary<Town, List<NPCData>> _byTown;
        [NonSerialized] Dictionary<int, List<NPCData>> _byClub;
        [NonSerialized] Dictionary<Occupation, List<NPCData>> _byOccupation;
        [NonSerialized] Dictionary<int, SoccerClub> _clubById;

        public int Count => npcs.Count;

        // ------------------------------------------------------------------
        // 追加・インデックス
        // ------------------------------------------------------------------

        public void AddClub(SoccerClub club)
        {
            clubs.Add(club);
            _clubById = null;
        }

        public void Add(NPCData npc)
        {
            npcs.Add(npc);
            if (_byId != null) IndexOne(npc);
        }

        /// <summary>ロード直後や大量の編集後に呼ぶ。</summary>
        public void RebuildIndex()
        {
            _byId = new Dictionary<int, NPCData>(npcs.Count);
            _byPhone = new Dictionary<string, NPCData>(npcs.Count);
            _byTown = new Dictionary<Town, List<NPCData>>();
            _byClub = new Dictionary<int, List<NPCData>>();
            _byOccupation = new Dictionary<Occupation, List<NPCData>>();
            foreach (var npc in npcs) IndexOne(npc);

            _clubById = new Dictionary<int, SoccerClub>(clubs.Count);
            foreach (var c in clubs) _clubById[c.id] = c;
        }

        void IndexOne(NPCData npc)
        {
            _byId[npc.id] = npc;
            if (!string.IsNullOrEmpty(npc.phoneNumber)) _byPhone[npc.phoneNumber] = npc;
            GetOrCreate(_byTown, npc.hometown).Add(npc);
            GetOrCreate(_byClub, npc.clubId).Add(npc);
            GetOrCreate(_byOccupation, npc.occupation).Add(npc);
        }

        static List<NPCData> GetOrCreate<TKey>(Dictionary<TKey, List<NPCData>> dict, TKey key)
        {
            if (!dict.TryGetValue(key, out var list))
            {
                list = new List<NPCData>();
                dict[key] = list;
            }
            return list;
        }

        void EnsureIndex()
        {
            if (_byId == null || _clubById == null) RebuildIndex();
        }

        /// <summary>クラブ移籍など、インデックスに関わる値を変えたあとに呼ぶ。</summary>
        public void MarkDirty() => _byId = null;

        // ------------------------------------------------------------------
        // 取得
        // ------------------------------------------------------------------

        public NPCData Get(int id)
        {
            EnsureIndex();
            return _byId.TryGetValue(id, out var npc) ? npc : null;
        }

        public bool TryGet(int id, out NPCData npc)
        {
            npc = Get(id);
            return npc != null;
        }

        /// <summary>電話番号から直接相手を引く（電話システム用）。</summary>
        public NPCData FindByPhone(string phone)
        {
            EnsureIndex();
            return phone != null && _byPhone.TryGetValue(phone, out var npc) ? npc : null;
        }

        public SoccerClub GetClub(int clubId)
        {
            EnsureIndex();
            return _clubById.TryGetValue(clubId, out var c) ? c : null;
        }

        public IReadOnlyList<NPCData> GetByTown(Town town)
        {
            EnsureIndex();
            return _byTown.TryGetValue(town, out var l) ? l : (IReadOnlyList<NPCData>)Array.Empty<NPCData>();
        }

        public IReadOnlyList<NPCData> GetClubMembers(int clubId)
        {
            EnsureIndex();
            return _byClub.TryGetValue(clubId, out var l) ? l : (IReadOnlyList<NPCData>)Array.Empty<NPCData>();
        }

        public IReadOnlyList<NPCData> GetByOccupation(Occupation occupation)
        {
            EnsureIndex();
            return _byOccupation.TryGetValue(occupation, out var l) ? l : (IReadOnlyList<NPCData>)Array.Empty<NPCData>();
        }

        public List<NPCData> Query(Predicate<NPCData> predicate)
        {
            var result = new List<NPCData>();
            foreach (var npc in npcs)
                if (predicate(npc)) result.Add(npc);
            return result;
        }

        /// <summary>指定タイプの関係にあるNPCを返す。</summary>
        public List<NPCData> GetRelated(NPCData npc, RelationType type)
        {
            var result = new List<NPCData>();
            foreach (var r in npc.relations)
                if (r.type == type && TryGet(r.targetId, out var other)) result.Add(other);
            return result;
        }

        // ------------------------------------------------------------------
        // ランキング
        // ------------------------------------------------------------------

        /// <summary>サッカー協会が発表する公式の市場価値ランキング。裏の人間・服役者・未登録者は載らない。</summary>
        public List<NPCData> GetOfficialMarketValueRanking(int top)
        {
            return Ranking(npc => npc.AppearsInOfficialRanking, top);
        }

        /// <summary>裏市場でだけ出回るランキング（裏の人間・未登録者も含む）。</summary>
        public List<NPCData> GetUndergroundMarketValueRanking(int top)
        {
            return Ranking(npc => npc.isUnderground || !npc.registeredWithAssociation, top);
        }

        public List<NPCData> Ranking(Predicate<NPCData> filter, int top)
        {
            var list = Query(filter);
            list.Sort((a, b) =>
            {
                int c = b.marketValue.CompareTo(a.marketValue);
                return c != 0 ? c : a.id.CompareTo(b.id);
            });
            if (top > 0 && list.Count > top) list.RemoveRange(top, list.Count - top);
            return list;
        }

        /// <summary>同級生（同じ街・同じ生まれ年）。</summary>
        public List<NPCData> GetClassmates(NPCData npc) => GetRelated(npc, RelationType.Classmate);
    }
}
