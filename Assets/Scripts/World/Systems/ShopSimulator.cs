using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.World
{
    /// <summary>
    /// 島民の買い物。在庫が減って完売、高額品は商談中→成立/破談。
    /// NPCが「買った」と話す内容は、ここで記録された購入履歴から作られる。
    /// </summary>
    public class ShopSimulator
    {
        public float dailyShopperRatio = 0.02f;
        public float hometownPreference = 0.7f;
        public float negotiationSuccessRate = 0.8f;
        public int keepHistoryDays = 60;

        readonly NPCDatabase _db;
        readonly WorldState _state;
        readonly SeededRandom _rng;

        public ShopSimulator(NPCDatabase db, WorldState state, SeededRandom rng)
        {
            _db = db;
            _state = state;
            _rng = rng;
        }

        public List<PurchaseRecord> TickDay(int day)
        {
            ResolveNegotiations(day);
            if (day % 7 == 0) Restock();

            var bought = new List<PurchaseRecord>();
            int shoppers = Math.Max(1, (int)(_db.Count * dailyShopperRatio));
            for (int i = 0; i < shoppers; i++)
            {
                var npc = _db.npcs[_rng.Range(0, _db.Count)];
                var record = TryShop(npc, day);
                if (record != null) bought.Add(record);
            }

            _state.purchases.RemoveAll(p => p.completed && p.day < day - keepHistoryDays);
            return bought;
        }

        PurchaseRecord TryShop(NPCData npc, int day)
        {
            if (npc.isIncarcerated || npc.age < 13) return null;
            long budget = Math.Max(5000, (npc.jobIncome + npc.salary) / 40);

            var candidates = new List<ShopItem>();
            var weights = new List<float>();
            foreach (var item in _state.items)
            {
                if (item.status != ItemStatus.OnSale || item.stock <= 0 || item.price > budget) continue;
                if (item.isUnderground && !npc.isUnderground) continue;
                if (item.alwaysSoldOutOnSurface) continue;
                float w = item.town == npc.hometown ? hometownPreference : 1f - hometownPreference;
                w *= 1f / (float)Math.Log10(item.price + 10); // 安い物ほどよく売れる
                if (item.IsLimited) w *= 0.6f;
                candidates.Add(item);
                weights.Add(w);
            }
            if (candidates.Count == 0) return null;
            return Buy(npc, candidates[_rng.WeightedIndex(weights)], day);
        }

        /// <summary>NPCが商品を買う（イベントから直接呼んでもよい）。</summary>
        public PurchaseRecord Buy(NPCData npc, ShopItem item, int day)
        {
            if (item.status != ItemStatus.OnSale || item.stock <= 0) return null;
            var record = new PurchaseRecord { npcId = npc.id, itemId = item.id, day = day };
            if (item.requiresNegotiation)
            {
                item.status = ItemStatus.Negotiating;
                item.buyerId = npc.id;
                item.negotiationEndDay = day + _rng.Range(2, 7);
            }
            else
            {
                Sell(item, npc.id);
                record.completed = true;
            }
            _state.purchases.Add(record);
            return record;
        }

        void Sell(ShopItem item, int buyerId)
        {
            item.stock--;
            if (item.stock <= 0)
            {
                item.stock = 0;
                item.status = ItemStatus.SoldOut;
                item.buyerId = buyerId;
            }
            else
            {
                item.status = ItemStatus.OnSale;
                item.buyerId = -1;
            }
        }

        void ResolveNegotiations(int day)
        {
            foreach (var item in _state.items)
            {
                if (item.status != ItemStatus.Negotiating || day < item.negotiationEndDay) continue;
                var record = _state.purchases.Find(p => p.itemId == item.id && p.npcId == item.buyerId && !p.completed);
                if (_rng.Chance(negotiationSuccessRate))
                {
                    Sell(item, item.buyerId);
                    if (record != null) record.completed = true;
                }
                else
                {
                    // 破談
                    item.status = ItemStatus.OnSale;
                    item.buyerId = -1;
                    if (record != null) _state.purchases.Remove(record);
                }
            }
        }

        void Restock()
        {
            foreach (var item in _state.items)
            {
                if (item.restockAmount <= 0 || item.status == ItemStatus.Negotiating) continue;
                if (item.stock < item.restockAmount) item.stock = item.restockAmount;
                item.status = ItemStatus.OnSale;
                item.buyerId = -1;
            }
        }
    }
}
