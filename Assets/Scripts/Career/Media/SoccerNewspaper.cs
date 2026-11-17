using System;
using System.Collections.Generic;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Career
{
    /// <summary>
    /// サッカー新聞。毎朝、前日の試合・移籍・市場価値・ゴシップ・宝くじなどを記事にする。
    /// ゴシップ記事は載った人の話題性を上下させる（＝市場価値が動く）。
    /// </summary>
    public class SoccerNewspaper
    {
        public int maxArticles = 12;
        public float gossipChancePerDay = 0.7f;
        public float gossipMinFame = 35f;
        public float marketHeadlineMinRatio = 0.1f;

        readonly NPCDatabase _db;
        readonly WorldState _world;
        readonly PlayerState _player;
        readonly EconomyState _economy;
        readonly MarketValueSystem _market;
        readonly PopularityVote _vote;
        readonly SeededRandom _rng;

        public SoccerNewspaper(NPCDatabase db, WorldState world, PlayerState player, EconomyState economy,
                               MarketValueSystem market, PopularityVote vote, SeededRandom rng)
        {
            _db = db;
            _world = world;
            _player = player;
            _economy = economy;
            _market = market;
            _vote = vote;
            _rng = rng;
        }

        public NewspaperIssue Publish(MediaState media, WorldDayReport world, DayReport population,
                                      List<EconomyEvent> economyEvents, List<FameEvent> fameEvents)
        {
            var issue = new NewspaperIssue { day = _db.currentDay, year = _db.currentYear };
            var a = issue.articles;

            if (world != null) { AddMatches(a, world.playedMatches); AddShop(a, world.purchases); }
            if (population != null) { AddMarket(a, population.headlines); AddLife(a, population.lifeEvents); }
            if (economyEvents != null) foreach (var e in economyEvents) AddEconomy(a, e);
            if (fameEvents != null) foreach (var e in fameEvents) AddFame(a, e);
            if (_rng.Chance(gossipChancePerDay)) AddGossip(a);
            if (issue.day % 7 == 0) AddRanking(a);
            if (issue.day % 30 == 0) AddVote(a, media);

            a.Sort((x, y) => y.priority.CompareTo(x.priority));
            if (a.Count > maxArticles) a.RemoveRange(maxArticles, a.Count - maxArticles);

            media.issues.Add(issue);
            if (media.issues.Count > MediaState.MaxIssues) media.issues.RemoveRange(0, media.issues.Count - MediaState.MaxIssues);
            return issue;
        }

        // ---------------- 試合 ----------------

        void AddMatches(List<Article> a, List<Match> matches)
        {
            foreach (var m in matches)
            {
                if (m.IsUnderground || m.category == MatchCategory.PrisonLeague) continue; // 表の新聞には載らない
                int priority;
                switch (m.category)
                {
                    case MatchCategory.IslandSelect: priority = 100; break;
                    case MatchCategory.RegionalDerby: priority = 70; break;
                    case MatchCategory.Beach: priority = 25; break;
                    default: priority = 15 + Math.Min(20, (m.homeScore + m.awayScore) * 3); break;
                }
                string result = m.homeScore > m.awayScore ? $"{m.homeName}が勝利" : m.homeScore < m.awayScore ? $"{m.awayName}が勝利" : "引き分け";
                a.Add(new Article
                {
                    category = ArticleCategory.Match, priority = priority,
                    title = $"【{MatchLabels.Of(m.category)}】{m.homeName} {m.homeScore}-{m.awayScore} {m.awayName}",
                    body = $"{m.venue}で行われた一戦は{result}。",
                    npcIds = new List<int>(m.homeLineup),
                });
            }
        }

        // ---------------- 市場価値・島のニュース ----------------

        void AddMarket(List<Article> a, List<MarketValueChange> headlines)
        {
            headlines.Sort((x, y) => Math.Abs(y.Ratio).CompareTo(Math.Abs(x.Ratio)));
            int count = 0;
            foreach (var c in headlines)
            {
                if (count >= 3) break;
                var n = _db.Get(c.npcId);
                if (n == null || !n.AppearsInOfficialRanking || Math.Abs(c.Ratio) < marketHeadlineMinRatio || c.after < 1000000) continue;
                bool up = c.Delta > 0;
                a.Add(new Article
                {
                    category = ArticleCategory.Market, priority = 30 + (int)(Math.Abs(c.Ratio) * 100),
                    title = $"{n.FullName}の市場価値が{(up ? "急上昇" : "急落")}",
                    body = $"{NPCLabels.Of(n.hometown)}の{NPCLabels.Of(n.occupation)}、{n.FullName}（{n.age}）の市場価値が{NPCLabels.Yen(c.before)}から{NPCLabels.Yen(c.after)}に。",
                    npcIds = { n.id },
                });
                count++;
            }
        }

        void AddLife(List<Article> a, List<LifeEvent> events)
        {
            foreach (var e in events)
            {
                var n = _db.Get(e.npcId);
                if (n == null) continue;
                switch (e.type)
                {
                    case LifeEventType.Awakened:
                        a.Add(new Article { category = ArticleCategory.Island, priority = 80, npcIds = { n.id },
                            title = $"あの{n.FullName}が覚醒!?",
                            body = $"これまで目立たなかった{n.FullName}（{n.age}）が別人のようなプレー。関係者も「何が起きたのかわからない」。" });
                        break;
                    case LifeEventType.Released:
                        a.Add(new Article { category = ArticleCategory.Island, priority = n.talent >= 80 ? 75 : 20, npcIds = { n.id },
                            title = $"{n.FullName}、出所",
                            body = n.talent >= 80 ? "刑務所リーグで伝説を残した男が、ついに島に戻ってきた。" : "刑期を終え、島に戻った。" });
                        break;
                }
            }
        }

        // ---------------- お金 ----------------

        void AddEconomy(List<Article> a, EconomyEvent e)
        {
            var n = e.npcId >= 0 ? _db.Get(e.npcId) : null;
            switch (e.type)
            {
                case EconomyEventType.Scouted:
                    if (n == null) break;
                    a.Add(new Article { category = ArticleCategory.Transfer, priority = 60 + (int)Math.Min(30, n.fame / 3), aboutPlayer = true, npcIds = { n.id },
                        title = $"{_player.teamName}、{n.FullName}を獲得",
                        body = $"移籍金は{NPCLabels.Yen(e.amount)}。{NPCLabels.Of(n.occupation)}との二刀流は続ける見込み。" });
                    break;
                case EconomyEventType.PlayerLeft:
                    if (n == null) break;
                    a.Add(new Article { category = ArticleCategory.Transfer, priority = 65, aboutPlayer = true, npcIds = { n.id },
                        title = $"給料未払い!? {n.FullName}が{_player.teamName}を退団",
                        body = "関係者によると、2か月にわたり給料が支払われていなかったという。" });
                    break;
                case EconomyEventType.LotteryDrawn:
                    a.Add(new Article { category = ArticleCategory.Lottery, priority = 10,
                        title = $"今週の島宝くじ 当選番号 {e.refId:D6}", body = "1等1億円。下5桁100万円、下4桁5万円、下3桁3000円、下1桁300円。" });
                    break;
                case EconomyEventType.LotteryWon:
                    if (e.amount >= 1000000)
                        a.Add(new Article { category = ArticleCategory.Lottery, priority = e.amount >= 100000000 ? 90 : 40,
                            title = $"島宝くじで{NPCLabels.Yen(e.amount)}の高額当選", body = "当選者は名乗り出ていない。島中で「誰だ」と噂に。" });
                    break;
                case EconomyEventType.StockSurge:
                case EconomyEventType.StockCrash:
                    a.Add(new Article { category = ArticleCategory.Stock, priority = 25,
                        title = $"{e.text}株が{(e.type == EconomyEventType.StockSurge ? "急騰" : "暴落")}", body = $"終値は{e.amount}円。" });
                    break;
                case EconomyEventType.LoanSharkHarassment:
                case EconomyEventType.LoanSharkSeizure:
                    if (_player.fame >= 30)
                    {
                        a.Add(new Article { category = ArticleCategory.Gossip, priority = 55, aboutPlayer = true,
                            title = $"{_player.name}に借金トラブル!?", body = "自宅前で強面の男たちが目撃された。本人はノーコメント。" });
                        _player.fame = Math.Max(0f, _player.fame - 1f);
                    }
                    break;
            }
        }

        // ---------------- 主人公 ----------------

        void AddFame(List<Article> a, FameEvent e)
        {
            var n = e.npcId >= 0 ? _db.Get(e.npcId) : null;
            switch (e.type)
            {
                case FameEventType.GreatMatch:
                    a.Add(new Article { category = ArticleCategory.Player, priority = 50, aboutPlayer = true,
                        title = $"{_player.name}、{e.text}", body = "移住者がまたしても島を沸かせた。" });
                    break;
                case FameEventType.FameMilestone:
                    a.Add(new Article { category = ArticleCategory.Player, priority = 45 + (int)e.fameDelta / 4, aboutPlayer = true,
                        title = MilestoneTitle((int)e.fameDelta), body = $"推定市場価値は{NPCLabels.Yen(_player.marketValue)}。" });
                    break;
                case FameEventType.ProvocativeCelebration:
                    a.Add(new Article { category = ArticleCategory.Gossip, priority = 40, aboutPlayer = true,
                        title = $"{_player.name}の挑発パフォーマンス「{e.text}」に賛否", body = "相手サポーターは激怒。一方で「痺れた」という声も。" });
                    break;
                case FameEventType.FanServiceBad:
                    if (n == null) break;
                    a.Add(new Article { category = ArticleCategory.Gossip, priority = 35, aboutPlayer = true, npcIds = { n.id },
                        title = $"{_player.name}、ファンに塩対応!?", body = $"{NPCLabels.Of(n.hometown)}の{NPCLabels.Of(n.occupation)}が「サインを断られた」と証言。" });
                    break;
                case FameEventType.TournamentWon:
                    a.Add(new Article { category = ArticleCategory.Player, priority = 98, aboutPlayer = true,
                        title = $"{_player.teamName}、{e.text}で優勝！", body = $"{_player.name}率いる移住者チームが頂点に立った。島中が祝福ムードに。" });
                    break;
                case FameEventType.MatchFixingExposed:
                    a.Add(new Article { category = ArticleCategory.Player, priority = 95, aboutPlayer = true,
                        title = $"{_player.name}に八百長疑惑", body = $"{e.text}での不自然なプレーに、ファンから怒りの声が上がっている。" });
                    break;
                case FameEventType.FanServiceGood:
                    if (n == null) break;
                    a.Add(new Article { category = ArticleCategory.Player, priority = 20, aboutPlayer = true, npcIds = { n.id },
                        title = $"{_player.name}の神対応に感動の声", body = $"{n.FullName}さん（{n.age}）「一生の宝物です」。" });
                    break;
            }
        }

        static string MilestoneTitle(int fame)
        {
            if (fame >= 95) return "もはや島の神。サッカーの神様に最も近い男";
            if (fame >= 80) return "島で知らない者はいない。移住者が島の顔に";
            if (fame >= 60) return "街を歩けば人だかり。移住者フィーバー";
            if (fame >= 40) return "噂の移住者、サインを求める列も";
            return "期待の移住者、じわじわ話題に";
        }

        // ---------------- お店 ----------------

        void AddShop(List<Article> a, List<PurchaseRecord> purchases)
        {
            foreach (var p in purchases)
            {
                var item = _world.GetItem(p.itemId);
                if (item == null || item.isUnderground || !item.IsLimited) continue;
                if (item.status == ItemStatus.SoldOut)
                    a.Add(new Article { category = ArticleCategory.Shop, priority = 22,
                        title = $"{item.shopName}の「{item.name}」、完売", body = "一点物だった。購入者は明かされていない。" });
                else if (item.status == ItemStatus.Negotiating && item.price >= 1000000)
                    a.Add(new Article { category = ArticleCategory.Shop, priority = 28,
                        title = $"「{item.name}」に商談の申し込み", body = $"価格は{NPCLabels.Yen(item.price)}。島の大物が動いたとの噂。" });
            }
        }

        // ---------------- ゴシップ ----------------

        void AddGossip(List<Article> a)
        {
            var candidates = new List<NPCData>();
            var weights = new List<float>();
            foreach (var n in _db.npcs)
            {
                if (n.fame < gossipMinFame || n.isIncarcerated) continue;
                candidates.Add(n);
                weights.Add(n.fame * n.fame);
            }
            if (candidates.Count == 0) return;
            var star = candidates[_rng.WeightedIndex(weights)];

            NPCRelation rel = star.relations.Count > 0 ? _rng.Pick(star.relations) : null;
            var other = rel != null ? _db.Get(rel.targetId) : null;
            string title, body;
            float hypeStar, hypeOther = 0f;

            if (other != null && rel.type == RelationType.Rival && rel.affinity < 0)
            {
                title = $"{star.FullName}と{other.FullName}、不仲説";
                body = "練習場で口論する姿が目撃された。";
                hypeStar = -0.03f; hypeOther = -0.03f;
            }
            else if (other != null && rel.type == RelationType.Friend)
            {
                title = $"{star.FullName}、深夜に{other.FullName}と密会!?";
                body = $"{NPCLabels.Of(star.hometown)}の飲み屋街で2人きりの姿が。「ただの飲み友達」と関係者。";
                hypeStar = 0.03f; hypeOther = 0.04f;
            }
            else if (other != null && rel.type == RelationType.Family)
            {
                title = $"{star.FullName}、家族の{other.FullName}とボールを蹴る姿";
                body = "サッカー一家の休日。ほほえましい光景にファンも和む。";
                hypeStar = 0.02f; hypeOther = 0.02f;
            }
            else if (other != null && rel.type == RelationType.Classmate)
            {
                title = $"{star.FullName}の同級生が語る「昔のあいつ」";
                body = other.talent > star.talent ? $"「小さい頃は{other.givenName}のほうが上手かった」との証言も。" : "「あいつは子供の頃から別格だった」。";
                hypeStar = 0.02f; hypeOther = other.talent > star.talent ? 0.03f : 0f;
            }
            else
            {
                title = $"{star.FullName}、本業の{NPCLabels.Of(star.occupation)}でも大忙し";
                body = "二足のわらじを履きこなす秘訣は「気合い」。";
                hypeStar = 0.01f;
            }

            _market.AddHype(star, hypeStar, "gossip");
            var ids = new List<int> { star.id };
            if (other != null && hypeOther != 0f) { _market.AddHype(other, hypeOther, "gossip"); ids.Add(other.id); }
            a.Add(new Article { category = ArticleCategory.Gossip, priority = 30 + (int)(star.fame / 4), title = title, body = body, npcIds = ids });
        }

        // ---------------- ランキング・人気投票 ----------------

        void AddRanking(List<Article> a)
        {
            var top = _db.GetOfficialMarketValueRanking(10);
            if (top.Count == 0) return;
            var lines = new List<string>();
            var ids = new List<int>();
            for (int i = 0; i < top.Count; i++)
            {
                lines.Add($"{i + 1}位 {top[i].FullName}（{NPCLabels.Of(top[i].hometown)}）{NPCLabels.Yen(top[i].marketValue)}");
                ids.Add(top[i].id);
            }
            a.Add(new Article { category = ArticleCategory.Ranking, priority = 50, npcIds = ids,
                title = "サッカー協会 今週の市場価値ランキング", body = string.Join("\n", lines) });
        }

        void AddVote(List<Article> a, MediaState media)
        {
            var result = _vote.Run(media);
            if (result.Count == 0) return;
            var lines = new List<string>();
            var ids = new List<int>();
            for (int i = 0; i < result.Count; i++)
            {
                lines.Add($"{i + 1}位 {result[i].name} {result[i].votes}票");
                if (result[i].npcId >= 0) ids.Add(result[i].npcId);
            }
            bool playerIn = media.playerVoteRank > 0;
            a.Add(new Article { category = ArticleCategory.Vote, priority = playerIn && media.playerVoteRank <= 3 ? 85 : 55,
                aboutPlayer = playerIn, npcIds = ids,
                title = $"月間人気投票 1位は{result[0].name}", body = string.Join("\n", lines) });
        }
    }
}
