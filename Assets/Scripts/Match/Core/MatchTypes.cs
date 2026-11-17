using System.Collections.Generic;
using SoccerIsland.NPC;

namespace SoccerIsland.Match
{
    public enum Side { Home, Away }

    /// <summary>コマンド。ボールを持っている側と守る側で選べるものが違う。</summary>
    public enum MatchCommand
    {
        // ---- 攻撃 ----
        Dribble,       // ドリブル
        FlairDribble,  // 華麗なドリブル（成功しにくいが、決まればファンが沸く）
        Pass,          // パス（1ゾーン前へ）
        LongPass,      // ロングパス（2ゾーン前へ。カットされやすい）
        OneTwo,        // ワンツー
        Shoot,         // シュート
        SpecialShot,   // 必殺シュート（気力を大きく使う）
        Keep,          // キープ（様子見）
        BackPass,      // バックパス
        // ---- 八百長用（ミッション中だけ出る） ----
        MissOnPurpose, // わざと外す
        SloppyPass,    // わざとパスミス
        // ---- 守備 ----
        Tackle,        // タックル
        PassCut,       // パスカット
        Block,         // ブロック（シュートを体で止める）
        OffsideTrap,   // オフサイドトラップ
        Wait,          // 様子を見る
    }

    /// <summary>試合中のコーチング（指示）。</summary>
    public enum CoachingCommand
    {
        Praise,       // 褒める
        Scold,        // 叱る
        CalmDown,     // 落ち着け
        GoForward,    // 攻めろ
        DefendDeep,   // 守れ
        PassAround,   // パスを回せ
        TakeOn,       // 仕掛けろ
        PlayPhysical, // 体を張れ
        FreeRole,     // 自由にやれ
        SupportRole,  // サポートに徹しろ
        GiveBallToMe, // （エゴイストの要求）俺によこせ → 「ボールを集めろ」
        RaiseLine,    // ラインを上げろ（オフサイドトラップを狙う）
    }

    /// <summary>コーチングで与えた個人の役割。</summary>
    public enum PlayerRole
    {
        Normal,
        Physical, // 体を張れ
        Support,  // サポートに徹しろ
        Free,     // 自由にやれ
    }

    /// <summary>試合に出ている選手。</summary>
    public class MatchPlayer
    {
        /// <summary>-1 は主人公。</summary>
        public int npcId;
        public string name;
        public bool isProtagonist;
        public Position position;
        public NPCAbilities stats;
        public Personality personality;
        public SoccerPersonality soccerPersonality;

        /// <summary>気力。行動するたびに減り、少ないと力が出ない。</summary>
        public int guts;
        public int maxGuts;
        /// <summary>テンション 0-100。高いほど力が出る。ムードメーカーのテンションはチーム全体に響く。</summary>
        public float tension = 60f;

        public int yellowCards;
        public bool sentOff;
        /// <summary>足がつった（ボールに関われない）。</summary>
        public bool cramped;

        /// <summary>今この選手が求めている指示（性格で変わる）。</summary>
        public CoachingCommand? demand;
        public string demandLine;
        public int lastTouchMinute;
        public PlayerRole role;
        public int lastDemandMinute = -100;

        // 試合の記録
        public int goals, assists, beautifulSkills, tacklesWon, ballsLost, shots;
        public float rating = 6f;

        public bool CanPlay => !sentOff;
        public bool CanTouchBall => !sentOff && !cramped;

        public float GutsRatio => maxGuts <= 0 ? 0f : (float)guts / maxGuts;

        public override string ToString() => name;
    }

    public enum TeamInstruction
    {
        None,
        GoForward,
        DefendDeep,
        PassAround,
        TakeOn,
        PlayPhysical,
        FeedProtagonist, // 主人公にボールを集める
        RaiseLine,
    }

    public class MatchTeam
    {
        public string name;
        public Side side;
        public List<MatchPlayer> players = new List<MatchPlayer>();
        public int score;

        /// <summary>性格バランスから決まる基本の雰囲気（-100〜100）。</summary>
        public ChemistryReport chemistry = new ChemistryReport();
        /// <summary>ミーティング・口論・ムードメーカーで上下する現在の雰囲気。</summary>
        public float mood;
        public TeamInstruction instruction;
        public int instructionUntilMinute;
        public bool offsideTrapPlanned;
        /// <summary>ボールを集める選手（「俺によこせ」に応えたとき）。</summary>
        public MatchPlayer focusPlayer;
        public int focusUntilMinute;

        public IEnumerable<MatchPlayer> Active
        {
            get { foreach (var p in players) if (p.CanPlay) yield return p; }
        }

        public MatchPlayer Protagonist
        {
            get { foreach (var p in players) if (p.isProtagonist) return p; return null; }
        }

        public MatchPlayer Keeper
        {
            get
            {
                MatchPlayer best = null;
                foreach (var p in Active)
                    if (p.position == Position.GK) return p;
                    else if (best == null || p.stats.goalkeeping > best.stats.goalkeeping) best = p;
                return best;
            }
        }

        public int ActiveCount
        {
            get { int c = 0; foreach (var p in players) if (p.CanPlay) c++; return c; }
        }
    }

    public enum MatchEventType
    {
        Kickoff, HalfTime, FullTime,
        Dribble, DribbleBeaten, BeautifulSkill,
        Pass, PassIntercepted, OneTwo, LongPass,
        Shot, SpecialShot, Goal, Save, Blocked, Missed,
        Tackle, Foul, YellowCard, RedCard, FreeKick,
        OffsideTrapSuccess, OffsideTrapFail,
        Keep, Passive,
        ChanceCreated,
        TrustChanged,
        CoachingAccepted, CoachingRejected, Demand,
        Argument,
        Cramp,
        Suspicion,
        Info,
    }

    /// <summary>実況ログ1行。</summary>
    public class MatchEvent
    {
        public MatchEventType type;
        public int minute;
        public Side side;
        public MatchPlayer actor;
        public MatchPlayer other;
        public string text;
        public float value;
    }

    public enum PromptKind
    {
        Attack,  // 主人公がボールを持った
        Defend,  // 主人公の前に相手が来た
    }

    /// <summary>主人公にコマンドを選ばせる場面。</summary>
    public class MatchPrompt
    {
        public PromptKind kind;
        public MatchPlayer actor;
        public MatchPlayer opponent;
        public int zone;
        public int minute;
        public List<MatchCommand> options = new List<MatchCommand>();
        public List<MatchPlayer> passTargets = new List<MatchPlayer>();
        public string situation;
    }

    public enum MissionStatus { InProgress, Success, Failed }

    public class MissionResult
    {
        public string title;
        public MissionStatus status;
        public string detail;
    }

    public class MatchResult
    {
        public int homeScore, awayScore;
        public Side? protagonistSide;
        public MatchPlayer protagonist;
        /// <summary>チームメイトからの評価（0-100）。</summary>
        public float trust;
        public float suspicion;
        public bool fixingExposed;
        public List<MissionResult> missions = new List<MissionResult>();
        public List<MatchEvent> log = new List<MatchEvent>();

        public bool ProtagonistWon => protagonistSide.HasValue &&
            (protagonistSide == Side.Home ? homeScore > awayScore : awayScore > homeScore);
        public bool ProtagonistLost => protagonistSide.HasValue &&
            (protagonistSide == Side.Home ? homeScore < awayScore : awayScore < homeScore);
    }
}
