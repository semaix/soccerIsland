using SoccerIsland.Match;

namespace SoccerIsland.Field
{
    /// <summary>試合まわりの日本語表示。</summary>
    public static class MatchText
    {
        public static string Command(MatchCommand c)
        {
            switch (c)
            {
                case MatchCommand.Dribble: return "ドリブル";
                case MatchCommand.FlairDribble: return "華麗なドリブル";
                case MatchCommand.SpecialDribble: return "必殺ドリブル";
                case MatchCommand.Pass: return "パス";
                case MatchCommand.LongPass: return "ロングパス";
                case MatchCommand.OneTwo: return "ワンツー";
                case MatchCommand.Centering: return "センタリング";
                case MatchCommand.Shoot: return "シュート";
                case MatchCommand.SpecialShot: return "必殺シュート";
                case MatchCommand.Keep: return "キープ";
                case MatchCommand.BackPass: return "バックパス";
                case MatchCommand.Header: return "ヘディング";
                case MatchCommand.Volley: return "ボレー";
                case MatchCommand.Trap: return "トラップ";
                case MatchCommand.MissOnPurpose: return "わざと外す";
                case MatchCommand.SloppyPass: return "わざとパスミス";
                case MatchCommand.Tackle: return "タックル";
                case MatchCommand.SpecialTackle: return "必殺タックル";
                case MatchCommand.PassCut: return "パスカット";
                case MatchCommand.Block: return "ブロック";
                case MatchCommand.SpecialBlock: return "必殺ブロック";
                case MatchCommand.OffsideTrap: return "オフサイドトラップ";
                case MatchCommand.Clear: return "クリア";
                case MatchCommand.Compete: return "競る";
                case MatchCommand.Catch: return "キャッチ";
                case MatchCommand.Punch: return "パンチング";
                case MatchCommand.SpecialSave: return "必殺セーブ";
                default: return "様子を見る";
            }
        }

        public static bool NeedsTarget(MatchCommand c) =>
            c == MatchCommand.Pass || c == MatchCommand.LongPass || c == MatchCommand.OneTwo || c == MatchCommand.BackPass || c == MatchCommand.Centering;

        public static string Coaching(CoachingCommand c)
        {
            switch (c)
            {
                case CoachingCommand.Praise: return "褒める";
                case CoachingCommand.Scold: return "叱る";
                case CoachingCommand.CalmDown: return "落ち着け";
                case CoachingCommand.GoForward: return "攻めろ";
                case CoachingCommand.DefendDeep: return "守れ";
                case CoachingCommand.PassAround: return "パスを回せ";
                case CoachingCommand.TakeOn: return "仕掛けろ";
                case CoachingCommand.PlayPhysical: return "体を張れ";
                case CoachingCommand.FreeRole: return "自由にやれ";
                case CoachingCommand.SupportRole: return "サポートに徹しろ";
                case CoachingCommand.GiveBallToMe: return "ボールを集めろ";
                default: return "ラインを上げろ";
            }
        }

        public static string Topic(MeetingTopic t)
        {
            switch (t)
            {
                case MeetingTopic.Attack: return "攻撃的にいこう";
                case MeetingTopic.Defend: return "守備から入ろう";
                case MeetingTopic.Enjoy: return "楽しもう";
                case MeetingTopic.FireUp: return "気合を入れろ！";
                case MeetingTopic.TrustAce: return "エースに任せる";
                case MeetingTopic.TeamFirst: return "チームのために戦おう";
                case MeetingTopic.Freedom: return "自由にやれ";
                case MeetingTopic.OffsideTrapPlan: return "オフサイドトラップで行く";
                default: return "勝ったらボーナスを出す";
            }
        }

        public static string Prompt(PromptKind k)
        {
            switch (k)
            {
                case PromptKind.Attack: return "攻撃";
                case PromptKind.Defend: return "守備";
                case PromptKind.Keeper: return "キーパー";
                case PromptKind.Aerial: return "空中戦";
                default: return "空中戦（守備）";
            }
        }
    }
}
