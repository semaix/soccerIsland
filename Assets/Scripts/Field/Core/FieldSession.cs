using System;
using System.Collections.Generic;
using System.Linq;
using SoccerIsland.Career;
using SoccerIsland.Match;
using SoccerIsland.NPC;
using SoccerIsland.World;

namespace SoccerIsland.Field
{
    /// <summary>
    /// フィールド画面のメニューの中身（Unity非依存）。どこで何ができるかと、選んだときの処理をまとめる。
    /// Unity 側（FieldController）はこれが返す MenuPage を描いて、選ばれたら MenuItem.select を呼ぶだけ。
    /// </summary>
    public class FieldSession
    {
        public IslandCareer Career { get; }
        /// <summary>今出ている大会（なければ null）。</summary>
        public TournamentSystem Tournament { get; private set; }
        /// <summary>電話で誘われた試合。</summary>
        public List<IncomingCall> Invites { get; } = new List<IncomingCall>();
        /// <summary>進行中の大会の試合なら true（試合後に CompleteRound する）。</summary>
        public bool PlayingTournamentMatch { get; private set; }

        public FieldSession(IslandCareer career) { Career = career; }

        NPCDatabase Db => Career.Population.Database;
        PlayerState P => Career.Player;
        string Name(NPCData n) => Career.Population.Knowledge.Knows(n.id, NPCInfoFlags.Name) ? n.FullName : $"見知らぬ{NPCLabels.Of(n.occupation)}";

        // ==================================================================
        // 日付・時間
        // ==================================================================

        /// <summary>日付が変わった報告を、画面に出すメッセージにする（朝刊・電話・お誘い）。</summary>
        public List<string> Digest(IEnumerable<CareerDayReport> reports)
        {
            var lines = new List<string>();
            foreach (var r in reports)
            {
                if (r == null) continue;
                var w = Career.World;
                lines.Add($"―― {Db.currentYear}年 {Db.currentDay}日目の朝（{WeatherSystem.Label(w.CurrentSeason)}・{WeatherSystem.Label(w.TodaysWeather)}）――");
                if (r.newspaper?.Headline != null) lines.Add("【朝刊】" + r.newspaper.Headline.title);
                foreach (var call in r.incomingCalls)
                {
                    lines.Add("（電話）" + call.text);
                    if (call.kind == IncomingCallKind.MatchInvite) Invites.Add(call);
                }
                foreach (var lv in r.economyEvents)
                    if (lv.type == EconomyEventType.LoanSharkDemandsFix) lines.Add("（闇金）" + lv.text);
            }
            Invites.RemoveAll(i => { var m = InviteMatch(i); return m == null || m.played || m.day < Db.currentDay; });
            return lines;
        }

        SoccerIsland.World.Match InviteMatch(IncomingCall i) => Career.World.State.GetMatch(i.matchId);

        MenuResult AfterTime(IEnumerable<CareerDayReport> reports, params string[] first)
        {
            var r = MenuResult.Say(first);
            r.messages.AddRange(Digest(reports));
            r.timeChanged = true;
            return r;
        }

        // ==================================================================
        // メインメニュー（X）
        // ==================================================================

        public MenuPage MainMenu() => MenuPage.List("メニュー", () => new List<MenuItem>
        {
            new MenuItem("つよさ", () => MenuResult.Open(Status())),
            new MenuItem("もちもの", () => MenuResult.Open(Inventory())),
            new MenuItem("しりあい", () => MenuResult.Open(Acquaintances())),
            new MenuItem("でんわ", () => MenuResult.Open(Phone())),
            new MenuItem("しんぶん", () => MenuResult.Open(Newspaper())),
            new MenuItem("そうかんず", () => MenuResult.Open(RelationMap())),
            new MenuItem("おかね", () => MenuResult.Open(Money())),
            new MenuItem("チーム", () => MenuResult.Open(Team())),
            new MenuItem("サイン・パフォーマンス", () => new MenuResult { openCreator = true, close = true }),
            new MenuItem("やすむ（時間を進める）", () => AfterTime(new[] { Career.AdvanceTime() }, "少し休んだ。")),
            new MenuItem("セーブ", () => new MenuResult { messages = { "SAVE" }, close = true }),
        });

        public MenuPage Status()
        {
            var t = new List<string>();
            t.Add($"{P.name}　{P.age}歳　{P.cycle}周目　Lv.{P.level}（次まで {PlayerGrowth.ExpToNext(P.level) - P.exp}）");
            t.Add($"知名度 {P.fame:0}　市場価値 {NPCLabels.Yen(P.marketValue)}　所持金 {NPCLabels.Yen(P.cash)}");
            t.Add($"疲労 {P.fatigue:0}　試合 {P.matchesPlayed}　ゴール {P.goals}　アシスト {P.assists}　検定 {ExamGrades.Label(P.examGrade)}");
            if (P.IsInjured(Db.currentDay)) t.Add($"けが：あと{P.injuredUntilDay - Db.currentDay + 1}日");
            string[] names = { "シュート", "パス", "ドリブル", "テクニック", "守備", "フィジカル", "スピード", "スタミナ", "メンタル", "キーパー" };
            for (int i = 0; i < NPCAbilities.StatCount; i += 2) t.Add($"{names[i]} {P.abilities[i],3}　　{names[i + 1]} {P.abilities[i + 1],3}");
            t.Add($"人気投票 {(Career.Media.playerVoteRank > 0 ? Career.Media.playerVoteRank + "位" : "圏外")}　謎のプレーヤーの手がかり {Career.Mystery.Clues}/{MysteryPlayerQuest.CluesNeeded}");
            foreach (Town town in Enum.GetValues(typeof(Town))) t.Add($"{NPCLabels.Of(town)}での評判 {Career.Towns.Of(town)}");
            return MenuPage.Text("つよさ", t);
        }

        public MenuPage Inventory() => MenuPage.List("もちもの", () =>
        {
            var list = new List<MenuItem>();
            foreach (var i in P.inventory.ToList())
            {
                var e = ItemEffects.Of(i.name);
                string name = i.name;
                list.Add(new MenuItem($"{name} ×{i.count}" + (P.equipped.Contains(name) ? "（E）" : ""), () => MenuResult.Say(Career.Inventory.Use(name).message), e?.description));
            }
            foreach (var b in P.matchBuffs) list.Add(new MenuItem("次の試合：" + ItemEffects.BuffName(b), () => MenuResult.Say(ItemEffects.BuffName(b)), enabled: false));
            if (list.Count == 0) list.Add(new MenuItem("（なにもない）", () => new MenuResult(), enabled: false));
            return list;
        });

        public MenuPage Acquaintances() => MenuPage.List("しりあい", () =>
        {
            var list = new List<MenuItem>();
            foreach (var e in Career.Population.Knowledge.entries.Where(x => x.Knows(NPCInfoFlags.Name)).OrderByDescending(x => x.affinity).Take(60))
            {
                var n = Db.Get(e.npcId);
                if (n == null) continue;
                var loc = Career.Locate(n.id);
                list.Add(new MenuItem($"{n.FullName}　好感度{e.affinity}", () => MenuResult.Open(Profile(n)), $"今は{DailySchedule.PlaceName(n, loc.place)}"));
            }
            if (list.Count == 0) list.Add(new MenuItem("（まだ誰とも知り合っていない）", () => new MenuResult(), enabled: false));
            return list;
        });

        public MenuPage Profile(NPCData n)
        {
            var p = Career.Population.GetProfile(n.id);
            var t = new List<string>
            {
                $"{p.name}（{p.age}）{p.gender}　{p.town}",
                $"職業 {p.occupation}　クラブ {p.club}　ポジション {p.position}",
                $"性格 {p.personality}　サッカーの性格 {p.soccerPersonality}",
                $"年俸 {p.salary}　市場価値 {p.marketValue}",
                $"才能度 {p.talentRank}　努力度 {p.effortRank}",
                $"電話 {p.phoneNumber}　好感度 {p.affinity}",
            };
            foreach (var note in p.notes) t.Add("※ " + note);
            if (P.dejaVu.Contains(n.id)) t.Add("※ なぜか、この人はいつか化ける気がする……");
            return MenuPage.Text("プロフィール", t);
        }

        public MenuPage Phone() => MenuPage.List("でんわ", () =>
        {
            var list = new List<MenuItem>();
            foreach (var id in Career.Phone.PhoneBook())
            {
                var n = Db.Get(id);
                if (n == null) continue;
                int npcId = id;
                list.Add(new MenuItem(n.FullName, () => MenuResult.Open(CallMenu(npcId)), n.phoneNumber, Career.Phone.CanCall(id)));
            }
            if (list.Count == 0) list.Add(new MenuItem("（番号を知っている人がいない）", () => new MenuResult(), enabled: false));
            return list;
        });

        MenuPage CallMenu(int npcId) => MenuPage.List("でんわ", () => new List<MenuItem>
        {
            new MenuItem("話す", () => CallResult(Career.Phone.Call(npcId, CallPurpose.Chat), npcId)),
            new MenuItem("練習に誘う", () => CallResult(Career.Phone.Call(npcId, CallPurpose.InviteTraining), npcId)),
            new MenuItem("ほかの人について聞く", () => MenuResult.Open(AskAboutMenu(npcId))),
        });

        MenuPage AskAboutMenu(int npcId) => MenuPage.List("だれについて聞く？", () =>
        {
            var list = new List<MenuItem>();
            foreach (var e in Career.Population.Knowledge.entries.Where(x => x.npcId != npcId && x.Knows(NPCInfoFlags.Name)).Take(30))
            {
                int target = e.npcId;
                list.Add(new MenuItem(Db.Get(target).FullName, () => CallResult(Career.Phone.Call(npcId, CallPurpose.AskAbout, target), npcId)));
            }
            return list;
        });

        MenuResult CallResult(CallResult r, int npcId)
        {
            var n = Db.Get(npcId);
            var res = new MenuResult { back = true };
            if (!r.answered) res.messages.Add("（" + r.line + "）");
            else if (r.conversation != null) foreach (var l in r.conversation.lines) res.messages.Add($"{n.FullName}「{l}」");
            else res.messages.Add($"{n.FullName}「{r.line}」");
            return res;
        }

        public MenuPage Newspaper()
        {
            var t = new List<string>();
            var issue = Career.Media.Latest;
            if (issue == null) t.Add("まだ新聞は届いていない。");
            else
            {
                t.Add($"サッカー新聞　{issue.year}年 {issue.day}日目");
                foreach (var a in issue.articles) { t.Add("■ " + a.title); foreach (var l in a.body.Split('\n')) t.Add("　" + l); }
            }
            return MenuPage.Text("しんぶん", t);
        }

        public MenuPage RelationMap()
        {
            var t = new List<string>();
            foreach (var r in Career.Population.Knowledge.knownRelations)
            {
                var a = Db.Get(r.fromId); var b = Db.Get(r.toId);
                if (a != null && b != null) t.Add($"{a.FullName} ―{NPCLabels.Of(r.type)}― {b.FullName}");
            }
            if (t.Count == 0) t.Add("まだ誰の関係もわからない。島民と話してみよう。");
            return MenuPage.Text($"そうかんず（{t.Count}本）", t);
        }

        public MenuPage Money() => MenuPage.List("おかね", () => new List<MenuItem>
        {
            new MenuItem($"所持金 {NPCLabels.Yen(P.cash)}　預金 {NPCLabels.Yen(P.savings)}", () => new MenuResult(), enabled: false),
            new MenuItem("株", () => MenuResult.Open(Stocks())),
            new MenuItem("toto", () => MenuResult.Open(Toto())),
            new MenuItem("宝くじを10枚買う", () => MenuResult.Say(Career.Betting.BuyLottery(10).Count > 0 ? "宝くじを10枚買った（毎週日曜に抽選）" : "お金が足りない"), "3000円"),
            new MenuItem("借金", () => MenuResult.Open(Loans()), $"{Career.Economy.loans.Count}件"),
        });

        MenuPage Stocks() => MenuPage.List("株", () =>
        {
            var list = new List<MenuItem>();
            foreach (var s in Career.Economy.stocks)
            {
                var h = Career.Economy.GetHolding(s.id);
                int id = s.id;
                list.Add(new MenuItem($"{s.name} {s.Price}円" + (h != null ? $"　{h.shares}株" : ""), () => MenuResult.Open(StockMenu(id))));
            }
            return list;
        });

        MenuPage StockMenu(int id) => MenuPage.List(Career.Economy.GetStock(id).name, () => new List<MenuItem>
        {
            new MenuItem("100株買う", () => MenuResult.Say(Career.Stocks.Buy(id, 100) ? "100株買った" : "お金が足りない")),
            new MenuItem("全部売る", () => { var h = Career.Economy.GetHolding(id); return MenuResult.Say(h != null && Career.Stocks.Sell(id, h.shares) ? "売った" : "持っていない"); }),
        });

        MenuPage Toto() => MenuPage.List("toto（1万円）", () =>
        {
            var list = new List<MenuItem>();
            foreach (var m in Career.World.State.matches.Where(x => Career.Betting.IsBettable(x)).OrderBy(x => x.day).Take(10))
            {
                int mid = m.id;
                foreach (TotoPick pick in Enum.GetValues(typeof(TotoPick)))
                {
                    var pk = pick;
                    string what = pk == TotoPick.Home ? m.homeName + "の勝ち" : pk == TotoPick.Away ? m.awayName + "の勝ち" : "引き分け";
                    list.Add(new MenuItem($"{m.day}日目{(m.IsUnderground ? "【裏】" : "")} {what}", () => MenuResult.Say(Career.Betting.PlaceBet(mid, pk, 10000) != null ? "1万円賭けた" : "賭けられない"), $"×{Career.Betting.Odds(m, pk):0.00}"));
                }
            }
            if (list.Count == 0) list.Add(new MenuItem("（賭けられる試合がない）", () => new MenuResult(), enabled: false));
            return list;
        });

        MenuPage Loans() => MenuPage.List("借金", () =>
        {
            var list = new List<MenuItem>();
            foreach (var l in Career.Economy.loans)
            {
                int id = l.id;
                list.Add(new MenuItem($"{(l.lender == Lender.Bank ? "銀行" : "闇金")} 残り{NPCLabels.Yen(l.Balance)}" + (l.overdueDays > 0 ? $"（延滞{l.overdueDays}日）" : ""),
                    () => { long paid = Career.Bank.Repay(id, Career.Economy.GetLoan(id)?.Balance ?? 0); return MenuResult.Say(paid > 0 ? $"{NPCLabels.Yen(paid)}返した" : "お金が足りない"); }, "全額返す"));
            }
            if (P.fixOfferLoanId >= 0)
            {
                list.Add(new MenuItem("闇金の八百長の誘いを引き受ける", () => { Career.AnswerFixOffer(true); return MenuResult.Say("……引き受けてしまった。次の試合はわざと負けなければならない。"); }));
                list.Add(new MenuItem("闇金の八百長の誘いを断る", () => { Career.AnswerFixOffer(false); return MenuResult.Say("きっぱり断った。"); }));
            }
            if (list.Count == 0) list.Add(new MenuItem("（借金はない）", () => new MenuResult(), enabled: false));
            return list;
        });

        public MenuPage Team() => MenuPage.List($"{P.teamName}（{P.contracts.Count}人）", () =>
        {
            var list = new List<MenuItem>();
            foreach (var ct in P.contracts)
            {
                var n = Db.Get(ct.npcId);
                if (n == null) continue;
                int id = n.id;
                list.Add(new MenuItem($"{n.FullName}（{n.position}）　年俸{NPCLabels.Yen(ct.salary)}", () => MenuResult.Open(MenuPage.List(n.FullName, () => new List<MenuItem>
                {
                    new MenuItem("プロフィール", () => MenuResult.Open(Profile(n))),
                    new MenuItem("放出する", () => { Career.Scouting.Release(id); return new MenuResult { back = true, messages = { $"{n.FullName}を放出した。" } }; }),
                })), n.IsInjured(Db.currentDay) ? "けが" : null));
            }
            if (list.Count == 0) list.Add(new MenuItem("（まだ選手がいない。島民をスカウトしよう）", () => new MenuResult(), enabled: false));
            return list;
        });

        // ==================================================================
        // 場所のメニュー（C）
        // ==================================================================

        public MenuPage PlaceMenu() => MenuPage.List($"{NPCLabels.Of(Career.Here.town)}・{IslandMap.PlaceLabel(Career.Here.place)}", () =>
        {
            var list = new List<MenuItem>();
            var here = Career.Here;
            var fest = Career.CurrentFestival;
            if (fest != null) list.Add(new MenuItem($"「{fest.name}」に参加する", () => MenuResult.Say(Career.JoinFestival().text)));

            switch (here.place)
            {
                case IslandPlace.Home:
                    list.Add(new MenuItem("寝る（次の朝まで）", Sleep));
                    list.Add(new MenuItem("セーブ", () => new MenuResult { messages = { "SAVE" }, close = true }));
                    break;
                case IslandPlace.Workplace:
                    list.Add(new MenuItem("お店", () => MenuResult.Open(Shop())));
                    list.Add(new MenuItem("銀行", () => MenuResult.Open(Bank())));
                    list.Add(new MenuItem("サッカー協会（大会・能力検定・ランキング）", () => MenuResult.Open(Association())));
                    if (Career.Economy.loans.Exists(l => l.lender == Lender.LoanShark))
                        list.Add(new MenuItem("交番で闇金を通報する", () => MenuResult.Say(Career.Police.ReportLoanShark())));
                    if (P.blackMarketAccess && Career.BlackMarket.KnowsLocation && Career.BlackMarket.CurrentTown == here.town)
                        list.Add(new MenuItem($"裏市場へ（{Career.BlackMarket.CurrentLocation}）", () => { Career.MoveTo(here.town, IslandPlace.BlackMarket); return MenuResult.Open(BlackMarketMenu()); }));
                    break;
                case IslandPlace.ClubGround:
                    list.Add(new MenuItem("練習する", () => MenuResult.Open(TrainingMenu())));
                    foreach (var item in MatchOptions()) list.Add(item);
                    break;
                case IslandPlace.Hospital:
                    list.Add(new MenuItem("けがを診てもらう", () => MenuResult.Say(Career.Hospital.TreatProtagonist()), NPCLabels.Yen(Career.Hospital.fee)));
                    break;
            }
            if (list.Count == 0) list.Add(new MenuItem("（ここでは特にすることがない）", () => new MenuResult(), enabled: false));
            return list;
        });

        MenuResult Sleep()
        {
            var reports = new List<CareerDayReport>();
            int day = Db.currentDay;
            while (Db.currentDay == day) { var r = Career.AdvanceTime(); if (r != null) reports.Add(r); }
            P.fatigue = Math.Max(0f, P.fatigue - 20f);
            var res = AfterTime(reports, "ぐっすり眠った。");
            res.close = true;
            return res;
        }

        public MenuPage Shop() => MenuPage.List($"{NPCLabels.Of(Career.Here.town)}のお店", () =>
        {
            var list = new List<MenuItem>();
            foreach (var i in Career.World.State.items.Where(x => !x.isUnderground && x.town == Career.Here.town))
            {
                int id = i.id;
                var e = ItemEffects.Of(i.name);
                string status = i.SurfaceStatus == ItemStatus.OnSale ? NPCLabels.Yen(i.price) : ItemLabels.Of(i.SurfaceStatus);
                list.Add(new MenuItem($"{i.shopName}「{i.name}」", () => MenuResult.Say(Career.Inventory.Buy(id) ? $"{i.name}を買った" : "買えない"), $"{status}　{e?.description}", i.SurfaceStatus == ItemStatus.OnSale));
            }
            return list;
        });

        public MenuPage BlackMarketMenu() => MenuPage.List($"裏市場（{Career.BlackMarket.CurrentLocation}）", () =>
        {
            var list = new List<MenuItem>();
            foreach (var i in Career.BlackMarket.Items())
            {
                int id = i.id;
                list.Add(new MenuItem($"「{i.name}」", () => MenuResult.Say(Career.BlackMarket.Buy(id) ? $"{i.name}を手に入れた" : "買えない"), i.status == ItemStatus.OnSale ? NPCLabels.Yen(i.price) : ItemLabels.Of(i.status), i.status == ItemStatus.OnSale));
            }
            list.Add(new MenuItem("裏ランキング", () => MenuResult.Open(MenuPage.Text("裏ランキング", Career.BlackMarket.UndergroundRanking(15).Select((n, k) => $"{k + 1}位 {n.FullName}　{NPCLabels.Yen(n.marketValue)}").ToList()))));
            list.Add(new MenuItem("情報屋（裏の人の情報を買う）", () => MenuResult.Open(InfoBroker()), NPCLabels.Yen(Career.BlackMarket.infoPrice)));
            list.Add(new MenuItem("裏トーナメントに出る", () => EnterTournament(TournamentSystem.CreateUndergroundTournament(Career)), "参加費50万円"));
            if (P.blackMarketAccess) list.Add(new MenuItem("闇金で100万円借りる（トイチ）", () => MenuResult.Say(Career.Bank.Borrow(Lender.LoanShark, 1000000, 10) != null ? "借りてしまった……10日で1割だ。" : "借りられない")));
            list.Add(new MenuItem("出る", () => { Career.MoveTo(Career.Here.town, IslandPlace.Workplace); return new MenuResult { close = true }; }));
            return list;
        });

        MenuPage InfoBroker() => MenuPage.List("情報屋", () =>
        {
            var list = new List<MenuItem>();
            foreach (var n in Career.BlackMarket.UndergroundRanking(20))
            {
                int id = n.id;
                list.Add(new MenuItem(Name(n), () => MenuResult.Say(Career.BlackMarket.BuyInfo(id) ? $"{n.FullName}の情報を手に入れた" : "買えない")));
            }
            return list;
        });

        public MenuPage Bank() => MenuPage.List("銀行", () => new List<MenuItem>
        {
            new MenuItem("100万円預ける", () => MenuResult.Say(Career.Bank.Deposit(1000000) ? "預けた" : "お金が足りない")),
            new MenuItem("100万円引き出す", () => MenuResult.Say(Career.Bank.Withdraw(1000000) ? "引き出した" : "預金が足りない")),
            new MenuItem("100万円借りる（180日）", () => MenuResult.Say(Career.Bank.Borrow(Lender.Bank, 1000000, 180) != null ? "借りた" : "借りられない（信用が足りない）"), $"年利{Career.Bank.BankAnnualRate * 100:0.0}%"),
            new MenuItem("借金を返す", () => MenuResult.Open(Loans())),
        });

        public MenuPage Association() => MenuPage.List("サッカー協会", () =>
        {
            var list = new List<MenuItem>
            {
                new MenuItem("能力検定を受ける", () => { var r = Career.Exam.TakeExam(); return MenuResult.Say(r == null ? "今は受けられない（30日に1回・受験料）" : $"検定結果：{ExamGrades.Label(r.after)}"); }, NPCLabels.Yen(Career.Exam.fee)),
                new MenuItem("市場価値ランキング", () => MenuResult.Open(MenuPage.Text("協会 市場価値ランキング", Db.GetOfficialMarketValueRanking(20).Select((n, k) => $"{k + 1}位 {n.FullName}（{NPCLabels.Of(n.hometown)}）{NPCLabels.Yen(n.marketValue)}").ToList()))),
                new MenuItem("リーグ順位表", () => MenuResult.Open(MenuPage.Text("協会リーグ", Career.World.League.Table().Select((s, k) => $"{k + 1}位 {s.name}　勝点{s.Points}（{s.won}勝{s.drawn}分{s.lost}敗）").ToList()))),
                new MenuItem("サッカーの神様を目指す大会に出る", () => EnterTournament(TournamentSystem.CreateGodCup(Career))),
                new MenuItem("地域対抗カップに出る", () => EnterTournament(TournamentSystem.CreateRegionalCup(Career))),
            };
            if (Career.World.CurrentSeason == Season.Summer) list.Add(new MenuItem("夏のビーチサッカー大会に出る", () => EnterTournament(TournamentSystem.CreateBeachFestival(Career))));
            if (P.cycle >= 2) list.Add(new MenuItem("島の頂上決戦に出る", () => EnterTournament(TournamentSystem.CreateSummitCup(Career))));
            if (P.cycle >= 3) list.Add(new MenuItem("転生の謎を解く最終決戦に挑む", () => EnterTournament(TournamentSystem.CreateReincarnationFinal(Career))));
            if (ReincarnationSystem.CanRetire(P)) list.Add(new MenuItem("引退する（次の人生へ）", () => new MenuResult { messages = { "RETIRE" }, close = true }));
            return list;
        });

        MenuResult EnterTournament(TournamentSystem t)
        {
            if (t == null) return MenuResult.Say("参加できない");
            if (Tournament != null && !Tournament.State.finished) return MenuResult.Say($"今は「{Tournament.State.name}」に出ている");
            Tournament = t;
            return MenuResult.Close($"「{t.State.name}」にエントリーした！ グラウンドに行けば試合ができる。");
        }

        public MenuPage TrainingMenu() => MenuPage.List($"練習（今日 {P.trainingsToday}/3・疲労 {P.fatigue:0}）", () =>
        {
            var list = new List<MenuItem>();
            var partner = BestPartnerHere();
            foreach (TrainingMenu m in Enum.GetValues(typeof(TrainingMenu)))
            {
                var menu = m;
                list.Add(new MenuItem(SoccerIsland.Career.TrainingSystem.Name(m), () => Train(menu, -1)));
                if (partner != null) list.Add(new MenuItem($"{SoccerIsland.Career.TrainingSystem.Name(m)}（{partner.FullName}と）", () => Train(menu, partner.id)));
            }
            return list;
        });

        NPCData BestPartnerHere()
        {
            NPCData best = null;
            int bestAff = 29;
            foreach (var n in Career.PeopleHere())
            {
                var e = Career.Population.Knowledge.Get(n.id);
                if (e != null && e.affinity > bestAff) { best = n; bestAff = e.affinity; }
            }
            return best;
        }

        MenuResult Train(TrainingMenu m, int partner)
        {
            var r = Career.Training.Train(m, partner);
            if (!r.done) return MenuResult.Say(r.refusal);
            var res = MenuResult.Say($"{SoccerIsland.Career.TrainingSystem.Name(m)}をした。" + (r.statUps.Count > 0 ? "能力が上がった！" : ""));
            foreach (var s in r.impressions) res.messages.Add($"（{s}）");
            if (r.partnerLine != null) res.messages.Add($"{Db.Get(partner).FullName}「{r.partnerLine}」");
            return res;
        }

        /// <summary>グラウンドで選べる試合：チームの今日の試合／大会の次の試合／電話で誘われた試合。</summary>
        public List<MenuItem> MatchOptions()
        {
            var list = new List<MenuItem>();
            var team = MatchFactory.TodaysTeamMatch(Career);
            if (team != null) list.Add(new MenuItem($"試合：{team.homeName} vs {team.awayName}（{MatchLabels.Of(team.category)}）", () => StartMatch(MatchFactory.FromWorldMatch(Career, team), false)));
            if (Tournament != null && !Tournament.State.finished)
            {
                var setup = Tournament.PlayerMatchSetup();
                if (setup != null) list.Add(new MenuItem($"{Tournament.State.name} {Tournament.State.RoundName}", () => StartMatch(Tournament.PlayerMatchSetup(), true)));
                else list.Add(new MenuItem($"{Tournament.State.name}：残りの試合を見届ける", () => { Tournament.CompleteRound(); return MenuResult.Say(TournamentNews()); }));
            }
            foreach (var inv in Invites.ToList())
            {
                var m = Career.World.State.GetMatch(inv.matchId);
                if (m == null || m.played || m.day != Db.currentDay) continue;
                var call = inv;
                list.Add(new MenuItem($"助っ人：{m.homeName} vs {m.awayName}", () => { Invites.Remove(call); return StartMatch(MatchFactory.FromInvite(Career, call), false); }));
            }
            return list;
        }

        MenuResult StartMatch(MatchSetup setup, bool tournament)
        {
            if (setup == null) return MenuResult.Say("試合はもう終わっている");
            PlayingTournamentMatch = tournament;
            return new MenuResult { match = new MatchSession(Career, setup), close = true };
        }

        /// <summary>試合が終わったあと（MatchScreen から呼ぶ）。大会なら回戦を進める。</summary>
        public List<string> FinishMatch(MatchSession session)
        {
            var lines = new List<string>();
            var r = session.Result;
            if (r == null) return lines;
            lines.Add($"試合終了　{session.Setup.home.name} {r.homeScore} - {r.awayScore} {session.Setup.away.name}");
            if (r.protagonist != null) lines.Add($"評価 {r.protagonist.rating:0.0}　チームメイトの評価 {r.trust:0}　経験値 +{r.protagonistExp}");
            foreach (var m in r.missions) lines.Add($"【{m.title}】{(m.status == MissionStatus.Success ? "成功" : "失敗")}　{m.detail}");
            foreach (var lv in r.levelUps) lines.Add($"レベルアップ！ Lv.{lv}");
            foreach (var s in r.learnedSpecials) lines.Add($"必殺技「{s.name}」を覚えた！");
            if (PlayingTournamentMatch && Tournament != null)
            {
                Tournament.CompleteRound(r);
                lines.Add(TournamentNews());
                PlayingTournamentMatch = false;
            }
            return lines;
        }

        string TournamentNews()
        {
            var st = Tournament.State;
            if (st.finished) return st.PlayerWon ? $"「{st.name}」優勝！ 賞金{NPCLabels.Yen(st.prize)}！" : $"「{st.name}」は{st.entries[st.champion].name}の優勝で幕を閉じた。";
            return st.PlayerAlive ? $"「{st.name}」{st.RoundName}に進出！" : $"「{st.name}」敗退……。";
        }

        // ==================================================================
        // 島民に話しかけたとき（Z）
        // ==================================================================

        public MenuPage TalkMenu(int npcId)
        {
            var n = Db.Get(npcId);
            return MenuPage.List(Name(n), () =>
            {
                var list = new List<MenuItem>
                {
                    new MenuItem("話す", () => Talk(npcId)),
                    new MenuItem("プロフィール", () => MenuResult.Open(Profile(n))),
                };
                if (P.FindContract(npcId) == null)
                    list.Add(new MenuItem("スカウトする", () => Scout(npcId), NPCLabels.Yen(Career.Scouting.Price(n))));
                if (Career.Here.place == IslandPlace.ClubGround)
                    list.Add(new MenuItem("一緒に練習する", () => MenuResult.Open(MenuPage.List("練習メニュー", () =>
                        Enum.GetValues(typeof(TrainingMenu)).Cast<TrainingMenu>().Select(m => new MenuItem(SoccerIsland.Career.TrainingSystem.Name(m), () => Train(m, npcId))).ToList()))));
                if (Career.Romance.Unlocked && n.age >= 20)
                {
                    list.Add(new MenuItem("デートに誘う", () => MenuResult.Open(DateMenu(npcId))));
                    list.Add(new MenuItem("告白する", () => { var r = Career.Romance.Confess(npcId); return MenuResult.Say(r.ok ? $"{n.FullName}「{r.line}」" : $"{r.reason}" + (r.line != null ? $"　{n.FullName}「{r.line}」" : "")); }));
                }
                if (n.isStranded && !Career.Castaways.IsGrateful(npcId))
                    list.Add(new MenuItem("協会に登録してあげる", () => MenuResult.Say(Career.Castaways.Register(npcId) ? $"{n.FullName}「ありがとう……この恩は忘れない」" : "まだそこまで頼まれていない（またはお金が足りない）"), NPCLabels.Yen(Career.Castaways.registrationFee)));
                return list;
            });
        }

        MenuPage DateMenu(int npcId) => MenuPage.List("どこに行く？", () => new List<MenuItem>
        {
            DateItem(npcId, DateSpot.Bar, "煌浜の飲み屋", "8000円"),
            DateItem(npcId, DateSpot.Cafe, "喫茶店", "3000円"),
            DateItem(npcId, DateSpot.Beach, "夜の浜辺", "0円"),
            DateItem(npcId, DateSpot.Match, "サッカー観戦", "1000円"),
        });

        MenuItem DateItem(int npcId, DateSpot spot, string label, string cost) =>
            new MenuItem(label, () => { var r = Career.Romance.Date(npcId, spot); return MenuResult.Say(r.ok ? $"{Db.Get(npcId).FullName}「{r.line}」" : r.reason); }, cost);

        /// <summary>話す。その場にいなければ話せない。ファンのお願いは別に返す（はい／いいえを聞くため）。</summary>
        public MenuResult Talk(int npcId)
        {
            var r = Career.Interact(npcId);
            if (r.notHere) return MenuResult.Close("……もういない。");
            var n = Db.Get(npcId);
            var res = new MenuResult { close = true };
            foreach (var l in r.conversation.lines) res.messages.Add($"{Name(n)}「{l}」");
            PendingFan = r.fanRequest;
            if (r.fanRequest != null) res.messages.Add($"{Name(n)}「{r.fanRequest.line}」");
            return res;
        }

        /// <summary>話したときに来たファンのお願い（応えるか断るかを待っている）。</summary>
        public FanRequest PendingFan { get; set; }

        public string AnswerFan(bool accept)
        {
            if (PendingFan == null) return null;
            var res = Career.Fans.Respond(PendingFan, accept);
            PendingFan = null;
            if (res == FanResponseResult.NoSignature) return "（サインをまだ作っていない……メニューの「サイン・パフォーマンス」で作れる）";
            return accept ? "（快く応えた）" : "（断った……）";
        }

        MenuResult Scout(int npcId)
        {
            var n = Db.Get(npcId);
            var result = Career.Scouting.Scout(npcId);
            switch (result)
            {
                case ScoutResult.Success: return MenuResult.Close($"{n.FullName}「よろしく頼む！」　{n.FullName}がチームに加わった！");
                case ScoutResult.NotEnoughMoney: return MenuResult.Say($"お金が足りない（{NPCLabels.Yen(Career.Scouting.Price(n))}必要）");
                case ScoutResult.NotAcquainted: return MenuResult.Say("まずは話して知り合いにならないと。");
                case ScoutResult.LowAffinity: return MenuResult.Say($"{Name(n)}「……まだあんたのことをよく知らないし。」");
                case ScoutResult.Incarcerated: return MenuResult.Say("服役中は獲得できない。");
                case ScoutResult.TooYoung: return MenuResult.Say("15歳未満は獲得できない。");
                case ScoutResult.NeedsBlackMarket: return MenuResult.Say($"{Name(n)}「表の人間の誘いには乗れねえな。」");
                case ScoutResult.AlreadyInTeam: return MenuResult.Say("もうチームにいる。");
                default: return MenuResult.Say($"{Name(n)}「悪いけど、今のチームじゃ行く気にならない。」");
            }
        }
    }
}
