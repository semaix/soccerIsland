using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.World
{
    /// <summary>
    /// 試合予定を作る。出場メンバーは予定作成時に決めるので、NPCが「今度試合がある」と言えば必ずその試合に出る。
    ///
    /// 曜日（day % 7）: 0=月 … 4=金 5=土 6=日
    ///  月〜木 : 水曜に刑務所リーグ（A棟 vs B棟）
    ///  金(深夜): 裏フットサル（会場は毎回変わる）
    ///  土     : 職場対抗戦・ビーチサッカー
    ///  日     : 草サッカー（4週に1回は地域対抗試合）
    ///  60日ごと: 島代表戦
    /// </summary>
    public class MatchScheduler
    {
        static readonly string[] UndergroundVenues =
        {
            "廃墟の地下駐車場", "深夜のバー裏コート", "閉鎖された倉庫", "旧灯台の下", "潰れたボウリング場",
        };

        readonly NPCDatabase _db;
        readonly WorldState _state;
        readonly SeededRandom _rng;

        public MatchScheduler(NPCDatabase db, WorldState state, SeededRandom rng)
        {
            _db = db;
            _state = state;
            _rng = rng;
        }

        public void EnsureScheduledThrough(int lastDay)
        {
            for (int day = Math.Max(_state.scheduledThroughDay + 1, _db.currentDay); day <= lastDay; day++)
                ScheduleDay(day);
            if (lastDay > _state.scheduledThroughDay) _state.scheduledThroughDay = lastDay;
        }

        void ScheduleDay(int day)
        {
            var busy = new HashSet<int>();
            int weekday = day % 7;

            if (day % 60 == 59) ScheduleIslandSelect(day, busy);

            switch (weekday)
            {
                case 2:
                    foreach (var c in ClubsOf(ClubCategory.Prison)) SchedulePrison(day, c, busy);
                    break;
                case 4:
                    ScheduleClubPairs(day, ClubsOf(ClubCategory.Underground), MatchCategory.UndergroundFutsal, busy);
                    break;
                case 5:
                    ScheduleClubPairs(day, ClubsOf(ClubCategory.Workplace), MatchCategory.Workplace, busy);
                    ScheduleBeach(day, busy);
                    break;
                case 6:
                    if (day % 28 == 27) ScheduleDerby(day, busy);
                    ScheduleClubPairs(day, ClubsOf(ClubCategory.Town), MatchCategory.Kusa, busy);
                    break;
            }
        }

        List<SoccerClub> ClubsOf(ClubCategory category)
        {
            var list = new List<SoccerClub>();
            foreach (var c in _db.clubs) if (c.category == category) list.Add(c);
            return list;
        }

        void ScheduleClubPairs(int day, List<SoccerClub> clubs, MatchCategory category, HashSet<int> busy)
        {
            _rng.Shuffle(clubs);
            for (int i = 0; i + 1 < clubs.Count; i += 2)
                TryAddClubMatch(day, category, clubs[i], clubs[i + 1], busy);
        }

        void ScheduleBeach(int day, HashSet<int> busy)
        {
            var beach = ClubsOf(ClubCategory.Beach);
            if (beach.Count == 0) return;
            var opponents = ClubsOf(ClubCategory.Town).FindAll(c => c.town == Town.Kirahama);
            if (opponents.Count == 0) return;
            TryAddClubMatch(day, MatchCategory.Beach, beach[0], _rng.Pick(opponents), busy, "煌浜ビーチ");
        }

        void TryAddClubMatch(int day, MatchCategory category, SoccerClub home, SoccerClub away, HashSet<int> busy, string venue = null)
        {
            int size = MatchLabels.TeamSize(category);
            var homeLineup = PickLineup(_db.GetClubMembers(home.id), size, busy, category);
            var awayLineup = PickLineup(_db.GetClubMembers(away.id), size, busy, category);
            if (homeLineup.Count < 5 || awayLineup.Count < 5) return;

            Add(new Match
            {
                day = day, category = category,
                homeClubId = home.id, awayClubId = away.id,
                homeName = home.name, awayName = away.name,
                venue = venue ?? VenueFor(category, home.town),
                homeLineup = homeLineup, awayLineup = awayLineup,
            }, busy);
        }

        void SchedulePrison(int day, SoccerClub prison, HashSet<int> busy)
        {
            var members = new List<NPCData>(_db.GetClubMembers(prison.id));
            members.RemoveAll(n => !n.isIncarcerated);
            if (members.Count < 10) return;
            _rng.Shuffle(members);
            int half = members.Count / 2;
            var a = PickLineup(members.GetRange(0, half), 11, busy, MatchCategory.PrisonLeague);
            var b = PickLineup(members.GetRange(half, members.Count - half), 11, busy, MatchCategory.PrisonLeague);
            Add(new Match
            {
                day = day, category = MatchCategory.PrisonLeague,
                homeClubId = prison.id, awayClubId = prison.id,
                homeName = "A棟", awayName = "B棟", venue = "刑務所 運動場",
                homeLineup = a, awayLineup = b,
            }, busy);
        }

        void ScheduleDerby(int day, HashSet<int> busy)
        {
            var towns = (Town[])Enum.GetValues(typeof(Town));
            var list = new List<Town>(towns);
            _rng.Shuffle(list);
            for (int i = 0; i + 1 < list.Count; i += 2)
            {
                var home = SelectTeam(_db.GetByTown(list[i]), 11, busy);
                var away = SelectTeam(_db.GetByTown(list[i + 1]), 11, busy);
                if (home.Count < 11 || away.Count < 11) continue;
                Add(new Match
                {
                    day = day, category = MatchCategory.RegionalDerby,
                    homeName = NPCLabels.Of(list[i]) + "選抜", awayName = NPCLabels.Of(list[i + 1]) + "選抜",
                    venue = NPCLabels.Of(list[i]) + "スタジアム",
                    homeLineup = home, awayLineup = away,
                }, busy);
            }
        }

        void ScheduleIslandSelect(int day, HashSet<int> busy)
        {
            var lineup = new List<int>();
            foreach (var n in _db.GetOfficialMarketValueRanking(40))
            {
                if (lineup.Count >= 11) break;
                if (n.age >= 15 && !busy.Contains(n.id)) lineup.Add(n.id);
            }
            if (lineup.Count < 11) return;
            Add(new Match
            {
                day = day, category = MatchCategory.IslandSelect,
                homeName = "島代表", awayName = "本土選抜", venue = "島中央スタジアム",
                homeLineup = lineup,
            }, busy);
        }

        /// <summary>地域対抗・島代表用：街の中から表の実力上位を選ぶ（裏の人間・服役者は除く）。</summary>
        List<int> SelectTeam(IReadOnlyList<NPCData> pool, int size, HashSet<int> busy)
        {
            var candidates = new List<NPCData>();
            foreach (var n in pool)
                if (n.AppearsInOfficialRanking && n.clubId >= 0 && n.age >= 15 && n.age <= 50 && !busy.Contains(n.id)) candidates.Add(n);
            candidates.Sort((a, b) => b.PublicOverall.CompareTo(a.PublicOverall));
            var result = new List<int>();
            for (int i = 0; i < candidates.Count && result.Count < size; i++) result.Add(candidates[i].id);
            return result;
        }

        List<int> PickLineup(IReadOnlyList<NPCData> members, int size, HashSet<int> busy, MatchCategory category)
        {
            var scored = new List<KeyValuePair<NPCData, float>>();
            foreach (var n in members)
            {
                if (busy.Contains(n.id) || n.age < 12 || n.age > 72) continue;
                if (n.isIncarcerated != (category == MatchCategory.PrisonLeague)) continue;
                // 監督の目は節穴：表の実力＋気分で選ぶ。裏の試合では本気の実力で選ばれる
                float strength = category == MatchCategory.UndergroundFutsal ? n.Overall : n.PublicOverall;
                scored.Add(new KeyValuePair<NPCData, float>(n, strength + _rng.Gaussian(0f, 8f)));
            }
            scored.Sort((a, b) => b.Value.CompareTo(a.Value));
            var lineup = new List<int>();
            for (int i = 0; i < scored.Count && lineup.Count < size; i++) lineup.Add(scored[i].Key.id);
            return lineup;
        }

        string VenueFor(MatchCategory category, Town town)
        {
            switch (category)
            {
                case MatchCategory.UndergroundFutsal: return _rng.Pick(UndergroundVenues);
                case MatchCategory.Beach: return "煌浜ビーチ";
                case MatchCategory.Workplace: return NPCLabels.Of(town) + "市民グラウンド";
                default: return NPCLabels.Of(town) + (_rng.Chance(0.5f) ? "河川敷" : "グラウンド");
            }
        }

        void Add(Match m, HashSet<int> busy)
        {
            m.id = _state.nextMatchId++;
            _state.matches.Add(m);
            foreach (var id in m.homeLineup) busy.Add(id);
            foreach (var id in m.awayLineup) busy.Add(id);
        }
    }
}
