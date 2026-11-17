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

        // 街ごとの主人公の評判（Town の順）
        public List<int> townReputation = new List<int>();

        // 参加したお祭り（"日:種類"）
        public List<string> festivalsJoined = new List<string>();

        // 漂流者
        public List<int> registeredCastaways = new List<int>();
    }
}
