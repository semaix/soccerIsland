using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    public enum RomanceStatus
    {
        None,
        Dating,  // デートする仲
        Partner, // 恋人
    }

    public enum DateSpot
    {
        Bar,   // 煌浜の飲み屋（外交的が好き）
        Cafe,  // 喫茶店（内向的が好き）
        Beach, // 夜の浜辺（根暗が好き。無料）
        Match, // サッカー観戦（サッカー好きなら誰でも）
    }

    [Serializable]
    public class RomanceEntry
    {
        public int npcId;
        /// <summary>恋愛感情 0-100（好感度とは別）。</summary>
        public int love;
        public RomanceStatus status;
        public int lastDateDay = -1;
    }

    [Serializable]
    public class RomanceState
    {
        public List<RomanceEntry> entries = new List<RomanceEntry>();

        public RomanceEntry Get(int npcId)
        {
            foreach (var e in entries) if (e.npcId == npcId) return e;
            return null;
        }

        public int PartnerId
        {
            get { foreach (var e in entries) if (e.status == RomanceStatus.Partner) return e.npcId; return -1; }
        }
    }

    public class RomanceResult
    {
        public bool ok;
        public string reason;
        public string line;
        public int loveDelta;
    }

    /// <summary>
    /// 恋愛（2周目から）。相手は20歳以上の島民。性別は問わない。
    /// デート・プレゼント・告白で恋人になる。恋人と同じチームだと試合で燃える。
    /// 有名になると熱愛がゴシップになる。
    /// </summary>
    public class RomanceSystem
    {
        public int minAge = 20;
        public int minAffinity = 40;

        readonly PlayerState _player;
        readonly IslandPopulation _pop;
        readonly WorldState _world;
        readonly RomanceState _state;
        readonly List<FameEvent> _fameEvents;
        readonly SeededRandom _rng;

        public RomanceState State => _state;

        public RomanceSystem(PlayerState player, IslandPopulation pop, WorldState world, RomanceState state, List<FameEvent> fameEvents, SeededRandom rng)
        {
            _player = player;
            _pop = pop;
            _world = world;
            _state = state;
            _fameEvents = fameEvents;
            _rng = rng;
        }

        public bool Unlocked => ReincarnationSystem.RomanceUnlocked(_player);

        public string CheckEligible(NPCData n)
        {
            if (!Unlocked) return "まだその気になれない……（2周目から）";
            if (n == null) return "相手がいない";
            if (n.age < minAge) return "相手は20歳以上";
            if (n.isIncarcerated) return "服役中";
            var e = _pop.Knowledge.Get(n.id);
            if (e == null || !e.Knows(NPCInfoFlags.Name)) return "まだ知り合っていない";
            if (e.affinity < minAffinity) return "まだそこまで仲良くない";
            return null;
        }

        RomanceEntry Entry(int id)
        {
            var e = _state.Get(id);
            if (e == null) { e = new RomanceEntry { npcId = id }; _state.entries.Add(e); }
            return e;
        }

        static long Cost(DateSpot s) => s == DateSpot.Bar ? 8000 : s == DateSpot.Cafe ? 3000 : s == DateSpot.Match ? 1000 : 0;

        public RomanceResult Date(int npcId, DateSpot spot)
        {
            var n = _pop.Database.Get(npcId);
            var r = new RomanceResult { reason = CheckEligible(n) };
            if (r.reason != null) return r;
            int day = _pop.Database.currentDay;
            var e = Entry(npcId);
            if (e.lastDateDay == day) { r.reason = "今日はもう会った"; return r; }
            if (!_player.TrySpend(Cost(spot), "デート", day)) { r.reason = "お金が足りない"; return r; }

            bool likes = spot == DateSpot.Match ||
                         (spot == DateSpot.Bar && n.personality == Personality.Extrovert) ||
                         (spot == DateSpot.Cafe && n.personality == Personality.Introvert) ||
                         (spot == DateSpot.Beach && n.personality == Personality.Gloomy);
            r.loveDelta = likes ? 8 + _rng.Range(0, 4) : 3 + _rng.Range(0, 3);
            e.love = Math.Min(100, e.love + r.loveDelta);
            e.lastDateDay = day;
            if (e.status == RomanceStatus.None) e.status = RomanceStatus.Dating;
            var k = _pop.Knowledge.GetOrCreate(npcId);
            k.affinity = Math.Min(100, k.affinity + 2);
            r.ok = true;
            r.line = likes ? "今日、すごく楽しかった。また誘ってね。" : "……うん、まあまあだったかな。";

            if (_player.fame >= 50f && _rng.Chance(0.25f))
                _fameEvents.Add(new FameEvent { type = FameEventType.RomanceRumor, day = day, npcId = npcId, text = SpotName(spot) });
            return r;
        }

        /// <summary>お店の商品を買ってプレゼントする（在庫が減り、最後の1個なら完売する）。</summary>
        public RomanceResult Gift(int npcId, int itemId)
        {
            var n = _pop.Database.Get(npcId);
            var r = new RomanceResult { reason = CheckEligible(n) };
            if (r.reason != null) return r;
            var item = _world.GetItem(itemId);
            if (item == null || item.SurfaceStatus != ItemStatus.OnSale || item.stock <= 0 || item.isUnderground) { r.reason = "その商品は買えない"; return r; }
            int day = _pop.Database.currentDay;
            if (!_player.TrySpend(item.price, "プレゼント: " + item.name, day)) { r.reason = "お金が足りない"; return r; }
            item.stock--;
            if (item.stock <= 0) { item.status = ItemStatus.SoldOut; item.buyerId = -1; }

            var e = Entry(npcId);
            r.loveDelta = Math.Min(15, 3 + (int)Math.Log10(Math.Max(10, item.price)) * 2);
            e.love = Math.Min(100, e.love + r.loveDelta);
            if (e.status == RomanceStatus.None) e.status = RomanceStatus.Dating;
            r.ok = true;
            r.line = $"え、{item.name}？ ……ありがとう。";
            return r;
        }

        public RomanceResult Confess(int npcId)
        {
            var n = _pop.Database.Get(npcId);
            var r = new RomanceResult { reason = CheckEligible(n) };
            if (r.reason != null) return r;
            if (_state.PartnerId >= 0 && _state.PartnerId != npcId) { r.reason = "もう恋人がいる"; return r; }
            var e = Entry(npcId);
            if (e.status == RomanceStatus.Partner) { r.reason = "もう恋人だ"; return r; }
            var k = _pop.Knowledge.Get(npcId);
            float chance = e.love < 70 ? 0f : (e.love - 60) / 40f * (k.affinity >= 60 ? 1f : 0.5f);
            if (_rng.Chance(chance))
            {
                e.status = RomanceStatus.Partner;
                r.ok = true;
                r.line = n.personality == Personality.Gloomy ? "……私でいいなら。" : "うん……！ よろしくお願いします！";
                if (_player.fame >= 40f)
                    _fameEvents.Add(new FameEvent { type = FameEventType.RomanceRevealed, day = _pop.Database.currentDay, npcId = npcId });
            }
            else
            {
                e.love = Math.Max(0, e.love - 10);
                r.reason = "ふられた";
                r.line = "ごめんなさい。友達でいたいの。";
            }
            return r;
        }

        static string SpotName(DateSpot s) => s == DateSpot.Bar ? "煌浜の飲み屋" : s == DateSpot.Cafe ? "喫茶店" : s == DateSpot.Beach ? "夜の浜辺" : "サッカー観戦";
    }
}
