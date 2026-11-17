using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    public enum FestivalKind
    {
        NewYearKickoff,  // 正月の初蹴り（元日〜3日）
        HanamiSoccer,    // 春の花見サッカー
        SummerFestival,  // 浜辺の夏祭り（煌浜）
        HarvestFestival, // 古鐘町の収穫祭
        CleanupVolunteer,// 台風一過の清掃ボランティア（勇心町）
        YearEndGala,     // 大晦日の表彰パーティー（天峰台）
    }

    /// <summary>年中行事1つ分。</summary>
    public class Festival
    {
        public FestivalKind kind;
        public string name;
        public string description;
        /// <summary>開催する街。</summary>
        public Town town;
        public IslandPlace place;
        public TimeOfDay time;
        /// <summary>島中から人が集まるか（false ならその街の人だけ）。</summary>
        public bool islandWide;
        /// <summary>その日の会話での好感度の上がりやすさ。</summary>
        public float affinityBonus = 1f;
        /// <summary>その日のファンの声のかけやすさ。</summary>
        public float fanBonus = 1f;
    }

    /// <summary>
    /// 島の年中行事。1年は365日（0日目＝元日）。
    /// お祭りの時間・場所には島民が集まり、主人公が参加するとご褒美がある。
    /// </summary>
    public class IslandCalendar
    {
        readonly int _seed;

        public IslandCalendar(int seed) { _seed = seed; }

        public static int DayOfYear(int day) => ((day % 365) + 365) % 365;

        public List<Festival> On(int day)
        {
            var list = new List<Festival>();
            int d = DayOfYear(day);
            if (d <= 2)
                list.Add(new Festival { kind = FestivalKind.NewYearKickoff, name = "正月の初蹴り", town = Town.Furugane, place = IslandPlace.ClubGround, time = TimeOfDay.Morning, islandWide = true,
                    description = "古鐘町のグラウンドで、島中の人が今年最初のボールを蹴る。", affinityBonus = 1.5f });
            if (d == 30)
                list.Add(new Festival { kind = FestivalKind.HanamiSoccer, name = "花見サッカー", town = Town.Yushin, place = IslandPlace.ClubGround, time = TimeOfDay.Afternoon, islandWide = true,
                    description = "勇心町の桜並木のグラウンドで、お花見をしながらのんびりサッカー。", affinityBonus = 2f });
            if (d == 150)
                list.Add(new Festival { kind = FestivalKind.SummerFestival, name = "浜辺の夏祭り", town = Town.Kirahama, place = IslandPlace.Beach, time = TimeOfDay.Night, islandWide = true,
                    description = "煌浜の浜辺に屋台と花火。浴衣姿の島民でごった返す。", affinityBonus = 1.5f, fanBonus = 2f });
            if (d == 220)
                list.Add(new Festival { kind = FestivalKind.HarvestFestival, name = "古鐘町の収穫祭", town = Town.Furugane, place = IslandPlace.Workplace, time = TimeOfDay.Afternoon, islandWide = false,
                    description = "古鐘町の商店街で、とれたての野菜と魚がふるまわれる。人情の街の一番の祭り。", affinityBonus = 1.5f });
            if (d == 364)
                list.Add(new Festival { kind = FestivalKind.YearEndGala, name = "大晦日の表彰パーティー", town = Town.Tenpou, place = IslandPlace.Workplace, time = TimeOfDay.Evening, islandWide = true,
                    description = "天峰台のホテルで、協会主催の年忘れパーティー。島の有名人が勢ぞろい。", fanBonus = 2f });
            // 台風一過：嵐の翌日、勇心町の浜辺で清掃ボランティア
            if (day > 0 && WeatherSystem.WeatherOf(_seed, day - 1) == Weather.Storm && WeatherSystem.WeatherOf(_seed, day) != Weather.Storm)
                list.Add(new Festival { kind = FestivalKind.CleanupVolunteer, name = "台風一過の清掃ボランティア", town = Town.Yushin, place = IslandPlace.Beach, time = TimeOfDay.Morning, islandWide = false,
                    description = "嵐で荒れた浜辺を、勇心町の人たちが総出で片付ける。人助け好きの街らしい光景。", affinityBonus = 2f });
            return list;
        }

        /// <summary>その時間・場所で開かれているお祭り（なければ null）。</summary>
        public Festival At(int day, TimeOfDay time, Town town, IslandPlace place)
        {
            foreach (var f in On(day)) if (f.time == time && f.town == town && f.place == place) return f;
            return null;
        }

        /// <summary>島民がお祭りに来るか（来るならその場所）。</summary>
        public Festival Attending(NPCData n, int day, TimeOfDay time)
        {
            if (n.isIncarcerated) return null;
            foreach (var f in On(day))
            {
                if (f.time != time || (!f.islandWide && n.hometown != f.town)) continue;
                if (f.kind == FestivalKind.YearEndGala && n.fame < 40) continue;         // 有名人だけ
                if (f.kind == FestivalKind.CleanupVolunteer && n.hometown != Town.Yushin) continue;
                unchecked
                {
                    uint h = (uint)(n.id * 2246822519u) ^ (uint)(day * 3266489917u) ^ (uint)f.kind;
                    h ^= h >> 15;
                    float r = (h % 1000) / 1000f;
                    float rate = n.personality == Personality.Extrovert ? 0.75f : n.personality == Personality.Introvert ? 0.4f : 0.2f;
                    if (f.kind == FestivalKind.CleanupVolunteer && n.soccerPersonality == SoccerPersonality.Devoted) rate = 0.9f;
                    if (r < rate) return f;
                }
            }
            return null;
        }
    }

    public class FestivalResult
    {
        public bool ok;
        public string text;
        public int peopleMet;
    }

    /// <summary>主人公のお祭りへの参加。</summary>
    public class FestivalSystem
    {
        readonly PlayerState _player;
        readonly IslandPopulation _pop;
        readonly IslandCalendar _calendar;
        readonly FameSystem _fame;
        readonly TownReputation _towns;
        readonly InventorySystem _inventory;
        readonly IslandEventsState _state;

        public IslandCalendar Calendar => _calendar;

        public FestivalSystem(PlayerState player, IslandPopulation pop, IslandCalendar calendar, FameSystem fame, TownReputation towns, InventorySystem inventory, IslandEventsState state)
        {
            _state = state;
            _player = player;
            _pop = pop;
            _calendar = calendar;
            _fame = fame;
            _towns = towns;
            _inventory = inventory;
        }

        /// <summary>今いる場所で開かれているお祭りに参加する。そこにいる島民全員と少し仲良くなる。</summary>
        public FestivalResult Join(Func<List<NPCData>> peopleHere)
        {
            int day = _pop.Database.currentDay;
            var f = _calendar.At(day, (TimeOfDay)_player.timeOfDay, _player.currentTown, (IslandPlace)_player.currentPlace);
            if (f == null) return new FestivalResult { text = "ここでは何もやっていない" };
            string key = day + ":" + f.kind;
            if (_state.festivalsJoined.Contains(key)) return new FestivalResult { text = "もう参加した" };
            _state.festivalsJoined.Add(key);
            if (_state.festivalsJoined.Count > 50) _state.festivalsJoined.RemoveAt(0);

            var people = peopleHere();
            foreach (var n in people)
            {
                var e = _pop.Knowledge.GetOrCreate(n.id);
                e.affinity = Math.Min(100, e.affinity + 3);
                _pop.Knowledge.Reveal(n.id, NPCInfoFlags.Name);
            }

            string text;
            switch (f.kind)
            {
                case FestivalKind.NewYearKickoff:
                    PlayerGrowth.AddExperience(_player, 20);
                    _fame.AddFame(1f);
                    text = "今年最初のボールを蹴った。島中の人と「今年もよろしく」と挨拶を交わした。";
                    break;
                case FestivalKind.HanamiSoccer:
                    _player.fatigue = Math.Max(0f, _player.fatigue - 30f);
                    text = "桜の下でのんびりボールを蹴った。心も体もほぐれた。";
                    break;
                case FestivalKind.SummerFestival:
                    _fame.AddFame(1.5f);
                    _towns.Change(Town.Kirahama, +4);
                    text = "屋台を回って、花火を見上げた。浴衣の島民たちに何度も声をかけられた。";
                    break;
                case FestivalKind.HarvestFestival:
                    _towns.Change(Town.Furugane, +6);
                    _inventory.Add("おにぎり", 3);
                    text = "とれたての野菜と魚をごちそうになった。おにぎりまで持たせてくれた。人情の街だ。";
                    break;
                case FestivalKind.CleanupVolunteer:
                    _towns.Change(Town.Yushin, +8);
                    _player.fatigue = Math.Min(100f, _player.fatigue + 15f);
                    text = "朝から浜辺のごみを拾った。勇心町の人たちに「あんた、いい人だね」と言われた。";
                    break;
                default:
                    _fame.AddFame(_player.fame >= 40f ? 3f : 0.5f);
                    text = _player.fame >= 40f ? "島の有名人たちと乾杯した。来年こそ表彰される側になりたい。" : "場違いな気がして、隅でジュースを飲んでいた……。";
                    break;
            }
            return new FestivalResult { ok = true, text = $"【{f.name}】{text}（{people.Count}人と交流）", peopleMet = people.Count };
        }
    }
}
