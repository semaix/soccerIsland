namespace SoccerIsland.NPC
{
    /// <summary>
    /// 会話テキストのテンプレート。性格で口調が変わる。
    /// 本番では外部テキスト（ローカライズテーブル）に差し替える前提の仮実装。
    /// </summary>
    public static class NPCDialogue
    {
        static string Pick(SeededRandom rng, params string[] options) => options[rng.Range(0, options.Length)];

        /// <summary>一人称。</summary>
        static string Me(NPCData n) => n.gender == Gender.Female ? "あたし" : "俺";

        public static string Introduction(NPCData n, SeededRandom rng)
        {
            string job = NPCLabels.Of(n.occupation);
            switch (n.personality)
            {
                case Personality.Extrovert:
                    return Pick(rng,
                        $"おっ、見ない顔だね！ {n.givenName}だよ！ {NPCLabels.Of(n.hometown)}で{job}やってる{n.age}歳！",
                        $"引っ越してきた人？ {n.FullName}です！ {job}やってます、{n.age}歳。よろしくね！");
                case Personality.Introvert:
                    return Pick(rng,
                        $"……{n.FullName}です。{job}を、しています。{n.age}歳です。",
                        $"あ、どうも……{n.familyName}です。{job}です。年は{n.age}……です。");
                default:
                    return Pick(rng,
                        $"……何？ {n.familyName}。{job}。{n.age}。……もういい？",
                        $"……{n.FullName}……{job}……どうせ覚えないでしょ。");
            }
        }

        public static string Greeting(NPCData n, int affinity, SeededRandom rng)
        {
            if (affinity >= 70) return Pick(rng, $"あ、来てくれたんだ！", $"待ってたよ、今日は何の話する？");
            if (n.personality == Personality.Gloomy) return Pick(rng, "……また来たの。", "……何。");
            if (n.personality == Personality.Introvert) return Pick(rng, "あ……こんにちは。", "……どうも。");
            return Pick(rng, "よっ！", "おーい、元気？");
        }

        public static string Annoyed(NPCData n, SeededRandom rng)
        {
            switch (n.personality)
            {
                case Personality.Extrovert: return "今日はもう話し疲れたって！ また明日な！";
                case Personality.Introvert: return "……すみません、今日はもう……。";
                default: return Pick(rng, "……しつこい。", "……………。");
            }
        }

        public static string Reveal(NPCData n, NPCInfoFlags flag, NPCDatabase db, SeededRandom rng)
        {
            switch (flag)
            {
                case NPCInfoFlags.Club:
                {
                    var club = db.GetClub(n.clubId);
                    string name = club != null ? club.name : "どこにも";
                    return club != null && club.category == ClubCategory.Underground
                        ? $"クラブ？ ……表には出てないけど、{name}ってとこで蹴ってる。"
                        : $"クラブは{name}に入ってるよ。";
                }
                case NPCInfoFlags.Position:
                    return $"ポジションは{PositionText(n.position)}。";
                case NPCInfoFlags.SoccerPersonality:
                    return SoccerPersonalityLine(n);
                case NPCInfoFlags.Personality:
                    return n.personality == Personality.Extrovert ? "人と話すのが好きなんだよね！"
                         : n.personality == Personality.Introvert ? "……人と話すのは、あんまり得意じゃなくて。"
                         : "……友達？ 別にいらない。";
                case NPCInfoFlags.Salary:
                    return n.salary > 0 ? $"クラブからは年{NPCLabels.Yen(n.salary)}もらってる。本業のほうが{(n.jobIncome > n.salary ? "多い" : "少ない")}かな。"
                                        : "サッカーでお金はもらってないよ。趣味みたいなもん。";
                case NPCInfoFlags.MarketValue:
                    return $"協会の査定だと、市場価値は{NPCLabels.Yen(n.marketValue)}らしい。{(n.AppearsInOfficialRanking ? "" : $"……まあ、{Me(n)}は協会には載ってないけどな。")}";
                case NPCInfoFlags.Effort:
                    return n.effort >= 70 ? "毎朝5時から走ってる。練習しないと落ち着かないんだ。"
                         : n.effort >= 40 ? "練習？ 週に何回かはしてるよ。"
                         : "練習とか、だるくない？";
                case NPCInfoFlags.Talent:
                    return n.talent >= 80 ? "昔から「天才」って言われてきた。自分でもそう思う。"
                         : n.talent >= 55 ? "小さい頃はそこそこ上手いほうだったかな。"
                         : "才能？ ないない。いつも補欠だったし。";
                case NPCInfoFlags.PhoneNumber:
                    return $"そうだ、番号教えとくよ。{n.phoneNumber}。いつでも電話して。";
                case NPCInfoFlags.GrowthHint:
                    return GrowthHint(n);
                case NPCInfoFlags.UndergroundSecret:
                    return Pick(rng,
                        "……ここだけの話、夜は裏フットサルに出てる。表の試合じゃ本気出してないんだ。",
                        "……あんたなら信用できる。裏市場の場所、教えてやるよ。");
                default:
                    return "";
            }
        }

        static string PositionText(Position p)
        {
            switch (p)
            {
                case Position.GK: return "キーパー";
                case Position.DF: return "ディフェンダー";
                case Position.MF: return "ミッドフィルダー";
                default: return "フォワード";
            }
        }

        static string SoccerPersonalityLine(NPCData n)
        {
            switch (n.soccerPersonality)
            {
                case SoccerPersonality.MoodMaker: return $"チームが暗いと嫌なんだよね。{Me(n)}が盛り上げないと！";
                case SoccerPersonality.Egoist: return $"パス？ {Me(n)}が決めたほうが早いでしょ。";
                case SoccerPersonality.Sense: return "誰も思いつかないプレーがしたいんだ。ヒールリフトとかさ。";
                case SoccerPersonality.Hysteric: return "審判がムカつくんだよ！ この前もイエローもらった！";
                case SoccerPersonality.Fighter: return "体ぶつけてナンボでしょ。";
                case SoccerPersonality.Devoted: return "点を取る人を支えるのが好きなんだ。";
                case SoccerPersonality.God: return "……サッカーは、呼吸と同じだよ。";
                case SoccerPersonality.Gifted: return "ボールの軌道が線で見えるんだ。みんなは見えないの？";
                case SoccerPersonality.Phantom: return $"……{Me(n)}のプレーを見た奴は、だいたい忘れられなくなる。";
                default: return "";
            }
        }

        static string GrowthHint(NPCData n)
        {
            switch (n.growthType)
            {
                case GrowthType.Early: return n.age < 24 ? "今が一番体が動く気がする。" : "昔はもっと動けたんだけどなあ。";
                case GrowthType.Late: return n.age < 28 ? "なんか最近、やっと体と頭が噛み合ってきた。" : "年取ってからのほうが楽しいよ、サッカー。";
                case GrowthType.Awakening: return n.hasAwakened ? "ある日突然、全部わかったんだ。ボールが止まって見えた。" : "ずっと下手だけど……なぜか辞められないんだよね。いつか化ける気がして。";
                case GrowthType.Longevity: return "怪我ってしたことないんだよね。";
                default: return "まあ、普通に上手くなってきたかな。";
            }
        }

        public static string Gossip(NPCData speaker, NPCData target, NPCRelation rel, int tone, SeededRandom rng)
        {
            string name = target.FullName;
            switch (rel.type)
            {
                case RelationType.Classmate:
                    if (tone > 0)
                        return target.talent >= 70
                            ? $"同級生の{name}、知ってる？ あいつは小さい頃から別格だったよ。"
                            : $"同級生の{name}はいい奴だよ。昔はそんなに上手くなかったけどね。";
                    return tone < 0 ? $"同級生の{name}？ ……あいつの話はしたくない。" : $"{name}とは同級生。小学校のチームで一緒だった。";
                case RelationType.Family:
                    return $"{name}は家族なんだ。{(tone > 0 ? "自慢の家族だよ。" : tone < 0 ? "……最近口きいてないけど。" : "")}";
                case RelationType.Coworker:
                    return tone > 0 ? $"職場の{name}、仕事もサッカーも真面目でさ。" : tone < 0 ? $"職場の{name}？ 練習サボってばっかりだよ。" : $"{name}とは同じ職場。";
                case RelationType.Rival:
                    return tone < 0 ? $"{name}……あいつにだけは負けたくない。" : $"{name}とはライバルみたいなもんかな。";
                case RelationType.Friend:
                    return $"{name}とはよく飲みに行くよ。{(tone > 0 ? "最近すごい調子いいらしい。" : "")}";
                case RelationType.Mentor:
                    return $"{name}さんは{Me(speaker)}の師匠。あの人に全部教わった。";
                case RelationType.Pupil:
                    return $"{name}は{Me(speaker)}の弟子。{(target.talent >= 70 ? "あいつは化けるぞ。" : "まだまだだけどな。")}";
                case RelationType.Boss:
                    return $"……{name}さんには逆らえない。この街の裏を仕切ってる人だ。";
                case RelationType.Subordinate:
                    return $"{name}？ ああ、うちの若いのだ。";
                case RelationType.Spouse:
                    return tone > 0 ? $"{name}と結婚してるんだ。毎朝ボールを蹴ってから仕事に行くんだよ。" : $"{name}？ ……うちの連れ合いだよ。";
                default:
                    return $"{name}って知ってる？";
            }
        }
    }
}
