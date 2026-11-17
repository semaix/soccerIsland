namespace SoccerIsland.NPC
{
    /// <summary>enum の日本語表示名。UI・会話テキストで使う。</summary>
    public static class NPCLabels
    {
        public static string Of(Gender g) => g == Gender.Male ? "男性" : "女性";

        public static string Of(Town t)
        {
            switch (t)
            {
                case Town.Yushin: return "勇心町";
                case Town.Furugane: return "古鐘町";
                case Town.Kirahama: return "煌浜";
                case Town.Tenpou: return "天峰台";
                default: return t.ToString();
            }
        }

        public static string Of(Occupation o)
        {
            switch (o)
            {
                case Occupation.Student: return "学生";
                case Occupation.Salaryman: return "サラリーマン";
                case Occupation.Construction: return "土木作業員";
                case Occupation.Doctor: return "医者";
                case Occupation.Nurse: return "看護師";
                case Occupation.ShopClerk: return "ショップ店員";
                case Occupation.ConvenienceClerk: return "コンビニ店員";
                case Occupation.Mafia: return "マフィア";
                case Occupation.Police: return "警官";
                case Occupation.Firefighter: return "消防士";
                case Occupation.Fisherman: return "漁師";
                case Occupation.Farmer: return "農家";
                case Occupation.Teacher: return "教師";
                case Occupation.Bartender: return "バーテンダー";
                case Occupation.Chef: return "料理人";
                case Occupation.Hairdresser: return "美容師";
                case Occupation.Banker: return "銀行員";
                case Occupation.Reporter: return "サッカー新聞記者";
                case Occupation.Lifeguard: return "ライフセーバー";
                case Occupation.Idol: return "アイドル";
                case Occupation.Unemployed: return "無職";
                case Occupation.Retired: return "隠居";
                case Occupation.Prisoner: return "受刑者";
                case Occupation.Castaway: return "漂流者";
                default: return o.ToString();
            }
        }

        public static string Of(Personality p)
        {
            switch (p)
            {
                case Personality.Introvert: return "内向的";
                case Personality.Extrovert: return "外交的";
                case Personality.Gloomy: return "根暗";
                default: return p.ToString();
            }
        }

        public static string Of(SoccerPersonality p)
        {
            switch (p)
            {
                case SoccerPersonality.MoodMaker: return "ムードメーカー";
                case SoccerPersonality.Egoist: return "エゴイスト";
                case SoccerPersonality.Sense: return "センス的";
                case SoccerPersonality.Hysteric: return "ヒステリー";
                case SoccerPersonality.Fighter: return "ファイター";
                case SoccerPersonality.Devoted: return "献身的";
                case SoccerPersonality.God: return "ゴッド";
                case SoccerPersonality.Phantom: return "幻影";
                case SoccerPersonality.Gifted: return "ギフテッド";
                default: return p.ToString();
            }
        }

        public static string Of(GrowthType g)
        {
            switch (g)
            {
                case GrowthType.Early: return "早熟";
                case GrowthType.Normal: return "普通";
                case GrowthType.Late: return "晩成";
                case GrowthType.Awakening: return "覚醒型";
                case GrowthType.Longevity: return "持続型";
                default: return g.ToString();
            }
        }

        public static string Of(RelationType r)
        {
            switch (r)
            {
                case RelationType.Classmate: return "同級生";
                case RelationType.Family: return "家族";
                case RelationType.Coworker: return "同僚";
                case RelationType.Friend: return "友人";
                case RelationType.Rival: return "ライバル";
                case RelationType.Boss: return "親分";
                case RelationType.Subordinate: return "子分";
                case RelationType.Mentor: return "師匠";
                case RelationType.Pupil: return "弟子";
                case RelationType.Crush: return "片思い";
                case RelationType.Spouse: return "夫婦";
                default: return r.ToString();
            }
        }

        /// <summary>才能度・努力度(0-100)をプレイヤー向けのランク表記にする。数値そのものは見せない。</summary>
        public static string Rank(int value)
        {
            if (value >= 95) return "SS";
            if (value >= 85) return "S";
            if (value >= 70) return "A";
            if (value >= 55) return "B";
            if (value >= 40) return "C";
            if (value >= 25) return "D";
            return "E";
        }

        /// <summary>金額を「1億2000万円」形式にする。</summary>
        public static string Yen(long yen)
        {
            if (yen <= 0) return "0円";
            long oku = yen / 100000000;
            long man = (yen % 100000000) / 10000;
            if (oku > 0 && man > 0) return $"{oku}億{man}万円";
            if (oku > 0) return $"{oku}億円";
            if (man > 0) return $"{man}万円";
            return $"{yen}円";
        }
    }
}
