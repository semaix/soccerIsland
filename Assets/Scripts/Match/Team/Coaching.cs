using System;
using SoccerIsland.NPC;

namespace SoccerIsland.Match
{
    public class CoachingResult
    {
        public bool accepted;
        public bool metDemand;
        public float tensionDelta;
        public string line;
        /// <summary>クールダウン中などで指示が出せなかった。</summary>
        public bool notAllowed;
    }

    /// <summary>
    /// 試合中のコーチング。性格ごとに「今ほしい指示」（要求）があり、応えるとテンションと信頼が上がる。
    ///  エゴイスト：ボールに触れないと「俺によこせ！」
    ///  ムードメーカー：テンションが下がると「褒めてくれよ〜」
    ///  ヒステリー：ファウル・カードの後は「（イライラ）」→ 落ち着かせる
    ///  ファイター：負けていると「体ぶつけていいか？」
    ///  献身的：「俺は何をすればいい？」→ 役割を与える
    ///  センス的：縛られると「自由にやらせろ」
    /// </summary>
    public static class Coaching
    {
        public const int DemandIntervalMinutes = 8;

        /// <summary>状況から要求を更新する。新しい要求が出たら true。</summary>
        public static bool UpdateDemand(MatchPlayer p, MatchTeam team, MatchTeam opponent, int minute, SeededRandom rng)
        {
            if (p.isProtagonist || !p.CanPlay || p.demand.HasValue) return false;
            if (minute - p.lastDemandMinute < DemandIntervalMinutes) return false;
            if (!rng.Chance(0.15f)) return false; // 毎回は言ってこない

            CoachingCommand? d = null;
            string line = null;
            bool losing = team.score < opponent.score;
            switch (p.soccerPersonality)
            {
                case SoccerPersonality.Egoist:
                    if (minute - p.lastTouchMinute >= 12) { d = CoachingCommand.GiveBallToMe; line = "おい、こっちによこせ！ 決めてやるから！"; }
                    break;
                case SoccerPersonality.MoodMaker:
                    if (p.tension < 45) { d = CoachingCommand.Praise; line = "なあ、ちょっと褒めてくれよ〜"; }
                    break;
                case SoccerPersonality.Hysteric:
                    if (p.tension > 85 || p.yellowCards > 0) { d = CoachingCommand.CalmDown; line = "（審判をにらみつけている……）"; }
                    break;
                case SoccerPersonality.Fighter:
                    if (losing) { d = CoachingCommand.PlayPhysical; line = "体、ぶつけていいよな？"; }
                    break;
                case SoccerPersonality.Devoted:
                    if (minute >= 10 && p.role == PlayerRole.Normal) { d = CoachingCommand.SupportRole; line = "何をすればいい？ 指示をくれ。"; }
                    break;
                case SoccerPersonality.Sense:
                case SoccerPersonality.Gifted:
                    if (team.instruction == TeamInstruction.PassAround || team.instruction == TeamInstruction.DefendDeep)
                    { d = CoachingCommand.FreeRole; line = "……こんなサッカー、つまらない。自由にやらせて。"; }
                    break;
            }
            if (d == null && p.personality == Personality.Introvert && p.tension < 35)
            { d = CoachingCommand.Praise; line = "（自信なさそうにうつむいている）"; }

            if (d == null) return false;
            p.demand = d;
            p.demandLine = line;
            p.lastDemandMinute = minute;
            return true;
        }

        /// <summary>個人への指示。trust（チームメイトからの評価）が低いと聞いてもらえないことがある。</summary>
        public static CoachingResult CoachPlayer(MatchPlayer p, CoachingCommand cmd, float trust, SeededRandom rng)
        {
            var r = new CoachingResult();
            bool met = p.demand.HasValue && p.demand.Value == cmd;
            r.metDemand = met;

            if (!met && trust < 30f && rng.Chance(0.5f))
            {
                r.line = "……お前に言われたくない。";
                r.tensionDelta = -5f;
                Apply(p, r);
                return r;
            }

            if (met)
            {
                r.accepted = true;
                r.tensionDelta = 20f;
                r.line = MetLine(p, cmd);
                if (cmd == CoachingCommand.CalmDown) p.tension = Math.Min(p.tension, 55f);
                p.demand = null;
                Apply(p, r);
                return r;
            }

            // 要求と違う指示への反応
            switch (cmd)
            {
                case CoachingCommand.Praise:
                    r.tensionDelta = p.personality == Personality.Gloomy ? 3f : p.soccerPersonality == SoccerPersonality.Egoist ? 6f : 8f;
                    r.line = p.personality == Personality.Gloomy ? "……別に。" : "へへ、どうも！";
                    break;
                case CoachingCommand.Scold:
                    if (p.soccerPersonality == SoccerPersonality.Hysteric)
                    { r.tensionDelta = 40f; r.line = "ふざけんな！ お前に言われる筋合いはねえ！"; }
                    else if (p.soccerPersonality == SoccerPersonality.Fighter)
                    { r.tensionDelta = 10f; r.line = "……クソッ、わかったよ！"; }
                    else if (p.personality == Personality.Introvert || p.personality == Personality.Gloomy)
                    { r.tensionDelta = -20f; r.line = "……すみません……。"; }
                    else
                    { r.tensionDelta = -5f; r.line = "わ、わかったって。"; }
                    break;
                case CoachingCommand.CalmDown:
                    r.tensionDelta = p.tension > 75f ? -15f : -5f;
                    r.line = "ああ、落ち着いてるよ。";
                    break;
                case CoachingCommand.FreeRole:
                    r.tensionDelta = p.soccerPersonality == SoccerPersonality.Devoted ? -10f : 5f;
                    r.line = p.soccerPersonality == SoccerPersonality.Devoted ? "自由って言われても……。" : "おう。";
                    break;
                case CoachingCommand.SupportRole:
                    r.tensionDelta = p.soccerPersonality == SoccerPersonality.Egoist ? -20f : p.soccerPersonality == SoccerPersonality.Sense ? -10f : 3f;
                    r.line = p.soccerPersonality == SoccerPersonality.Egoist ? "脇役なんてごめんだ。" : "了解。";
                    break;
                default:
                    r.tensionDelta = 2f;
                    r.line = "了解！";
                    break;
            }
            if (p.soccerPersonality == SoccerPersonality.God) r.tensionDelta *= 0.2f;
            r.accepted = r.tensionDelta >= 0f;
            Apply(p, r);
            return r;
        }

        static string MetLine(MatchPlayer p, CoachingCommand cmd)
        {
            switch (cmd)
            {
                case CoachingCommand.GiveBallToMe: return "わかってるじゃねえか！";
                case CoachingCommand.Praise: return p.soccerPersonality == SoccerPersonality.MoodMaker ? "よっしゃー！ 元気出たー！" : "……ありがとう。がんばる。";
                case CoachingCommand.CalmDown: return "……ふう。悪い、熱くなってた。";
                case CoachingCommand.PlayPhysical: return "待ってました！";
                case CoachingCommand.SupportRole: return "任せて。みんなを支える。";
                case CoachingCommand.FreeRole: return "……それでいい。見てて。";
                default: return "了解！";
            }
        }

        static void Apply(MatchPlayer p, CoachingResult r)
        {
            p.tension = Math.Max(0f, Math.Min(100f, p.tension + r.tensionDelta));
        }

        public static TeamInstruction ToTeamInstruction(CoachingCommand cmd)
        {
            switch (cmd)
            {
                case CoachingCommand.GoForward: return TeamInstruction.GoForward;
                case CoachingCommand.DefendDeep: return TeamInstruction.DefendDeep;
                case CoachingCommand.PassAround: return TeamInstruction.PassAround;
                case CoachingCommand.TakeOn: return TeamInstruction.TakeOn;
                case CoachingCommand.PlayPhysical: return TeamInstruction.PlayPhysical;
                case CoachingCommand.GiveBallToMe: return TeamInstruction.FeedProtagonist;
                case CoachingCommand.RaiseLine: return TeamInstruction.RaiseLine;
                default: return TeamInstruction.None;
            }
        }
    }
}
