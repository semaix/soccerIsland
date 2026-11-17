using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Career
{
    public enum FanRequestType
    {
        Photo,     // 写真を撮って
        Autograph, // サインして
        Handshake, // 握手して
    }

    public class FanRequest
    {
        public int npcId;
        public FanRequestType type;
        public string line;
    }

    public enum FanResponseResult
    {
        Accepted,
        Refused,
        NoSignature, // サインをまだ作っていない
    }

    /// <summary>
    /// 主人公が有名になると、島民からサイン・写真・握手を求められる。
    /// 応じると好感度・知名度が上がり、断ると「塩対応」と噂される。
    /// </summary>
    public class FanInteractionSystem
    {
        public float minimumFame = 15f;
        public float maxRequestChance = 0.6f;
        public float badRumorChance = 0.3f;
        /// <summary>街ごとの評判による倍率（IslandCareer が差し込む）。</summary>
        public Func<Town, float> townMultiplier;

        readonly PlayerState _player;
        readonly NPCKnowledgeBook _book;
        readonly FameSystem _fame;
        readonly SeededRandom _rng;
        readonly List<FameEvent> _events;
        readonly Func<int> _today;
        /// <summary>同じ人に1日に何度も頼まれないように。npcId → 最後に頼まれた日</summary>
        readonly Dictionary<int, int> _lastAsked = new Dictionary<int, int>();

        public FanInteractionSystem(PlayerState player, NPCKnowledgeBook book, FameSystem fame, SeededRandom rng, List<FameEvent> events, Func<int> today)
        {
            _player = player;
            _book = book;
            _fame = fame;
            _rng = rng;
            _events = events;
            _today = today;
        }

        public float RequestChance(NPCData npc)
        {
            if (_player.fame < minimumFame || npc.isIncarcerated) return 0f;
            float c = (_player.fame - minimumFame) / (100f - minimumFame) * maxRequestChance;
            switch (npc.personality)
            {
                case Personality.Extrovert: c *= 1.4f; break;
                case Personality.Introvert: c *= 0.7f; break;
                case Personality.Gloomy: c *= 0.4f; break;
            }
            if (npc.soccerPersonality == SoccerPersonality.MoodMaker) c *= 1.3f;
            if (npc.fame > _player.fame) c *= 0.3f; // 自分より有名な人はねだらない
            if (npc.age < 18) c *= 1.5f;             // 子供はファンになりやすい
            c *= 1f + InventorySystem.FanBonus(_player); // おしゃれだと声をかけられやすい
            if (townMultiplier != null) c *= townMultiplier(npc.hometown);
            return Math.Min(0.9f, c);
        }

        /// <summary>近くを通った・話しかけたときに呼ぶ。お願いされなければ null。</summary>
        public FanRequest Roll(NPCData npc)
        {
            int day = _today();
            if (_lastAsked.TryGetValue(npc.id, out var last) && last == day) return null;
            if (!_rng.Chance(RequestChance(npc))) return null;
            _lastAsked[npc.id] = day;

            FanRequestType type;
            float r = _rng.Value;
            if (_player.fame >= 40f && r < 0.45f) type = FanRequestType.Autograph;
            else if (_player.fame >= 70f && r < 0.6f) type = FanRequestType.Handshake;
            else type = FanRequestType.Photo;

            string line;
            switch (type)
            {
                case FanRequestType.Autograph: line = $"あ、あの！ {_player.name}さんですよね!? サインください！"; break;
                case FanRequestType.Handshake: line = $"{_player.name}さん！ 握手してもらえませんか！"; break;
                default: line = npc.personality == Personality.Introvert ? "……あの、一緒に写真、いいですか……？" : "写真撮ってもいいですか!? 一緒に！"; break;
            }
            return new FanRequest { npcId = npc.id, type = type, line = line };
        }

        public FanResponseResult Respond(FanRequest request, bool accept)
        {
            int day = _today();
            var entry = _book.GetOrCreate(request.npcId);
            if (accept)
            {
                if (request.type == FanRequestType.Autograph && !_player.signature.IsValid) return FanResponseResult.NoSignature;
                entry.affinity = Math.Min(100, entry.affinity + 5);
                _fame.AddFame(0.15f);
                if (_rng.Chance(0.1f))
                    _events.Add(new FameEvent { type = FameEventType.FanServiceGood, day = day, npcId = request.npcId, fameDelta = 0.15f });
                return FanResponseResult.Accepted;
            }

            entry.affinity = Math.Max(0, entry.affinity - 5);
            if (_rng.Chance(badRumorChance))
            {
                float d = _fame.AddFame(-0.5f);
                _events.Add(new FameEvent { type = FameEventType.FanServiceBad, day = day, npcId = request.npcId, fameDelta = d });
            }
            return FanResponseResult.Refused;
        }
    }
}
