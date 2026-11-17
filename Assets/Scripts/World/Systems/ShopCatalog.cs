using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.World
{
    /// <summary>初期の商品一覧。</summary>
    public static class ShopCatalog
    {
        public static List<ShopItem> CreateDefault()
        {
            var list = new List<ShopItem>();
            // ---- 表のお店 ----
            Add(list, "BAR ネオン", Town.Kirahama, "サッカーハイボール", 800, 40, 40);
            Add(list, "煙屋ゲン", Town.Furugane, "魔除けタバコ", 600, 15, 15);
            Add(list, "ナイトクリニック", Town.Yushin, "ノイローゼ注射", 30000, 4, 3);
            Add(list, "天峰マルシェ", Town.Tenpou, "若返りフレーク", 4800, 8, 6);
            Add(list, "キラマート", Town.Kirahama, "使用済みポテチ（伝説の選手が食べた）", 50000, 1, 0);
            Add(list, "タピオカ屋 黒糖丸", Town.Kirahama, "脅迫タピオカ", 900, 0, 0, alwaysSoldOut: true);
            Add(list, "ブティック SHINE", Town.Kirahama, "限定スパイク「流星」", 180000, 1, 0, negotiation: true);
            Add(list, "ブティック SHINE", Town.Kirahama, "ブランドジャージ", 25000, 3, 0);
            Add(list, "天峰ギャラリー", Town.Tenpou, "伝説のユニフォーム（サイン入り）", 1200000, 1, 0, negotiation: true);
            Add(list, "ユウシンマート", Town.Yushin, "おにぎり", 150, 60, 60);
            Add(list, "ユウシンマート", Town.Yushin, "スポーツドリンク", 160, 60, 60);
            Add(list, "古鐘商店", Town.Furugane, "手作りすね当て", 3000, 4, 0);
            Add(list, "古鐘商店", Town.Furugane, "大漁ハチマキ", 1200, 6, 3);
            Add(list, "天峰スポーツ", Town.Tenpou, "最新トレーニングシューズ", 22000, 3, 2);
            Add(list, "ブティック SHINE", Town.Kirahama, "島ブランドのサングラス", 18000, 4, 2);
            Add(list, "古着屋 波止場", Town.Furugane, "ヤンキー刺繍の特攻ジャージ", 9800, 3, 1);
            Add(list, "キラマート", Town.Kirahama, "スタミナ弁当", 650, 30, 30);
            Add(list, "ユウシンマート", Town.Yushin, "エナジーゼリー", 220, 40, 40);

            // ---- 裏市場（場所はころころ変わる） ----
            Add(list, "裏市場", Town.Kirahama, "脅迫タピオカ（裏ルート）", 5000, 8, 4, underground: true);
            Add(list, "裏市場", Town.Furugane, "闇スパイク", 300000, 1, 0, underground: true, negotiation: true);
            Add(list, "裏市場", Town.Tenpou, "若返りフレーク（原液）", 80000, 2, 0, underground: true);
            Add(list, "裏市場", Town.Kirahama, "裏toto 必勝メモ", 20000, 3, 1, underground: true);
            return list;
        }

        static void Add(List<ShopItem> list, string shop, Town town, string name, long price, int stock, int restock,
                        bool underground = false, bool alwaysSoldOut = false, bool negotiation = false)
        {
            list.Add(new ShopItem
            {
                id = list.Count, shopName = shop, town = town, name = name, price = price,
                stock = stock, restockAmount = restock, isUnderground = underground,
                alwaysSoldOutOnSurface = alwaysSoldOut, requiresNegotiation = negotiation,
            });
        }
    }
}
