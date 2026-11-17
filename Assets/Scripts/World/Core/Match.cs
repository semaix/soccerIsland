using System;
using System.Collections.Generic;

namespace SoccerIsland.World
{
    public enum MatchCategory
    {
        Kusa,              // 草サッカー
        RegionalDerby,     // 地域対抗試合
        Workplace,         // 職場対抗
        Beach,             // ビーチサッカー
        PrisonLeague,      // 刑務所リーグ
        UndergroundFutsal, // 裏フットサル（深夜）
        IslandSelect,      // 島代表
    }

    /// <summary>予定された試合。出場メンバーは予定作成時に確定する（会話と矛盾させないため）。</summary>
    [Serializable]
    public class Match
    {
        public int id;
        public int day;
        public MatchCategory category;
        /// <summary>-1 のときは homeName を使う（島代表など）。</summary>
        public int homeClubId = -1;
        public int awayClubId = -1;
        public string homeName;
        public string awayName;
        public string venue;
        public List<int> homeLineup = new List<int>();
        public List<int> awayLineup = new List<int>();

        public bool played;
        public int homeScore;
        public int awayScore;
        /// <summary>得点者（NPC ID。-1 は主人公）。ゴールの数だけ並ぶ。</summary>
        public List<int> homeScorers = new List<int>();
        public List<int> awayScorers = new List<int>();

        public bool IsUnderground => category == MatchCategory.UndergroundFutsal;
        public bool Involves(int npcId) => homeLineup.Contains(npcId) || awayLineup.Contains(npcId);
        public bool IsHome(int npcId) => homeLineup.Contains(npcId);
        public string OpponentOf(int npcId) => IsHome(npcId) ? awayName : homeName;
    }

    public static class MatchLabels
    {
        public static string Of(MatchCategory c)
        {
            switch (c)
            {
                case MatchCategory.Kusa: return "草サッカー";
                case MatchCategory.RegionalDerby: return "地域対抗試合";
                case MatchCategory.Workplace: return "職場対抗戦";
                case MatchCategory.Beach: return "ビーチサッカー";
                case MatchCategory.PrisonLeague: return "刑務所リーグ";
                case MatchCategory.UndergroundFutsal: return "裏フットサル";
                case MatchCategory.IslandSelect: return "島代表戦";
                default: return c.ToString();
            }
        }

        public static int TeamSize(MatchCategory c) =>
            c == MatchCategory.Beach || c == MatchCategory.UndergroundFutsal ? 5 : 11;
    }
}
