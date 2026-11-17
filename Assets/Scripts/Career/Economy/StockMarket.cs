using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    /// <summary>
    /// 株。チーム株（試合結果・所属選手の話題性で動く）と商店株（売れ行き・完売で動く）。
    /// </summary>
    public class StockMarket
    {
        public double tradingFee = 0.005;
        public double minimumPrice = 10;
        public int historyLength = 90;
        public double surgeThreshold = 0.12;

        readonly NPCDatabase _db;
        readonly WorldState _world;
        readonly EconomyState _state;
        readonly PlayerState _player;
        readonly SeededRandom _rng;
        readonly List<EconomyEvent> _events;

        public StockMarket(NPCDatabase db, WorldState world, EconomyState state, PlayerState player, SeededRandom rng, List<EconomyEvent> events)
        {
            _db = db;
            _world = world;
            _state = state;
            _player = player;
            _rng = rng;
            _events = events;
        }

        /// <summary>新規ゲーム時に上場銘柄を作る。</summary>
        public void ListInitialStocks()
        {
            foreach (var club in _db.clubs)
            {
                if (!club.registeredWithAssociation || club.category == ClubCategory.Prison) continue;
                double avg = 0;
                int n = 0;
                foreach (var m in _db.GetClubMembers(club.id)) { avg += m.marketValue; n++; }
                if (n == 0) continue;
                AddStock(club.name, StockKind.Club, club.id, null, Math.Max(100, avg / n / 1000));
            }
            var shops = new HashSet<string>();
            foreach (var item in _world.items)
            {
                if (item.isUnderground || !shops.Add(item.shopName)) continue;
                AddStock(item.shopName, StockKind.Shop, -1, item.shopName, 300 + _rng.Range(0, 700));
            }
        }

        void AddStock(string name, StockKind kind, int clubId, string shop, double price)
        {
            var s = new Stock { id = _state.stocks.Count, name = name, kind = kind, clubId = clubId, shopName = shop, price = Math.Round(price) };
            s.history.Add((float)s.price);
            _state.stocks.Add(s);
        }

        public long Cost(Stock s, long shares) => (long)Math.Ceiling(s.price * shares * (1 + tradingFee));

        public bool Buy(int stockId, long shares)
        {
            var s = _state.GetStock(stockId);
            if (s == null || shares <= 0) return false;
            long cost = Cost(s, shares);
            if (!_player.TrySpend(cost, $"株購入: {s.name} {shares}株", _db.currentDay)) return false;
            var h = _state.GetHolding(stockId);
            if (h == null) _state.holdings.Add(h = new Holding { stockId = stockId });
            h.averageCost = (h.averageCost * h.shares + cost) / (h.shares + shares);
            h.shares += shares;
            return true;
        }

        public bool Sell(int stockId, long shares)
        {
            var s = _state.GetStock(stockId);
            var h = _state.GetHolding(stockId);
            if (s == null || h == null || shares <= 0 || shares > h.shares) return false;
            long proceeds = (long)Math.Floor(s.price * shares * (1 - tradingFee));
            _player.Earn(proceeds, $"株売却: {s.name} {shares}株", _db.currentDay);
            h.shares -= shares;
            if (h.shares == 0) _state.holdings.Remove(h);
            return true;
        }

        /// <summary>評価額の合計。</summary>
        public long PortfolioValue()
        {
            double v = 0;
            foreach (var h in _state.holdings)
            {
                var s = _state.GetStock(h.stockId);
                if (s != null) v += s.price * h.shares;
            }
            return (long)v;
        }

        public void TickDay(WorldDayReport report)
        {
            var clubResult = new Dictionary<int, double>();
            if (report != null)
            {
                foreach (var m in report.playedMatches)
                {
                    double weight = m.category == MatchCategory.RegionalDerby || m.category == MatchCategory.IslandSelect ? 1.5 : 1.0;
                    double home = m.homeScore > m.awayScore ? 0.04 : m.homeScore < m.awayScore ? -0.035 : 0.0;
                    Accumulate(clubResult, m.homeClubId, home * weight);
                    Accumulate(clubResult, m.awayClubId, -home * weight);
                }
            }

            var shopSales = new Dictionary<string, double>();
            if (report != null)
            {
                foreach (var p in report.purchases)
                {
                    var item = _world.GetItem(p.itemId);
                    if (item == null || item.isUnderground) continue;
                    double effect = 0.006 + (item.status == ItemStatus.SoldOut ? 0.02 : 0.0);
                    shopSales[item.shopName] = (shopSales.TryGetValue(item.shopName, out var v) ? v : 0) + effect;
                }
            }

            foreach (var s in _state.stocks)
            {
                double change = _rng.Gaussian(0f, 0.015f);
                if (s.kind == StockKind.Club)
                {
                    if (clubResult.TryGetValue(s.clubId, out var r)) change += r;
                    change += AverageHype(s.clubId) * 0.03;
                }
                else
                {
                    change -= 0.002; // 何も売れないと少しずつ下がる
                    if (shopSales.TryGetValue(s.shopName, out var sales)) change += sales;
                }
                double before = s.price;
                s.price = Math.Max(minimumPrice, Math.Round(s.price * (1 + change), 1));
                s.history.Add((float)s.price);
                if (s.history.Count > historyLength) s.history.RemoveRange(0, s.history.Count - historyLength);

                double ratio = (s.price - before) / before;
                if (Math.Abs(ratio) >= surgeThreshold)
                    _events.Add(new EconomyEvent
                    {
                        type = ratio > 0 ? EconomyEventType.StockSurge : EconomyEventType.StockCrash,
                        day = _db.currentDay, refId = s.id, amount = s.Price, text = s.name,
                    });
            }
        }

        double AverageHype(int clubId)
        {
            double sum = 0;
            int n = 0;
            foreach (var m in _db.GetClubMembers(clubId)) { sum += m.hype; n++; }
            return n == 0 ? 0 : sum / n;
        }

        static void Accumulate(Dictionary<int, double> d, int key, double v)
        {
            if (key < 0) return;
            d[key] = (d.TryGetValue(key, out var cur) ? cur : 0) + v;
        }
    }
}
