using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    public enum TravelEventKind
    {
        FanEncounter,   // ファンに声をかけられた（サイン・写真）
        Acquaintance,   // 知り合いにばったり
        YankeeTrouble,  // ヤンキーに絡まれた
        FoundMoney,     // お金を拾った
        StreetFootball, // 路地裏で子供たちとサッカー
        Downpour,       // にわか雨
    }

    public class TravelEvent
    {
        public TravelEventKind kind;
        public Town town;
        public string text;
        public int npcId = -1;
        /// <summary>ファンのお願い（FanEncounter のとき。FanInteractionSystem.Respond で応える）。</summary>
        public FanRequest fanRequest;
        public long money;
    }

    /// <summary>
    /// 街から街へ移動する途中の出来事。1時間帯の移動ごとに起きることがある。
    /// ・有名になるとファンに声をかけられる
    /// ・古鐘町やライバルの街を夕方・深夜に通るとヤンキーに絡まれる（強いか有名なら逆にファンになる）
    /// ・昼間の路地裏では子供たちがサッカーをしている
    /// </summary>
    public class TravelEvents
    {
        public float chancePerLeg = 0.45f;

        readonly PlayerState _player;
        readonly IslandPopulation _pop;
        readonly TownReputation _towns;
        readonly SeededRandom _rng;

        public TravelEvents(PlayerState player, IslandPopulation pop, TownReputation towns, SeededRandom rng)
        {
            _player = player;
            _pop = pop;
            _towns = towns;
            _rng = rng;
        }

        public TravelEvent Roll(Town through, TimeOfDay time, Weather weather)
        {
            if (!_rng.Chance(chancePerLeg)) return null;
            int day = _pop.Database.currentDay;

            if (WeatherSystem.IsBad(weather) && _rng.Chance(0.35f))
            {
                _player.fatigue = Math.Min(100f, _player.fatigue + 5f);
                return new TravelEvent { kind = TravelEventKind.Downpour, town = through, text = weather == Weather.Snow ? "吹雪の中を歩いて、体が冷えきった……" : "にわか雨でびしょ濡れになった……" };
            }

            bool dark = time == TimeOfDay.Evening || time == TimeOfDay.Night;
            bool rough = through == Town.Furugane || TownRivalry.AreRivals(_player.homeTown, through);
            var w = new[]
            {
                _player.fame >= 15f ? _player.fame / 10f : 0f,             // ファン
                2f,                                                          // 知り合い
                dark && rough ? 3f : rough ? 0.8f : 0.3f,                    // ヤンキー
                1f,                                                          // お金を拾う
                !dark && !WeatherSystem.IsBad(weather) ? 2f : 0f,            // 路地裏サッカー
            };
            switch (_rng.WeightedIndex(w))
            {
                case 0: return Fan(through);
                case 1: return Acquaintance(through) ?? FoundMoney(through);
                case 2: return Yankees(through, day);
                case 3: return FoundMoney(through);
                default: return StreetFootball(through);
            }
        }

        TravelEvent Fan(Town town)
        {
            var people = _pop.Database.GetByTown(town);
            NPCData fan = null;
            for (int i = 0; i < 10 && fan == null; i++)
            {
                var n = people[_rng.Range(0, people.Count)];
                if (!n.isIncarcerated && n.fame < _player.fame) fan = n;
            }
            if (fan == null) return FoundMoney(town);
            var type = _player.fame >= 40f && _rng.Chance(0.5f) ? FanRequestType.Autograph : FanRequestType.Photo;
            var req = new FanRequest
            {
                npcId = fan.id, type = type,
                line = type == FanRequestType.Autograph ? $"あっ！ {_player.name}さんだ！ サインください！" : $"{_player.name}さん!? 一緒に写真撮ってもらえませんか！",
            };
            return new TravelEvent { kind = TravelEventKind.FanEncounter, town = town, npcId = fan.id, fanRequest = req, text = $"{NPCLabels.Of(town)}の道で、{NPCLabels.Of(fan.occupation)}の人に声をかけられた。「{req.line}」" };
        }

        TravelEvent Acquaintance(Town town)
        {
            var known = new List<NPCKnowledgeEntry>();
            foreach (var e in _pop.Knowledge.entries)
            {
                if (e.affinity < 20 || !e.Knows(NPCInfoFlags.Name)) continue;
                var n = _pop.Database.Get(e.npcId);
                if (n != null && n.hometown == town && !n.isIncarcerated) known.Add(e);
            }
            if (known.Count == 0) return null;
            var entry = known[_rng.Range(0, known.Count)];
            var who = _pop.Database.Get(entry.npcId);
            entry.affinity = Math.Min(100, entry.affinity + 2);
            string line = who.personality == Personality.Extrovert ? "おっ、こんなところで！ 今度一緒にボール蹴ろうぜ！"
                        : who.personality == Personality.Introvert ? "……あ、こんにちは。偶然ですね。"
                        : "……なんでここに。";
            return new TravelEvent { kind = TravelEventKind.Acquaintance, town = town, npcId = who.id, text = $"道で{who.FullName}にばったり会った。「{line}」" };
        }

        TravelEvent Yankees(Town town, int day)
        {
            bool tough = _player.abilities.physical >= 65 || _player.fame >= 50f;
            if (tough)
            {
                _towns.Change(town, +2);
                return new TravelEvent { kind = TravelEventKind.YankeeTrouble, town = town,
                    text = _player.fame >= 50f ? "ヤンキーに絡まれた……と思ったら「あの、ファンです！」と握手を求められた。"
                                               : "ヤンキーに絡まれたが、にらみ返したら「兄貴！」と呼ばれるようになった。" };
            }
            long lost = _player.ForceSpend(_rng.Range(5000, 20001), "ヤンキーにカツアゲされた", day);
            if (lost == 0) _player.fatigue = Math.Min(100f, _player.fatigue + 10f);
            return new TravelEvent { kind = TravelEventKind.YankeeTrouble, town = town, money = -lost,
                text = lost > 0 ? $"ヤンキーに絡まれて、{NPCLabels.Yen(lost)}取られた……。もっと強くならないと。" : "ヤンキーに追いかけ回されて、へとへとになった……。" };
        }

        TravelEvent FoundMoney(Town town)
        {
            long money = _rng.Range(1, 11) * 1000;
            _player.Earn(money, "道で拾った", _pop.Database.currentDay);
            return new TravelEvent { kind = TravelEventKind.FoundMoney, town = town, money = money, text = $"道端に{NPCLabels.Yen(money)}落ちていた。……ありがたくいただこう。" };
        }

        TravelEvent StreetFootball(Town town)
        {
            PlayerGrowth.AddExperience(_player, 8);
            _towns.Change(town, +1);
            return new TravelEvent { kind = TravelEventKind.StreetFootball, town = town,
                text = $"{NPCLabels.Of(town)}の路地裏で、子供たちがボールを蹴っていた。混ざって遊んだら「すげえ、うまい！」と大喜び。" };
        }
    }
}
