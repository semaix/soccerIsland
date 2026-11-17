using System;
using SoccerIsland.NPC;

namespace SoccerIsland.World
{
    public enum ItemStatus
    {
        OnSale,      // 販売中
        Negotiating, // 商談中
        SoldOut,     // 完売
    }

    [Serializable]
    public class ShopItem
    {
        public int id;
        public string name;
        public string shopName;
        public Town town;
        public long price;
        public int stock;
        /// <summary>入荷数。0なら再入荷しない一点物・限定品。</summary>
        public int restockAmount;
        /// <summary>裏市場の商品か。</summary>
        public bool isUnderground;
        /// <summary>表では常に完売（裏市場にだけ在庫がある）。</summary>
        public bool alwaysSoldOutOnSurface;
        /// <summary>この値段以上は即売りせず商談になる。</summary>
        public bool requiresNegotiation;

        public ItemStatus status = ItemStatus.OnSale;
        /// <summary>商談中・購入済みの相手（NPC ID）。</summary>
        public int buyerId = -1;
        public int negotiationEndDay;

        public bool IsLimited => restockAmount <= 0;

        /// <summary>表のお店で見たときの状態。</summary>
        public ItemStatus SurfaceStatus => alwaysSoldOutOnSurface ? ItemStatus.SoldOut : status;
    }

    [Serializable]
    public class PurchaseRecord
    {
        public int npcId;
        public int itemId;
        public int day;
        /// <summary>商談中なら false、成立したら true。</summary>
        public bool completed;
        /// <summary>プレイヤーに話したか（同じ話を何度もしない）。</summary>
        public bool mentioned;
    }

    public static class ItemLabels
    {
        public static string Of(ItemStatus s)
        {
            switch (s)
            {
                case ItemStatus.OnSale: return "販売中";
                case ItemStatus.Negotiating: return "商談中";
                default: return "完売";
            }
        }
    }
}
