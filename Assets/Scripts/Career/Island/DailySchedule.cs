using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    public enum TimeOfDay
    {
        Morning,   // 朝
        Afternoon, // 昼
        Evening,   // 夕方
        Night,     // 深夜
    }

    public enum IslandPlace
    {
        Home,        // 家
        Workplace,   // 職場・学校
        ClubGround,  // クラブのグラウンド（練習・試合）
        Bar,         // 飲み屋街
        Beach,       // 浜辺
        Hospital,    // 病院（勇心町）
        Prison,      // 刑務所
        BlackMarket, // 裏市場（深夜だけ）
    }

    /// <summary>
    /// 島民の1日。時間帯ごとにどこにいるかが決まる（同じ日・同じ時間なら毎回同じ場所）。
    /// ・朝と昼は職場（学生は学校）。試合の日の昼はグラウンド
    /// ・夕方は努力家ならグラウンドで練習、外交的な大人は飲み屋街
    /// ・深夜、裏の人間は裏市場へ。謎のプレーヤーは誰もいないグラウンドでボールを蹴っている
    /// </summary>
    public class DailySchedule
    {
        readonly NPCDatabase _db;
        readonly WorldState _world;
        /// <summary>年中行事（お祭りの日は島民が集まる）。</summary>
        public IslandCalendar calendar;

        public DailySchedule(NPCDatabase db, WorldState world)
        {
            _db = db;
            _world = world;
        }

        static float Roll(int npcId, int day, TimeOfDay t)
        {
            unchecked
            {
                uint h = (uint)(npcId * 73856093) ^ (uint)(day * 19349663) ^ (uint)((int)t * 83492791);
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return (h % 10000) / 10000f;
            }
        }

        public IslandPlace WhereIs(NPCData n, int day, TimeOfDay t)
        {
            if (n.isIncarcerated) return IslandPlace.Prison;
            float r = Roll(n.id, day, t);
            if (n.IsInjured(day)) return t == TimeOfDay.Afternoon ? IslandPlace.Hospital : IslandPlace.Home;
            bool matchToday = _world.NextMatchOf(n.id, day, 0) != null;
            var weather = WeatherSystem.WeatherOf(_db.seed, day);
            bool bad = WeatherSystem.IsBad(weather);
            if (weather == Weather.Storm && t != TimeOfDay.Night && n.occupation != Occupation.Doctor && n.occupation != Occupation.Nurse &&
                n.occupation != Occupation.Police && n.occupation != Occupation.Firefighter) return IslandPlace.Home; // 嵐の日はみんな家にこもる

            switch (t)
            {
                case TimeOfDay.Morning:
                    if (n.occupation == Occupation.Retired) return r < 0.5f && !bad ? IslandPlace.Beach : IslandPlace.Home;
                    if (n.occupation == Occupation.Unemployed || n.occupation == Occupation.Castaway) return r < 0.3f && !bad ? IslandPlace.Beach : IslandPlace.Home;
                    if (n.occupation == Occupation.Bartender) return IslandPlace.Home; // 夜型
                    return IslandPlace.Workplace;

                case TimeOfDay.Afternoon:
                    if (matchToday) return IslandPlace.ClubGround;
                    if (n.occupation == Occupation.Retired || n.occupation == Occupation.Unemployed) return r < 0.4f ? IslandPlace.ClubGround : IslandPlace.Home;
                    if (n.occupation == Occupation.Castaway) return IslandPlace.Beach;
                    return IslandPlace.Workplace;

                case TimeOfDay.Evening:
                    if (n.occupation == Occupation.Bartender) return IslandPlace.Bar;
                    // 雨の日にグラウンドで練習するのは、よほどの努力家だけ
                    if (n.effort >= (bad ? 80 : 60) && r < 0.4f + n.effort / 250f) return IslandPlace.ClubGround;
                    if (n.age >= 20 && n.personality == Personality.Extrovert && r < (bad ? 0.75f : 0.6f)) return IslandPlace.Bar;
                    if (n.personality == Personality.Gloomy && r < 0.3f && !bad) return IslandPlace.Beach;
                    return IslandPlace.Home;

                default:
                    if (n.soccerPersonality == SoccerPersonality.Phantom && r < 0.7f) return IslandPlace.ClubGround;
                    if (n.isUnderground && r < 0.5f) return IslandPlace.BlackMarket;
                    if (n.occupation == Occupation.Bartender) return IslandPlace.Bar;
                    if (n.age >= 20 && n.personality == Personality.Extrovert && r < 0.35f) return IslandPlace.Bar;
                    return IslandPlace.Home;
            }
        }

        /// <summary>島民が今いる街と場所。職場は職業で街が決まる（オフィス街は天峰台、病院は勇心町…）。</summary>
        public IslandLocation Locate(NPCData n, int day, TimeOfDay t, int blackMarketTown = -1)
        {
            var fest = calendar?.Attending(n, day, t);
            if (fest != null && WhereIs(n, day, t) != IslandPlace.Hospital) return new IslandLocation(fest.town, fest.place);
            var place = WhereIs(n, day, t);
            Town town = n.hometown;
            switch (place)
            {
                case IslandPlace.Bar: town = Town.Kirahama; break;
                case IslandPlace.Hospital: town = Town.Yushin; break;
                case IslandPlace.Prison: town = Town.Furugane; break;
                case IslandPlace.BlackMarket: if (blackMarketTown >= 0) town = (Town)blackMarketTown; break;
                case IslandPlace.ClubGround:
                    var club = _db.GetClub(n.clubId);
                    if (club != null) town = club.town;
                    break;
                case IslandPlace.Workplace: town = WorkTown(n); break;
            }
            return new IslandLocation(town, place);
        }

        public static Town WorkTown(NPCData n)
        {
            switch (n.occupation)
            {
                case Occupation.Doctor:
                case Occupation.Nurse:
                case Occupation.Firefighter: return Town.Yushin;
                case Occupation.Salaryman:
                case Occupation.Banker:
                case Occupation.Reporter: return Town.Tenpou;
                case Occupation.Idol:
                case Occupation.Lifeguard:
                case Occupation.Bartender: return Town.Kirahama;
                case Occupation.Mafia: return Town.Furugane;
                default: return n.hometown;
            }
        }

        /// <summary>その場所の具体的な名前（職場は職業で変わる）。</summary>
        public static string PlaceName(NPCData n, IslandPlace p)
        {
            string town = NPCLabels.Of(n.hometown);
            switch (p)
            {
                case IslandPlace.Home: return $"{town}の自宅";
                case IslandPlace.ClubGround: return n.soccerPersonality == SoccerPersonality.Phantom ? "誰もいない夜のグラウンド" : $"{town}のグラウンド";
                case IslandPlace.Bar: return "煌浜の飲み屋街";
                case IslandPlace.Beach: return $"{town}の浜辺";
                case IslandPlace.Hospital: return "勇心病院";
                case IslandPlace.Prison: return "島刑務所";
                case IslandPlace.BlackMarket: return "裏市場";
            }
            switch (n.occupation)
            {
                case Occupation.Student: return $"{town}の学校";
                case Occupation.Doctor:
                case Occupation.Nurse: return "勇心病院";
                case Occupation.Police: return $"{town}の交番";
                case Occupation.Firefighter: return "勇心消防署";
                case Occupation.Fisherman: return $"{town}の港";
                case Occupation.Farmer: return $"{town}の畑";
                case Occupation.Construction: return "工事現場";
                case Occupation.Salaryman:
                case Occupation.Banker: return "天峰台のオフィス街";
                case Occupation.Reporter: return "サッカー新聞社";
                case Occupation.Lifeguard: return "煌浜ビーチ";
                case Occupation.Mafia: return "黒潮會の事務所";
                case Occupation.Idol: return "煌浜の劇場";
                default: return $"{town}の店";
            }
        }

        /// <summary>今その場所にいる島民（街を指定すると、その街の人だけ）。</summary>
        public List<NPCData> PeopleAt(IslandPlace place, int day, TimeOfDay t, Town? town = null)
        {
            var list = new List<NPCData>();
            foreach (var n in _db.npcs)
            {
                if (town.HasValue && n.hometown != town.Value && place != IslandPlace.Bar && place != IslandPlace.Hospital && place != IslandPlace.BlackMarket) continue;
                if (WhereIs(n, day, t) == place) list.Add(n);
            }
            return list;
        }

        public static string Label(TimeOfDay t) => t == TimeOfDay.Morning ? "朝" : t == TimeOfDay.Afternoon ? "昼" : t == TimeOfDay.Evening ? "夕方" : "深夜";
    }
}
