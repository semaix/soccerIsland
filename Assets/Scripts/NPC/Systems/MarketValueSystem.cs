using System;
using System.Collections.Generic;

namespace SoccerIsland.NPC
{
    /// <summary>市場価値が変わったことの通知。サッカー新聞・ランキング更新に使う。</summary>
    public struct MarketValueChange
    {
        public int npcId;
        public long before;
        public long after;
        public string reason;

        public long Delta => after - before;
        public float Ratio => before <= 0 ? 0f : (float)(after - before) / before;
    }

    /// <summary>
    /// 市場価値の変動を扱う。
    /// ・話題性(hype)：会話の噂・試合の活躍で上下し、毎日少しずつ0に戻る
    /// ・日々の揺らぎ：小さなランダム変動
    /// </summary>
    public class MarketValueSystem
    {
        public float hypeDecayPerDay = 0.96f;
        public float dailyNoiseStdDev = 0.012f;
        public float minHype = -0.5f;
        public float maxHype = 1.0f;
        public int historyInterval = 7;
        public int maxHistory = 104;

        readonly NPCDatabase _db;
        readonly SeededRandom _rng;

        public event Action<MarketValueChange> OnChanged;

        public MarketValueSystem(NPCDatabase db, SeededRandom rng)
        {
            _db = db;
            _rng = rng;
        }

        /// <summary>話題性を変化させて市場価値を再計算する。</summary>
        public MarketValueChange AddHype(NPCData npc, float amount, string reason)
        {
            npc.hype = Clamp(npc.hype + amount, minHype, maxHype);
            return Refresh(npc, reason);
        }

        /// <summary>現在のパラメータから市場価値・年俸を再計算する。</summary>
        public MarketValueChange Refresh(NPCData npc, string reason, float noise = 0f)
        {
            long before = npc.marketValue;
            npc.marketValue = MarketValueCalculator.Calculate(npc, noise);
            npc.salary = MarketValueCalculator.CalculateSalary(npc);
            var change = new MarketValueChange { npcId = npc.id, before = before, after = npc.marketValue, reason = reason };
            if (change.Delta != 0) OnChanged?.Invoke(change);
            return change;
        }

        /// <summary>1日進める。大きく動いたNPCを返す（新聞ネタ）。</summary>
        public List<MarketValueChange> TickDay(int day, float headlineThreshold = 0.1f)
        {
            var headlines = new List<MarketValueChange>();
            foreach (var npc in _db.npcs)
            {
                npc.hype *= hypeDecayPerDay;
                if (Math.Abs(npc.hype) < 0.001f) npc.hype = 0f;

                var c = Refresh(npc, "daily", _rng.Gaussian(0f, dailyNoiseStdDev));
                if (Math.Abs(c.Ratio) >= headlineThreshold) headlines.Add(c);

                if (day % historyInterval == 0) Record(npc, day);
            }
            return headlines;
        }

        public void Record(NPCData npc, int day)
        {
            var h = npc.marketValueHistory;
            if (h.Count > 0 && h[h.Count - 1].day == day) h[h.Count - 1] = new MarketValueRecord(day, npc.marketValue);
            else h.Add(new MarketValueRecord(day, npc.marketValue));
            if (h.Count > maxHistory) h.RemoveRange(0, h.Count - maxHistory);
        }

        static float Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
