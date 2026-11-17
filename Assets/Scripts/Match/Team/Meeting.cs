using System;
using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Match
{
    /// <summary>試合前ミーティングで主人公が話すこと。</summary>
    public enum MeetingTopic
    {
        Attack,          // 攻撃的にいこう
        Defend,          // 守備から入ろう
        Enjoy,           // 楽しもう
        FireUp,          // 気合を入れる（檄）
        TrustAce,        // エースに任せる（ace を指定）
        TeamFirst,       // チームのために戦おう
        Freedom,         // 自由にやれ
        OffsideTrapPlan, // オフサイドトラップで行く
        BonusPromise,    // 勝ったらボーナスを出す
    }

    public class MeetingReaction
    {
        public MatchPlayer player;
        public float tensionDelta;
        public string line;
    }

    public class MeetingArgument
    {
        public MatchPlayer a, b;
        public string lineA, lineB;
        public bool resolvedByMoodMaker;
    }

    public class MeetingResult
    {
        public MeetingTopic topic;
        public List<MeetingReaction> reactions = new List<MeetingReaction>();
        public List<MeetingArgument> arguments = new List<MeetingArgument>();
        public float moodDelta;
    }

    /// <summary>
    /// 試合前のミーティング。同じ話でも性格によって受け取り方が違い、方針をめぐって口論になることもある。
    /// </summary>
    public static class Meeting
    {
        public static MeetingResult Hold(MatchTeam team, MeetingTopic topic, SeededRandom rng, MatchPlayer ace = null)
        {
            var result = new MeetingResult { topic = topic };
            foreach (var p in team.Active)
            {
                if (p.isProtagonist) continue;
                float d = Reaction(p, topic, ace);
                p.tension = Clamp(p.tension + d);
                result.reactions.Add(new MeetingReaction { player = p, tensionDelta = d, line = Line(p, topic, d, ace) });
                result.moodDelta += d * 0.15f;
            }

            if (topic == MeetingTopic.OffsideTrapPlan) team.offsideTrapPlanned = true;
            FindArguments(team, topic, ace, rng, result);

            team.mood = Math.Max(-100f, Math.Min(100f, team.mood + result.moodDelta));
            return result;
        }

        static float Reaction(MatchPlayer p, MeetingTopic topic, MatchPlayer ace)
        {
            float d = 0f;
            var sp = p.soccerPersonality;
            switch (topic)
            {
                case MeetingTopic.Attack:
                    d = sp == SoccerPersonality.Egoist ? 15 : sp == SoccerPersonality.Fighter ? 10 : sp == SoccerPersonality.Sense ? 10 : sp == SoccerPersonality.Devoted ? 0 : 5;
                    break;
                case MeetingTopic.Defend:
                    d = sp == SoccerPersonality.Egoist ? -15 : sp == SoccerPersonality.Sense ? -10 : sp == SoccerPersonality.Devoted ? 10 : sp == SoccerPersonality.Fighter ? 5 : 0;
                    if (p.position == Position.DF || p.position == Position.GK) d += 5;
                    break;
                case MeetingTopic.Enjoy:
                    d = sp == SoccerPersonality.MoodMaker ? 20 : sp == SoccerPersonality.Sense ? 15 : sp == SoccerPersonality.Fighter ? -5 : 5;
                    break;
                case MeetingTopic.FireUp:
                    d = sp == SoccerPersonality.Fighter ? 20 : sp == SoccerPersonality.Hysteric ? 15 : sp == SoccerPersonality.MoodMaker ? 10 : 5;
                    if (p.personality == Personality.Introvert) d -= 15;
                    if (p.personality == Personality.Gloomy) d -= 20;
                    break;
                case MeetingTopic.TrustAce:
                    if (p == ace) d = 25;
                    else d = sp == SoccerPersonality.Egoist ? -15 : sp == SoccerPersonality.Devoted ? 10 : -3;
                    break;
                case MeetingTopic.TeamFirst:
                    d = sp == SoccerPersonality.Devoted ? 20 : sp == SoccerPersonality.Egoist ? -15 : sp == SoccerPersonality.MoodMaker ? 10 : 3;
                    break;
                case MeetingTopic.Freedom:
                    d = sp == SoccerPersonality.Sense || sp == SoccerPersonality.Gifted ? 25 : sp == SoccerPersonality.Egoist ? 10 : sp == SoccerPersonality.Devoted ? -10 : 0;
                    break;
                case MeetingTopic.OffsideTrapPlan:
                    d = (p.position == Position.DF && p.stats.mental >= 55) ? 10 : sp == SoccerPersonality.Hysteric ? -5 : 0;
                    break;
                case MeetingTopic.BonusPromise:
                    d = sp == SoccerPersonality.Egoist ? 20 : sp == SoccerPersonality.Devoted ? 0 : 5;
                    break;
            }
            if (sp == SoccerPersonality.God) d *= 0.2f; // 神は動じない
            return d;
        }

        static string Line(MatchPlayer p, MeetingTopic topic, float d, MatchPlayer ace)
        {
            if (topic == MeetingTopic.TrustAce && p == ace) return "任せとけ。全部決めてやる。";
            if (d >= 15) return p.soccerPersonality == SoccerPersonality.MoodMaker ? "よっしゃー！ みんな行くぞー！" : "いいね、燃えてきた！";
            if (d >= 5) return "了解。";
            if (d <= -15) return p.personality == Personality.Gloomy ? "……そういうの、無理。" : "は？ 冗談だろ。";
            if (d < 0) return "……まあ、いいけど。";
            return "……。";
        }

        static void FindArguments(MatchTeam team, MeetingTopic topic, MatchPlayer ace, SeededRandom rng, MeetingResult result)
        {
            var egoists = new List<MatchPlayer>();
            var devoted = new List<MatchPlayer>();
            var sense = new List<MatchPlayer>();
            var hysteric = new List<MatchPlayer>();
            bool moodMaker = false;
            foreach (var p in team.Active)
            {
                if (p.isProtagonist) continue;
                switch (p.soccerPersonality)
                {
                    case SoccerPersonality.Egoist: egoists.Add(p); break;
                    case SoccerPersonality.Devoted: devoted.Add(p); break;
                    case SoccerPersonality.Sense: sense.Add(p); break;
                    case SoccerPersonality.Hysteric: hysteric.Add(p); break;
                    case SoccerPersonality.MoodMaker: moodMaker = true; break;
                }
            }

            if (topic == MeetingTopic.Defend && egoists.Count > 0 && devoted.Count > 0)
                Argue(result, rng.Pick(egoists), rng.Pick(devoted), "守備なんかやってられるか！", "勝つためだろ！ わがまま言うな！", moodMaker, rng);
            if (topic == MeetingTopic.Freedom && sense.Count > 0 && devoted.Count > 0)
                Argue(result, rng.Pick(devoted), rng.Pick(sense), "自由って……ポジションは守ってくれよ。", "型にはめたら俺は死ぬ。", moodMaker, rng);
            if (topic == MeetingTopic.TrustAce && ace != null)
                foreach (var e in egoists)
                    if (e != ace) { Argue(result, e, ace, "なんでそいつがエースなんだよ！", "実力の差だろ。", moodMaker, rng); break; }
            if (topic == MeetingTopic.FireUp && hysteric.Count >= 2)
                Argue(result, hysteric[0], hysteric[1], "お前が一番気合入ってねえんだよ！", "あ!? もういっぺん言ってみろ！", moodMaker, rng);
            if (topic == MeetingTopic.Attack && egoists.Count >= 2)
                Argue(result, egoists[0], egoists[1], "点を取るのは俺だ。", "いや、俺だ。", moodMaker, rng);
        }

        static void Argue(MeetingResult result, MatchPlayer a, MatchPlayer b, string la, string lb, bool moodMaker, SeededRandom rng)
        {
            bool resolved = moodMaker && rng.Chance(0.6f);
            result.arguments.Add(new MeetingArgument { a = a, b = b, lineA = la, lineB = lb, resolvedByMoodMaker = resolved });
            result.moodDelta += resolved ? 3f : -10f;
            if (!resolved) { a.tension = Clamp(a.tension - 5f); b.tension = Clamp(b.tension - 5f); }
        }

        static float Clamp(float v) => v < 0f ? 0f : (v > 100f ? 100f : v);
    }
}
