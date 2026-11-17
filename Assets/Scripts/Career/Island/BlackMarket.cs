using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    /// <summary>
    /// 裏市場。場所は3日ごとに変わる（深夜のバー、廃墟、港の倉庫…）。
    /// 裏の顔を持つ店主・バーテンダー・マフィアと仲良くなると、今の場所を教えてくれる。
    /// 中では、いつも完売の商品の裏ルート品・裏ランキング・裏でしか出回らない人の情報が買える。
    /// </summary>
    public class BlackMarket
    {
        public static readonly string[] Locations =
        {
            "深夜だけ開くバー「月影」の奥", "廃墟になったボウリング場", "港の第三倉庫", "古鐘の神社の地下", "閉店後のブティックSHINE", "灯台の下の洞窟",
        };
        public const int PeriodDays = 3;
        public long infoPrice = 300000;

        readonly PlayerState _player;
        readonly IslandPopulation _pop;
        readonly WorldState _world;
        readonly IslandEventsState _state;

        public BlackMarket(PlayerState player, IslandPopulation pop, WorldState world, IslandEventsState state)
        {
            _player = player;
            _pop = pop;
            _world = world;
            _state = state;
        }

        int Period => _pop.Database.currentDay / PeriodDays;

        public string CurrentLocation => Locations[(Period + _pop.Database.seed % Locations.Length + Locations.Length) % Locations.Length];

        public bool KnowsLocation => _state.knownBlackMarketPeriod == Period;

        /// <summary>入れるか（裏市場の存在を知っていて、今の場所も知っている）。</summary>
        public bool CanEnter => _player.blackMarketAccess && KnowsLocation;

        public void LearnLocation() => _state.knownBlackMarketPeriod = Period;

        public List<ShopItem> Items()
        {
            var list = new List<ShopItem>();
            if (!CanEnter) return list;
            foreach (var i in _world.items) if (i.isUnderground) list.Add(i);
            return list;
        }

        /// <summary>買った物は持ち物に入る（inventory を渡したとき）。</summary>
        public InventorySystem inventory;

        public bool Buy(int itemId)
        {
            var item = _world.GetItem(itemId);
            if (!CanEnter || item == null || !item.isUnderground || item.status != ItemStatus.OnSale || item.stock <= 0) return false;
            if (!_player.TrySpend(item.price, "裏市場: " + item.name, _pop.Database.currentDay)) return false;
            item.stock--;
            if (item.stock <= 0) { item.status = ItemStatus.SoldOut; item.buyerId = -1; }
            inventory?.Add(item.name);
            return true;
        }

        /// <summary>裏ランキング（協会に載らない人たちの市場価値）。裏市場の中でしか見られない。</summary>
        public List<NPCData> UndergroundRanking(int top) => CanEnter ? _pop.Database.GetUndergroundMarketValueRanking(top) : new List<NPCData>();

        /// <summary>裏でしか情報が出回らない人の情報を買う。</summary>
        public bool BuyInfo(int npcId)
        {
            var n = _pop.Database.Get(npcId);
            if (!CanEnter || n == null || (!n.isUnderground && n.registeredWithAssociation)) return false;
            if (!_player.TrySpend(infoPrice, "裏の情報屋", _pop.Database.currentDay)) return false;
            _pop.Knowledge.Reveal(n.id, NPCInfoFlags.Basic | NPCInfoFlags.Club | NPCInfoFlags.SoccerPersonality |
                                        NPCInfoFlags.MarketValue | NPCInfoFlags.UndergroundSecret);
            return true;
        }
    }

    /// <summary>裏の顔を持つ店主たちが、今夜の裏市場の場所を教えてくれる。</summary>
    public class BlackMarketHintProvider : IConversationTopicProvider
    {
        readonly BlackMarket _market;
        public int requiredAffinity = 60;

        public BlackMarketHintProvider(BlackMarket market) { _market = market; }

        public bool TryGetTopic(NPCData npc, TopicContext ctx, out TopicMention mention)
        {
            mention = default;
            if (!npc.isUnderground || ctx.affinity < requiredAffinity || _market.KnowsLocation) return false;
            if (npc.occupation != Occupation.Bartender && npc.occupation != Occupation.ShopClerk && npc.occupation != Occupation.Mafia &&
                npc.occupation != Occupation.ConvenienceClerk) return false;
            _market.LearnLocation();
            mention = new TopicMention { kind = TopicKind.Other, refId = -1, line = $"……今夜の裏市場は{_market.CurrentLocation}だ。合言葉は「ボールは丸い」。" };
            return true;
        }
    }
}
