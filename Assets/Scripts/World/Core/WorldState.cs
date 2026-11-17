using System;
using System.Collections.Generic;

namespace SoccerIsland.World
{
    /// <summary>試合予定・お店の在庫・購入履歴。NPCデータとは別ファイルでセーブする。</summary>
    [Serializable]
    public class WorldState
    {
        public int nextMatchId;
        /// <summary>この日までの試合予定を作成済み。</summary>
        public int scheduledThroughDay = -1;
        public List<Match> matches = new List<Match>();
        public List<ShopItem> items = new List<ShopItem>();
        public List<PurchaseRecord> purchases = new List<PurchaseRecord>();

        public Match GetMatch(int id)
        {
            foreach (var m in matches) if (m.id == id) return m;
            return null;
        }

        public ShopItem GetItem(int id)
        {
            foreach (var i in items) if (i.id == id) return i;
            return null;
        }

        public List<Match> MatchesOn(int day)
        {
            var list = new List<Match>();
            foreach (var m in matches) if (m.day == day) list.Add(m);
            return list;
        }

        /// <summary>そのNPCが出る、まだ終わっていない直近の試合。</summary>
        public Match NextMatchOf(int npcId, int fromDay, int withinDays)
        {
            Match best = null;
            foreach (var m in matches)
            {
                if (m.played || m.day < fromDay || m.day > fromDay + withinDays || !m.Involves(npcId)) continue;
                if (best == null || m.day < best.day) best = m;
            }
            return best;
        }
    }
}
