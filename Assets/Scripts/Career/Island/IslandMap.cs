using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    /// <summary>島の上の位置（街＋場所）。</summary>
    [Serializable]
    public struct IslandLocation
    {
        public Town town;
        public IslandPlace place;

        public IslandLocation(Town town, IslandPlace place) { this.town = town; this.place = place; }

        public bool Same(IslandLocation o) => town == o.town && place == o.place;

        public override string ToString() => $"{NPCLabels.Of(town)}・{IslandMap.PlaceLabel(place)}";
    }

    /// <summary>
    /// 島のマップ。4つの街は島をぐるりと囲むように並んでいる（勇心町—古鐘町—煌浜—天峰台—勇心町）。
    /// 隣の街へは1時間帯、反対側の街へは2時間帯かかる。街の中の移動は時間がかからない。
    /// </summary>
    public static class IslandMap
    {
        static readonly Town[] Ring = { Town.Yushin, Town.Furugane, Town.Kirahama, Town.Tenpou };

        /// <summary>移動にかかる時間帯の数。</summary>
        public static int Distance(Town a, Town b)
        {
            int ia = Array.IndexOf(Ring, a), ib = Array.IndexOf(Ring, b);
            int d = Math.Abs(ia - ib);
            return Math.Min(d, Ring.Length - d);
        }

        /// <summary>その街にある場所。</summary>
        public static List<IslandPlace> PlacesIn(Town town, Town playerHome)
        {
            var list = new List<IslandPlace> { IslandPlace.Workplace, IslandPlace.ClubGround, IslandPlace.Beach };
            if (town == playerHome) list.Insert(0, IslandPlace.Home);
            if (town == Town.Kirahama) list.Add(IslandPlace.Bar);
            if (town == Town.Yushin) list.Add(IslandPlace.Hospital);
            if (town == Town.Furugane) list.Add(IslandPlace.Prison);
            return list;
        }

        public static bool Exists(Town town, IslandPlace place, Town playerHome) =>
            place == IslandPlace.BlackMarket || PlacesIn(town, playerHome).Contains(place);

        public static string PlaceLabel(IslandPlace p)
        {
            switch (p)
            {
                case IslandPlace.Home: return "自宅";
                case IslandPlace.Workplace: return "街なか（職場・お店）";
                case IslandPlace.ClubGround: return "グラウンド";
                case IslandPlace.Bar: return "飲み屋街";
                case IslandPlace.Beach: return "浜辺";
                case IslandPlace.Hospital: return "勇心病院";
                case IslandPlace.Prison: return "島刑務所（面会）";
                default: return "裏市場";
            }
        }

        /// <summary>裏市場の開催場所（BlackMarket.Locations の順）がある街。</summary>
        public static readonly Town[] BlackMarketTowns = { Town.Kirahama, Town.Tenpou, Town.Furugane, Town.Furugane, Town.Kirahama, Town.Yushin };
    }
}
