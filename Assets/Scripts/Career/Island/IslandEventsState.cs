using System;
using System.Collections.Generic;

namespace SoccerIsland.Career
{
    /// <summary>島のイベント（裏市場・謎のプレーヤー・漂流者）の進み具合。</summary>
    [Serializable]
    public class IslandEventsState
    {
        // 裏市場
        /// <summary>場所を知っている期間（場所が変わる3日単位の番号）。-1 は知らない。</summary>
        public int knownBlackMarketPeriod = -1;

        // 謎のプレーヤー探し
        public int mysteryClues;
        public List<int> clueSources = new List<int>();
        public bool phantomFound;

        // 漂流者
        public List<int> registeredCastaways = new List<int>();
    }
}
