namespace SoccerIsland.NPC
{
    public enum Gender
    {
        Male,
        Female,
    }

    /// <summary>島の街。街ごとに雰囲気・性格・ライバル意識がある。</summary>
    public enum Town
    {
        /// <summary>勇心町：人を救うのが好きな、気の強い街。病院・交番が多い。</summary>
        Yushin,
        /// <summary>古鐘町：人情的な旧街。田舎。ヤンキーが多い。</summary>
        Furugane,
        /// <summary>煌浜：お店・飲み屋がいっぱいある垢抜けた街。チャラい。</summary>
        Kirahama,
        /// <summary>天峰台：意識が高い、島1番というプライドがある街。</summary>
        Tenpou,
    }

    public enum Occupation
    {
        Student,        // 学生
        Salaryman,      // サラリーマン
        Construction,   // 土木作業員
        Doctor,         // 医者
        Nurse,          // 看護師
        ShopClerk,      // ショップ店員
        ConvenienceClerk, // コンビニ店員
        Mafia,          // マフィア
        Police,         // 警官
        Firefighter,    // 消防士
        Fisherman,      // 漁師
        Farmer,         // 農家
        Teacher,        // 教師
        Bartender,      // バーテンダー
        Chef,           // 料理人
        Hairdresser,    // 美容師
        Banker,         // 銀行員
        Reporter,       // サッカー新聞記者
        Lifeguard,      // ライフセーバー
        Idol,           // アイドル
        Unemployed,     // 無職
        Retired,        // 隠居
        Prisoner,       // 受刑者
        Castaway,       // 漂流者
    }

    /// <summary>普段の性格。チームの雰囲気バランスに影響する。</summary>
    public enum Personality
    {
        Introvert,  // 内向的
        Extrovert,  // 外交的
        Gloomy,     // 根暗
    }

    /// <summary>サッカーの性格。ミーティング・コーチングでの要求が変わる。</summary>
    public enum SoccerPersonality
    {
        MoodMaker,  // ムードメーカー：本人のテンションでチームの雰囲気が変わる
        Egoist,     // エゴイスト：自分が良ければいい
        Sense,      // センス的：ファンタジックなプレー
        Hysteric,   // ヒステリー：カードをもらいやすい
        Fighter,    // ファイター：暴力的で体を張る
        Devoted,    // 献身的：脇役のプレーで満足

        // ---- 特殊性格（レア） ----
        God,        // ゴッド
        Phantom,    // 幻影：謎のプレーヤー。姿を見せない
        Gifted,     // ギフテッド：常人には理解できないプレー
    }

    /// <summary>成長タイプ。能力値が年齢によってどう伸びるか。</summary>
    public enum GrowthType
    {
        Early,      // 早熟：若いうちにピーク、衰えも早い
        Normal,     // 普通
        Late,       // 晩成：30前後でピーク
        Awakening,  // 覚醒型：ずっと下手だが、ある年齢で突然化ける
        Longevity,  // 持続型：ピークが長く衰えにくい
    }

    public enum Position
    {
        GK,
        DF,
        MF,
        FW,
    }

    /// <summary>NPC同士の関係（相関図の線）。</summary>
    public enum RelationType
    {
        Classmate,  // 同級生
        Family,     // 家族
        Coworker,   // 同僚
        Friend,     // 友人
        Rival,      // ライバル
        // 以下は「相手が自分にとって何か」を表す
        Boss,       // 相手が自分の親分・上司
        Subordinate,// 相手が自分の子分・部下
        Mentor,     // 相手が自分の師匠
        Pupil,      // 相手が自分の弟子
        Crush,      // 片思い（2周目以降の恋愛要素用）
    }

    /// <summary>会話で判明する情報。プレイヤーの「知っていること」をビットで管理する。</summary>
    [System.Flags]
    public enum NPCInfoFlags
    {
        None              = 0,
        Name              = 1 << 0,
        Age               = 1 << 1,
        Occupation        = 1 << 2,
        Club              = 1 << 3,
        Personality       = 1 << 4,
        SoccerPersonality = 1 << 5,
        Position          = 1 << 6,
        Salary            = 1 << 7,
        MarketValue       = 1 << 8,
        Talent            = 1 << 9,
        Effort            = 1 << 10,
        GrowthHint        = 1 << 11,
        PhoneNumber       = 1 << 12,
        UndergroundSecret = 1 << 13,
        ExamGrade         = 1 << 14,

        Basic = Name | Age | Occupation,
        All = Name | Age | Occupation | Club | Personality | SoccerPersonality | Position |
              Salary | MarketValue | Talent | Effort | GrowthHint | PhoneNumber | UndergroundSecret | ExamGrade,
    }
}
