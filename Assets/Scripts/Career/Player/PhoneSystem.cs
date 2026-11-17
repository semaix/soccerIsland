using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    public enum CallPurpose
    {
        Chat,           // 普通に話す（会話と同じ扱い）
        InviteTraining, // 練習に誘う
        AskAbout,       // ほかの人について聞く
    }

    public enum IncomingCallKind
    {
        MatchInvite, // 「助っ人に来てくれ！」
        Gossip,      // 噂話
        LoanShark,   // 闇金の取り立て
        Thanks,      // サイン・写真のお礼など
    }

    [Serializable]
    public class CallRecord
    {
        public int day;
        public int npcId;
        public bool outgoing;
        public bool answered;
        public string summary;
    }

    [Serializable]
    public class PhoneState
    {
        public List<CallRecord> log = new List<CallRecord>();
        public const int MaxLog = 100;

        public bool CalledToday(int npcId, int day)
        {
            for (int i = log.Count - 1; i >= 0 && log[i].day == day; i--)
                if (log[i].npcId == npcId && log[i].outgoing) return true;
            return false;
        }
    }

    public class CallResult
    {
        public bool answered;
        public string line;
        public ConversationResult conversation;
        public bool accepted;
        /// <summary>AskAbout で新しくわかった関係。</summary>
        public KnownRelation? learnedRelation;
    }

    public class IncomingCall
    {
        public int callerId;
        public IncomingCallKind kind;
        public string text;
        /// <summary>MatchInvite の試合ID。</summary>
        public int matchId = -1;
        /// <summary>MatchInvite で入るのがホーム側か。</summary>
        public bool joinHome;
    }

    /// <summary>
    /// 電話。番号を知っている島民に直接かけられる。仲良くなった島民からはかかってくる。
    /// 番号はすべて島内専用の架空の番号で、端末の電話機能・連絡先は使わない。
    /// </summary>
    public class PhoneSystem
    {
        public int incomingAffinity = 50;
        public float incomingChancePerFriend = 0.04f;
        public int maxIncomingPerDay = 2;

        readonly PlayerState _player;
        readonly IslandPopulation _pop;
        readonly WorldState _world;
        readonly EconomyState _economy;
        readonly PhoneState _state;
        readonly SeededRandom _rng;

        public PhoneState State => _state;

        public PhoneSystem(PlayerState player, IslandPopulation pop, WorldState world, EconomyState economy, PhoneState state, SeededRandom rng)
        {
            _player = player;
            _pop = pop;
            _world = world;
            _economy = economy;
            _state = state;
            _rng = rng;
        }

        public List<int> PhoneBook() => _pop.Knowledge.PhoneBook();

        public bool CanCall(int npcId)
        {
            var n = _pop.Database.Get(npcId);
            return n != null && !string.IsNullOrEmpty(n.phoneNumber) && _pop.Knowledge.Knows(npcId, NPCInfoFlags.PhoneNumber) &&
                   !_state.CalledToday(npcId, _pop.Database.currentDay);
        }

        public float AnswerChance(NPCData n)
        {
            if (n.isIncarcerated || string.IsNullOrEmpty(n.phoneNumber)) return 0f;
            float c = n.personality == Personality.Extrovert ? 0.9f : n.personality == Personality.Introvert ? 0.65f : 0.4f;
            if (n.isUnderground) c *= 0.6f;
            if (n.soccerPersonality == SoccerPersonality.Phantom) c = 0.05f;
            var e = _pop.Knowledge.Get(n.id);
            if (e != null) c += e.affinity / 400f;
            return Math.Min(0.98f, c);
        }

        /// <summary>電話をかける。</summary>
        public CallResult Call(int npcId, CallPurpose purpose, int aboutNpcId = -1)
        {
            var r = new CallResult();
            var n = _pop.Database.Get(npcId);
            int day = _pop.Database.currentDay;
            if (n == null || !CanCall(npcId)) { r.line = "電話をかけられない"; return r; }

            r.answered = _rng.Chance(AnswerChance(n));
            if (!r.answered)
            {
                r.line = n.personality == Personality.Gloomy ? "……（出ない）" : "留守番電話に切り替わった。";
                Log(day, npcId, true, false, "不在");
                return r;
            }

            switch (purpose)
            {
                case CallPurpose.Chat:
                    r.conversation = _pop.Talk(npcId);
                    r.line = r.conversation.lines.Count > 0 ? r.conversation.lines[0] : "もしもし？";
                    break;
                case CallPurpose.InviteTraining:
                    var e = _pop.Knowledge.GetOrCreate(npcId);
                    r.accepted = e.affinity >= 30 && (n.effort >= 40 || _rng.Chance(0.5f));
                    if (r.accepted) e.affinity = Math.Min(100, e.affinity + 2);
                    r.line = r.accepted ? "いいよ、一緒に練習しよう！" : n.effort < 40 ? "練習？ だるいからパス。" : "ごめん、今日は無理。";
                    break;
                case CallPurpose.AskAbout:
                    r.line = AskAbout(n, aboutNpcId, day, r);
                    break;
            }
            Log(day, npcId, true, true, purpose.ToString());
            return r;
        }

        string AskAbout(NPCData callee, int targetId, int day, CallResult r)
        {
            var target = _pop.Database.Get(targetId);
            if (target == null) return "誰のこと？";
            var rel = callee.FindRelation(targetId);
            if (rel == null) return $"{target.FullName}？ うーん、よく知らないなあ。";
            if (_pop.Knowledge.AddRelation(callee.id, targetId, rel.type, callee.id, day))
                r.learnedRelation = new KnownRelation { fromId = callee.id, toId = targetId, type = rel.type, sourceId = callee.id, discoveredDay = day };
            _pop.Knowledge.Reveal(targetId, NPCInfoFlags.Name | NPCInfoFlags.Age);
            if (rel.type == RelationType.Classmate) _pop.Knowledge.Reveal(targetId, NPCInfoFlags.Talent);
            return $"{target.FullName}なら{NPCLabels.Of(rel.type)}だよ。" + (rel.affinity >= 30 ? "いいやつだよ。" : rel.affinity <= -20 ? "……あんまり好きじゃない。" : "");
        }

        /// <summary>今日かかってくる電話。日付が進んだ後に呼ぶ。</summary>
        public List<IncomingCall> IncomingCalls()
        {
            var calls = new List<IncomingCall>();
            int day = _pop.Database.currentDay;

            // 闇金の取り立て
            foreach (var loan in _economy.loans)
            {
                if (loan.lender != Lender.LoanShark || loan.overdueDays <= 0 || loan.overdueDays % 3 != 1) continue;
                calls.Add(new IncomingCall { callerId = -1, kind = IncomingCallKind.LoanShark,
                    text = $"「おう、{_player.name}さんよ。{NPCLabels.Yen(loan.Balance)}、いつ返してくれるんだ？」" });
            }

            foreach (var e in _pop.Knowledge.entries.ToArray()) // 噂話で手帳に人が増えるのでコピーを回す
            {
                if (calls.Count >= maxIncomingPerDay) break;
                if (e.affinity < incomingAffinity || !e.Knows(NPCInfoFlags.PhoneNumber)) continue;
                var n = _pop.Database.Get(e.npcId);
                if (n == null || n.isIncarcerated || string.IsNullOrEmpty(n.phoneNumber)) continue;
                float chance = incomingChancePerFriend * (n.personality == Personality.Extrovert ? 2f : n.personality == Personality.Gloomy ? 0.4f : 1f);
                if (!_rng.Chance(chance)) continue;
                calls.Add(MakeCall(n, day));
            }
            foreach (var c in calls) Log(day, c.callerId, false, true, c.kind.ToString());
            return calls;
        }

        IncomingCall MakeCall(NPCData n, int day)
        {
            // 自分が出る試合があれば助っ人に誘う
            var m = _world.NextMatchOf(n.id, day, 3);
            if (m != null && !m.IsUnderground && m.category != MatchCategory.PrisonLeague && _rng.Chance(0.6f))
            {
                bool home = m.homeLineup.Contains(n.id);
                string when = m.day == day ? "今日" : m.day == day + 1 ? "明日" : $"{m.day - day}日後";
                return new IncomingCall { callerId = n.id, kind = IncomingCallKind.MatchInvite, matchId = m.id, joinHome = home,
                    text = $"{n.FullName}「{when}の{MatchLabels.Of(m.category)}、人が足りないんだ！ 助っ人に来てくれない？」" };
            }
            if (n.relations.Count > 0 && _rng.Chance(0.7f))
            {
                var rel = _rng.Pick(n.relations);
                var t = _pop.Database.Get(rel.targetId);
                if (t != null)
                {
                    _pop.Knowledge.AddRelation(n.id, t.id, rel.type, n.id, day);
                    _pop.Knowledge.Reveal(t.id, NPCInfoFlags.Name);
                    return new IncomingCall { callerId = n.id, kind = IncomingCallKind.Gossip,
                        text = $"{n.FullName}「ねえ聞いて、{NPCLabels.Of(rel.type)}の{t.FullName}がさ……」" };
                }
            }
            return new IncomingCall { callerId = n.id, kind = IncomingCallKind.Thanks, text = $"{n.FullName}「この前はありがとう！ また遊ぼうね。」" };
        }

        void Log(int day, int npcId, bool outgoing, bool answered, string summary)
        {
            _state.log.Add(new CallRecord { day = day, npcId = npcId, outgoing = outgoing, answered = answered, summary = summary });
            if (_state.log.Count > PhoneState.MaxLog) _state.log.RemoveRange(0, _state.log.Count - PhoneState.MaxLog);
        }
    }
}
