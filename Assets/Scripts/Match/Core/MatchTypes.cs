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
        Centering,     // センタリング（サイドからゴール前へ上げる → 空中戦）
        SpecialDribble,// 必殺ドリブル
        // ---- 空中戦（攻撃） ----
        Header,        // ヘディングシュート
        Volley,        // ボレーシュート
        Trap,          // トラップして持つ
        // ---- 八百長用（ミッション中だけ出る） ----
        MissOnPurpose, // わざと外す
        SloppyPass,    // わざとパスミス
        // ---- 守備 ----
        Tackle,        // タックル
        PassCut,       // パスカット
        Block,         // ブロック（シュートを体で止める）
        OffsideTrap,   // オフサイドトラップ
        Wait,          // 様子を見る
        SpecialTackle, // 必殺タックル
        SpecialBlock,  // 必殺ブロック
        // ---- 空中戦（守備） ----
        Clear,         // クリア
        Compete,       // 競り合う
        // ---- キーパー ----
        Catch,         // キャッチ
        Punch,         // パンチング
        SpecialSave,   // 必殺セーブ
    }

    /// <summary>
    /// 操作方式。FullTeam は自チームのボール保持者・守備者・キーパー全員にコマンドを出す（キャプテン翼式）。
    /// ProtagonistOnly は主人公が絡む場面だけ。
    /// </summary>
    public enum ControlMode
    {
        FullTeam,
        ProtagonistOnly,
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

    /// <summary>ボスチームの特殊能力。</summary>
    public enum BossTrait
    {
        Intimidation,   // 威圧：キックオフ時に相手のテンションを下げる
        BribedReferee,  // 審判買収：自分たちのファウルは取られにくく、相手のファウルは取られやすい
        GeniusVision,   // 天才の読み：相手のコマンドを読んで守る
        IronWall,       // 鉄壁：キーパーが強い
        Unbeatable,     // 絶対王者：条件を満たさないと能力が大幅に上がる（どうしても勝てない裏チーム）
        Warded,         // 魔除け：相手の威圧・審判買収が効かない
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
        public List<SpecialMove> specials = new List<SpecialMove>();
        /// <summary>この試合で稼いだ経験値（主人公のみ使う）。</summary>
        public int exp;

        /// <summary>ガッツ（気力）。行動するたびに減り、少ないと力が出ない。</summary>
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
        /// <summary>本来のポジション（フォーメーションで別の位置に置くと少し力が落ちる）。</summary>
        public Position naturalPosition;
        public bool naturalSet;
        public bool OutOfPosition => naturalSet && naturalPosition != position;

        public void RememberNaturalPosition()
        {
            if (naturalSet) return;
            naturalPosition = position;
            naturalSet = true;
        }
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

    /// <summary>フォーメーション（GKを除く DF-MF-FW）。</summary>
    public enum Formation
    {
        F442,  // 4-4-2 バランス
        F433,  // 4-3-3 攻撃的
        F352,  // 3-5-2 中盤重視
        F541,  // 5-4-1 守備的
        F343,  // 3-4-3 超攻撃的
    }

    /// <summary>作戦。</summary>
    public enum TeamTactic
    {
        Balanced,     // バランス
        Counter,      // カウンター（奪ったら縦に速く）
        WingAttack,   // サイド攻撃（センタリング多め）
        CentralBreak, // 中央突破（ドリブル・ワンツー）
        Possession,   // パスをつなぐ
        HighPress,    // 前線からプレス（ガッツを消耗）
        ParkTheBus,   // ゴール前を固める
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
        public Formation formation = Formation.F442;
        public TeamTactic tactic = TeamTactic.Balanced;
        /// <summary>ボス戦などの特殊能力。</summary>
        public List<BossTrait> traits = new List<BossTrait>();
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
        Disobey,       // 指示を無視した
        Centering, Header, Volley, Clear, Catch, Punch,
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
        /// <summary>2D演出でカットイン（顔アップ・技名表示）を入れる場面か。</summary>
        public bool cutIn;
        /// <summary>カットインで出す技名（必殺技など）。</summary>
        public string moveName;
        /// <summary>カットインで叫ぶセリフ。</summary>
        public string shout;
    }

    public enum PromptKind
    {
        Attack,       // 自チームの選手がボールを持って相手と対決
        Defend,       // 相手のボール保持者に自チームの選手が向かう
        Keeper,       // 相手のシュート！ キーパーの対応
        Aerial,       // 味方のセンタリング！ 空中で合わせる
        AerialDefend, // 相手のセンタリング！ 空中で競る
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
        /// <summary>左(0)・中央(1)・右(2)。2Dのミニマップ表示用。</summary>
        public int lane;
        /// <summary>選んでいる選手の残りガッツ。</summary>
        public int guts;
        public int maxGuts;
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
        /// <summary>主人公がこの試合で稼いだ経験値。</summary>
        public int protagonistExp;
        /// <summary>結果反映で上がったレベル。</summary>
        public List<int> levelUps = new List<int>();
        /// <summary>レベルアップで覚えた必殺技。</summary>
        public List<SpecialMove> learnedSpecials = new List<SpecialMove>();

        public bool ProtagonistWon => protagonistSide.HasValue &&
            (protagonistSide == Side.Home ? homeScore > awayScore : awayScore > homeScore);
        public bool ProtagonistLost => protagonistSide.HasValue &&
            (protagonistSide == Side.Home ? homeScore < awayScore : awayScore < homeScore);
    }
}
